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

        /// <summary>
        /// Adds quantity to the exact LOT in a physical slot, creating the LOT when absent.
        /// This is the canonical LOT-aware receiving/move operation.
        /// </summary>
        void AddLot(int slotId, StockSlotLot lot);
    }
}
