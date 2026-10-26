using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Models
{
    public sealed class InspectionLogEntry
    {
        public string InspectionCode { get; set; }
        public string ItemCode { get; set; }
        public string TemCodeTong { get; set; }
        public string LotNoTong { get; set; }
        public string NSXTong { get; set; }
        public int SoLuongTong { get; set; }
        public string BoxTemCode { get; set; }
        public string BoxLotNo { get; set; }
        public string BoxNSX { get; set; }
        public bool IsMatch { get; set; }
        public string MismatchFields { get; set; }
        public DateTime CheckedAt { get; set; }
        public string FinalResult { get; set; }  // "PASS" | "FAIL"
        public string MaPhieu { get; set; }
    }

}
