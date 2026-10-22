using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Shared.Common;
using System;
using System.Data.SqlClient;

namespace PCTP.Infrastructure.Stock
{
    /// <summary>
    /// Transitional adapter for STOCKTP. KhoCore only sees the balance port;
    /// SQL/storage details remain in Infrastructure.
    /// </summary>
    public sealed class LegacyStockBalanceRepositoryAdapter : SqlRepositoryBase, IStockBalanceRepository
    {
        public LegacyStockBalanceRepositoryAdapter(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public int GetAvailableQuantity(string lotNo)
        {
            object value = ExecuteScalar(
                "SELECT ISNULL(SUM(SLCONLAI),0) FROM STOCKTP WHERE LOT=@lot",
                new SqlParameter("@lot", lotNo ?? ""));
            return value == null || value == System.DBNull.Value ? 0 : System.Convert.ToInt32(value);
        }

        public void DecreaseAvailableQuantity(string lotNo, int quantity)
        {
            if (!TryDecreaseAvailableQuantity(lotNo, quantity))
                throw new System.InvalidOperationException(
                    string.Format("STOCKTP LOT [{0}] không đủ hoặc đã thay đổi.", lotNo));
        }

        public bool TryDecreaseAvailableQuantity(string lotNo, int quantity)
        {
            if (string.IsNullOrWhiteSpace(lotNo) || quantity <= 0)
                return false;

            int affected = ExecuteNonQuery(
                @"UPDATE STOCKTP
                  SET SLCONLAI = SLCONLAI - @q,
                      SLXUAT = ISNULL(SLXUAT,0) + @q,
                      NGAYXUAT = GETDATE()
                  WHERE LOT=@lot AND SLCONLAI >= @q",
                new SqlParameter("@lot", lotNo),
                new SqlParameter("@q", quantity));

            return affected > 0;
        }

        public void AdjustAvailableQuantity(string lotNo, int delta)
        {
            if (string.IsNullOrWhiteSpace(lotNo) || delta == 0)
                return;

            if (delta < 0)
            {
                DecreaseAvailableQuantity(lotNo, -delta);
                return;
            }

            int affected = ExecuteNonQuery(
                "UPDATE STOCKTP SET SLCONLAI = ISNULL(SLCONLAI,0) + @q WHERE LOT=@lot",
                new SqlParameter("@lot", lotNo),
                new SqlParameter("@q", delta));

            if (affected == 0)
                throw new System.InvalidOperationException(
                    string.Format("Không tìm thấy STOCKTP LOT [{0}] để điều chỉnh tăng tồn.", lotNo));
        }
    }
}
