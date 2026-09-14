using System;

namespace PCTP.Modules.BaoCao.Application.Contracts
{
    /// <summary>
    /// Read model only. It deliberately contains no domain entity references.
    /// </summary>
    public sealed class ItemHistoryRow
    {
        public string QrCode { get; set; }
        public string PartNo { get; set; }
        public string LotNo { get; set; }
        public DateTime? EventTime { get; set; }
        public string EventType { get; set; }
        public string DocumentNo { get; set; }
        public string SourceModule { get; set; }
        public decimal Quantity { get; set; }
        public string UserName { get; set; }
        public string LocationCode { get; set; }
        public string CustomerNo { get; set; }
        public string Status { get; set; }
    }
}
