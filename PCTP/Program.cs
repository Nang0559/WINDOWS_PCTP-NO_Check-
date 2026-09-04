
using PCTP.QRCODE_HVN.YMN;
using PCTP.YMN;
using PCTP.FuctionPrint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Windows.Forms;
using PCTP.QRCODE_HVN.Report;
using PCTP.VIEWSTOCK;

namespace PCTP
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("YMVN_APP");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new UF_TACHLOT());
            //Application.Run(new YAMAHAQRCDE_SP());
            //Application.Run(new FrmTEST());
            //Application.Run(new GIAOHANGYMN());
            //Application.Run(new MENU_AUTO_QRCODE());
            //Application.Run(new PGH_XK());
          //  Application.Run(new MainStock());
            Application.Run(new Main_APP());
            // Application.Run(new UF_QRCodeERR());
            //Application.Run(new QRCODE_HVN.ComaprePart.ComaparePart());
            //Application.Run(new IFS_PUR_OR.FRM_PURCHASE_ODERScs());
            //Application.Run(new PCTP.QRCODE_HVN.NhanLaiNG.frm_NhanHangHVN());
        }
    }
}
