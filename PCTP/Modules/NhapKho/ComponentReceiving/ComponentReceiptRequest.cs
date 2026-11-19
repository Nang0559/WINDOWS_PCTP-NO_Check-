using System;

namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public sealed class ComponentReceiptRequest
    {
        public string SiteCode { get; set; }
        public string ReceiptNo { get; set; }
        public string QrPayload { get; set; }
        public decimal Quantity { get; set; }
        public int TargetSlotId { get; set; }
        public string ReceivedBy { get; set; }
        public string IdempotencyKey { get; set; }
    }

    public sealed class ComponentReceiptResult
    {
        public long ComponentReceiptId { get; set; }
        public long ComponentReceiptLineId { get; set; }
        public long InventoryTransactionId { get; set; }
        public long ComponentLotId { get; set; }
        public long MaterialLotId { get; set; }
        public decimal ReceivedQty { get; set; }
    }
}