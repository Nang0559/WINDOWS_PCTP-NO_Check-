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
            var rows = LoadData(
                "SELECT SLCONLAI FROM STOCKTP WHERE LOT=@lot",
                new SqlParameter("@lot", lotNo ?? ""));
            if (rows == null || rows.Rows.Count == 0) return 0;
            if (rows.Rows.Count != 1)
                throw new InvalidOperationException(
                    string.Format("STOCKTP vi phạm khóa LOT duy nhất: LOT [{0}] có {1} dòng.", lotNo, rows.Rows.Count));
            return rows.Rows[0]["SLCONLAI"] == System.DBNull.Value
                ? 0
                : Convert.ToInt32(rows.Rows[0]["SLCONLAI"]);
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

            if (affected > 1)
                throw new InvalidOperationException(
                    string.Format("STOCKTP vi phạm khóa LOT duy nhất: UPDATE tác động {0} dòng cho LOT [{1}].", affected, lotNo));
            return affected == 1;
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

            if (affected != 1)
                throw new System.InvalidOperationException(
                    string.Format("STOCKTP yêu cầu đúng một dòng LOT [{0}] khi điều chỉnh tăng, nhưng UPDATE tác động {1} dòng.", lotNo, affected));
        }
    }
}
