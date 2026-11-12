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

        // ============================================================
        // PHIẾU GIAO DB: 1 NGÀY LẬP + 1 NHÀ MÁY (khách hàng HVN) = TỐI ĐA 1 PHIẾU (header)
        // + 1..n chi tiết, chi tiết có thể thêm ở nhiều thời điểm.
        // Dòng "CHƯA GIAO" (STATUS = 'NG' hoặc NULL) mới được sửa / xóa; dòng đã xử lý
        // (STATUS <> 'NG', do LuuGiaoDB đặt 'OK' sau khi gán LOT) bị KHÓA.
        // ============================================================

        // Điều kiện "chưa giao" dùng chung cho mọi câu lệnh sửa/xóa (đổi ở MỘT chỗ nếu nghiệp vụ đổi).
        private const string ChuaGiao = "ISNULL(D.STATUS, 'NG') = 'NG'";

        private static SqlParameter PNgay(DateTime ngay) =>
            new SqlParameter("@ngay", SqlDbType.Date) { Value = ngay.Date };

        /// <summary>
        /// Header của phiếu theo ngày + nhà máy (0 hoặc 1 dòng): IDP, NAME, Note, NGAYLAP, NHAMAY,
        /// NHAMAYNAME, SOPHIEU (số header trùng khóa — bình thường = 1; &gt;1 là dữ liệu cũ bị trùng,
        /// khi đó lấy header có IDP nhỏ nhất làm phiếu chính).
        /// </summary>
        public DataTable GetHeaderGiaoDB(DateTime ngayLap, int nhaMay)
        {
            string sql = $@"
            SELECT TOP (1) H.IDP, H.NAME, H.Note, H.NGAYLAP, H.NHAMAY, H.NHAMAYNAME,
                   (SELECT COUNT(*) FROM [{TableHd}] X
                    WHERE CAST(X.NGAYLAP AS date) = @ngay AND X.NHAMAY = @nm) AS SOPHIEU
            FROM [{TableHd}] H
            WHERE CAST(H.NGAYLAP AS date) = @ngay AND H.NHAMAY = @nm
            ORDER BY H.IDP";
            return LoadData(sql, PNgay(ngayLap), new SqlParameter("@nm", nhaMay));
        }

        /// <summary>
        /// Chi tiết của phiếu theo ngày + nhà máy (gộp cả header trùng khóa nếu có):
        /// ID, IDP, MaHang, TenHang, SoLuong, GioGiao, CUA, TRUYEN, DaGiao (bit: 1 = đã xử lý, bị khóa).
        /// </summary>
        public DataTable GetChiTietGiaoDB(DateTime ngayLap, int nhaMay)
        {
            string sql = $@"
            SELECT D.ID, D.IDP,
                   ISNULL(D.MAHANG, '')  AS MaHang,
                   ISNULL(D.TENHANG, '') AS TenHang,
                   CAST(ISNULL(D.SOLUONG, 0) AS int) AS SoLuong,
                   ISNULL(D.GIOGIAO, '') AS GioGiao,
                   ISNULL(D.CUA, '')     AS CUA,
                   ISNULL(D.TRUYEN, '')  AS TRUYEN,
                   CAST(CASE WHEN {ChuaGiao} THEN 0 ELSE 1 END AS bit) AS DaGiao
            FROM [{TableCt}] D
            INNER JOIN [{TableHd}] H ON H.IDP = D.IDP
            WHERE CAST(H.NGAYLAP AS date) = @ngay AND H.NHAMAY = @nm
            ORDER BY D.ID";
            return LoadData(sql, PNgay(ngayLap), new SqlParameter("@nm", nhaMay));
        }

        // Giữ tên cũ cho code gọi cũ: nay cũng theo quy tắc 1 ngày + 1 nhà máy = 1 phiếu
        // (đã có phiếu thì THÊM chi tiết vào phiếu đó, không tạo header thứ hai).
        public int TaoPhieuVaChiTietGiaoDB(
            string ten, DateTime ngayLap, int nhaMay, string nhaMayName,
            string note, DataTable chiTiet)
        {
            if (chiTiet == null || chiTiet.Rows.Count == 0)
                throw new ArgumentException("Chi tiết phiếu không được rỗng.", nameof(chiTiet));

            return LuuPhieuGiaoDB(ten, ngayLap, nhaMay, nhaMayName, note, chiTiet);
        }

        /// <summary>
        /// Tạo mới HOẶC cập nhật phiếu Giao DB của (ngày lập, nhà máy) trong MỘT transaction.
        /// <para>chiTiet theo RowState: Added (ID null) → thêm; Modified → sửa; Deleted → xóa;
        /// Unchanged → bỏ qua. Sửa/xóa dòng đã giao (hoặc không còn thuộc phiếu) → ném
        /// <see cref="InvalidOperationException"/> và rollback toàn bộ.</para>
        /// Cột bắt buộc: MaHang, TenHang, SoLuong, GioGiao, CUA, TRUYEN; cột ID (nếu có) = ID dòng DB.
        /// </summary>
        public int LuuPhieuGiaoDB(
            string ten, DateTime ngayLap, int nhaMay, string nhaMayName,
            string note, DataTable chiTiet)
        {
            if (chiTiet == null) throw new ArgumentNullException(nameof(chiTiet));

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                DateTime ngay = ngayLap.Date;

                // Khóa (UPDLOCK/HOLDLOCK) rồi tìm header của khóa nghiệp vụ -> 2 máy lưu cùng lúc
                // không thể cùng tạo 2 phiếu cho một ngày + nhà máy.
                object found = ExecuteScalar($@"
                    SELECT TOP (1) IDP FROM [{TableHd}] WITH (UPDLOCK, HOLDLOCK)
                    WHERE CAST(NGAYLAP AS date) = @ngay AND NHAMAY = @nm
                    ORDER BY IDP",
                    PNgay(ngay), new SqlParameter("@nm", nhaMay));

                int idp;
                if (found == null || found == DBNull.Value)
                {
                    idp = TaoHeader(ten, ngay, nhaMay, nhaMayName, note);
                }
                else
                {
                    idp = Convert.ToInt32(found);
                    ExecuteNonQuery($@"
                        UPDATE [{TableHd}]
                        SET NAME = @ten, Note = @note, NHAMAYNAME = @nhaMayName
                        WHERE CAST(NGAYLAP AS date) = @ngay AND NHAMAY = @nm",
                        new SqlParameter("@ten", (object)ten ?? DBNull.Value),
                        new SqlParameter("@note", (object)note ?? DBNull.Value),
                        new SqlParameter("@nhaMayName", (object)nhaMayName ?? DBNull.Value),
                        PNgay(ngay), new SqlParameter("@nm", nhaMay));
                }

                KiemTraDongSuaXoa(chiTiet, ngay, nhaMay);

                bool idCtLaIdentity = false;
                object idCtCheck = ExecuteScalar(
                    $"SELECT ISNULL(COLUMNPROPERTY(OBJECT_ID(N'dbo.{TableCt}'), N'ID', 'IsIdentity'), 0)");
                if (idCtCheck != null && idCtCheck != DBNull.Value)
                    idCtLaIdentity = Convert.ToInt32(idCtCheck) == 1;

                foreach (DataRow row in chiTiet.Rows)
                {
                    switch (row.RowState)
                    {
                        case DataRowState.Added:
                        case DataRowState.Detached:
                            ThemChiTiet(idp, row, nhaMayName, idCtLaIdentity);
                            break;
                        case DataRowState.Modified:
                            SuaChiTiet(row);
                            break;
                        case DataRowState.Deleted:
                            XoaChiTiet(row);
                            break;
                    }
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

        /// <summary>
        /// Xóa cả phiếu (header + toàn bộ chi tiết) của ngày + nhà máy.
        /// Chỉ cho xóa khi KHÔNG còn dòng nào đã giao. Trả về số chi tiết đã xóa.
        /// </summary>
        public int XoaPhieuGiaoDB(DateTime ngayLap, int nhaMay)
        {
            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                var pNm = new SqlParameter("@nm", nhaMay);

                object daGiao = ExecuteScalar($@"
                    SELECT COUNT(*) FROM [{TableCt}] D WITH (UPDLOCK, HOLDLOCK)
                    INNER JOIN [{TableHd}] H ON H.IDP = D.IDP
                    WHERE CAST(H.NGAYLAP AS date) = @ngay AND H.NHAMAY = @nm
                      AND NOT ({ChuaGiao})",
                    PNgay(ngayLap), pNm);
                int soDaGiao = Convert.ToInt32(daGiao);
                if (soDaGiao > 0)
                    throw new InvalidOperationException(
                        $"Phiếu có {soDaGiao} dòng đã giao nên không thể xóa cả phiếu. " +
                        "Chỉ xóa được từng dòng chưa giao.");

                int soCt = ExecuteNonQuery($@"
                    DELETE D FROM [{TableCt}] D
                    INNER JOIN [{TableHd}] H ON H.IDP = D.IDP
                    WHERE CAST(H.NGAYLAP AS date) = @ngay AND H.NHAMAY = @nm",
                    PNgay(ngayLap), new SqlParameter("@nm", nhaMay));

                ExecuteNonQuery($@"
                    DELETE FROM [{TableHd}]
                    WHERE CAST(NGAYLAP AS date) = @ngay AND NHAMAY = @nm",
                    PNgay(ngayLap), new SqlParameter("@nm", nhaMay));

                if (ownTransaction) Uow.Commit();
                return soCt;
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private int TaoHeader(string ten, DateTime ngay, int nhaMay, string nhaMayName, string note)
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
                new SqlParameter("@ngayLap", ngay),
                new SqlParameter("@nhaMay", nhaMay),
                new SqlParameter("@nhaMayName", (object)nhaMayName ?? DBNull.Value));

            return Convert.ToInt32(idpObj);
        }

        private static object GiaTri(DataRow r, string col)
        {
            object v = r.RowState == DataRowState.Deleted ? r[col, DataRowVersion.Original] : r[col];
            return v;
        }

        private static int? IdDong(DataRow r)
        {
            if (!r.Table.Columns.Contains("ID")) return null;
            object v = GiaTri(r, "ID");
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt32(v);
        }

        // Dòng sửa/xóa phải (1) còn thuộc phiếu này và (2) chưa giao. Kiểm tra TRƯỚC khi ghi để báo
        // lỗi rõ ràng; câu UPDATE/DELETE bên dưới vẫn kèm điều kiện chưa giao (chống đua giữa 2 máy).
        private void KiemTraDongSuaXoa(DataTable chiTiet, DateTime ngay, int nhaMay)
        {
            var ids = new List<int>();
            foreach (DataRow r in chiTiet.Rows)
            {
                if (r.RowState != DataRowState.Modified && r.RowState != DataRowState.Deleted) continue;
                int? id = IdDong(r);
                if (id == null)
                    throw new InvalidOperationException("Dòng cần sửa/xóa không có ID (dữ liệu form không hợp lệ).");
                ids.Add(id.Value);
            }
            if (ids.Count == 0) return;

            // ids là int → ghép chuỗi an toàn.
            DataTable db = LoadData($@"
                SELECT D.ID, D.MAHANG, CAST(CASE WHEN {ChuaGiao} THEN 0 ELSE 1 END AS bit) AS DaGiao
                FROM [{TableCt}] D
                INNER JOIN [{TableHd}] H ON H.IDP = D.IDP
                WHERE D.ID IN ({string.Join(",", ids)})
                  AND CAST(H.NGAYLAP AS date) = @ngay AND H.NHAMAY = @nm",
                PNgay(ngay), new SqlParameter("@nm", nhaMay));

            var trangThai = new Dictionary<int, bool>();
            var tenMa = new Dictionary<int, string>();
            foreach (DataRow d in db.Rows)
            {
                int id = Convert.ToInt32(d["ID"]);
                trangThai[id] = Convert.ToBoolean(d["DaGiao"]);
                tenMa[id] = Convert.ToString(d["MAHANG"]);
            }

            foreach (int id in ids)
            {
                if (!trangThai.TryGetValue(id, out bool daGiao))
                    throw new InvalidOperationException(
                        $"Dòng chi tiết #{id} không còn thuộc phiếu này (có thể đã bị xóa ở máy khác). " +
                        "Hãy chọn lại ngày/nhà máy để tải lại phiếu.");
                if (daGiao)
                    throw new InvalidOperationException(
                        $"Dòng {tenMa[id]} (#{id}) đã giao nên không được sửa/xóa. Hãy tải lại phiếu.");
            }
        }

        private void ThemChiTiet(int idp, DataRow row, string nhaMayName, bool idCtLaIdentity)
        {
            string sql = idCtLaIdentity
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

            ExecuteNonQuery(sql,
                new SqlParameter("@idp", idp),
                new SqlParameter("@ma", row["MaHang"]),
                new SqlParameter("@ten", row["TenHang"]),
                new SqlParameter("@sl", row["SoLuong"]),
                new SqlParameter("@gio", row["GioGiao"]),
                new SqlParameter("@nm", (object)nhaMayName ?? DBNull.Value),
                new SqlParameter("@cua", row["CUA"]),
                new SqlParameter("@tr", row["TRUYEN"]));
        }

        private void SuaChiTiet(DataRow row)
        {
            int id = IdDong(row).Value;
            int n = ExecuteNonQuery($@"
                UPDATE D
                SET D.MAHANG = @ma, D.TENHANG = @ten, D.SOLUONG = @sl,
                    D.GIOGIAO = @gio, D.CUA = @cua, D.TRUYEN = @tr
                FROM [{TableCt}] D
                WHERE D.ID = @id AND {ChuaGiao}",
                new SqlParameter("@id", id),
                new SqlParameter("@ma", row["MaHang"]),
                new SqlParameter("@ten", row["TenHang"]),
                new SqlParameter("@sl", row["SoLuong"]),
                new SqlParameter("@gio", row["GioGiao"]),
                new SqlParameter("@cua", row["CUA"]),
                new SqlParameter("@tr", row["TRUYEN"]));
            if (n == 0)
                throw new InvalidOperationException(
                    $"Dòng #{id} vừa được xử lý/giao ở máy khác nên không sửa được. Hãy tải lại phiếu.");
        }

        private void XoaChiTiet(DataRow row)
        {
            int id = IdDong(row).Value;
            int n = ExecuteNonQuery($@"
                DELETE D FROM [{TableCt}] D
                WHERE D.ID = @id AND {ChuaGiao}",
                new SqlParameter("@id", id));
            if (n == 0)
                throw new InvalidOperationException(
                    $"Dòng #{id} vừa được xử lý/giao ở máy khác nên không xóa được. Hãy tải lại phiếu.");
        }
    }
}
