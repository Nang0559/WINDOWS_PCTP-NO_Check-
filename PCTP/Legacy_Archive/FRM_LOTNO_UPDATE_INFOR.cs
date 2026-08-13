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
using PCTP;
using PCTP.ClassSQL;
using DevExpress.CodeParser;
using DevExpress.Xpf;
using System.IO;
using System.Xml.Linq;
using DevExpress.XtraEditors.Controls;
using System.Text.RegularExpressions;
using System.Xml.Schema;
using MyValidation;

namespace PCTP
{
    public partial class FRM_LOTNO_UPDATE_INFOR : ValidatedForm
    {
        public FRM_LOTNO_UPDATE_INFOR()
        {
            InitializeComponent();
            cmdUpdate.Enabled = false;
            Load_();
        }
        public static string LOTNO;
        SQLPROVIDER sqlBrv = new SQLPROVIDER();
        private void Load_()
        {
            string sql;
            DataTable tbl_Lot_If = new DataTable();
            txt_UDSLXuat.Text = "0";
            txtUDSLNHAP.Text = "0";
            LOTNO = txt_LOTNO.Text.Trim();
            if (LOTNO == null || LOTNO=="")
            {
                sql = "select * from stocktp ";
                tbl_Lot_If = sqlBrv.ExecuteQuery(sqlBrv.B7R2_FCCdb, sql);
                gridCLOT_IF.DataSource = tbl_Lot_If;
            }
            else
            {
                sql = "select * from stocktp where substring(Lot,1,13) = '" + LOTNO + "' or LOT = '"+ LOTNO +"'";


                tbl_Lot_If = sqlBrv.ExecuteQuery(sqlBrv.B7R2_FCCdb, sql);
                gridCLOT_IF.DataSource = tbl_Lot_If;
                txt_LOTNO.Text = tbl_Lot_If.Rows[0]["LOT"].ToString();
                txtSLSX.Text = tbl_Lot_If.Rows[0]["SLSX"].ToString();
                txtSLNHAP.Text = tbl_Lot_If.Rows[0]["SLNHAP"].ToString();
                txtSLDX.Text = tbl_Lot_If.Rows[0]["SLXUAT"].ToString();
                txtSLCL.Text = tbl_Lot_If.Rows[0]["SLCONLAI"].ToString();
            }
        }

        //private void txtUDSLSX_TextChanged(object sender, EventArgs e)
        //{
        //    if(int.Parse(txtUDSLSX.Text) > int.Parse(txtSLSX.Text))
        //    {
        //        MessageBox.Show("Không thể nhập SL sản xuất > sl đã đăng ký !","Thông Báo",MessageBoxButtons.OK,MessageBoxIcon.Error);
        //    }    
        //}

        private void txtUDSLNHAP_TextChanged(object sender, EventArgs e)
        {



        }

        private void txtUDSLCL_EditValueChanged(object sender, EventArgs e)
        {
           
        }

        private void cmdUpdate_Click(object sender, EventArgs e)
        {
            string sql;
            if (ValidateChildren(ValidationConstraints.Enabled))
            {
               
                    
                        sql = "update stocktp set slnhap = " + int.Parse(txtUDSLNHAP.Text) + " , slxuat = " + int.Parse(txt_UDSLXuat.Text) +  " , slconlai = " + int.Parse(txtUDSLNHAP.Text) + "-" +  int.Parse(txt_UDSLXuat.Text) + " where Lot = '" + txt_LOTNO.Text + "'";
                    
                    
                    sqlBrv.ExecuteNonQuery(sqlBrv.B7R2_FCCdb, sql);
                    Load_();
                    MessageBox.Show("Done !!!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
            }
        }

        private void txtUDSLNHAP_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                //if (KTNHAP() == true)
                //{
                //    cmdUpdate.Enabled = true;
                //}
                //else
                //    cmdUpdate.Enabled = false;
            }
        }
        private Boolean KTNHAP()
        {
            Boolean KT = true;
            try
            {
                if (txtUDSLNHAP.Text != "")
                {
                    if (int.Parse(txtUDSLNHAP.Text) > int.Parse(txtSLSX.Text))
                    {

                        DialogResult rs = MessageBox.Show("Không thể nhập kho : Sl nhập > sl đã đăng ký sản xuất!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        if (rs == DialogResult.OK)
                        {

                            txtUDSLNHAP.EditValue = null;
                            txtUDSLNHAP.Refresh();
                            KT = false;

                        }

                    }
                    if (txtUDSLNHAP.Text != "")
                    {
                        if (int.Parse(txtUDSLNHAP.Text) < int.Parse(txtSLDX.Text))
                        {
                            DialogResult rs = MessageBox.Show("Không thể nhập kho : Sl nhập < sl đã xuất !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            if (rs == DialogResult.OK)
                            {
                                txtUDSLNHAP.EditValue = null;
                                txtUDSLNHAP.Refresh();
                                KT = false;
                            }


                        }
                    }
                }

            }
            catch
            {
                KT = false;
            }
            return KT;
        }
        private void txtUDSLNHAP_EditValueChanged(object sender, EventArgs e)
        {
            if (txtUDSLNHAP.Text== "")
            {
                txtUDSLNHAP.Text = "0";
            }
    }

        private void cmd_TK_Click(object sender, EventArgs e)
        {
            Load_();
        }

        private void txtUDSLNHAP_Properties_Validating(object sender, CancelEventArgs e)
        {
            if(txtSLSX.Text =="" )
                txtSLSX.Text = "0";
            if (txtUDSLNHAP.Text != "" && txt_UDSLXuat.Text != "")
            {
                if (int.Parse(txtUDSLNHAP.Text.ToString()) < int.Parse(txt_UDSLXuat.Text))
                {
                    e.Cancel = true;
                    txtUDSLNHAP.Focus();

                    if (int.Parse(txtUDSLNHAP.EditValue.ToString()) > int.Parse(txtSLDX.Text))
                    {
                        eProvider.SetError(txtUDSLNHAP, "số lượng sửa không thể lớn hơn số lượng đã xuất");
                        eProvider.SetError(txtSLDX, "");

                    }
                    if (int.Parse(txtSLSX.Text) < int.Parse(txtUDSLNHAP.EditValue.ToString()))
                    {
                        eProvider.SetError(txtUDSLNHAP, "số lượng sửa không thể lớn hơn số lượng đã đăng ký SX");
                        eProvider.SetError(txtSLSX, "");
                    }
                }
                else
                {
                    e.Cancel = false;
                    eProvider.SetError(txtUDSLNHAP, null);
                    cmdUpdate.Enabled = true;
                }
            }

        }

        //private void txtUDSLCL_Validating(object sender, CancelEventArgs e)
        //{
        //    int SLNHAP = int.Parse(txtSLNHAP.Text);
        //    if (txtUDSLNHAP.Text != "")
        //    {
        //        SLNHAP = int.Parse(txtUDSLNHAP.Text);
        //    }
        //    if (int.Parse(txtUDSLCL.Text) != (SLNHAP - int.Parse(txtSLDX.Text)))
        //    {
        //        e.Cancel = true;
        //        txtUDSLCL.Focus();
        //        eProvider.SetError(txtUDSLCL, "số lượng còn lại khác sl nhập - sl xuất");
        //    }
        //    else
        //    {
        //        e.Cancel = false;
        //        eProvider.SetError(txtUDSLCL, null);
        //        cmdUpdate.Enabled = true;
        //    }

        //}

        private void txtE_UDSLXuat_EditValueChanging(object sender, ChangingEventArgs e)
        {
           

        }

        private void txt_UDSLXuat_Validating(object sender, CancelEventArgs e)
        {
            int SLDXUAT;

            if (txtUDSLNHAP.Text != "" && txt_UDSLXuat.Text != "")
            {

                if (int.Parse(txt_UDSLXuat.Text) > int.Parse(txtUDSLNHAP.Text))
                {
                    MessageBox.Show("Không thể nhập kho : Sl còn lại  khác số lượng nhập - sl xuất !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txt_UDSLXuat.Text = "";
                }
            }

        }
    }
}