using System.Collections.Generic;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    public sealed class StockMovementResult
    {
        public bool Success { get; private set; }
        public bool Duplicate { get; private set; }
        public string Message { get; private set; }
        public IList<StockSlotLot> ConsumedLots { get; private set; }

        /// <summary>MES InventoryTransactionId của phát sinh vừa ghi; caller phải lưu vào chứng từ/audit.</summary>
        public long? InventoryTransactionId { get; private set; }

        public StockMovementResult WithTransaction(long inventoryTransactionId)
        {
            InventoryTransactionId = inventoryTransactionId;
            return this;
        }

        private StockMovementResult(bool success, bool duplicate, string message)
        {
            Success = success;
            Duplicate = duplicate;
            Message = message;
            ConsumedLots = new List<StockSlotLot>();
        }

        public static StockMovementResult Ok(string message = null)
        {
            return new StockMovementResult(true, false, message);
        }

        public static StockMovementResult OkWithConsumedLots(
            IList<StockSlotLot> consumedLots,
            string message = null)
        {
            var result = new StockMovementResult(true, false, message);
            if (consumedLots != null)
                result.ConsumedLots = consumedLots;
            return result;
        }

        public static StockMovementResult Fail(string message)
        {
            return new StockMovementResult(false, false, message);
        }

        public static StockMovementResult AlreadyProcessed(string message)
        {
            return new StockMovementResult(false, true, message);
        }
    }
}
