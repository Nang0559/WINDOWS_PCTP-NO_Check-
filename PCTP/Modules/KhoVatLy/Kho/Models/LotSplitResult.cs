using PCTP.Modules.KhoVatLy.Kho.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class LotSplitResult
    {
        public List<LotInfo> RemainingLots { get; set; }
            = new List<LotInfo>();

        public List<LotInfo> ExportLots { get; set; }
            = new List<LotInfo>();
    }
}
