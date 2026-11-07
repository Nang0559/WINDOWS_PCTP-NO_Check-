using System;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    /// <summary>
    /// Delivery order row with QR scan evidence projected as child rows in the UI.
    /// BaoCao is read-only; this model is a projection of delivery evidence.
    /// </summary>
    public sealed class DeliveryTraceRow
    {
        public string DeliveryKey { get; set; }
        public int DeliveryStt { get; set; }
        public string Door { get; set; }
        public string Conveyor { get; set; }
        public string DocumentNo { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string DeliveryTime { get; set; }
        public string CustomerCode { get; set; }
        public string CustomerName { get; set; }
        public string PartNo { get; set; }
        public string PartName { get; set; }
        public string QRCode { get; set; }
        public int? QrScanOrder { get; set; }
        public DateTime? QrScanDate { get; set; }
        public string QrScanTime { get; set; }
        public string QrLotNo { get; set; }
        public string QrStatus { get; set; }
        public string CustomerLabelData { get; set; }
        public string LotNoRaw { get; set; }
        public decimal? Quantity { get; set; }
        public string Unit { get; set; }
        public string Factory { get; set; }
        public string Status { get; set; }
    }
}
