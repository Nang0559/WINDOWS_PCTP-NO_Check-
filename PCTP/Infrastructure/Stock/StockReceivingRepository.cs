using System;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;

namespace PCTP.Infrastructure.Stock
{
    /// <summary>
    /// KhoCore receiving persistence for dbo.STOCKTP. All operations participate
    /// in the workflow UnitOfWork so STOCKTP and Slot/SlotLot/history are atomic.
    /// </summary>
    public sealed class StockReceivingRepository : SqlRepositoryBase, IStockReceivingRepository
    {
        public StockReceivingRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public bool Exists(string lotNo)
        {
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(lotNo)) return false;
            object value = ExecuteScalar(
                "SELECT COUNT(1) FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE LOT=@lot",
                new SqlParameter("@lot", lotNo.Trim()));
            int count = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            if (count > 1) throw new InvalidOperationException(
                string.Format("STOCKTP vi phạm khóa LOT duy nhất: LOT [{0}] có {1} dòng.", lotNo, count));
            return count == 1;
        }

        public int GetReceivedQuantity(string lotNo)
        {
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(lotNo)) return 0;
            DataTable rows = LoadData(
                "SELECT ISNULL(SLNHAP,0) AS SLNHAP FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE LOT=@lot",
                new SqlParameter("@lot", lotNo.Trim()));
            if (rows == null || rows.Rows.Count == 0) return 0;
            if (rows.Rows.Count != 1) throw new InvalidOperationException(
                string.Format("STOCKTP vi phạm khóa LOT duy nhất: LOT [{0}] có {1} dòng.", lotNo, rows.Rows.Count));
            return Convert.ToInt32(rows.Rows[0]["SLNHAP"]);
        }

        public void Insert(StockReceivingRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(record.LotNo)) throw new ArgumentException("LOT không được để trống.", nameof(record));
            if (record.ReceivedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(record.ReceivedQuantity));
            if (Exists(record.LotNo)) throw new InvalidOperationException(
                string.Format("LOT [{0}] đã tồn tại trong STOCKTP; không được tạo dòng thứ hai.", record.LotNo));

            int caSx;
            if (!int.TryParse(record.ProductionCase, out caSx)) caSx = 0;
            int affected = ExecuteNonQuery(@"
INSERT INTO STOCKTP
    (LOT, MODEL, PART, NAME, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, SATUS)
VALUES
    (@lot, @model, @part, @name, @casx, @ngaysx, @slsx, GETDATE(), @slnhap, GETDATE(), 0, @slconlai, @status);",
                new SqlParameter("@lot", record.LotNo.Trim()),
                new SqlParameter("@model", (object)record.Model ?? DBNull.Value),
                new SqlParameter("@part", (object)record.ItemCode ?? DBNull.Value),
                new SqlParameter("@name", (object)record.ItemName ?? DBNull.Value),
                new SqlParameter("@casx", caSx),
                new SqlParameter("@ngaysx", (object)record.ProductionDate ?? DBNull.Value),
                new SqlParameter("@slsx", record.ProductionQuantity),
                new SqlParameter("@slnhap", record.ReceivedQuantity),
                new SqlParameter("@slconlai", record.ReceivedQuantity),
                new SqlParameter("@status", record.Status));
            if (affected != 1) throw new InvalidOperationException(
                string.Format("STOCKTP INSERT LOT [{0}] phải tạo đúng một dòng, thực tế {1}.", record.LotNo, affected));
        }

        public void Update(string lotNo, int receivedQuantityDelta, int status)
        {
            EnsureTransaction();
            if (string.IsNullOrWhiteSpace(lotNo)) throw new ArgumentException("LOT không được để trống.", nameof(lotNo));
            if (receivedQuantityDelta <= 0) throw new ArgumentOutOfRangeException(nameof(receivedQuantityDelta));
            int affected = ExecuteNonQuery(@"
UPDATE STOCKTP
SET SLNHAP = ISNULL(SLNHAP, 0) + @sl,
    SLCONLAI = ISNULL(SLCONLAI, 0) + @sl,
    NGAYNHAP = GETDATE(),
    SATUS = @status
WHERE LOT = @lot;",
                new SqlParameter("@sl", receivedQuantityDelta),
                new SqlParameter("@status", status),
                new SqlParameter("@lot", lotNo.Trim()));
            if (affected != 1) throw new InvalidOperationException(
                string.Format("STOCKTP yêu cầu đúng một dòng LOT [{0}], nhưng UPDATE tác động {1} dòng. Transaction phải rollback.", lotNo, affected));
        }

        private void EnsureTransaction()
        {
            if (!HasTransaction) throw new InvalidOperationException(
                "StockReceivingRepository chỉ được gọi trong UnitOfWork transaction của workflow nhập kho.");
        }
    }
}