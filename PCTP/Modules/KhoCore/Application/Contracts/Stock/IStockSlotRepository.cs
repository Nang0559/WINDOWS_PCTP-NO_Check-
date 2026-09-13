namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Minimal physical-slot mutation port used by the central stock movement service.
    /// Implementations may temporarily delegate to legacy SlotService during migration.
    /// </summary>
    public interface IStockSlotRepository
    {
        int GetLotQuantity(int slotLotId);
        string GetLotNo(int slotLotId);
        string GetItemCode(int slotLotId);
        int? GetSlotId(int slotLotId);
        void DecreaseLotQuantity(int slotLotId, int quantity);
        void AddQuantity(int slotId, int quantity, string itemCode);
        void AddLot(int slotId, StockSlotLot lot);

        /// <summary>
        /// Removes quantity from the requested LOT in a physical slot.
        /// The adapter owns the legacy Slot/SlotLot split semantics (FIFO by ImportDate).
        /// </summary>
        void TakeLot(int slotId, string lotNo, int quantity);
    }
}
