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
using DevExpress.DocumentServices.ServiceModel.DataContracts;
namespace PCTP
{
    public partial class MOLAILOT : Form
    {
        public MOLAILOT()
        {
            InitializeComponent();
        }
        PCTP.ClassSQL.SQLPROVIDER sqlBrv = new PCTP.ClassSQL.SQLPROVIDER();

        private void TXT_DOCQRCODE_TextChanged(object sender, EventArgs e)
        {

        }

        private void TXT_DOCQRCODE_KeyPress(object sender, KeyPressEventArgs e)
        {

            string  MH, sp;
            string LOT, LOTDD2 = "", LOTDD3 = "", LOTDD4 = "", LOTCH = "", LOTCH2 = "";
            int KTOK, SL_LOT;
            string sql;

            if (e.KeyChar == 13)
            {
                
                string QRFCC = TXT_DOCQRCODE.Text.Trim();
                string[] arrQRFCC = QRFCC.Split(':');
                string LONO = arrQRFCC[0];
                sp = arrQRFCC[5];
                string ID = "select STUFF('00000', 5-LEN(id)+1, LEN(id), id) from B20Item where code = '" + arrQRFCC[1] + "'";
                ID = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, ID);
                int IntID = int.Parse(ID);
                sql = "select CustomerCode  from B20ItemQuyCach where ItemCode = '" + arrQRFCC[1] + "' ";
                string KQ = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sql);
                //if (KQ == "0100002")
                //{
                //    string LOT1 = "";
                //    if (LONO.Substring(6, 1) == "0")
                //    {
                //        LOT1 = LONO.Substring(0, 6) + ID;

                //    }
                //    else
                //    {
                //        LOT1 = LONO.Substring(0, 6) + IntID.ToString();

                //    }
                //    string Ca = LONO.Substring(LOT1.Length, 1);
                //    string Gear = LONO.Substring(LOT1.Length + 1, 1);
                //    string Gear1 = LONO.Substring(LOT1.Length + 1, 1);
                //    if (int.Parse(Gear) == 0)
                //        Gear = "";
                //    string BP = "", BP2 = "", BP3 = "", BP4 = "";
                //    // xua ly bộ phận
                //    BP = LONO.Substring((LONO.Length - 8), 4);
                //    LOTDD2 = LONO.Substring(0, 6) + IntID.ToString() + Ca + BP + Gear;
                //    // dang 2
                //    BP2 = LONO.Substring((LOT1.Length + 2), 4);
                //    LOTDD3 = LONO.Substring(0, 6) + IntID.ToString() + Ca + BP2 + Gear;
                //    // dang 3
                //    BP3 = LONO.Substring((LOT1.Length + 5), 4);
                //    LOTDD4 = LONO.Substring(0, 6) + IntID.ToString() + Ca + BP3 + Gear;
                //    //dang khác
                //    LOT = LONO.Substring(0, 6) + IntID.ToString() + Ca;

                //    // Lot Chuan
                //   // LOTCH2 = LONO.Substring(0, 6) + IntID.ToString() + casx;


                //}
                //else
                //{
                    string LOT1 = "";

                    LOT1 = LONO.Substring(0, 6) + ID;
                    
                   
                    string sqllinecode = "select STUFF('000', 3-LEN(B.Id)+1, LEN(B.Id), B.Id) as Id from B30AccDoc A,B20Lines B where B.Code = A.LinesCode and  stt = '" + sp + "'";
                    string Linescode = sqlBrv.ExecuteReader(sqlBrv.B7R2_FCCdb, sqllinecode);
                    ///
                    string sqlc = "select  STUFF('0000', 4-LEN(A.MachinesCode)+1, LEN(A.MachinesCode), A.MachinesCode) as Dept, GearCode,ShiftCode from B30AccDoc A where  stt = '" + sp + "'";
                    DataTable tbsql = sqlBrv.ExecuteQuery(sqlBrv.B7R2_FCCdb, sqlc);
                    object dept = tbsql.Rows[0]["Dept"];
                    object GearCode = tbsql.Rows[0]["GearCode"];
                    object Casx = tbsql.Rows[0]["ShiftCode"];
                    

                   
                    string BP = "", BP2 = "", BP3 = "", BP4 = "";
                    // xua ly bộ phận
                    BP = LONO.Substring((LONO.Length - 8), 4);
                    LOTDD2 = LONO.Substring(0, 6) + IntID.ToString() + Casx + BP + GearCode;
                    // dang 2
                    BP2 = LONO.Substring((LOT1.Length + 2), 4);
                    LOTDD3 = LONO.Substring(0, 6) + IntID.ToString() + Casx + BP2 + GearCode;
                    // dang 3
                    BP3 = LONO.Substring((LOT1.Length + 5), 4);
                    LOTDD4 = LONO.Substring(0, 6) + IntID.ToString() + Casx + BP3 + GearCode;
                    //dang khác
                    LOT = LONO.Substring(0, 6) + IntID.ToString() + Casx;
                    // Lot Chuan 
                    LOTCH = LONO.Substring(0, 6) +ID.ToString() + Casx + GearCode + Linescode + dept;
                //}
                //LOT = LONO.Substring(0, 6 + ID.Length + 1);
                SL_LOT = arrQRFCC[3].Length;
                
                KTOK = arrQRFCC.Length;
                if(KTOK != 6)
                {
                    MessageBox.Show("Lỗi định dạng !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    
                    sql = "select SATUS,LOT,MODEL,PART,NAME,CASX,NGAYSX,SLSX,NGAYNHAP,SLNHAP,NGAYXUAT,SLXUAT,SLCONLAI from stocktp where lot = '" + LOT + "' OR lot = '" + LOTDD2 + "' OR lot = '" + LOTDD3 + "' OR lot = '" + LOTDD4 + "' or lot = '"+ LOTCH  + "'";
                    SqlConnection conn = DBUtils.GetDBConnection();
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.CommandType = CommandType.Text;
                    SqlDataAdapter da1 = new SqlDataAdapter(cmd); //chuyen du lieu ve
                    DataTable dt1 = new DataTable(); //tạo một kho ảo để lưu trữ dữ liệu
                    da1.Fill(dt1);  // đổ dữ liệu vào kho

                    GW_MOLOT.DataSource = dt1;
                    if (GW_MOLOT.RowCount <0)
                    {
                        MessageBox.Show("Không tồn tại LOT cần mở !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    foreach (DataGridViewColumn dc in GW_MOLOT.Columns)
                    {
                        if (dc.Index.Equals(0))
                        {
                            dc.ReadOnly = false;
                        }
                        else
                        {
                            dc.ReadOnly = true;
                        }
                    }
                    TXT_DOCQRCODE.Text = "";
                    GW_MOLOT.CurrentCell = GW_MOLOT.Rows[0].Cells[0];
                    GW_MOLOT.CurrentCell.Selected = true;
                    
                    GW_MOLOT.BeginEdit(true);
                    conn.Close();
                }
            }
        }

        private void CMD_OK_Click(object sender, EventArgs e)
        {
            if (GW_MOLOT.RowCount < 0)
            {
                MessageBox.Show("Không tồn tại LOT cần mở !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                int status = Convert.ToInt32(GW_MOLOT.Rows[0].Cells[0].Value);
                string LOT = GW_MOLOT.Rows[0].Cells[1].Value.ToString();
                string sql = "update stocktp set SATUS = " + status  + "  where lot = '" + LOT + "'";
                SqlConnection conn = DBUtils.GetDBConnection();
                conn.Open();
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                conn.Close();
                MessageBox.Show("XONG !", "THÔNG BÁO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
