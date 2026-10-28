using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Single-writer boundary for customer delivery stock mutations.
    /// Implementations must execute all PART/LOT allocations, STOCKTP, SlotLot,
    /// delivery ledger, history and pending-delivery updates in one UoW.
    /// Replays of the same BusinessKey must never mutate stock twice.
    /// </summary>
    public interface IDeliveryStockCommandHandler
    {
        DeliveryStockCommandResult Execute(DeliveryStockCommand command);
    }

    public sealed class DeliveryStockCommandResult
    {
        public bool Success { get; private set; }
        public bool Duplicate { get; private set; }
        public string Message { get; private set; }
        public int CommittedQuantity { get; private set; }

        private DeliveryStockCommandResult(bool success, bool duplicate, string message, int committedQuantity)
        {
            Success = success;
            Duplicate = duplicate;
            Message = message;
            CommittedQuantity = committedQuantity;
        }

        public static DeliveryStockCommandResult Ok(int quantity)
        {
            return new DeliveryStockCommandResult(true, false, null, quantity);
        }

        public static DeliveryStockCommandResult AlreadyProcessed(string message, int committedQuantity)
        {
            return new DeliveryStockCommandResult(true, true, message, committedQuantity);
        }

        public static DeliveryStockCommandResult Fail(string message)
        {
            return new DeliveryStockCommandResult(false, false, message, 0);
        }
    }
}
