using PCTP.Modules.KhoVatLy.Kho.Models;
using System.Collections.Generic;


namespace PCTP.Modules.KhoCore.Models
{
    public class LotSplitResult
    {
        public List<LotInfo> RemainingLots { get; set; }
            = new List<LotInfo>();

        public List<LotInfo> ExportLots { get; set; }
            = new List<LotInfo>();
    }
}
