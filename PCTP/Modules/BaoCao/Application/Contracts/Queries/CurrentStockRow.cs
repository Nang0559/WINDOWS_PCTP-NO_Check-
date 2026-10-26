namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public sealed class CurrentStockRow
    {
        public string ItemCode { get; set; }
        public string LotNo { get; set; }
        public string QrCode { get; set; }
        public decimal Quantity { get; set; }
        public string LocationCode { get; set; }
        public string Status { get; set; }
    }
}
