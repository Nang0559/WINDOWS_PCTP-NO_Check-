using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PCTP.ClassSQL;

namespace PCTP.QRCODE_HVN.NhanLaiNG.NhanLaiTP
{
    public partial class frmNhanLaiTP : DevExpress.XtraEditors.XtraForm
    {
        public frmNhanLaiTP()
        {
            InitializeComponent();
        }
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public event EventHandler StockListViewLoadEventRaised;
        public event EventHandler EditStockMenuClickEventRaised;
        private void DefaultLoad()
        {
            
        }
        private void LoadStockToGrid(BindingSource stockListBindingSource,Dictionary<string,string> headingsDictionary, Dictionary<string, float> gridColumnWidthsDictionary, int rowHeight)
        {
           
            //this.gridCtrDONHANG.DataSource = stockListBindingSource;
            //int optionsWidth = 0;
            //SetGridColumnWidths(gridColumnWidthsDictionary, ref optionsWidth);
        }
    }
}