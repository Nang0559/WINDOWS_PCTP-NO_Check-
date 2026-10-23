using System;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// LOT payload used by KhoCore stock mutation ports.
    /// Keeps the central movement service independent from legacy LotInfo.
    /// </summary>
    public sealed class StockSlotLot
    {
        public string LotNo { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? TemCode { get; set; }
        public string? RawQr { get; set; }
        public DateTime? ImportDate { get; set; }

        /// <summary>0 = Active (mặc định). 3 = NG cách ly (không thuộc STOCKTP).</summary>
        public byte PhieuStatus { get; set; }
    }
}
