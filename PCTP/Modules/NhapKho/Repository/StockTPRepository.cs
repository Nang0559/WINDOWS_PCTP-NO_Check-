using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Domain.Events;
using PCTP.Infrastructure;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public sealed class StockTpRepository : SqlRepositoryBase, IStockTpRepository
    {
        public StockTpRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // TRA CỨU STOCKTP
        // ============================================================

        public bool ExistsStockTp(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot))
                return false;

            const string sql = @"
        SELECT COUNT(1)
        FROM STOCKTP
        WHERE LOT = @lot;";

            object result = ExecuteScalar(sql, new SqlParameter("@lot", lot));
            return ToInt(result) > 0;
        }

        public StockItem GetByLot(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot))
                return null;

            const string sql = @"
        SELECT LOT, PART, NAME, MODEL, SLNHAP, SLCONLAI, SLXUAT,
               SATUS, CASX, NGAYSX, NGAYNHAP
        FROM STOCKTP
        WHERE LOT = @lot;";

            DataTable dt = LoadData(sql, new SqlParameter("@lot", lot));
            if (dt == null || dt.Rows.Count == 0)
                return null;

            return MapStockItem(dt.Rows[0]);
        }

        public int GetSlConLai(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot))
                return 0;

            const string sql = @"
        SELECT ISNULL(SLCONLAI, 0)
        FROM STOCKTP
        WHERE LOT = @lot;";

            return ToInt(ExecuteScalar(sql, new SqlParameter("@lot", lot)));
        }

        public int GetSlDaNhap(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot))
                return 0;

            const string sql = @"
        SELECT ISNULL(SLNHAP, 0)
        FROM STOCKTP
        WHERE LOT = @lot;";

            return ToInt(ExecuteScalar(sql, new SqlParameter("@lot", lot)));
        }

        // ============================================================
        // NHẬP KHO
        // ============================================================

        public void InsertStockTp(NhapKhoItem item, int status)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(item.Lot))
                throw new ArgumentException("LOT không được để trống.", nameof(item));
            if (item.SlNhap <= 0)
                throw new ArgumentOutOfRangeException(nameof(item.SlNhap), "Số lượng nhập phải lớn hơn 0.");

            const string sql = @"
        INSERT INTO STOCKTP
        (LOT, MODEL, PART, NAME, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, SATUS)
        VALUES
        (@lot, @model, @part, @name, @casx, @ngaysx, @slsx, GETDATE(), @slnhap, GETDATE(), 0, @slconlai, @status);";

            ExecuteNonQuery(sql,
                new SqlParameter("@lot", item.Lot),
                new SqlParameter("@model", (object)item.Model ?? DBNull.Value),
                new SqlParameter("@part", (object)item.Part ?? DBNull.Value),
                new SqlParameter("@name", (object)item.Name ?? DBNull.Value),
                new SqlParameter("@casx", item.CaSX),
                new SqlParameter("@ngaysx", (object)item.NgaySX ?? DBNull.Value),
                new SqlParameter("@slsx", item.SlSanXuat),
                new SqlParameter("@slnhap", item.SlNhap),
                new SqlParameter("@slconlai", item.SlNhap),
                new SqlParameter("@status", status));
        }

        public void UpdateStockTp(string lot, int slSeNhap, int status)
        {
            if (string.IsNullOrWhiteSpace(lot))
                throw new ArgumentException("LOT không được để trống.", nameof(lot));
            if (slSeNhap <= 0)
                throw new ArgumentOutOfRangeException(nameof(slSeNhap), "Số lượng nhập phải lớn hơn 0.");

            const string sql = @"
        UPDATE STOCKTP
        SET SLNHAP = ISNULL(SLNHAP, 0) + @sl,
            SLCONLAI = ISNULL(SLCONLAI, 0) + @sl,
            NGAYNHAP = GETDATE(),
            SATUS = @status
        WHERE LOT = @lot;";

            int affected = ExecuteNonQuery(sql,
                new SqlParameter("@sl", slSeNhap),
                new SqlParameter("@status", status),
                new SqlParameter("@lot", lot));

            if (affected == 0)
                throw new InvalidOperationException($"Không tìm thấy LOT '{lot}' trong STOCKTP.");
        }

        // ============================================================
        // XUẤT KHO THẬT
        // ============================================================

        public void XuatKhoThat(string lot, int soLuong)
        {
            if (string.IsNullOrWhiteSpace(lot))
                throw new ArgumentException("LOT không được để trống.", nameof(lot));
            if (soLuong <= 0)
                throw new ArgumentOutOfRangeException(nameof(soLuong), "Số lượng xuất phải lớn hơn 0.");

            const string sql = @"
        UPDATE STOCKTP
        SET SLXUAT = ISNULL(SLXUAT, 0) + @sl,
            SLCONLAI = ISNULL(SLCONLAI, 0) - @sl,
            NGAYXUAT = GETDATE()
        WHERE LOT = @lot AND ISNULL(SLCONLAI, 0) >= @sl;";

            int affected = ExecuteNonQuery(sql,
                new SqlParameter("@sl", soLuong),
                new SqlParameter("@lot", lot));

            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không thể xuất {soLuong} sản phẩm của LOT '{lot}'. " +
                    "LOT không tồn tại hoặc số lượng tồn không đủ.");
        }

        // ============================================================
        // ĐỐI CHIẾU TỒN KHO
        // ============================================================

        public List<(string Lot, int SlConLai)> GetDanhSachLotConTon()
        {
            const string sql = @"
        SELECT LOT, ISNULL(SLCONLAI, 0) AS SLCONLAI
        FROM STOCKTP
        WHERE ISNULL(SLCONLAI, 0) > 0
        ORDER BY LOT;";

            DataTable dt = LoadData(sql);
            var result = new List<(string Lot, int SlConLai)>();
            if (dt == null) return result;

            foreach (DataRow row in dt.Rows)
            {
                if (row["LOT"] == DBNull.Value) continue;
                string lot = row["LOT"].ToString();
                if (string.IsNullOrWhiteSpace(lot)) continue;
                result.Add((lot, ToInt(row["SLCONLAI"])));
            }
            return result;
        }

        public Dictionary<string, int> GetSlConLaiBatch(IEnumerable<string> lots)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (lots == null) return result;

            var lotList = lots.Where(x => !string.IsNullOrWhiteSpace(x))
                               .Select(x => x.Trim())
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .ToList();
            if (lotList.Count == 0) return result;

            var parameters = new List<SqlParameter>();
            var placeholders = new List<string>();
            for (int i = 0; i < lotList.Count; i++)
            {
                string name = "@lot" + i;
                placeholders.Add(name);
                parameters.Add(new SqlParameter(name, lotList[i]));
            }

            string sql = $@"
        SELECT LOT, ISNULL(SLCONLAI, 0) AS SLCONLAI
        FROM STOCKTP
        WHERE LOT IN ({string.Join(", ", placeholders)});";

            DataTable dt = LoadData(sql, parameters.ToArray());
            if (dt == null) return result;

            foreach (DataRow row in dt.Rows)
            {
                if (row["LOT"] == DBNull.Value) continue;
                string lot = row["LOT"].ToString();
                if (string.IsNullOrWhiteSpace(lot)) continue;
                result[lot] = ToInt(row["SLCONLAI"]);
            }
            return result;
        }

        // ============================================================
        // MAPPING / SAFE CONVERSION — giữ nguyên như bạn đã viết
        // ============================================================
        private static StockItem MapStockItem(DataRow row) => new StockItem
        {
            Lot = GetString(row, "LOT"),
            Part = GetString(row, "PART"),
            Name = GetString(row, "NAME"),
            Model = GetString(row, "MODEL"),
            SlNhap = GetNullableInt(row, "SLNHAP"),
            SlConLai = GetNullableInt(row, "SLCONLAI"),
            SlXuat = GetNullableInt(row, "SLXUAT"),
            Satus = GetNullableShort(row, "SATUS"),
            CaSX = GetNullableShort(row, "CASX"),
            NgaySX = GetNullableDateTime(row, "NGAYSX"),
            NgayNhap = GetNullableDateTime(row, "NGAYNHAP")
        };

        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            if (value is int i) return i;
            if (value is decimal dec) return Convert.ToInt32(dec);
            if (value is double dbl) return Convert.ToInt32(dbl);
            if (value is float fl) return Convert.ToInt32(fl);
            if (int.TryParse(value.ToString(), out int result)) return result;
            if (decimal.TryParse(value.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal d))
                return Convert.ToInt32(d);
            return 0;
        }

        private static string GetString(DataRow row, string column) =>
            !row.Table.Columns.Contains(column) || row[column] == DBNull.Value ? null : row[column]?.ToString();

        private static int? GetNullableInt(DataRow row, string column) =>
            !row.Table.Columns.Contains(column) || row[column] == DBNull.Value ? (int?)null : ToInt(row[column]);

        private static short? GetNullableShort(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            try { return Convert.ToInt16(row[column]); } catch { return null; }
        }

        private static DateTime? GetNullableDateTime(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
            if (row[column] is DateTime dt) return dt;
            return DateTime.TryParse(row[column].ToString(), out DateTime result) ? result : (DateTime?)null;
        }
    }
}
