using System.Collections.Generic;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Result of removing a quantity from a physical LOT.
    /// Keeps exported LOT metadata available to workflow modules without exposing
    /// legacy Slot/SlotLot persistence types from KhoCore.
    /// </summary>
    public sealed class StockSlotTakeResult
    {
        public int Quantity { get; set; }
        public IList<StockSlotLot> ExportLots { get; set; }

        public StockSlotTakeResult()
        {
            ExportLots = new List<StockSlotLot>();
        }
    }
}
