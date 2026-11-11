using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuGiaoDBRepository : SqlRepositoryBase, IPhieuGiaoDBRepository
    {
        // Bảng NGHIỆP VỤ (lưu lâu dài, nguồn gốc của đơn Giao DB) — KHÔNG phải bảng tạm.
        // Tên cũ TMPPHIEUGIAODBHD / TMPPHIEUGIAOHANGDBCT dễ nhầm với bảng làm việc
        // TMPPHIEUGIAOHANGDB (bị SP xóa và dựng lại mỗi lần upload).
        public const string TableHd = "FVN_Purchase_Order_DB_HD";
        public const string TableCt = "FVN_Purchase_Order_DB_CT";

        public PhieuGiaoDBRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        // ============================================================
        // IPhieuGiaoDBRepository
        // ============================================================

        public DataTable GetDanhSachMaHang()
        {
            const string sql = @"
            SELECT ID, Code, Name
            FROM B20Item
            WHERE LEN(Code) > 10
            GROUP BY ID, Code, Name
            ORDER BY ID";
            return LoadData(sql);
        }

        public DataTable LoadTmpPhieuGiaoDB(string tenBan, DateTime ngayGiao, int addNm)
        {
            Db.ValidateTableName(tenBan);

            // Bảng TMPPHIEUGIAOHANGDB ở một số DB chưa có Note / PO_NO / PO_ITEM (HOP, STATUSDOC cũng
            // có thể thiếu) -> chỉ SELECT cột có thật, cột thiếu trả về '' để lưới vẫn bind được.
            string Cot(string name, bool isText = true)
            {
                bool co = TonTaiCot(tenBan, name);
                if (co) return isText ? $"ISNULL([{name}], '') AS [{name}]" : $"[{name}]";
                return isText ? $"CAST('' AS NVARCHAR(200)) AS [{name}]" : $"CAST(NULL AS NVARCHAR(50)) AS [{name}]";
            }

            string sql = $@"
            SELECT
                '' AS IDP, STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV,
                SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU, NHAMAY, ADDNM,
                {Cot("HOP", false)}, {Cot("STATUSDOC", false)}, {Cot("Note")},
                {Cot("PO_NO")}, {Cot("PO_ITEM")}
            FROM [{tenBan}]
            WHERE CAST(NGAYGIAO AS DATE) = @ngayGiao
              AND ADDNM = @addNm";

            return LoadData(sql,
                new SqlParameter("@ngayGiao", ngayGiao.Date),
                new SqlParameter("@addNm", addNm));
        }

        // Kiểm tra cột có tồn tại trong bảng không (tránh "Invalid column name" khi schema DB khác nhau).
        private bool TonTaiCot(string tenBang, string tenCot)
        {
            object r = ExecuteScalar(
                "SELECT CASE WHEN COL_LENGTH(@tbl, @col) IS NULL THEN 0 ELSE 1 END",
                new SqlParameter("@tbl", tenBang),
                new SqlParameter("@col", tenCot));
            return r != null && r != DBNull.Value && Convert.ToInt32(r) == 1;
        }

        /// <summary>
        /// Đơn Giao DB đang chờ xử lý (STATUS='NG') gom theo ngày lập + nhà máy của phiếu.
        /// Dùng để báo rõ lý do khi lưới rỗng (lệch ngày / lệch nhà máy / chưa nạp lên lưới).
        /// Cột: NGAYGIAO (date), ADDNM (int), SODONG (int).
        /// </summary>
        public DataTable ThongKeDonChoXuLy()
        {
            string sql = $@"
            SELECT CAST(H.NGAYLAP AS date) AS NGAYGIAO,
                   H.NHAMAY               AS ADDNM,
                   COUNT(*)               AS SODONG
            FROM [{TableCt}] D
            INNER JOIN [{TableHd}] H ON H.IDP = D.IDP
            WHERE D.STATUS = 'NG'
            GROUP BY CAST(H.NGAYLAP AS date), H.NHAMAY
            ORDER BY CAST(H.NGAYLAP AS date), H.NHAMAY";
            return LoadData(sql);
        }

        public void LuuGiaoDB(
            DataTable donHang, string gioFccMoTa, int addNm,
            string tmpTable, string ifsTable, string nhaMayOverride = "",
            string docQrTable = "DOCQRCODE_DB")
        {
            if (donHang == null) throw new ArgumentNullException(nameof(donHang));

            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(ifsTable);
            Db.ValidateTableName(docQrTable);

            // DropCreate + BulkInsert + SP + UPDATE cuối: một transaction duy nhất.
            // Trước đây 3 bước đầu tự mở connection riêng (auto-commit), chỉ UPDATE cuối
            // tham gia Uow → lỗi giữa chừng để lại bảng IFS/TMP ở trạng thái dở.
            InTransaction(() => LuuGiaoDBCore(donHang, gioFccMoTa, addNm, tmpTable, ifsTable, nhaMayOverride, docQrTable));
        }

        private void LuuGiaoDBCore(
            DataTable donHang, string gioFccMoTa, int addNm,
            string tmpTable, string ifsTable, string nhaMayOverride, string docQrTable)
        {
            Db.DropCreate(Connection, Transaction, ifsTable, donHang);
            Db.BulkInsert(Connection, Transaction, ifsTable, donHang);

            string nhaMay = !string.IsNullOrWhiteSpace(nhaMayOverride)
                ? nhaMayOverride
                : (addNm == 1
                    ? "HON DA - VIET NAM(NHA MAY VP)"
                    : "HON DA - VIET NAM(NHA MAY HA NAM)");

            var tables = new PhieuTableSet(tmpTable, ifsTable, docQrTable);

            Db.CallPhieuSP(
                Connection, Transaction,
                "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                nhaMay, gioFccMoTa, addNm, tables);

            string sql = $@"
            UPDATE D
            SET D.GGFCC = T.GIOGIAO, D.LOT = T.LOT, D.NGAYGIAO = T.NGAYGIAO, D.STATUS = 'OK'
            FROM [{tmpTable}] T
            INNER JOIN [{TableCt}] D
                ON D.MAHANG = T.MAHANG
               AND D.IDP = SUBSTRING(T.TTPHIEU, CHARINDEX('-', T.TTPHIEU) + 1, LEN(T.TTPHIEU))
               AND D.STATUS = 'NG'
               AND T.LOT <> ''";

            ExecuteNonQuery(sql);
        }

        // Đơn chờ xử lý = các dòng STATUS='NG' của bảng nghiệp vụ FVN_Purchase_Order_DB_CT.
        public DataTable BuildDonHangTuUpload()
        {
            string sql = $@"
            SELECT
                D.IDP,
                H.NGAYLAP,
                H.NHAMAY  AS ADDNM_HEADER,
                D.MaHang  AS MAHANG,
                D.TenHang AS TENHANG,
                D.SoLuong AS SOLUONG,
                D.GioGiao AS GIOGIAO,
                D.NhaMay  AS NHAMAY,
                D.CUA,
                D.TRUYEN
            FROM [{TableCt}] D
            INNER JOIN [{TableHd}] H ON H.IDP = D.IDP
            WHERE D.STATUS = 'NG'";

            DataTable raw = LoadData(sql);

            var dt = new DataTable();
            dt.Columns.Add("STT", typeof(string));
            dt.Columns.Add("CUA", typeof(string));
            dt.Columns.Add("TRUYEN", typeof(string));
            dt.Columns.Add("MAHANG", typeof(string));
            dt.Columns.Add("TENHANG", typeof(string));
            dt.Columns.Add("LOT", typeof(string));
            dt.Columns.Add("DV", typeof(string));
            dt.Columns.Add("SOLUONG", typeof(int));
            dt.Columns.Add("NGAYGIAO", typeof(DateTime));
            dt.Columns.Add("GIOGIAO", typeof(string));
            dt.Columns.Add("STATUS", typeof(string));
            dt.Columns.Add("TTPHIEU", typeof(string));
            dt.Columns.Add("NHAMAY", typeof(string));
            dt.Columns.Add("ADDNM", typeof(int));
            dt.Columns.Add("HOP", typeof(string));
            dt.Columns.Add("STATUSDOC", typeof(string));
            dt.Columns.Add("Note", typeof(string));
            dt.Columns.Add("PO_NO", typeof(string));
            dt.Columns.Add("PO_ITEM", typeof(string));

            int stt = 1;
            foreach (DataRow r in raw.Rows)
            {
                string idp = r["IDP"].ToString();
                var row = dt.NewRow();
                row["STT"] = (stt++).ToString();
                row["CUA"] = r["CUA"]?.ToString() ?? "";
                row["TRUYEN"] = r["TRUYEN"]?.ToString() ?? "";
                row["MAHANG"] = r["MAHANG"]?.ToString() ?? "";
                row["TENHANG"] = r["TENHANG"]?.ToString() ?? "";
                row["LOT"] = "";
                row["DV"] = "PCS";
                row["SOLUONG"] = DbValueHelper.SafeInt(r["SOLUONG"]);
                row["NGAYGIAO"] = r["NGAYLAP"] == DBNull.Value ? (object)DateTime.Now : r["NGAYLAP"];
                row["GIOGIAO"] = r["GIOGIAO"]?.ToString() ?? "";
                row["STATUS"] = "NG";
                row["TTPHIEU"] = $"GIAODB-{idp}";
                row["NHAMAY"] = r["NHAMAY"]?.ToString() ?? "";
                row["ADDNM"] = DbValueHelper.SafeInt(r["ADDNM_HEADER"]);
                row["HOP"] = "";
                row["STATUSDOC"] = "NG";
                row["Note"] = "";
                row["PO_NO"] = "";
                row["PO_ITEM"] = "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ✅ THAY THẾ HOÀN TOÀN SinhIDPMoi() + UploadChiTietGiaoDB() cũ.
        // Header tạo TRƯỚC (IDENTITY tự sinh, không tính tay MAX+1 nữa),
        // Detail tạo SAU cùng đúng IDP vừa sinh — tất cả trong 1 Uow transaction.
        public int TaoPhieuVaChiTietGiaoDB(
            string ten, DateTime ngayLap, int nhaMay, string nhaMayName,
            string note, DataTable chiTiet)
        {
            if (chiTiet == null || chiTiet.Rows.Count == 0)
                throw new ArgumentException("Chi tiết phiếu không được rỗng.", nameof(chiTiet));

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                // IDP là IDENTITY ở một số DB nhưng chỉ là INT NOT NULL (không identity) ở DB khác
                // (vd DB B7R2_FCC đang chạy) -> kiểm tra rồi chọn cách sinh IDP phù hợp.
                bool idpLaIdentity = false;
                object idCheck = ExecuteScalar(
                    $"SELECT ISNULL(COLUMNPROPERTY(OBJECT_ID(N'dbo.{TableHd}'), N'IDP', 'IsIdentity'), 0)");
                if (idCheck != null && idCheck != DBNull.Value)
                    idpLaIdentity = Convert.ToInt32(idCheck) == 1;

                string sqlHeader = idpLaIdentity
                    ? $@"
                INSERT INTO [{TableHd}]
                    (NAME, TRANGTHAIGIAO, Note, NGAYLAP, NHAMAY, NHAMAYNAME)
                OUTPUT INSERTED.IDP
                VALUES (@ten, 0, @note, @ngayLap, @nhaMay, @nhaMayName)"
                    // Không identity: tự cấp IDP = MAX+1 trong CÙNG câu lệnh, khóa UPDLOCK/HOLDLOCK
                    // để 2 máy lưu cùng lúc không trùng IDP (nằm trong transaction của Uow).
                    : $@"
                INSERT INTO [{TableHd}]
                    (IDP, NAME, TRANGTHAIGIAO, Note, NGAYLAP, NHAMAY, NHAMAYNAME)
                OUTPUT INSERTED.IDP
                SELECT ISNULL(MAX(IDP), 0) + 1, @ten, 0, @note, @ngayLap, @nhaMay, @nhaMayName
                FROM [{TableHd}] WITH (UPDLOCK, HOLDLOCK)";

                object idpObj = ExecuteScalar(sqlHeader,
                    new SqlParameter("@ten", (object)ten ?? DBNull.Value),
                    new SqlParameter("@note", (object)note ?? DBNull.Value),
                    new SqlParameter("@ngayLap", ngayLap),
                    new SqlParameter("@nhaMay", nhaMay),
                    new SqlParameter("@nhaMayName", (object)nhaMayName ?? DBNull.Value));

                int idp = Convert.ToInt32(idpObj);

                // Bảng chi tiết cũng có thể có ID INT NOT NULL (không identity) -> tự cấp ID = MAX+1.
                bool idCtLaIdentity = false;
                object idCtCheck = ExecuteScalar(
                    $"SELECT ISNULL(COLUMNPROPERTY(OBJECT_ID(N'dbo.{TableCt}'), N'ID', 'IsIdentity'), 0)");
                if (idCtCheck != null && idCtCheck != DBNull.Value)
                    idCtLaIdentity = Convert.ToInt32(idCtCheck) == 1;

                string sqlChiTiet = idCtLaIdentity
                    ? $@"
                    INSERT INTO [{TableCt}]
                        (IDP, MAHANG, TENHANG, SOLUONG, GIOGIAO, NHAMAY, CUA, TRUYEN, STATUS, TTNHAN)
                    VALUES
                        (@idp, @ma, @ten, @sl, @gio, @nm, @cua, @tr, 'NG', 1)"
                    : $@"
                    INSERT INTO [{TableCt}]
                        (ID, IDP, MAHANG, TENHANG, SOLUONG, GIOGIAO, NHAMAY, CUA, TRUYEN, STATUS, TTNHAN)
                    SELECT ISNULL(MAX(ID), 0) + 1, @idp, @ma, @ten, @sl, @gio, @nm, @cua, @tr, 'NG', 1
                    FROM [{TableCt}] WITH (UPDLOCK, HOLDLOCK)";

                foreach (DataRow row in chiTiet.Rows)
                {
                    ExecuteNonQuery(sqlChiTiet,
                        new SqlParameter("@idp", idp),
                        new SqlParameter("@ma", row["MaHang"]),
                        new SqlParameter("@ten", row["TenHang"]),
                        new SqlParameter("@sl", row["SoLuong"]),
                        new SqlParameter("@gio", row["GioGiao"]),
                        new SqlParameter("@nm", nhaMayName),
                        new SqlParameter("@cua", row["CUA"]),
                        new SqlParameter("@tr", row["TRUYEN"]));
                }

                if (ownTransaction) Uow.Commit();
                return idp;
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }
    }
}
