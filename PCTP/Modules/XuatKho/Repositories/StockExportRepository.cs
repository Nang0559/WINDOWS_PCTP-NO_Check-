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
            object kq = ExecuteScalar(
                "SELECT ISNULL(SLCONLAI,0) FROM STOCKTP WHERE LOT = @lot",
                new SqlParameter("@lot", lotNo));
            return kq == null || kq == DBNull.Value ? 0 : Convert.ToInt32(kq);
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

            if (rows == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy LOT [{lotNo}] trong STOCKTP để trừ xuất.");
        }
        public void AdjustSlConLai(string lotNo, int delta)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("AdjustSlConLai phải chạy trong transaction.");

            int rows = ExecuteNonQuery(
                "UPDATE STOCKTP SET SLCONLAI = ISNULL(SLCONLAI,0) + @delta WHERE LOT = @lot",
                new SqlParameter("@delta", delta),
                new SqlParameter("@lot", lotNo));

            if (rows == 0)
                throw new InvalidOperationException($"Không tìm thấy LOT [{lotNo}] trong STOCKTP.");
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
            return rows > 0;
        }
        // Thêm vào IStockExportRepository / StockExportRepository
        public List<StockTpLotInfo> FindLotsWithStock(string maHang, string lotNo)
        {
            string sql = string.IsNullOrWhiteSpace(lotNo)
                ? "SELECT LOT, PART, SLCONLAI FROM STOCKTP WHERE PART = @ma AND SLCONLAI > 0 ORDER BY LOT"
                : "SELECT LOT, PART, SLCONLAI FROM STOCKTP WHERE PART = @ma AND LOT = @lot AND SLCONLAI > 0";

            var pars = string.IsNullOrWhiteSpace(lotNo)
                ? new[] { new SqlParameter("@ma", maHang) }
                : new[] { new SqlParameter("@ma", maHang), new SqlParameter("@lot", lotNo) };

            DataTable dt = LoadData(sql, pars);
            return dt.Rows.Cast<DataRow>().Select(r => new StockTpLotInfo
            {
                LotNo = r["LOT"].ToString().Trim(),
                ItemCode = r["PART"] as string,
                SlConLai = Convert.ToInt32(r["SLCONLAI"])
            }).ToList();
        }
    }
}
