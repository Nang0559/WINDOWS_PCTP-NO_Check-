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
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP
{
    public partial class NHAPKHOKHONGQRCODE : DevExpress.XtraEditors.XtraForm
    {
        public NHAPKHOKHONGQRCODE()
        {
            InitializeComponent();
        }
        SQLPROVIDER sqlBrv = new SQLPROVIDER();
        private void LODDL()
        {
            DataTable Tbl, Shift,TK;
            string sql = "select Code, Name from  ITEM_NO_QRCODE group by Code, Name ";
            string shift = "select Code,Name from B20Shift  where IsActive= 1 order by Code";
            string SQLTK  = "select lot,part,name,ngaysx,casx,slsx,ngaynhap,slnhap,ngayxuat,slxuat,slconlai as SLCONLAI , slconlaitmp as SOLUONGDANGGIAO from STOCKTP where  slconlai > 0 and part in (select Code from ITEM_NO_QRCODE) ";
            TK = sqlBrv.ExecuteQuery(sqlBrv.B7R2_FCCdb, SQLTK);
            gridCTTCT.DataSource = TK;
            Tbl = sqlBrv.ExecuteQuery(sqlBrv.B7R2_FCCdb, sql);
            
            lookUpMHNOQR.Properties.DataSource = Tbl;
          
            lookUpMHNOQR.Properties.DisplayMember = "Code";
            lookUpMHNOQR.Properties.ValueMember = "Code";
            Shift = sqlBrv.ExecuteQuery(sqlBrv.B7R2_FCCdb, shift);
            lookUpCA.Properties.DataSource = Shift;

            lookUpCA.Properties.DisplayMember = "Code";
            lookUpCA.Properties.ValueMember = "Code";
            sidePThemMoi.Enabled = false;
            textCode.Text = "";
            textName.Text = "";
            textSLNHAP.Text = "";
            lookUpCA.Text = "";
            dateENgayNhap.DateTime = DateTime.Now;
        }

        private void NHAPKHOKHONGQRCODE_Load(object sender, EventArgs e)
        {
            LODDL();
           
        }

        private void sidePanel3_Click(object sender, EventArgs e)
        {

        }

        private void CMDTHEMMOI_Click(object sender, EventArgs e)
        {
            sidePThemMoi.Enabled = true;
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            string Code,Name,sqlinsert,IDtrung ,IDMAX,sqlB20,sql_TIMIDMAX = "select max(ID) from B20Item";
            int ID;
            Code = textCode.Text.Trim();
            Name = textName.Text.Trim();
            if (Code.Trim() == "" || Name.Trim() == "")
            {
                XtraMessageBox.Show("Không được bỏ trống giá trị tên hàng và mã hàng ! ");
            }
            else
            {
                sqlB20 = "select Id from b20item where Code = '" + Code + "'";
                IDMAX = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sqlB20);
                if (KTNHAPMM() == true)
                {
                    //extCode.Select();
                    //textCode.IsModified = true;
                    //textName.Select();
                    //textName.IsModified = true;
                    //if (textCode.DoValidate() && textCode.DoValidate())
                    //{
                        if (IDMAX == null || IDMAX == "")
                    {

                        IDMAX = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sql_TIMIDMAX);
                        IDtrung = "select ID from ITEM_NO_QRCODE where ID = " + int.Parse(IDMAX) + "+ 1";
                        if (sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, IDtrung) != null || sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, IDtrung) != "")
                        {
                            IDtrung = "select max(Id) from ITEM_NO_QRCODE ";
                            IDMAX = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, IDtrung);
                            sqlB20 = "insert into b20item  (ParentId,Code,Name,Unit) values (1,'" + Code + "',' " + Name + "','Cái')";
                            sqlBrv.ExecuteNonQuery(sqlBrv.B7R2_FCCdb, sqlB20);
                        }
                        ID = int.Parse(IDMAX) + 1;

                    }
                    else
                    {
                        ID = int.Parse(IDMAX);
                    }




                    sqlinsert = "insert into ITEM_NO_QRCODE values (" + ID + ",'" + Code + "','" + Name + "')";

                    sqlBrv.ExecuteNonQuery(sqlBrv.B7R2_FCCdb, sqlinsert);

                    MessageBox.Show("Complete!!! ", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LODDL();
                }
            }
        }
            // Kiểm tra nhập mã mới .
            private Boolean KTNHAPMM()
            {
                Boolean KQ = false;
                string sql = "select count(*) from ITEM_NO_QRCODE where Code = '" + textCode.Text.Trim() + "'";

                string KQTV = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sql);
                if (textCode.Text == "" || textName.Text == "")
                {
                    MessageBox.Show("Không được bỏ trắng giá trị ", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    KQ = false;
                }
                else
                {
                    if (int.Parse(KQTV) == 0)
                    {
                        KQ = true;
                    }
                    else
                    {

                        MessageBox.Show("Đã có mã trùng, không thể nhập ", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        KQ = false;

                    }
                }
                return KQ;
            }
        private string LOTNO()
        {
            string Code,TIMID,CA,LOT = "";
            CA = lookUpCA.Text.Trim();
            Code = lookUpMHNOQR.Text.Trim();
            string Ngay = dateNN.DateTime.ToString("yyMMdd");
            string sql = "select Id from ITEM_NO_QRCODE where Code = '" + Code + "'";
            TIMID = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sql);

            LOT = "SP" + Ngay + TIMID + CA;
            return LOT;
        }
        private Boolean KiemTRaNhapKho()
        {
            Boolean KQ= false;
            string MH = lookUpMHNOQR.Text.Trim();
            string N = dateNN.DateTime.ToString("yyMMdd");
            string SL = textSLNHAP.Text;
            string Ca = lookUpCA.Text.Trim();
            if(MH == lookUpMHNOQR.Properties.NullText || N =="010101" || SL == "" || Ca == lookUpCA.Properties.NullText )
            {
                KQ = false;
            }
            else
            {
                KQ = true;
            }
            return KQ;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }
        private void SaveData()
        {
            string MH = lookUpMHNOQR.Text.Trim();
            string sql = "select Name from ITEM_NO_QRCODE where Code = '" + MH + "'";
            string Name = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sql);
            string N = dateNN.DateTime.ToString("MM/dd/yyy");
            string NN = dateENgayNhap.DateTime.ToString("MM/dd/yyy");
            string SL = textSLNHAP.Text;
            string Ca = lookUpCA.Text.Trim();
            string TT;
            sql= "select LOT from STOCKTP where LOT = '" + LOTNO() + "'";
            TT = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sql);
                
            if(TT == "")
            {
                sql = "INSERT INTO STOCKTP(LOT,Part, NAME, CASX, NGAYSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, Satus) VALUES('" + LOTNO() + "', '" + MH + "','" + Name.Trim() + "'," + Ca + ",'" + N + "','" + NN + "', " + SL + ", '" + N + "', " + 0 + ", " + SL + ",  " + 1 + " )";
                sqlBrv.ExecuteNonQuery(sqlBrv.B7R2_FCCdb, sql);
            }
            else
            {
                sql = "UPDATE STOCKTP SET slnhap = (slnhap + " + SL + "),SLCONLAI = (SLCONLAI + " + SL + "),NGAYNHAP = '" + N + "'  WHERE LOT = '" + LOTNO() + "'";
                sqlBrv.ExecuteNonQuery(sqlBrv.B7R2_FCCdb, sql);
            }
        
        }

        private void cmdSua_ButtonClick(object sender, DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
        {
           string LOT =  gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "lot").ToString();
            int slnhap = int.Parse(gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "slnhap").ToString());
            int slconlai = int.Parse(gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "SLCONLAI").ToString());
            int slsx;
            int slxuat = int.Parse(gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "slxuat").ToString());
            if (gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "slsx").ToString() == "")
            {
                slsx = 0;
            }
            else { slsx = int.Parse(gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "slsx").ToString()); }
            string sql = "update Stocktp set SLNHAP =  " + slnhap + ",slconlai =" + slconlai + ",slsx = " + slsx + ",slxuat = "+ slxuat + " where lot = '" + LOT + "'";
            sqlBrv.ExecuteNonQuery(sqlBrv.B7R2_FCCdb, sql);
            MessageBox.Show("Sửa LOT :  " + LOT + " . " , "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LODDL();
        }

        private void cmdxoa_Click(object sender, EventArgs e)
        {
            string sql,LOT = gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "lot").ToString();
            DialogResult rs = MessageBox.Show("Xóa LOT :  " + LOT + " . ", "Thông Báo", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if(rs== DialogResult.Yes)
            {
                sql = "delete Stocktp where LOT = '" + LOT + "'";
                sqlBrv.ExecuteNonQuery(sqlBrv.B7R2_FCCdb, sql);
                gridVCTK.DeleteRow(gridVCTK.FocusedRowHandle);
            }
            LODDL();
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            lookUpMHNOQR.Select();
            lookUpMHNOQR.IsModified = true;
            dateNN.Select();
            dateNN.IsModified = true;
            textSLNHAP.Select();
            textSLNHAP.IsModified = true;
            lookUpCA.Select();
            lookUpCA.IsModified = true;

            if (lookUpMHNOQR.DoValidate() && dateNN.DoValidate() &&  textSLNHAP.DoValidate() && lookUpCA.DoValidate() )
            {
                SaveData();
                MessageBox.Show("OK ", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                NHAPKHOKHONGQRCODE NewForm = new NHAPKHOKHONGQRCODE();
                NewForm.Show();
                this.Dispose(false);
                //LODDL();
            }
            else
                XtraMessageBox.Show("Hãy nhập đầy đủ thông tin !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);


        }
        
       

        private void gridVCTK_KeyDown(object sender, KeyEventArgs e)
        {
            GridView view = sender as GridView;
            if (e.Control && e.KeyCode == Keys.C)
            {
                if (view.GetRowCellValue(view.FocusedRowHandle, view.FocusedColumn) != null && view.GetRowCellValue(view.FocusedRowHandle, view.FocusedColumn).ToString() != String.Empty)
                    Clipboard.SetText(view.GetRowCellValue(view.FocusedRowHandle, view.FocusedColumn).ToString());
                else
                    MessageBox.Show("The value in the selected cell is null or empty!");
                e.Handled = true;
            }
        }

        

        private void textCode_Properties_Validating(object sender, CancelEventArgs e)
        {
            TextEdit edit = sender as TextEdit;
            e.Cancel = string.IsNullOrEmpty(edit.Text);
        }

        private void textName_Properties_Validating(object sender, CancelEventArgs e)
        {
            TextEdit edit = sender as TextEdit;
            e.Cancel = string.IsNullOrEmpty(edit.Text);
        }

        private void lookUpMHNOQR_Properties_Validating(object sender, CancelEventArgs e)
        {
            LookUpEdit edit = sender as LookUpEdit;

            if (string.IsNullOrEmpty(edit.EditValue == null ? "" : edit.EditValue.ToString()) == true)
            {
                
                lookUpMHNOQR.ErrorText = "Hãy chọn Mã Hàng !";
                e.Cancel = true;
            }
        }

        private void dateNN_Properties_Validating(object sender, CancelEventArgs e)
        {
            DateEdit edit = sender as DateEdit;
            if (string.IsNullOrEmpty(edit.Text) == true)
            {
                dateNN.ErrorText = "Chọn Ngày Sản Xuất !";
                e.Cancel = true;
            }
        }

        private void textSLNHAP_Properties_Validating(object sender, CancelEventArgs e)
        {
            TextEdit edit = sender as TextEdit;
            if (string.IsNullOrEmpty(edit.Text) == true || int.Parse(edit.Text)==0)
            {
                textSLNHAP.ErrorText = "Chọn Số Lượng Nhập !";
                e.Cancel = true;
            }
        }

        private void lookUpCA_Properties_Validating(object sender, CancelEventArgs e)
        {
            LookUpEdit edit = sender as LookUpEdit;

            if (string.IsNullOrEmpty(edit.EditValue == null ? "" : edit.EditValue.ToString()) == true)
            {
                lookUpCA.ErrorText = "Chọn Ca Sản Xuất !";
                e.Cancel = true;
            }
        }
    }
}