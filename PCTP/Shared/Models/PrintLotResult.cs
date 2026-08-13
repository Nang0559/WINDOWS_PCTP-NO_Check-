using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class PrintLotResult
    {
        public string LotNo { get; set; }
        public string TemCode { get; set; }
        public string QrData { get; set; }
        public int Quantity { get; set; }
        public string ItemCode { get; set; }

        public string ProductName { get; set; }

        public DateTime? ImportDate { get; set; }
        public List<LotInfo> Lots { get; set; } = new List<LotInfo>();
    }
}
