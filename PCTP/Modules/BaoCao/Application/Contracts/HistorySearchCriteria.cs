using System;
using System.Collections.Generic;

namespace PCTP.Modules.BaoCao.Application.Contracts
{
    public sealed class HistorySearchCriteria
    {
        public string QrCode { get; set; }
        public string LotNo { get; set; }
        /// <summary>Nhiều LOT cùng lúc (các LOT của một LOT ghép). Khớp nếu LOT bất kỳ tương đương.</summary>
        public IList<string> LotNos { get; set; }
        public string PartNo { get; set; }
        public string DocumentNo { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string SourceModule { get; set; }
    }
}
