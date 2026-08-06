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
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.QRCODE_HVN
{
    public partial class FRM_LISTRUNGMSL : DevExpress.XtraEditors.XtraForm
    {
        public FRM_LISTRUNGMSL()
        {
            InitializeComponent();
            
        }
        public ListView _listView { get; set; }
        public static string MAHANG = null, STTPHIEU = null,SL = null;
        public FRM_LISTRUNGMSL(ListView listView)
        {
            _listView = listView;
            InitializeComponent();

        }
        private void BAN_QRCODE_Load(object sender, EventArgs e)
        {
            listVTrungMaSL.Items.AddRange((from ListViewItem item in _listView.Items
                                      select (ListViewItem)item.Clone()).ToArray());

        }
        
        //private Boolean KTMAHANG()
        //{
        //    //Boolean KQ = false;
        //    //for (int i = 0; i< PHIEUGIAOHANG)
        //    //return KQ;

        //}
        private void txt_DOCQRCODE_KeyPress(object sender, KeyPressEventArgs e)
        {
           
        }

        private void listVTrungMaSL_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void listVTrungMaSL_DoubleClick(object sender, EventArgs e)
        {
            if (listVTrungMaSL.SelectedItems.Count == 0)
            {

                return;
            }
            else
            {
                ListViewItem item = listVTrungMaSL.SelectedItems[0];
                STTPHIEU = item.SubItems[0].Text;
                MAHANG = item.SubItems[2].Text;
                SL = item.SubItems[4].Text;
                this.Close();
            }
        }
    }
}