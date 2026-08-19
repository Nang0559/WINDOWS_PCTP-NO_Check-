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

namespace PCTP.QRCODE_HVN
{
    public partial class FRM_SUALOTHVN : DevExpress.XtraEditors.XtraForm
    {
        public FRM_SUALOTHVN()
        {
            InitializeComponent();
        }
        public string _MH;
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public FRM_SUALOTHVN(string MH)
        {
            InitializeComponent();
            _MH= MH;
            load();
        }
        private void FRM_SUALOTHVN_Load(object sender, EventArgs e)
        {
            string sql = "select * from docqrcode where MAHANGFCC = '" + _MH + "' order by LOTHVN";
            
            DataTable table = new DataTable();
            table = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCtrSUALOTHVN.DataSource = table;
            
        }
        private void load()
        {
            string sql = "select * from docqrcode where MAHANGFCC = '" + _MH + "' order by LOTHVN";

            DataTable table = new DataTable();
            table = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCtrSUALOTHVN.DataSource = table;
        }
        private void simpleButton1_Click(object sender, EventArgs e)
        {
            int STT;
            string sqlupdate;
            if (LOTBD.Text == "")
            {
                MessageBox.Show("Bạn hãy chọn LOT Gốc để bắt đầu sửa !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {

                sqlupdate = "update tmpphieugiaohang set LOT = '' where MAHANG = '" + _MH + "'";
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                //sqlupdate = "update docqrcode set KETQUA = 'OK' where stt =" + STTDAU + "";
                sqlupdate = "update docqrcode set KETQUA = 'OK' where KETQUA = 'DG' ";
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                for (int i = 0; i < gridVSUALOTHVN.RowCount; i++)
                {
                    if (gridVSUALOTHVN.IsRowSelected(i) == true)
                    {
                        STT = int.Parse(gridVSUALOTHVN.GetRowCellValue(i, "STT").ToString());
                        sqlupdate = "update docqrcode set SUALOTHVN = '" + (Double.Parse(LOTBD.Text.Trim()) + 1).ToString() + "',KETQUA = 'OK' where stt =" + STT + "";
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                        LOTBD.Text = (Double.Parse(LOTBD.Text.Trim()) + 1).ToString();
                    }
                }
            }
            this.Close();
        }
        private string STTDAU;
        private void gridCtrSUALOTHVN_DoubleClick(object sender, EventArgs e)
        {
            LOTBD.Text = gridVSUALOTHVN.GetRowCellValue(gridVSUALOTHVN.FocusedRowHandle, "LOTHVN").ToString();
            STTDAU = gridVSUALOTHVN.GetRowCellValue(gridVSUALOTHVN.FocusedRowHandle, "STT").ToString();
        }
    }
}