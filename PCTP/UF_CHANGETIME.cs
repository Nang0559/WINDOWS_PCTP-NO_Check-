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

namespace PCTP
{
    public partial class UF_CHANGETIME : DevExpress.XtraEditors.XtraForm
    {
        public UF_CHANGETIME()
        {
            InitializeComponent();

        }
        IFSPROVIDER iFSPROVIDER = new IFSPROVIDER();
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        private void LoadHVNTime()
        {
            DataTable Tbl = new DataTable();
            string sql = "select * from QRCODE_CHANGETIME order by GIOHVN";
            Tbl = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            gridControl1.DataSource = Tbl;
        }
        

        private void UF_CHANGETIME_Load(object sender, EventArgs e)
        {
            LoadHVNTime();
           
        }

        private void cmdsua_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            string GIOHVN = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "GIOHVN").ToString();
            string GIOfccHVNvp = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "GioFCCVP").ToString();
            string GIOfccHVNhn = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "GIOFCCHN").ToString();
            string _Update = "update QRCODE_CHANGETIME set GioFCCVP = '" + GIOfccHVNvp + "', GIOFCCHN = '" + GIOfccHVNhn + "' where GIOHVN = '"+ GIOHVN  + "'";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, _Update);
            MessageBox.Show("Sửa OK !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadHVNTime();
        }
    }
}