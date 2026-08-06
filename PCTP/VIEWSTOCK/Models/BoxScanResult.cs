using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class BoxScanResult
    {
        public string TemCode { get; set; }
        public string ItemCode { get; set; }
        public string NSX { get; set; }
        public bool IsMatch { get; set; }
        public string MismatchFields { get; set; }
    }
}
