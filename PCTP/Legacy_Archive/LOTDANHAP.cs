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
using PCTP.ClassSQL;
using DevExpress.XtraGrid;

namespace PCTP
{
    public partial class LOTDANHAP : DevExpress.XtraEditors.XtraForm
    {
        public LOTDANHAP()
        {
            InitializeComponent();

        }
    public LOTDANHAP(string lot)
    {
        InitializeComponent();
            C_NO = lot;
            load_data();
        }
            SQLPROVIDER SQLPROVIDER = new SQLPROVIDER();
        public static string C_NO;
        private void load_data()
        {
            DataSet dt = new DataSet();
            
            string sql = "select * from nhap_tp_his where lotcase = '" + C_NO + "'";
            dt= SQLPROVIDER.ExecuteQuery_Dataset(SQLPROVIDER.B7R2_FCCdb, sql);
            gridCtrLOTDATA.DataSource = dt.Tables[0];
         
                foreach (string item in SQLPROVIDER.c_Ns)
                {
                    string CSNO = item.Substring(0, item.Length - 12);
                string Ti = item.Substring(item.Length - 12,12);
                if (CSNO.Contains(C_NO) == true)
                    {
                    gridView1.AddNewRow();
                    gridView1.SetRowCellValue(GridControl.NewItemRowHandle, gridView1.Columns["LOTCASE"], C_NO);
                    gridView1.SetRowCellValue(GridControl.NewItemRowHandle, gridView1.Columns["TimeSpan"],DateTime.ParseExact(Ti,"yyMMddHHmmss",null));
                    gridView1.UpdateCurrentRow();

                    }
                    
                }
          
            
            
            
        }

        private void LOTDANHAP_Load(object sender, EventArgs e)
        {
            load_data();
        }
    }
}