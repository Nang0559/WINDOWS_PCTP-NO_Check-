using System;

namespace PCTP.Modules.BaoCao.Application.Contracts
{
    public sealed class HistorySearchCriteria
    {
        public string QrCode { get; set; }
        public string LotNo { get; set; }
        public string PartNo { get; set; }
        public string DocumentNo { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string SourceModule { get; set; }
    }
}
