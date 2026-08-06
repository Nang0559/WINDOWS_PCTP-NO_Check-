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
using DevExpress.XtraTreeList;
using DevExpress.XtraTreeList.Columns;
using DevExpress.XtraTreeList.Nodes;
using System.Data.SqlClient;
namespace PCTP
{
    public partial class TRAHANGNG : DevExpress.XtraEditors.XtraForm
    {
        public TRAHANGNG()
        {
            InitializeComponent();
        }
        SQLPROVIDER PROVIDER = new SQLPROVIDER();
        string LOT_NO;
       
        int SLCON = 0;
        private void txtQRCODE_KeyPress(object sender, KeyPressEventArgs e)
        {
                //string LOT_NO;
                int SL_LOT;
               string LOT;
            string MAHANG="";
            string KQ = "";
            DataSet DTKHO = new DataSet();
            DataSet DTTRA = new DataSet();
            DataSet DTNHANTRA = new DataSet();
            string SLDANHAP;
                int TTSL;
                TTSL = 0;
                Boolean TONTAI = true;
                 
            if (e.KeyChar == 13)
            {
                string QRFCC = txtQRCODE.Text.Trim();
                string[] arrQRFCC = QRFCC.Split(':');
                string LONO = arrQRFCC[0];
                string MH = arrQRFCC[1];
                string ID = "select ID from B20Item where code = '" + arrQRFCC[1] + "'";
                ID = PROVIDER.ExecuteReader(PROVIDER.B7R2_FCCdb, ID);
                string sql = "select CustomerCode  from B20ItemQuyCach where ItemCode = '" + MH + "' ";
                KQ = PROVIDER.ExecuteReader(PROVIDER.B7R2_FCCdb, sql);
                SL_LOT = arrQRFCC[3].Length;
                /*
                if (KQ == "0100002")
                {

                    LOT_NO = LONO.Substring(0, 6 + ID.Length + 2);
                    if (LOT_NO.Substring(LOT_NO.Length - 1, 1) == "0")
                    {
                        LOT_NO = LONO.Substring(0, 6 + ID.Length + 1);
                    }
                }
                else
                {
                    LOT_NO = LONO.Substring(0, 6 + ID.Length + 1);
                }
                */
                LOT_NO = LONO.Substring(0, 13);
                //LOT_NO = LONO.Substring(0, 6 + ID.Length + 1);
                string sql0 = "select kho.LOT,kho.PART,kho.NAME,kho.CASX,kho.NGAYSX,kho.SLSX,kho.SLNHAP,kho.SLXUAT,kho.SLCONLAI " +  
                    " from STOCKTP as kho" +
                    " where kho.lot LIKE '" + LOT_NO + "%'";
                DTKHO = PROVIDER.ExecuteQuery_Dataset(PROVIDER.B7R2_FCCdb, sql0);
                string sql1 = "select tra.NGAYTRA, tra.SLTRA, tra.LY_DO_NG " +
                    " from STOCKTPTRAHANG as tra " +
                " where tra.lot LIKE '" + LOT_NO + "%'";
                DTTRA = PROVIDER.ExecuteQuery_Dataset(PROVIDER.B7R2_FCCdb, sql1);
                string sql2 = "select NGAY_NHAN_TRA,SL_NHAN_TRA  from STOCKTPNHANTRA where lot LIKE '" + LOT_NO + "%'";
                    DTNHANTRA = PROVIDER.ExecuteQuery_Dataset(PROVIDER.B7R2_FCCdb, sql2);
                if (DTKHO.Tables[0].Rows.Count >= 1)
                {
                    gridCtrTTKho.DataSource = DTKHO.Tables[0];
                    gridCtrTTTraHang.DataSource = DTTRA.Tables[0];
                    gridCtrNHANTRA.DataSource = DTNHANTRA.Tables[0];
                    foreach (DataRow dr in DTKHO.Tables[0].Rows)
                    {
                        SLCON = Convert.ToInt32(dr["SLCONLAI"].ToString());
                        MAHANG = dr["Part"].ToString();
                    }
                    if (SLCON > 0 )
                    {
                        txtMahang.Text = MAHANG;
                    }
                    else
                    {
                        MessageBox.Show("Không thể trả hàng vì số lượng hiện tại bằng 0", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                }
                else

                {
                    MessageBox.Show("Code nhập vào không đúng . Không tồn tại LOT NO trong kho ","Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

              
            }

            
        }
        private void loaddl()
        {
            DataSet DTKHO = new DataSet();
            DataSet DTTRA = new DataSet();
            DataSet DTNHANTRA = new DataSet();
            string sql0 = "select kho.LOT,kho.PART,kho.NAME,kho.CASX,kho.NGAYSX,kho.SLSX,kho.SLNHAP,kho.SLXUAT,kho.SLCONLAI " +
                    " from STOCKTP as kho" +
                    " where kho.lot LIKE '" + LOT_NO + "%'";
            DTKHO = PROVIDER.ExecuteQuery_Dataset(PROVIDER.B7R2_FCCdb, sql0);
            string sql1 = "select tra.NGAYTRA, tra.SLTRA, tra.LY_DO_NG " +
                " from STOCKTPTRAHANG as tra " +
            " where tra.lot LIKE '" + LOT_NO + "%'";
            DTTRA = PROVIDER.ExecuteQuery_Dataset(PROVIDER.B7R2_FCCdb, sql1);
            string sql2 = "select NGAY_NHAN_TRA,SL_NHAN_TRA  from STOCKTPNHANTRA where lot LIKE '" + LOT_NO + "%'";
            DTNHANTRA = PROVIDER.ExecuteQuery_Dataset(PROVIDER.B7R2_FCCdb, sql2);
            if (DTKHO.Tables[0].Rows.Count >= 1)
            {
                gridCtrTTKho.DataSource = DTKHO.Tables[0];
                gridCtrTTTraHang.DataSource = DTTRA.Tables[0];
                gridCtrNHANTRA.DataSource = DTNHANTRA.Tables[0];
            }
        }
        private void txtQRCODE_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            LISTTRAHANG f_LISTRHANG = new LISTTRAHANG();
            f_LISTRHANG.ShowDialog();
        }
        private void TH ()
        {
            string LDNG = txtLydoNG.Text;
            string MH = txtMahang.Text;
            int SLT = 0;
            if (txtSLtra.Text != "")
            {
                SLT = int.Parse(txtSLtra.Text);
            }
            else
            {
                
            }

            if(GridViewTTKHO.RowCount>0)
            {
                if(LDNG == "" || txtSLtra.Text == "")
                {
                    MessageBox.Show("Hãy nhập đầy đủ thông tin !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    try
                    {
                        string sql = "INSERT INTO STOCKTPTRAHANG (LOT,NGAYTRA,SLTRA,TTSLTRA,LY_DO_NG,STATUS,SLNHANLAI,SLCONLAI) " +
                            "      VALUES ( "
                            + "'" + LOT_NO + "'" + "," + "Convert(smalldatetime ,'" + NgayTra.DateTime + "',104)" + "," + SLT + "," + SLT + "," + "N'" + LDNG + "'" + "," + "'" + 0 + "'" + "," + "'" + 0 + "'" + "," + "'" + SLT + "'" + ")";
                        PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql);
                        sql = "UPDATE STOCKTP SET SLCONLAI = SLCONLAI - " + SLT + ", SLNHAP = SLNHAP - " + SLT + " WHERE LOT LIKE '" + LOT_NO + "%'";
                        PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql);
                        MessageBox.Show("Done !", "ATWH", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        txtLydoNG.Text = "";
                        txtMahang.Text = "";
                        txtQRCODE.Text = "";
                        txtSLtra.Text = "0";
                        txtSLtra.Refresh();
                    }
                    catch
                    {
                        MessageBox.Show("say ra loi ! vui lòng liên hệ admin", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Không có trong kho ", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void cmdtrahang_Click(object sender, EventArgs e)
        {
            TH();
            loaddl();
        }

        private void txtSLtra_EditValueChanging(object sender, DevExpress.XtraEditors.Controls.ChangingEventArgs e)
        {
           
        }

        private void txtSLtra_TextChanged(object sender, EventArgs e)
        {
            if (int.Parse(txtSLtra.EditValue.ToString()) > SLCON)
            {
                MessageBox.Show("Lỗi dữ liệu . sl trả không thể lớn hơn số lượng còn lại trong kho !","Thông Báo",MessageBoxButtons.OK ,MessageBoxIcon.Error);
                txtSLtra.Text  = "0";
                txtSLtra.Refresh();
                return;
            }
        }
    }
}