using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.YMN;

namespace PCTP.QRCODE_HVN.PGH
{
    public partial class frm_err_cnk : DevExpress.XtraEditors.XtraForm
    {
       
        public frm_err_cnk(DataTable dt)
        {
            InitializeComponent();
            dtt = dt;
            gridControlcnk_err.DataSource = dtt;
        }
        public frm_err_cnk(List<DS_ERR_CNK> _dser)
        {
            InitializeComponent();
            dser = _dser;
            gridControlcnk_err.DataSource = dser;
        }
        public static DataTable dtt = new DataTable();
        public static List<DS_ERR_CNK> dser = new List<DS_ERR_CNK>();
        private void GridView_cnk_err_RowClick(object sender, DevExpress.XtraGrid.Views.Grid.RowClickEventArgs e)
        {
            String lot = (sender as GridView).GetFocusedRowCellDisplayText("LOT");
            lot = "'" + lot + "'";
            TONKHOTP tONKHOTP = new TONKHOTP(lot);
            tONKHOTP.ShowDialog();
        }
    }
}