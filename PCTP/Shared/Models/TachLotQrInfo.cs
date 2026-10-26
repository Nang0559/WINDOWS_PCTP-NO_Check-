using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Models
{
    public class TachLotQrInfo
    {
        public string LotNo { get; set; } = "";
        public string ItemCode { get; set; } = "";
        public string NgaySX { get; set; } = "";
        public string SlTemFccRaw { get; set; } = ""; // giữ string — Dien0tolot() cần ghép nguyên văn
    }
}
