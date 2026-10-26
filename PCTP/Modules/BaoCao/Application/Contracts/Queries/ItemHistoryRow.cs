using System;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public sealed class ItemHistoryRow
    {
        public string ItemCode { get; set; }
        public string QrCode { get; set; }
        public string LotNo { get; set; }
        public string ActionType { get; set; }
        public decimal Quantity { get; set; }
        public DateTime? EventTime { get; set; }
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public string DocumentNo { get; set; }
        public string UserName { get; set; }
    }
}
