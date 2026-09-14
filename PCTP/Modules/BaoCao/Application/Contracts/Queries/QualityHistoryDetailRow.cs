using System;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public sealed class QualityHistoryDetailRow
    {
        public string BoxLotNo { get; set; }
        public string BoxProductionDate { get; set; }
        public bool IsMatch { get; set; }
        public string MismatchFields { get; set; }
        public DateTime? CheckedAt { get; set; }
    }
}
