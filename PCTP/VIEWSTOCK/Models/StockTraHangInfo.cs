using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class StockTraHangInfo
    {
        public string Lot { get; set; }
        public DateTime? NgayTra { get; set; }
        public int SlTra { get; set; }
        public int SlNhanLai { get; set; }
        public string LyDoNg { get; set; }
        public int SlConLai => SlTra - SlNhanLai;
    }
}
