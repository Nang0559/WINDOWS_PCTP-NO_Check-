using System;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public sealed class QualityHistoryRow
    {
        public string InspectionCode { get; set; }
        public string ItemCode { get; set; }
        public string LotNo { get; set; }
        public string ProductionDate { get; set; }
        public int TotalQuantity { get; set; }
        public string DocumentNo { get; set; }
        public int TotalBox { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public string FinalResult { get; set; }
        public DateTime? CheckedAt { get; set; }
    }
}
