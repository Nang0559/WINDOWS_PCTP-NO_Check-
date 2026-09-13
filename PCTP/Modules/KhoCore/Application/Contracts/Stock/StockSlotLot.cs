using System;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// LOT payload used by KhoCore stock mutation ports.
    /// Keeps the central movement service independent from legacy LotInfo.
    /// </summary>
    public sealed class StockSlotLot
    {
        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
        public string TemCode { get; set; }
        public string RawQr { get; set; }
        public DateTime? ImportDate { get; set; }
    }
}
