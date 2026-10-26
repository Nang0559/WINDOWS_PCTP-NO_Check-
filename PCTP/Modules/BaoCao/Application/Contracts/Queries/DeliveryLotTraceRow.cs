namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    /// <summary>
    /// One LOT allocation inside one delivered carton.
    /// Never treat a composite LOT string as one LOT.
    /// </summary>
    public sealed class DeliveryLotTraceRow
    {
        public string DeliveryKey { get; set; }
        public string QRCode { get; set; }
        public string LotNo { get; set; }
        public decimal Quantity { get; set; }
    }
}
