using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Models
{
    public sealed class InspectionResult
    {
        public bool AllPassed { get; set; }
        public int ScannedCount { get; set; }
        public int FailedCount { get; set; }
        public List<BoxScanResult> Details { get; set; } = new();
    }
}
