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
using System.Data.SqlClient;

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
            Tbl = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridControl1.DataSource = Tbl;
        }
        

        private void UF_CHANGETIME_Load(object sender, EventArgs e)
        {
            LoadHVNTime();
           
        }

        private void cmdsua_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
            object gioHvnValue = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "GIOHVN");
            object gioFccVpValue = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "GioFCCVP");
            object gioFccHnValue = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "GIOFCCHN");
            if (gioHvnValue == null || gioHvnValue == DBNull.Value)
            {
                MessageBox.Show("Không xác định được khóa GIOHVN.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            const string updateSql = @"
UPDATE QRCODE_CHANGETIME
SET GioFCCVP = @GioFCCVP,
    GIOFCCHN = @GIOFCCHN
WHERE GIOHVN = @GIOHVN;";
            int affected = sqlBRV.ExecuteNonQuery(
                sqlBRV.B7R2_FCCdb,
                updateSql,
                new SqlParameter("@GioFCCVP", SqlDbType.NVarChar, 50) { Value = Convert.ToString(gioFccVpValue) ?? string.Empty },
                new SqlParameter("@GIOFCCHN", SqlDbType.NVarChar, 50) { Value = Convert.ToString(gioFccHnValue) ?? string.Empty },
                new SqlParameter("@GIOHVN", SqlDbType.NVarChar, 50) { Value = Convert.ToString(gioHvnValue) });
            if (affected != 1)
            {
                MessageBox.Show("Không cập nhật được đúng một dòng. Hãy kiểm tra khóa GIOHVN có duy nhất hay không.",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("Sửa OK !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadHVNTime();
        }
    }
}