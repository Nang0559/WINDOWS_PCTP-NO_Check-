namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
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
        /// Atomically consumes quantity from LOT-equivalent records in a Slot.
        /// ItemCode is part of the selection key so the same LOT key cannot
        /// accidentally consume another item's stock.
        /// </summary>
        StockSlotTakeResult TakeLot(int slotId, string lotNo, string itemCode, int quantity);
    }
}
