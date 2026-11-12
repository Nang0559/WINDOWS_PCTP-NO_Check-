using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// Xem / sửa / xóa các đơn đã upload vào BẢNG ĐƠN RIÊNG (Purchase_Order_YMVN, Purchase_Order_HTN, ...).
    /// Dòng đơn được nhận diện bằng khóa tự nhiên PO + mã hàng + ngày giờ giao (bảng không có cột ID),
    /// giống OrderLineKey / OrderTableSql.MarkDelivered. Dòng ĐÃ GIAO (IsDelivered = 1) không bao giờ sửa/xóa.
    /// </summary>
    public sealed class UploadedOrderRepository
    {
        private static readonly Regex SafeName =
            new Regex("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant);

        private readonly string _connectionString;
        private readonly string _table;

        public UploadedOrderRepository(string connectionString, string orderTable)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Thiếu chuỗi kết nối.", nameof(connectionString));
            if (orderTable == null || !SafeName.IsMatch(orderTable))
                throw new ArgumentException("Tên bảng đơn hàng không hợp lệ.", nameof(orderTable));

            _connectionString = connectionString;
            _table = orderTable;
        }

        public string TableName => _table;

        /// <summary>Thông tin cấu trúc bảng đích (có cột IsDelivered không, độ rộng tối đa từng cột chữ).</summary>
        public sealed class TableInfo
        {
            public bool HasIsDelivered { get; set; }
            public bool HasDeliveredDate { get; set; }
            public Dictionary<string, int> MaxLength { get; } =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public TableInfo GetInfo()
        {
            var info = new TableInfo();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_NAME = @t", conn))
                {
                    cmd.Parameters.AddWithValue("@t", _table);
                    using (var rd = cmd.ExecuteReader())
                    {
                        int soCot = 0;
                        while (rd.Read())
                        {
                            soCot++;
                            string col = rd.GetString(0);
                            if (string.Equals(col, "IsDelivered", StringComparison.OrdinalIgnoreCase))
                                info.HasIsDelivered = true;
                            else if (string.Equals(col, "DeliveredDate", StringComparison.OrdinalIgnoreCase))
                                info.HasDeliveredDate = true;

                            if (!rd.IsDBNull(1) && rd.GetInt32(1) > 0)   // -1 (MAX) bỏ qua
                                info.MaxLength[col] = rd.GetInt32(1);
                        }
                        if (soCot == 0)
                            throw new InvalidOperationException($"Không tìm thấy bảng [{_table}] trong DB.");
                    }
                }
            }
            return info;
        }

        /// <summary>Đơn có NgayGiao trong [tuNgay, denNgay] (theo ngày). Có cột o_* giữ khóa gốc để sửa/xóa.</summary>
        public DataTable Load(DateTime tuNgay, DateTime denNgay, TableInfo info)
        {
            string cotGiao = info.HasIsDelivered
                ? ", CAST(ISNULL(IsDelivered, 0) AS bit) AS DaGiao"
                : ", CAST(0 AS bit) AS DaGiao";
            string cotNgayGiao = info.HasDeliveredDate ? ", DeliveredDate" : ", CAST(NULL AS datetime) AS DeliveredDate";

            string sql = $@"
                SELECT Oder_no, Part_no, Part_name, NgayGiao,
                       ISNULL(Slgiao, 0) AS Slgiao, ISNULL(QCDG, 0) AS QCDG, Gear, CUA
                       {cotGiao}{cotNgayGiao}
                FROM [{_table}]
                WHERE NgayGiao >= @tu AND NgayGiao < @den
                ORDER BY NgayGiao, Oder_no, Part_no";

            var dt = TaoBang(info);
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@tu", SqlDbType.DateTime).Value = tuNgay.Date;
                    cmd.Parameters.Add("@den", SqlDbType.DateTime).Value = denNgay.Date.AddDays(1);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            DataRow r = dt.NewRow();
                            r["Oder_no"] = Txt(rd["Oder_no"]);
                            r["Part_no"] = Txt(rd["Part_no"]);
                            r["Part_name"] = Txt(rd["Part_name"]);
                            r["NgayGiao"] = rd["NgayGiao"];
                            r["Slgiao"] = Convert.ToInt32(rd["Slgiao"]);
                            r["QCDG"] = Convert.ToInt32(rd["QCDG"]);
                            r["Gear"] = Txt(rd["Gear"]);
                            r["CUA"] = Txt(rd["CUA"]);        // nchar → cắt khoảng trắng đệm
                            bool daGiao = Convert.ToBoolean(rd["DaGiao"]);
                            r["DaGiao"] = daGiao;
                            r["TrangThai"] = daGiao ? "Đã giao" : "Chưa giao";
                            r["DeliveredDate"] = rd["DeliveredDate"];
                            r["o_Oder_no"] = r["Oder_no"];
                            r["o_Part_no"] = r["Part_no"];
                            r["o_NgayGiao"] = r["NgayGiao"];
                            dt.Rows.Add(r);
                        }
                    }
                }
            }
            dt.AcceptChanges();   // dòng nạp từ DB = Unchanged; thêm / sửa / xóa sau đó mới thành thay đổi
            return dt;
        }

        public static DataTable TaoBang(TableInfo info)
        {
            var dt = new DataTable();
            AddText(dt, "Oder_no", info);
            AddText(dt, "Part_no", info);
            AddText(dt, "Part_name", info);
            dt.Columns.Add("NgayGiao", typeof(DateTime));
            dt.Columns.Add("Slgiao", typeof(int));
            dt.Columns.Add("QCDG", typeof(int));
            AddText(dt, "Gear", info);
            AddText(dt, "CUA", info);
            dt.Columns.Add("TrangThai", typeof(string));
            dt.Columns.Add("DaGiao", typeof(bool));
            dt.Columns.Add("DeliveredDate", typeof(DateTime));
            dt.Columns.Add("o_Oder_no", typeof(string));
            dt.Columns.Add("o_Part_no", typeof(string));
            dt.Columns.Add("o_NgayGiao", typeof(DateTime));
            return dt;
        }

        private static void AddText(DataTable dt, string name, TableInfo info)
        {
            // Không đặt DataColumn.MaxLength (gán chuỗi dài sẽ ném lỗi ngay trong ô lưới);
            // độ rộng tối đa được áp ở editor + kiểm tra trước khi Lưu (xem UploadedOrderManagerControl).
            dt.Columns.Add(name, typeof(string));
        }

        private static string Txt(object v) =>
            v == null || v == DBNull.Value ? "" : Convert.ToString(v, CultureInfo.InvariantCulture).Trim();

        private static DateTime Phut(DateTime d) =>
            new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0);   // smalldatetime không có giây

        private static object Orig(DataRow r, string col) =>
            r.RowState == DataRowState.Added ? r[col] : r[col, DataRowVersion.Original];

        private static string Mo(string po, string part, object ngay) =>
            $"PO {po} / {part} / {Convert.ToDateTime(ngay):dd/MM/yyyy HH:mm}";

        /// <summary>
        /// Lưu thay đổi trong MỘT transaction (lỗi một dòng → rollback toàn bộ). Dòng Added → thêm,
        /// Modified → sửa, Deleted → xóa. Sửa/xóa dòng đã giao, dòng không còn tồn tại hoặc khóa bị trùng
        /// (không xác định được dòng nào) đều bị từ chối. Trả về số dòng đã ghi.
        /// </summary>
        public int Save(DataTable dt, TableInfo info)
        {
            if (dt == null) throw new ArgumentNullException(nameof(dt));
            if (!info.HasIsDelivered)
                throw new InvalidOperationException(
                    $"Bảng [{_table}] chưa có cột IsDelivered nên không biết dòng nào đã giao. " +
                    "Chạy script SQL/PCTP_OrderTables_AddIsDelivered.sql rồi thử lại.");

            int soGhi = 0;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (DataRow r in dt.Rows)
                        {
                            switch (r.RowState)
                            {
                                case DataRowState.Deleted: XoaDong(conn, tran, r); soGhi++; break;
                                case DataRowState.Modified: SuaDong(conn, tran, r); soGhi++; break;
                                case DataRowState.Added: ThemDong(conn, tran, r); soGhi++; break;
                            }
                        }
                        tran.Commit();
                    }
                    catch
                    {
                        try { tran.Rollback(); } catch { /* connection có thể đã đóng */ }
                        throw;
                    }
                }
            }
            return soGhi;
        }

        // Đếm số dòng khớp khóa (khóa UPDLOCK/HOLDLOCK để 2 máy không sửa chéo nhau).
        private int Dem(SqlConnection c, SqlTransaction t, string po, string part, DateTime ngay, bool chiDaGiao)
        {
            string sql = $@"SELECT COUNT(*) FROM [{_table}] WITH (UPDLOCK, HOLDLOCK)
                            WHERE Oder_no = @o AND Part_no = @p AND NgayGiao = @n"
                         + (chiDaGiao ? " AND ISNULL(IsDelivered, 0) = 1" : "");
            using (var cmd = new SqlCommand(sql, c, t))
            {
                Khoa(cmd, po, part, ngay);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static void Khoa(SqlCommand cmd, string po, string part, DateTime ngay, string suffix = "")
        {
            cmd.Parameters.Add("@o" + suffix, SqlDbType.NVarChar, 200).Value = po;
            cmd.Parameters.Add("@p" + suffix, SqlDbType.NVarChar, 200).Value = part;
            cmd.Parameters.Add("@n" + suffix, SqlDbType.DateTime).Value = ngay;
        }

        // Dòng gốc phải tồn tại đúng 1 và chưa giao.
        private void KiemTraDongGoc(SqlConnection c, SqlTransaction t, string po, string part, DateTime ngay)
        {
            int n = Dem(c, t, po, part, ngay, false);
            if (n == 0)
                throw new InvalidOperationException(
                    $"{Mo(po, part, ngay)} không còn trong bảng (có thể đã bị xóa/đổi ở máy khác). Hãy tải lại.");
            if (n > 1)
                throw new InvalidOperationException(
                    $"{Mo(po, part, ngay)} có {n} dòng trùng khóa nên không xác định được dòng cần sửa/xóa. " +
                    "Hãy xử lý trùng trong DB trước.");
            if (Dem(c, t, po, part, ngay, true) > 0)
                throw new InvalidOperationException($"{Mo(po, part, ngay)} đã giao nên không được sửa/xóa.");
        }

        private void XoaDong(SqlConnection c, SqlTransaction t, DataRow r)
        {
            string po = Txt(Orig(r, "o_Oder_no")), part = Txt(Orig(r, "o_Part_no"));
            DateTime ngay = Convert.ToDateTime(Orig(r, "o_NgayGiao"));
            KiemTraDongGoc(c, t, po, part, ngay);

            using (var cmd = new SqlCommand($@"DELETE FROM [{_table}]
                WHERE Oder_no = @o AND Part_no = @p AND NgayGiao = @n AND ISNULL(IsDelivered, 0) = 0", c, t))
            {
                Khoa(cmd, po, part, ngay);
                if (cmd.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException($"Không xóa được {Mo(po, part, ngay)} (vừa bị giao/đổi ở máy khác).");
            }
        }

        private void SuaDong(SqlConnection c, SqlTransaction t, DataRow r)
        {
            string oPo = Txt(r["o_Oder_no"]), oPart = Txt(r["o_Part_no"]);
            DateTime oNgay = Convert.ToDateTime(r["o_NgayGiao"]);
            KiemTraDongGoc(c, t, oPo, oPart, oNgay);

            string po = Txt(r["Oder_no"]), part = Txt(r["Part_no"]);
            DateTime ngay = Phut(Convert.ToDateTime(r["NgayGiao"]));

            bool doiKhoa = !string.Equals(po, oPo, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(part, oPart, StringComparison.OrdinalIgnoreCase)
                        || ngay != oNgay;
            if (doiKhoa && Dem(c, t, po, part, ngay, false) > 0)
                throw new InvalidOperationException($"Đã có đơn {Mo(po, part, ngay)}; không thể đổi trùng khóa.");

            using (var cmd = new SqlCommand($@"UPDATE [{_table}]
                SET Oder_no = @o2, Part_no = @p2, Part_name = @pn, NgayGiao = @n2,
                    Slgiao = @sl, QCDG = @q, Gear = @g, CUA = @cua
                WHERE Oder_no = @o AND Part_no = @p AND NgayGiao = @n AND ISNULL(IsDelivered, 0) = 0", c, t))
            {
                Khoa(cmd, oPo, oPart, oNgay);
                cmd.Parameters.Add("@o2", SqlDbType.NVarChar, 200).Value = po;
                cmd.Parameters.Add("@p2", SqlDbType.NVarChar, 200).Value = part;
                cmd.Parameters.Add("@pn", SqlDbType.NVarChar, 400).Value = Txt(r["Part_name"]);
                cmd.Parameters.Add("@n2", SqlDbType.DateTime).Value = ngay;
                cmd.Parameters.Add("@sl", SqlDbType.Int).Value = Convert.ToInt32(r["Slgiao"]);
                cmd.Parameters.Add("@q", SqlDbType.Int).Value = Convert.ToInt32(r["QCDG"]);
                cmd.Parameters.Add("@g", SqlDbType.NVarChar, 400).Value = Txt(r["Gear"]);
                cmd.Parameters.Add("@cua", SqlDbType.NVarChar, 100).Value = Txt(r["CUA"]);
                if (cmd.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException($"Không sửa được {Mo(oPo, oPart, oNgay)} (vừa bị giao/đổi ở máy khác).");
            }
        }

        private void ThemDong(SqlConnection c, SqlTransaction t, DataRow r)
        {
            string po = Txt(r["Oder_no"]), part = Txt(r["Part_no"]);
            DateTime ngay = Phut(Convert.ToDateTime(r["NgayGiao"]));

            if (Dem(c, t, po, part, ngay, false) > 0)
                throw new InvalidOperationException($"Đã có đơn {Mo(po, part, ngay)}; sửa dòng đó thay vì thêm mới.");

            using (var cmd = new SqlCommand($@"INSERT INTO [{_table}]
                (Oder_no, Part_no, Part_name, NgayGiao, Slgiao, QCDG, Gear, CUA, IsDelivered)
                VALUES (@o, @p, @pn, @n, @sl, @q, @g, @cua, 0)", c, t))
            {
                Khoa(cmd, po, part, ngay);
                cmd.Parameters.Add("@pn", SqlDbType.NVarChar, 400).Value = Txt(r["Part_name"]);
                cmd.Parameters.Add("@sl", SqlDbType.Int).Value = Convert.ToInt32(r["Slgiao"]);
                cmd.Parameters.Add("@q", SqlDbType.Int).Value = Convert.ToInt32(r["QCDG"]);
                cmd.Parameters.Add("@g", SqlDbType.NVarChar, 400).Value = Txt(r["Gear"]);
                cmd.Parameters.Add("@cua", SqlDbType.NVarChar, 100).Value = Txt(r["CUA"]);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
