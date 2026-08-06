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

namespace PCTP
{
    public partial class UF_KTBANQR : Form
    {
        public UF_KTBANQR()
        {
            InitializeComponent();
        }
        public int nexti = 0;

        DataGridView data;
        ComboBox gioxuat;
        DateTimePicker ngayxuat;
      
        public UF_KTBANQR(DataGridView dt,ComboBox gx, DateTimePicker nx)
        {
            InitializeComponent();
            data = dt;
            gioxuat = gx;
            ngayxuat = nx;
        }
        private void UF_KTBANQR_Load(object sender, EventArgs e)
        {

            LOT_NO.Text = data.Rows[0].Cells[0].Value.ToString().Trim();
            TTLOTNO(LOT_NO.Text);
        }
        private void TTLOTNO(string _LOTNO)
        {
            string _N = ngayxuat.Value.Day.ToString("00");

            string _T = ngayxuat.Value.Month.ToString("00");

            string _NM = ngayxuat.Value.Year.ToString("0000");
            string NGAYXUAT = _NM + "-" + _T + "-" + _N + " 00:00:00";
            SqlConnection conn = DBUtils.GetDBConnection();
            conn.Open();
            // -----------------------
            string sql1;
            sql1 = "select *  " +
                   " from LUUDOCQRCODE where lotfcc like '%" + _LOTNO.Trim() + "%' and ngayxuat = '" + NGAYXUAT + "' and  gioxuat = '" + gioxuat.Text.Trim() + "' order by lothvn";
                         

            

            SqlCommand com1 = new SqlCommand(sql1, conn); //bat dau truy van
            com1.CommandType = CommandType.Text;
            SqlDataAdapter da1 = new SqlDataAdapter(com1); //chuyen du lieu ve
            DataTable dt1 = new DataTable(); //tạo một kho ảo để lưu trữ dữ liệu
            da1.Fill(dt1);  // đổ dữ liệu vào kho
            GW_TTXUATHANGBANQR.DataSource = dt1;

            decimal Total = 0;
            decimal TotalSLHVN = 0;
            string LOTTP = "";
            string[] LOT;
            string[] LOTSL;
            for (int i = 0; i < GW_TTXUATHANGBANQR.Rows.Count; i++)
            {
                LOTTP = Convert.ToString(GW_TTXUATHANGBANQR.Rows[i].Cells["lotfcc"].Value);
                if (LOTTP.IndexOf(",") > 0)
                {
                    LOT = LOTTP.Split(',');
                    for (int j = 0; j < LOT.Length; j++)
                    {
                        if (LOT[j].StartsWith(_LOTNO) == true)
                        {
                            //LBL_LOT.Text = LOT[j];
                            string LOTSUB = LOT[j].Trim();
                            LOTSL = LOTSUB.Split('-');
                            int SL = Convert.ToInt32(LOTSL[1]);
                            //LBL_LOT.Text = Convert.ToString(SL);
                            GW_TTXUATHANGBANQR.Rows[i].Cells["sltemfcc"].Value = SL;
                            
                        }

                    }
                }
                Total += Convert.ToDecimal(GW_TTXUATHANGBANQR.Rows[i].Cells["SLTEMFCC"].Value);

                TotalSLHVN += Convert.ToInt32(GW_TTXUATHANGBANQR.Rows[i].Cells["sltemhvn"].Value);
            }
            LBL_SLTEMFCC.Text = Total.ToString();
            LBL_SLTEMHVN.Text = TotalSLHVN.ToString();
         
            conn.Close();
        }
        private void LOAD_TO_LIST(string LOTNO1)
        {
            for (int i = 0; i < data.Rows.Count; i++)
            {
                string LOTNO = data.Rows[i].Cells[0].Value.ToString().Trim();

            }
        }

        private void LOT_NO_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                GW_TTXUATHANGBANQR.DataSource = null;
                TTLOTNO(LOT_NO.Text.Trim());
            }
        }
    }
}
