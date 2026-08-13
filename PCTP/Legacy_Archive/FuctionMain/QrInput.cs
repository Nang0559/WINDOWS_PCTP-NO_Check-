using DevExpress.CodeParser;
using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.FuctionMain
{
    public class VarQRInput
    {
        public string LoNo { get; set; }
        public string MH { get; set; }
        public string BP { get; set; }
        public string Line { get; set; }
        public int SL { get; set; }
        public DateTime Nsx{ get; set; }
        public string SoTem { get; set; }
    public string SP { get; set; }

        public VarQRInput(string QR)
        {
           
            string[] QRInputtext;
            string LotNoBPSL = null;
            try
            {
                QRInputtext = QR.Split(':');
                LotNoBPSL = QRInputtext[0];
                MH = QRInputtext[1];
               // Nsx = DateTime.ParseExact(QRInputtext[2]);
                SL = int.Parse(QRInputtext[3]);
                if (QRInputtext.Length > 3)
                {
                    SP = QRInputtext[5];
                }
                LoNo = LotNoBPSL.Substring(0, 13);
                Line = LotNoBPSL.Substring(13, 3);
                BP = LotNoBPSL.Substring(16, 4);
                SoTem = LotNoBPSL.Substring(21, 4);
            }
            catch
            {
                XtraMessageBox.Show("Eror format !", "Thông Báo");
            }
        }

    }
  
}
