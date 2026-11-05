using PCTP.Domain.Entities;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// Vòng đời bảng TMP đang bắn QR (TMPPHIEUGIAOHANG/DOCQRCODE) — load, lưu (merge upsert),
    /// xoá khi kết thúc phiên, và xác định trạng thái đang bán (trống/đang bán/đã CNK).
    /// Implementation của <see cref="IPhieuTmpRepository"/>, một trong các mảnh được
    /// <see cref="PhieuRepository"/> tổng hợp lại qua kế thừa nhiều interface — xem
    /// WORKFLOW_GIAOHANGKHACH.md mục 2 để biết vị trí trong luồng Giao Hàng Khách.
    /// </summary>
    public sealed class PhieuTmpRepository : SqlRepositoryBase, IPhieuTmpRepository
    {
        public PhieuTmpRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // LoadPhieuDocQR
        // ============================================================
        public DataTable LoadPhieuDocQR(
            string ngayGiao, string nhaMay, string gioFcc, int addNm, PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            Db.ValidateTableName(tables.TmpTable);
            Db.ValidateTableName(tables.SourceTable);
            Db.ValidateTableName(tables.DocQRTable);

            DataTable tt = LoadData(
                $"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAOFCC, NHAMAY FROM [{tables.SourceTable}]");

            if (tt.Rows.Count > 0)
            {
                DataRow row = tt.Rows[0];
                ngayGiao = row["NGAYGIAO"]?.ToString() ?? "";
                nhaMay = row["NHAMAY"]?.ToString() ?? "";
                gioFcc = row["GIOGIAOFCC"]?.ToString() ?? "";
                addNm = DbValueHelper.ToInt(row["ADDNM"]);
            }

            return Db.CallPhieuSP("Usp_Qrcode_LOAD_PHIEU_DOCQR2405", ngayGiao, nhaMay, gioFcc, addNm, tables);
        }

        public DataTable LoadPhieuDocQR(
            string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tmpTable, string ifsTable, string docQRTable)
        {
            return LoadPhieuDocQR(ngayGiao, nhaMay, gioFcc, addNm,
                new PhieuTableSet(tmpTable, ifsTable, docQRTable));
        }

        // ============================================================
        // LuuVaLoad
        // ============================================================
        // ============================================================
        // Nửa 1: CHỈ đẩy dữ liệu vào bảng nguồn (IFS) — dùng cho CẢ 2 nhánh.
        // Đây là phần thay thế SyncIfsSnapshot đã xoá, nhưng dùng BulkInsert
        // thay vì insert từng dòng.
        // ============================================================
        public void PushIfsSnapshot(string ifsTable, DataTable donHang)
        {
            if (donHang == null) throw new ArgumentNullException(nameof(donHang));
            Db.ValidateTableName(ifsTable);
            ConvertDateTimeColumns(donHang);

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                SWLog.Measure($"4b. DropCreate [{ifsTable}]",
                    () => Db.DropCreate(Connection, Transaction, ifsTable, donHang));

                SWLog.Measure($"4c. BulkInsert {donHang.Rows.Count} rows → [{ifsTable}]",
                    () => Db.BulkInsert(Connection, Transaction, ifsTable, donHang));

                if (ownTransaction) Uow.Commit();
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }

        // ============================================================
        // Nửa 2: Đẩy vào IFS RỒI đồng bộ tiếp sang TMP — CHỈ dùng cho
        // customer KHÔNG có bảng riêng (nhánh 1).
        // ============================================================
        public DataTable LuuVaLoad(
            PhieuTableSet tables, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (donHang == null) throw new ArgumentNullException(nameof(donHang));
            Db.ValidateTableName(tables.TmpTable);
            Db.ValidateTableName(tables.SourceTable);
            Db.ValidateTableName(tables.DocQRTable);
            ConvertDateTimeColumns(donHang);

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                SWLog.Measure($"4b. DropCreate [{tables.SourceTable}]",
                    () => Db.DropCreate(Connection, Transaction, tables.SourceTable, donHang));

                SWLog.Measure($"4c. BulkInsert {donHang.Rows.Count} rows → [{tables.SourceTable}]",
                    () => Db.BulkInsert(Connection, Transaction, tables.SourceTable, donHang));

                SWLog.Measure($"4d. Guard DELETE [{tables.TmpTable}]",
                    () => GuardDeleteTmp(tables.TmpTable, tables.DocQRTable));

                var result = SWLog.Measure($"4e. CallSP [{tenSP}]",
                    () => Db.CallPhieuSP(Connection, Transaction, tenSP, ngayGiao, nhaMay, gioFcc, addNm, tables));

                if (ownTransaction) Uow.Commit();
                return result;
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }

        public DataTable LuuVaLoad(
            string tenSPBang, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tenBan, string docQRTable, string ifsView = "")
        {
            var tables = new PhieuTableSet(tenBan, tenSPBang, docQRTable, tenBan, ifsView);
            return LuuVaLoad(tables, tenSP, donHang, ngayGiao, nhaMay, gioFcc, addNm);
        }

        // ============================================================
        // InsertTmpRow
        // ============================================================
        public void InsertTmpRow(
            string tmpTable,
            string stt, string cua, string truyen, string maHang, string tenHang,
            string lot, string dv, int slXuat, string ngayGiao, string gear,
            string gioXuat, string poNo = "", string cusPoNo = "", string poItem = "")
        {
            Db.ValidateTableName(tmpTable);

            // Số PO của YMVN nằm ở cột PO_ITEM (đã chuyển từ CUA). Chỉ ghi cột này khi có
            // giá trị để không phụ thuộc bảng TMP khác (không có PO_ITEM) khi gọi không truyền poItem.
            bool coPoItem = !string.IsNullOrWhiteSpace(poItem);
            string poItemCol = coPoItem ? ", PO_ITEM" : "";
            string poItemVal = coPoItem ? ", @PO_ITEM" : "";

            string sql = $@"
                INSERT INTO [{tmpTable}]
                (
                    STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                    NGAYGIAO, GEAR, GIOGIAO, STATUS, PO_NO, TTPHIEU{poItemCol}
                )
                VALUES
                (
                    @STT, @CUA, @TRUYEN, @MAHANG, @TENHANG, @LOT, @DV, @SOLUONG,
                    @NGAYGIAO, @GEAR, @GIOGIAO, 'NG', @PO_NO, @CUSPO{poItemVal}
                )";

            var prms = new System.Collections.Generic.List<SqlParameter>
            {
                new SqlParameter("@STT", stt),
                new SqlParameter("@CUA", cua),
                new SqlParameter("@TRUYEN", truyen),
                new SqlParameter("@MAHANG", maHang),
                new SqlParameter("@TENHANG", tenHang),
                new SqlParameter("@LOT", lot),
                new SqlParameter("@DV", dv),
                new SqlParameter("@SOLUONG", slXuat),
                new SqlParameter("@NGAYGIAO", ngayGiao),
                new SqlParameter("@GEAR", gear),
                new SqlParameter("@GIOGIAO", gioXuat),
                new SqlParameter("@PO_NO", poNo ?? ""),
                new SqlParameter("@CUSPO", cusPoNo ?? "")
            };
            if (coPoItem)
                prms.Add(new SqlParameter("@PO_ITEM", poItem.Trim()));

            ExecuteNonQuery(sql, prms.ToArray());
        }

        // ============================================================
        // LoadTuTmpTable
        // ============================================================
        public DataTable LoadTuTmpTable(string tmpTable)
        {
            Db.ValidateTableName(tmpTable);
            string sql = $@"
SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO,
       STATUS, TTPHIEU, NHAMAY, ADDNM, HOP, STATUSDOC, Note,
       ISNULL(PO_NO, '') AS PO_NO, ISNULL(PO_ITEM, '') AS PO_ITEM
FROM [{tmpTable}]
ORDER BY TRY_CAST(STT AS INT), STT";
            return LoadData(sql);
        }

        // ============================================================
        // GetDonHangHienTai
        // ============================================================
        public DataTable GetDonHangHienTai(string tenBan)
        {
            Db.ValidateTableName(tenBan);
            return LoadData($"SELECT STT, MAHANG, LOT, STATUS, STATUSDOC FROM [{tenBan}] ORDER BY STT");
        }

        // ============================================================
        // LoadTmpOrderForGear
        // ============================================================
        // Nguồn duy nhất để đối chiếu Gear/số lượng YMVN khi bắn QR và khi Hoàn thành:
        // đúng tập dòng đã được SyncPhieuTuBangRiengChoDocQR ghi vào TMP (bỏ dòng
        // STATUS = 'OK'). Không dùng lưới Phiếu / IFS / Purchase_Order_YMVN vì các nguồn
        // đó có thể chứa dòng không nằm trong phiên bắn.
        public DataTable LoadTmpOrderForGear(string tmpTable)
        {
            Db.ValidateTableName(tmpTable);
            string sql = $@"
SELECT STT, MAHANG, ISNULL(GEAR, '') AS GEAR, SOLUONG, STATUS, ISNULL(PO_NO, '') AS PO_NO
FROM [{tmpTable}]
WHERE ISNULL(RTRIM(STATUS), '') <> 'OK'
ORDER BY TRY_CAST(STT AS INT), STT";
            return LoadData(sql);
        }

        // ============================================================
        // XoaTmpPhieu / XoaDocQRCode
        // ============================================================
        public void XoaTmpPhieu(string tenBan)
        {
            Db.ValidateTableName(tenBan);
            ExecuteNonQuery($"DELETE FROM [{tenBan}]");
        }

        public void XoaDocQRCode(string docQRTable)
        {
            Db.ValidateTableName(docQRTable);
            ExecuteNonQuery($"DELETE FROM [{docQRTable}]");
        }

        // ============================================================
        // GetTrangThaiDangBan
        // ============================================================
        public TrangThaiBan GetTrangThaiDangBan(PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return GetTrangThaiDangBan(tables.TmpTable, tables.DocQRTable);
        }

        public TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            var result = new TrangThaiBan();

            int demQR = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]"));
            if (demQR == 0) { result.DangBan = false; return result; }

            int demPhieu = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{tmpTable}]"));
            if (demPhieu == 0)
            {
                // Có DOCQRCODE nhưng mất TMP => không thể xác định session identity.
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            // DOCQRCODE không lưu nhà máy/ngày/giờ nên session identity phải lấy từ TMP và
            // phải đồng nhất trên toàn bộ TMP. Nhà máy (ADDNM) + ngày giao phải là DUY NHẤT.
            // GIOGIAO có thể nhiều giá trị: một khung giờ như (17+18+19)H có dòng giờ 17 và 19.
            // Việc các giờ đó thuộc cùng MỘT khung được presenter kiểm tra (cần danh sách khung).
            int demContext = DbValueHelper.ToInt(ExecuteScalar($@"
SELECT COUNT(*)
FROM
(
    SELECT DISTINCT
           ISNULL(ADDNM, 1) AS ADDNM,
           CONVERT(VARCHAR(10), NGAYGIAO, 120) AS NGAYGIAO
    FROM [{tmpTable}]
) S"));

            if (demContext != 1)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataTable gioTable = LoadData($"SELECT DISTINCT ISNULL(GIOGIAO, '') AS GIOGIAO FROM [{tmpTable}]");
            var gios = new System.Collections.Generic.List<string>();
            bool coGioTrong = false;
            foreach (DataRow g in gioTable.Rows)
            {
                string gio = Convert.ToString(g["GIOGIAO"]).Trim();
                if (gio.Length == 0) { coGioTrong = true; continue; }
                if (gio.Length == 1) gio = "0" + gio;
                if (!gios.Contains(gio, StringComparer.OrdinalIgnoreCase))
                    gios.Add(gio);
            }
            gios.Sort(StringComparer.OrdinalIgnoreCase);

            // Trộn dòng có giờ với dòng không có giờ vẫn là context không đồng nhất.
            if (coGioTrong && gios.Count > 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataTable dt = LoadData($"SELECT TOP 1 ADDNM, NGAYGIAO FROM [{tmpTable}]");
            if (dt.Rows.Count == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataRow row = dt.Rows[0];
            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = row["ADDNM"] == DBNull.Value ? 1 : Convert.ToInt32(row["ADDNM"]);
            // NHAMAY is deliberately not part of the session identity. The factory is ADDNM.
            result.NgayGiao = row["NGAYGIAO"] == DBNull.Value ? "" : Convert.ToDateTime(row["NGAYGIAO"]).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            result.GioGiaoTatCa = gios;
            result.GioGiaoFCC = gios.Count > 0 ? gios[0] : "";

            return result;
        }

        // ============================================================
        // GetTrangThaiDangBanSP
        // ============================================================
        /// <summary>
        /// SP là phiên theo ngày + nhà máy. TMP_SP có thể chứa nhiều
        /// GIOGIAO nên không được dùng GIOGIAO để xác định DataKhongKhop.
        /// </summary>
        public TrangThaiBan GetTrangThaiDangBanSP(PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            Db.ValidateTableName(tables.TmpTable);
            Db.ValidateTableName(tables.DocQRTable);

            var result = new TrangThaiBan();

            int demQR = DbValueHelper.ToInt(
                ExecuteScalar($"SELECT COUNT(*) FROM [{tables.DocQRTable}]"));
            if (demQR == 0)
            {
                result.DangBan = false;
                return result;
            }

            int demPhieu = DbValueHelper.ToInt(
                ExecuteScalar($"SELECT COUNT(*) FROM [{tables.TmpTable}]"));
            if (demPhieu == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            int demContext = DbValueHelper.ToInt(ExecuteScalar($@"
SELECT COUNT(*)
FROM
(
    SELECT DISTINCT
           ISNULL(ADDNM, 1) AS ADDNM,
           CONVERT(VARCHAR(10), NGAYGIAO, 120) AS NGAYGIAO
    FROM [{tables.TmpTable}]
) S"));

            if (demContext != 1)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataTable dt = LoadData($@"
SELECT TOP 1 ADDNM, NGAYGIAO
FROM [{tables.TmpTable}]
ORDER BY STT");

            if (dt.Rows.Count == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataRow row = dt.Rows[0];
            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = row["ADDNM"] == DBNull.Value ? 1 : Convert.ToInt32(row["ADDNM"]);
            result.NgayGiao = row["NGAYGIAO"] == DBNull.Value
                ? ""
                : Convert.ToDateTime(row["NGAYGIAO"]).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            result.GioGiaoFCC = "";
            return result;
        }

        // ============================================================
        // GetTrangThaiDangBanYMVN
        // ============================================================
        public TrangThaiBan GetTrangThaiDangBanYMVN(PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return GetTrangThaiDangBanYMVN(tables.TmpTable, tables.DocQRTable);
        }

        public TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            var result = new TrangThaiBan();

            int demTmp = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{tmpTable}] WHERE ADDNM = 0"));
            if (demTmp == 0) { result.DangBan = false; return result; }

            int demQR = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]"));
            if (demQR == 0) { result.DangBan = false; return result; }

            DataTable dt = LoadData($"SELECT TOP 1 NGAYGIAO, GIOGIAO FROM [{tmpTable}]");
            if (dt.Rows.Count == 0) { result.DangBan = false; return result; }

            DataRow row = dt.Rows[0];
            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = 1;
            result.NhaMay = "YAMAHA - VIET NAM";

            string ngayRaw = row["NGAYGIAO"]?.ToString() ?? "";
            result.NgayGiao = ngayRaw.Length >= 10 ? ngayRaw.Substring(0, 10) : ngayRaw;
            result.GioGiaoFCC = row["GIOGIAO"]?.ToString().Trim() ?? "";

            return result;
        }

        // ============================================================
        // EnsureTablesExist
        // ============================================================
        public void EnsureTablesExist()
        {
            string[] tables = { "IFSPHIEUGIAOHANG", "IFSPHIEUGIAOHANGView" };
            const string createSql =
                "IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type = 'U') " +
                "CREATE TABLE [{0}] (STT INT, MAHANG NVARCHAR(50), TENHANG NVARCHAR(100), SOLUONG INT, " +
                "NGAYGIAO SMALLDATETIME, GIOGIAO NVARCHAR(50), GIOGIAOFCC NVARCHAR(200), NHAMAY NVARCHAR(100), " +
                "ADDNM INT, LOT NVARCHAR(500), STATUS NVARCHAR(50), STATUSDOC NVARCHAR(50))";

            foreach (string table in tables)
                ExecuteNonQuery(string.Format(createSql, table));
        }

        // ============================================================
        // Private
        // ============================================================
        private void ConvertDateTimeColumns(DataTable donHang)
        {
            var dateTimeColumns = donHang.Columns.Cast<DataColumn>()
                .Where(c => c.DataType == typeof(DateTime))
                .Select(c => c.ColumnName)
                .ToList();

            foreach (string columnName in dateTimeColumns)
            {
                string tempName = columnName + "_STR";
                donHang.Columns.Add(tempName, typeof(string));
                foreach (DataRow row in donHang.Rows)
                    row[tempName] = row[columnName] == DBNull.Value ? "" : ((DateTime)row[columnName]).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                donHang.Columns.Remove(columnName);
                donHang.Columns[tempName].ColumnName = columnName;
            }
        }

        private void GuardDeleteTmp(string tmpTable, string docQRTable)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            int tmpExists = DbValueHelper.ToInt(ExecuteScalar(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[" + tmpTable + "]') AND type = 'U'"));
            if (tmpExists != 1) return;

            int docQRExists = DbValueHelper.ToInt(ExecuteScalar(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[" + docQRTable + "]') AND type = 'U'"));

            if (docQRExists == 1)
            {
                int demDocQR = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]"));
                if (demDocQR > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LuuVaLoad] SKIP DELETE [{tmpTable}] — đang có {demDocQR} dòng trong [{docQRTable}]");
                    return;
                }
            }

            ExecuteNonQuery($"DELETE FROM [{tmpTable}]");
            System.Diagnostics.Debug.WriteLine($"[LuuVaLoad] Đã DELETE [{tmpTable}]");
        }
       
    }
}