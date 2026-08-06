using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data.Common;
using System.Data.SqlTypes;
using Tutorial.SqlConn;
using PCTP;
using PCTP.ClassSQL;
namespace PCTP
{
    public partial class LISTNHAPKHO : Form
    {
        public ListView _listView { get; set; }
        public string HT;
        public LISTNHAPKHO()
        {
            InitializeComponent();
            
        }
        SQLPROVIDER PROVIDER = new SQLPROVIDER();
        public LISTNHAPKHO(ListView listView,string ht)
        {
            InitializeComponent();
            _listView = listView;
            HT = ht;
        }

        private void LISTNHAPKHO_Load(object sender, EventArgs e)
        {
            //LW_NHAP_KHO.Items.Clear();
            LW_NHAP_KHO.View = View.Details;
            LW_NHAP_KHO.Columns.Add("STT", 50);
            LW_NHAP_KHO.Columns.Add("LOT NO", 200);
            LW_NHAP_KHO.Columns.Add("MA SP", 170);
            LW_NHAP_KHO.Columns.Add("TEN SP", 170);
            LW_NHAP_KHO.Columns.Add("MODEL", 100);
            LW_NHAP_KHO.Columns.Add("NGAY SAN XUAT", 150);
            LW_NHAP_KHO.Columns.Add("CA SAN XUAT", 100);
            LW_NHAP_KHO.Columns.Add("SL SAN XUAT", 100);
            LW_NHAP_KHO.Columns.Add("SL ĐÃ NHẬP", 100);
            LW_NHAP_KHO.Columns.Add("SL CON LAI", 100);
            LW_NHAP_KHO.Columns.Add("SL SE NHAP", 100);
            
            LW_NHAP_KHO.Columns.Add("LOAI HINH", 100);
            LW_NHAP_KHO.Columns.Add("LY DO NG", 100);
            LW_NHAP_KHO.Columns.Add("SL DA TRA", 100);
            LW_NHAP_KHO.GridLines = true;
            LW_NHAP_KHO.FullRowSelect = true;
            LW_NHAP_KHO.View = View.Details;
            for (int i = 0; i < _listView.Items.Count; i ++)
            {
                ListViewItem LW = new ListViewItem((i+1).ToString() );//STT
               

                LW.SubItems.Add(_listView.Items[i].SubItems[1]);//LOT
                LW.SubItems.Add(_listView.Items[i].SubItems[2]);//Part
                LW.SubItems.Add(_listView.Items[i].SubItems[3]);//Name
                LW.SubItems.Add(_listView.Items[i].SubItems[4]);//Model
                LW.SubItems.Add(_listView.Items[i].SubItems[5]);//Ngay SX
                LW.SubItems.Add(_listView.Items[i].SubItems[6]);//CA SX
                LW.SubItems.Add(_listView.Items[i].SubItems[7]);//SL SX
                LW.SubItems.Add(_listView.Items[i].SubItems[8]);//SL DA NHAP
                LW.SubItems.Add(_listView.Items[i].SubItems[9]);//SL CON LAI
                
                LW.SubItems.Add(_listView.Items[i].SubItems[10]);//SL SE NHAP
                LW.SubItems[10].BackColor = Color.YellowGreen;
                LW.SubItems[10].ForeColor = Color.Red;
                LW.UseItemStyleForSubItems = false;
                LW.SubItems.Add(_listView.Items[i].SubItems[11]);//LH NHAP
                LW.SubItems.Add(_listView.Items[i].SubItems[12]);//LD NG
                LW.SubItems.Add(_listView.Items[i].SubItems[13]);//SL DA TRA
                LW_NHAP_KHO.Items.Add(LW);
            }
        }

        private void CMD_OK_Click(object sender, EventArgs e)
        {
            int StatuS;
            string sql;
            string sql1;
            //string sql2;
            


            for (int i = 0; i < LW_NHAP_KHO.Items.Count; i++)
            {
                string LOAIHINHNHAP = LW_NHAP_KHO.Items[i].SubItems[11].Text;
                string LOTNO = LW_NHAP_KHO.Items[i].SubItems[1].Text;
                string Model = LW_NHAP_KHO.Items[i].SubItems[4].Text;
                string part = LW_NHAP_KHO.Items[i].SubItems[2].Text;
                string Name = LW_NHAP_KHO.Items[i].SubItems[3].Text;
                string CaSX = LW_NHAP_KHO.Items[i].SubItems[6].Text;
                int SLSX = Convert.ToInt32(LW_NHAP_KHO.Items[i].SubItems[7].Text);
                int SLDANHAP = Convert.ToInt32(LW_NHAP_KHO.Items[i].SubItems[8].Text);
                DateTime NGAYSX = Convert.ToDateTime(LW_NHAP_KHO.Items[i].SubItems[5].Text);
                string LD = LW_NHAP_KHO.Items[i].SubItems[12].Text;
                int SLCONLAI = Convert.ToInt32(LW_NHAP_KHO.Items[i].SubItems[9].Text);
                int SLSENHAP = Convert.ToInt32(LW_NHAP_KHO.Items[i].SubItems[10].Text);
                int SLDATRA = Convert.ToInt32(LW_NHAP_KHO.Items[i].SubItems[13].Text);
                if (HT == "NK")
                {
                    if (LOAIHINHNHAP == "NHAP MOI")
                    {
                        if (SLDANHAP <= 0)
                        {
                            if (SLSENHAP == SLSX)
                            {
                                StatuS = 1;
                            }
                            else
                            {
                                StatuS = 0;
                            }

                            sql = "INSERT INTO STOCKTP(LOT, MODEL, Part, NAME, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, Satus) VALUES('" + LOTNO + "'" + ", " + "'" + Model + "'" + ", " + "'" + part + "'" + ", " + "'" + Name + "'" + ", " + CaSX + ", " + "'" + NGAYSX.ToString("MM/dd/yyyy") + "', " + SLSX + ", '" + DateTime.Today.ToString("MM/dd/yyyy") + "', " + (SLDANHAP + SLSENHAP) + ", '" + DateTime.Today.ToString("MM/dd/yyyy") + "', " + 0 + ", " + SLSENHAP + "," + StatuS + ")";
                        }
                        else
                        {
                            if ((SLSENHAP + SLDANHAP) == SLSX)
                            {
                                StatuS = 1;
                            }
                            else
                            {
                                StatuS = 0;
                            }
                            sql = "UPDATE STOCKTP SET slsx= " + SLSX + " ,slnhap = (slnhap + " + SLSENHAP + "),SLCONLAI = (SLCONLAI + " + SLSENHAP + "),NGAYNHAP = cast(GETDATE() as smalldatetime),Satus = " + StatuS + " WHERE LOT = '" + LOTNO + "'";
                        }
                        PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql);
                        
                        

                    }

                    else
                    {
                        if (SLSENHAP == SLDATRA)
                        {
                            StatuS = 1;
                        }
                        else
                        {
                            StatuS = 0;
                        }
                        sql1 = "UPDATE STOCKTPTRAHANG SET SLNHANLAI = SLNHANLAI + " + SLSENHAP + ",SLCONLAI = SLCONLAI - " + SLSENHAP + ",STATUS = " + StatuS + " WHERE LOT = '" + LOTNO + "' and Ly_do_ng = N'" + LD + "'";
                        //if ()
                        //{
                        sql = "UPDATE STOCKTP SET SLDATRA = (SLDATRA - " + SLSENHAP + ") ,SLCONLAI = (SLCONLAI +" + SLSENHAP + "),NGAYNHAP = cast(GETDATE() as smalldatetime),Satus = 0 WHERE LOT = '" + LOTNO + "'";
                        //}
                        //else
                        //{
                        PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql1);
                        //}
                        string sql2 = "INSERT INTO STOCKTPNHANTRA (LOT,PART_NO,PART_NAME,NGAY_NHAN_TRA,SL_NHAN_TRA,LY_DO_NG) VALUES ( " + "'" + LOTNO + "'" + "," + "'" + part + "'" + "," + "'" + Name + "'" + "," + "'" + DateTime.Now.ToString("MM/dd/yyyy") + "'" + "," + "'" + SLSENHAP + "'" + "," + "N'" + LD + "'" + ")";
                        PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql2);
                    }
                }
                else
                {
                    if (LD == "")
                    {
                        sql = "update stocktp set Satus = 1 WHERE LOT = '" + LOTNO + "'";
                        PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql);
                    }
                    else
                    {
                        sql = "update STOCKTPTRAHANG set Status = 1 WHERE LOT = '" + LOTNO + "' and Ly_do_ng = N'" + LD + "'";
                        PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql);
                    }
                }
            }
            
            foreach (string item in SQLPROVIDER.c_Ns)
            {
                {
                    sql = "insert into NHAP_TP_HIS (lotcase)  values ('" + item + "')";
                    PROVIDER.ExecuteNonQuery(PROVIDER.B7R2_FCCdb, sql);
                }
            }
            MessageBox.Show("Đã Cập Nhập Thành Công !", "THÔNG BÁO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // ListViewItem[] items = new ListViewItem[10];
            // LW_NHAP_KHO.Items.CopyTo(items, 2);
            SQLPROVIDER.c_Ns.Clear();
            this.Close();

        }

        private void CMD_XOA_Click(object sender, EventArgs e)
        {
            if (LW_NHAP_KHO.SelectedItems.Count > 0)
            {
                ListViewItem item = LW_NHAP_KHO.SelectedItems[0];
                int STT = Convert.ToInt32(item.SubItems[0].Text);
                var result = MessageBox.Show("BẠN MUỐN XÓA ITEM THỨ : " + STT + "!", "THÔNG BÁO",
                             MessageBoxButtons.OK,
                             MessageBoxIcon.Error);
                if (result == DialogResult.OK)
                {

                    for (int i = 0; i < LW_NHAP_KHO.SelectedItems.Count; i++)
                        LW_NHAP_KHO.Items.Remove(LW_NHAP_KHO.SelectedItems[i]);


                }
            }
            else
            {
                string MSG = "CHƯA CÓ ITEM NÀO ĐƯỢC CHỌN";
                MessageBox.Show(MSG, "THÔNG BÁO");
            }
        }

        private void LISTNHAPKHO_FormClosed(object sender, FormClosedEventArgs e)
        {
            PCTP.ClassSQL.SQLPROVIDER.c_Ns.Clear();
        }
    }
}
