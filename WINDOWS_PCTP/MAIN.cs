using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tutorial.SqlConn;
using System.Data.SqlClient;

namespace WINDOWS_PCTP
{
    public partial class MAIN : Form
    {
        public MAIN()
        {
            InitializeComponent();
        }

        private void MAIN_Load(object sender, EventArgs e)
        {
            SqlConnection conn = DBUtils.GetDBConnection();
            try
            {
                string sql = "sselect Image from vB20ImageStore_ITEM";
                conn.Open();
                DataSet ds = new DataSet();
                SqlDataAdapter adapter = new SqlDataAdapter(sql, conn);
                adapter.Fill(ds);
                foreach(DataRow row in ds.Tables[0].Rows)
{
                    LW_PGH.Items.Add(row["Image"].ToString());
                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }

        }
    }
}
