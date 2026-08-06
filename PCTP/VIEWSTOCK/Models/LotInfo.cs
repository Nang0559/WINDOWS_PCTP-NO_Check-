using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class LotInfo
    {
        public string LotNo { get; set; }

        public int Quantity { get; set; }

        public string TemCode { get; set; }

        public string RawQr { get; set; }

        public QRCodeInfo QRInfo { get; set; }
    }

}
