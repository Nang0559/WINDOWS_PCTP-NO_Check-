using System;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    /// <summary>
    /// Master record for one delivered carton/QRCode.
    /// BaoCao is read-only; this model is a projection of delivery evidence.
    /// </summary>
    public sealed class DeliveryTraceRow
    {
        public string DeliveryKey { get; set; }
        public string DocumentNo { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string CustomerCode { get; set; }
        public string CustomerName { get; set; }
        public string PartNo { get; set; }
        public string PartName { get; set; }
        public string QRCode { get; set; }
        public string CustomerLabelData { get; set; }
        public string LotNoRaw { get; set; }
        public decimal? Quantity { get; set; }
        public string Unit { get; set; }
        public string Factory { get; set; }
        public string Status { get; set; }
    }
}
