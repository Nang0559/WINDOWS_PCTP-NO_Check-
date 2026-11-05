using System;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Models;

namespace PCTP.Modules.XuatKho.Repositories
{
    /// <summary>Return internal-location stock to a physical Slot and STOCKTP atomically.</summary>
    public sealed partial class InternalTransferRepository
    {
        public DataTable GetReturnCandidates(string lotFilter)
        {
            const string sql = @"
SELECT t.TransferId, t.TransferNo, t.LocationId, l.LocationName,
       t.ItemCode, t.LotNo, t.Quantity AS TransferQuantity,
       ISNULL(r.ReturnedQuantity, 0) AS ReturnedQuantity,
       t.Quantity - ISNULL(r.ReturnedQuantity, 0) AS RemainingFromTransfer,
       ISNULL(s.Quantity, 0) AS InternalQuantity
FROM PCTP_InternalTransfer t
JOIN PCTP_InternalLocation l ON l.LocationId = t.LocationId
LEFT JOIN
(
    SELECT TransferId, SUM(Quantity) AS ReturnedQuantity
    FROM PCTP_InternalReturn
    GROUP BY TransferId
) r ON r.TransferId = t.TransferId
LEFT JOIN PCTP_InternalStock s
  ON s.LocationId = t.LocationId AND s.ItemCode = t.ItemCode AND s.LotNo = t.LotNo
WHERE t.Quantity > ISNULL(r.ReturnedQuantity, 0)
  AND ISNULL(s.Quantity, 0) > 0
  AND (@LotNo = N'' OR LTRIM(RTRIM(t.LotNo)) = LTRIM(RTRIM(@LotNo)))
ORDER BY l.LocationName, t.LotNo, t.CreatedAt, t.TransferId;";
            return LoadData(sql,
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 200) { Value = (lotFilter ?? string.Empty).Trim() });
        }

        public DataTable GetReturnSlots(string itemCode)
        {
            const string sql = @"
SELECT s.SlotId,
       w.Name + N' / ' + r.RackName + N' / Slot ' + CONVERT(nvarchar(20), s.SlotNumber)
       + N' | Tồn ' + CONVERT(nvarchar(20), ISNULL(s.Quantity, 0))
       + N' / ' + CONVERT(nvarchar(20), ISNULL(s.Capacity, 0)) AS DisplayText,
       ISNULL(s.Quantity, 0) AS Quantity,
       ISNULL(s.Capacity, 0) AS Capacity,
       s.ItemCode
FROM Slot s
JOIN Rack r ON r.RackId = s.RackId
JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
WHERE ISNULL(s.ItemCode, N'') IN (N'', @ItemCode)
  AND (ISNULL(s.Capacity, 0) <= 0 OR ISNULL(s.Quantity, 0) < ISNULL(s.Capacity, 0))
ORDER BY w.Name, r.RackName, s.SlotNumber;";
            return LoadData(sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = (itemCode ?? string.Empty).Trim() });
        }

        public sealed class ReturnResult
        {
            public int ReturnId { get; set; }
            public string ReturnNo { get; set; }
        }

        public ReturnResult ExecuteReturn(
            IUnitOfWork uow,
            IStockMovementService movement,
            IStockHistoryRepository history,
            ISlotService slotService,
            int transferId,
            int targetSlotId,
            int quantity,
            string reason,
            string performedBy,
            Guid requestKey)
        {
            if (uow == null || movement == null || history == null || slotService == null)
                throw new ArgumentNullException("Thiếu dependency thu hồi nội bộ.");
            if (transferId <= 0 || targetSlotId <= 0 || quantity <= 0)
                throw new ArgumentOutOfRangeException("Thông tin thu hồi không hợp lệ.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Lý do thu hồi là bắt buộc.");

            uow.Begin();
            try
            {
                DataTable transfer = LoadData(@"
SELECT TransferId, LocationId, ItemCode, LotNo, Quantity
FROM PCTP_InternalTransfer WITH (UPDLOCK, HOLDLOCK)
WHERE TransferId = @TransferId;",
                    new SqlParameter("@TransferId", SqlDbType.Int) { Value = transferId });
                if (transfer.Rows.Count != 1)
                    throw new InvalidOperationException("Không tìm thấy lần điều chuyển gốc.");

                DataRow t = transfer.Rows[0];
                int locationId = Convert.ToInt32(t["LocationId"]);
                string itemCode = Convert.ToString(t["ItemCode"]);
                string lotNo = Convert.ToString(t["LotNo"]);
                int transferred = Convert.ToInt32(t["Quantity"]);

                object returnedValue = ExecuteScalar(
                    "SELECT ISNULL(SUM(Quantity),0) FROM PCTP_InternalReturn WITH (UPDLOCK, HOLDLOCK) WHERE TransferId=@TransferId",
                    new SqlParameter("@TransferId", SqlDbType.Int) { Value = transferId });
                int returned = Convert.ToInt32(returnedValue);
                if (quantity > transferred - returned)
                    throw new InvalidOperationException("Số lượng thu hồi vượt phần chưa thu hồi của lần điều chuyển gốc.");

                object internalValue = ExecuteScalar(@"
SELECT Quantity FROM PCTP_InternalStock WITH (UPDLOCK, HOLDLOCK)
WHERE LocationId=@LocationId AND ItemCode=@ItemCode AND LotNo=@LotNo;",
                    new SqlParameter("@LocationId", SqlDbType.Int) { Value = locationId },
                    new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = itemCode },
                    new SqlParameter("@LotNo", SqlDbType.NVarChar, 200) { Value = lotNo });
                int internalAvailable = internalValue == null || internalValue == DBNull.Value ? 0 : Convert.ToInt32(internalValue);
                if (internalAvailable < quantity)
                    throw new InvalidOperationException("Số dư tại vị trí nội bộ không đủ hoặc đã được thu hồi ở giao dịch khác.");

                slotService.LockSlotForUpdate(targetSlotId);
                DataTable slot = LoadData(@"
SELECT ISNULL(Quantity,0) AS Quantity, ISNULL(Capacity,0) AS Capacity, ISNULL(ItemCode,N'') AS ItemCode
FROM Slot WITH (UPDLOCK, HOLDLOCK) WHERE SlotId=@SlotId;",
                    new SqlParameter("@SlotId", SqlDbType.Int) { Value = targetSlotId });
                if (slot.Rows.Count != 1)
                    throw new InvalidOperationException("Không tìm thấy Slot nhận hàng.");
                int slotQty = Convert.ToInt32(slot.Rows[0]["Quantity"]);
                int capacity = Convert.ToInt32(slot.Rows[0]["Capacity"]);
                string slotItem = Convert.ToString(slot.Rows[0]["ItemCode"]);
                if (!string.IsNullOrWhiteSpace(slotItem) &&
                    !string.Equals(slotItem.Trim(), itemCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Slot nhận đang chứa mã hàng khác.");
                if (capacity > 0 && slotQty + quantity > capacity)
                    throw new InvalidOperationException("Slot nhận không đủ sức chứa cho số lượng thu hồi.");

                int returnId = CreateReturn(requestKey, transferId, targetSlotId, quantity, reason, performedBy);
                int changed = ExecuteNonQuery(@"
UPDATE PCTP_InternalStock
SET Quantity = Quantity - @Quantity, UpdatedAt = GETDATE()
WHERE LocationId=@LocationId AND ItemCode=@ItemCode AND LotNo=@LotNo AND Quantity >= @Quantity;",
                    new SqlParameter("@Quantity", SqlDbType.Int) { Value = quantity },
                    new SqlParameter("@LocationId", SqlDbType.Int) { Value = locationId },
                    new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = itemCode },
                    new SqlParameter("@LotNo", SqlDbType.NVarChar, 200) { Value = lotNo });
                if (changed != 1)
                    throw new InvalidOperationException("Không thể trừ tồn nội bộ; dữ liệu đã thay đổi.");

                var movementResult = movement.Receive(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Receive,
                    TargetSlotId = targetSlotId,
                    LotNo = lotNo,
                    ItemCode = itemCode,
                    Quantity = quantity,
                    ReferenceType = "INTERNAL_RETURN",
                    ReferenceId = returnId.ToString(),
                    PerformedBy = performedBy,
                    OccurredAt = DateTime.Now,
                    Reason = "THU_HOI_NOI_BO"
                });
                if (movementResult == null || !movementResult.Success)
                    throw new InvalidOperationException(movementResult == null ? "Không nhận được kết quả nhập kho." : movementResult.Message);

                // StockMovementService inserts SlotLot; synchronize the Slot header in this same transaction.
                var slotRepository = new SlotRepository((PhieuSqlExecutor)null, uow);
                slotRepository.RefreshSlotHeader(targetSlotId);

                history.SaveHistory(
                    "RETURN_INT",
                    itemCode,
                    new LotInfo
                    {
                        ItemCode = itemCode,
                        LotNo = lotNo,
                        Quantity = quantity,
                        TemCode = null,
                        RawQr = null,
                        QRInfo = new QRCodeInfo { MaPhieu = "IR-" + returnId }
                    },
                    null,
                    targetSlotId,
                    performedBy);

                uow.Commit();
                return new ReturnResult { ReturnId = returnId, ReturnNo = "IR-" + returnId.ToString("D10") };
            }
            catch
            {
                try { uow.Rollback(); } catch { }
                throw;
            }
        }

        private int CreateReturn(Guid requestKey, int transferId, int targetSlotId, int quantity, string reason, string performedBy)
        {
            object id = ExecuteScalar(@"
INSERT INTO PCTP_InternalReturn
    (RequestKey, TransferId, TargetSlotId, Quantity, Reason, PerformedBy, CreatedAt)
OUTPUT INSERTED.ReturnId
VALUES
    (@RequestKey, @TransferId, @TargetSlotId, @Quantity, @Reason, @PerformedBy, GETDATE());",
                new SqlParameter("@RequestKey", SqlDbType.UniqueIdentifier) { Value = requestKey },
                new SqlParameter("@TransferId", SqlDbType.Int) { Value = transferId },
                new SqlParameter("@TargetSlotId", SqlDbType.Int) { Value = targetSlotId },
                new SqlParameter("@Quantity", SqlDbType.Int) { Value = quantity },
                new SqlParameter("@Reason", SqlDbType.NVarChar, 500) { Value = reason.Trim() },
                new SqlParameter("@PerformedBy", SqlDbType.NVarChar, 100) { Value = (object)performedBy ?? Environment.UserName });
            int returnId = Convert.ToInt32(id);
            ExecuteNonQuery("UPDATE PCTP_InternalReturn SET ReturnNo=@No WHERE ReturnId=@Id;",
                new SqlParameter("@No", SqlDbType.NVarChar, 40) { Value = "IR-" + returnId.ToString("D10") },
                new SqlParameter("@Id", SqlDbType.Int) { Value = returnId });
            return returnId;
        }
    }
}
