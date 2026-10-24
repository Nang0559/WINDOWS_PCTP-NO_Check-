using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Repositories
{
    public sealed class StockExportRepository : SqlRepositoryBase, IStockExportRepository
    {
        public StockExportRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public int GetSlConLai(string lotNo)
        {
            DataTable rows = LoadData(
                "SELECT ISNULL(SLCONLAI,0) AS SLCONLAI FROM STOCKTP WHERE LOT = @lot",
                new SqlParameter("@lot", lotNo));
            if (rows == null || rows.Rows.Count == 0) return 0;
            if (rows.Rows.Count != 1)
                throw new InvalidOperationException(
                    $"STOCKTP vi phạm khóa LOT duy nhất: LOT [{lotNo}] có {rows.Rows.Count} dòng.");
            return Convert.ToInt32(rows.Rows[0]["SLCONLAI"]);
        }

        public void DecreaseStockTp(string lotNo, int soLuong)
        {
            if (!HasTransaction)
                throw new InvalidOperationException(
                    "DecreaseStockTp phải chạy trong transaction (Uow.Begin() trước).");

            int rows = ExecuteNonQuery(
                @"UPDATE STOCKTP SET
                      SLXUAT   = ISNULL(SLXUAT,0)   + @sl,
                      SLCONLAI = ISNULL(SLCONLAI,0) - @sl,
                      NGAYXUAT = CAST(GETDATE() AS smalldatetime)
                  WHERE LOT = @lot",
                new SqlParameter("@sl", soLuong),
                new SqlParameter("@lot", lotNo));

            if (rows != 1)
                throw new InvalidOperationException(
                    $"STOCKTP yêu cầu đúng một dòng LOT [{lotNo}] khi trừ xuất, nhưng UPDATE tác động {rows} dòng. Transaction phải rollback.");
        }
        public void AdjustSlConLai(string lotNo, int delta)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("AdjustSlConLai phải chạy trong transaction.");

            int rows = ExecuteNonQuery(
                "UPDATE STOCKTP SET SLCONLAI = ISNULL(SLCONLAI,0) + @delta WHERE LOT = @lot",
                new SqlParameter("@delta", delta),
                new SqlParameter("@lot", lotNo));

            if (rows != 1)
                throw new InvalidOperationException($"STOCKTP yêu cầu đúng một dòng LOT [{lotNo}], nhưng UPDATE tác động {rows} dòng. Transaction phải rollback.");
        }
        // Thêm vào IStockExportRepository / StockExportRepository
        /// <summary>
        /// Trừ SLCONLAI có điều kiện — atomic, không cần SELECT...WITH(UPDLOCK) riêng.
        /// Trả về false nếu không đủ tồn (không trừ gì cả) — an toàn tuyệt đối với
        /// concurrent access vì SQL Server tự khoá dòng trong lúc UPDATE.
        /// </summary>
        public bool TryDecreaseSlConLai(string lotNo, int soLuong)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("TryDecreaseSlConLai phải chạy trong transaction.");

            int rows = ExecuteNonQuery(
                "UPDATE STOCKTP SET SLCONLAI = SLCONLAI - @sl " +
                "WHERE LOT = @lot AND SLCONLAI >= @sl",
                new SqlParameter("@sl", soLuong),
                new SqlParameter("@lot", lotNo));
            if (rows > 1)
                throw new InvalidOperationException($"STOCKTP vi phạm khóa LOT duy nhất: UPDATE tác động {rows} dòng cho LOT [{lotNo}].");
            return rows == 1;
        }
        // Thêm vào IStockExportRepository / StockExportRepository
        public List<StockTpLotInfo> FindLotsWithStock(string maHang, string lotNo)
        {
            // STOCKTP có dữ liệu cũ chứa khoảng trắng ở đầu/cuối PART/LOT.
            // So sánh sau trim để truy vấn không bỏ sót tồn hợp lệ; vẫn yêu cầu
            // đúng mã hàng và đúng LOT, không nới điều kiện sang mặt hàng khác.
            string sql = string.IsNullOrWhiteSpace(lotNo)
                ? @"SELECT LOT, PART, SLCONLAI
                    FROM STOCKTP
                    WHERE UPPER(LTRIM(RTRIM(PART))) = UPPER(LTRIM(RTRIM(@ma)))
                      AND ISNULL(SLCONLAI, 0) > 0
                    ORDER BY LOT"
                : @"SELECT LOT, PART, SLCONLAI
                    FROM STOCKTP
                    WHERE UPPER(LTRIM(RTRIM(PART))) = UPPER(LTRIM(RTRIM(@ma)))
                      AND LTRIM(RTRIM(LOT)) = LTRIM(RTRIM(@lot))
                      AND ISNULL(SLCONLAI, 0) > 0";

            var pars = string.IsNullOrWhiteSpace(lotNo)
                ? new[] { new SqlParameter("@ma", (maHang ?? string.Empty).Trim()) }
                : new[] {
                    new SqlParameter("@ma", (maHang ?? string.Empty).Trim()),
                    new SqlParameter("@lot", (lotNo ?? string.Empty).Trim())
                };

            DataTable dt = LoadData(sql, pars);
            return dt.Rows.Cast<DataRow>().Select(r => new StockTpLotInfo
            {
                LotNo = Convert.ToString(r["LOT"]).Trim(),
                ItemCode = Convert.ToString(r["PART"]).Trim(),
                SlConLai = r["SLCONLAI"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLCONLAI"])
            }).ToList();
        }
    }
}
