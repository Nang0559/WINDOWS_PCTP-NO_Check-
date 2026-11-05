using System;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Shared.Common;

namespace PCTP.Modules.XuatKho.Repositories
{
    /// <summary>
    /// Persists non-Slot internal locations and their independent LOT balance.
    /// All writes join the caller's UnitOfWork; no customer/delivery document is created.
    /// </summary>
    public sealed class InternalTransferRepository : SqlRepositoryBase
    {
        public sealed class TransferResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        public InternalTransferRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public TransferResult ExecuteTransfer(
            IUnitOfWork uow,
            IStockMovementService movement,
            IStockHistoryRepository history,
            ISlotService slotService,
            SlotLotViewInfo source,
            string destinationName,
            string reason,
            int quantity,
            string performedBy)
        {
            if (uow == null || movement == null || history == null || slotService == null)
                throw new ArgumentNullException("Thiếu dependency điều chuyển kho.");
            if (source == null || source.SlotLotId <= 0 || source.SlotId <= 0)
                throw new ArgumentException("Dòng SlotLot nguồn không hợp lệ.");
            if (quantity <= 0 || quantity > source.Quantity)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (string.IsNullOrWhiteSpace(destinationName) || string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Vị trí đích và lý do là bắt buộc.");

            uow.Begin();
            try
            {
                slotService.LockSlotForUpdate(source.SlotId);
                int transferId = CreateTransfer(source, destinationName.Trim(), reason.Trim(), quantity, performedBy);
                var result = movement.Export(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Export,
                    SlotLotId = source.SlotLotId,
                    LotNo = source.LotNo,
                    ItemCode = source.ItemCode,
                    Quantity = quantity,
                    ReferenceType = "INTERNAL_TRANSFER",
                    ReferenceId = transferId.ToString(),
                    PerformedBy = performedBy,
                    OccurredAt = DateTime.Now,
                    Reason = "CHUYEN_NOI_BO"
                });
                if (result == null || !result.Success)
                    throw new InvalidOperationException(result == null ? "Không nhận được kết quả trừ tồn." : result.Message);

                IncreaseDestinationStock(transferId, source.ItemCode, source.LotNo, quantity);
                history.SaveHistory(
                    "MOVE_INT",
                    source.ItemCode,
                    new LotInfo
                    {
                        ItemCode = source.ItemCode,
                        LotNo = source.LotNo,
                        Quantity = quantity,
                        TemCode = null,
                        RawQr = null,
                        QRInfo = new QRCodeInfo { MaPhieu = "INT-" + transferId }
                    },
                    source.SlotId,
                    null,
                    performedBy);

                uow.Commit();
                return new TransferResult { Success = true, Message = "Chuyển hàng nội bộ thành công." };
            }
            catch
            {
                try { uow.Rollback(); } catch { }
                throw;
            }
        }

        private int CreateTransfer(SlotLotViewInfo source, string destination, string reason, int quantity, string performedBy)
        {
            object id = ExecuteScalar(@"
DECLARE @LocationId int;
SELECT @LocationId = LocationId
FROM PCTP_InternalLocation WITH (UPDLOCK, HOLDLOCK)
WHERE LocationName = @LocationName AND IsActive = 1;
IF @LocationId IS NULL
BEGIN
    INSERT INTO PCTP_InternalLocation(LocationName, IsActive, CreatedAt)
    VALUES(@LocationName, 1, GETDATE());
    SET @LocationId = CONVERT(int, SCOPE_IDENTITY());
END;
INSERT INTO PCTP_InternalTransfer
    (LocationId, LotNo, ItemCode, Quantity, SourceSlotId, Reason, PerformedBy, CreatedAt)
OUTPUT INSERTED.TransferId
VALUES
    (@LocationId, @LotNo, @ItemCode, @Quantity, @SourceSlotId, @Reason, @PerformedBy, GETDATE());",
                new SqlParameter("@LocationName", SqlDbType.NVarChar, 150) { Value = destination },
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 200) { Value = source.LotNo },
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = source.ItemCode },
                new SqlParameter("@Quantity", SqlDbType.Int) { Value = quantity },
                new SqlParameter("@SourceSlotId", SqlDbType.Int) { Value = source.SlotId },
                new SqlParameter("@Reason", SqlDbType.NVarChar, 500) { Value = reason },
                new SqlParameter("@PerformedBy", SqlDbType.NVarChar, 100) { Value = (object)performedBy ?? Environment.UserName });
            int transferId = Convert.ToInt32(id);
            ExecuteNonQuery("UPDATE PCTP_InternalTransfer SET TransferNo = @No WHERE TransferId = @Id;",
                new SqlParameter("@No", SqlDbType.NVarChar, 40) { Value = "INT-" + transferId.ToString("D10") },
                new SqlParameter("@Id", SqlDbType.Int) { Value = transferId });
            return transferId;
        }

        private void IncreaseDestinationStock(int transferId, string itemCode, string lotNo, int quantity)
        {
            // HOLDLOCK + unique key serialize the update/insert race for a destination + item + LOT.
            int changed = ExecuteNonQuery(@"
UPDATE PCTP_InternalStock WITH (UPDLOCK, HOLDLOCK)
SET Quantity = Quantity + @Quantity, UpdatedAt = GETDATE()
WHERE LocationId = (SELECT LocationId FROM PCTP_InternalTransfer WHERE TransferId = @TransferId)
  AND ItemCode = @ItemCode AND LotNo = @LotNo;",
                new SqlParameter("@Quantity", SqlDbType.Int) { Value = quantity },
                new SqlParameter("@TransferId", SqlDbType.Int) { Value = transferId },
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = itemCode },
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 200) { Value = lotNo });
            if (changed == 0)
                ExecuteNonQuery(@"
INSERT INTO PCTP_InternalStock(LocationId, ItemCode, LotNo, Quantity, UpdatedAt)
SELECT LocationId, @ItemCode, @LotNo, @Quantity, GETDATE()
FROM PCTP_InternalTransfer WHERE TransferId = @TransferId;",
                    new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = itemCode },
                    new SqlParameter("@LotNo", SqlDbType.NVarChar, 200) { Value = lotNo },
                    new SqlParameter("@Quantity", SqlDbType.Int) { Value = quantity },
                    new SqlParameter("@TransferId", SqlDbType.Int) { Value = transferId });
        }
    }
}
