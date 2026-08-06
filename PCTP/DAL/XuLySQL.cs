using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.DAL
{
    public class XuLySqlServer
    {
        //private string ChuoiKetNoi = @"Data Source=FCCIT\SQLEXPRESS01;Initial Catalog=WPF_QuanLyCafe;Integrated Security=True";
        //public string ChuoiKetNoi = @"Data Source=192.168.200.14\BRAVO;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";
        public string ChuoiKetNoi = @"Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;User ID=sa;Password=fccbrv";
        public DataTable LoadData(string sql)
        {
            using (SqlConnection cnn = new SqlConnection(ChuoiKetNoi))
            {
                cnn.Open();
                SqlCommand cmd = new SqlCommand(sql, cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }
        public DataTable LoadDataParameter(string sql, string[] name, object[] values, int parameter)
        {
            using (SqlConnection cnn = new SqlConnection(ChuoiKetNoi))
            {
                cnn.Open();
                SqlCommand cmd = new SqlCommand(sql, cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                for (int i = 0; i < parameter; i++)
                {
                    cmd.Parameters.AddWithValue(name[i], values[i]);
                }
                DataTable dt = new DataTable();
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(dt);
                return dt;
            }
        }
        public int Execute(string query, params SqlParameter[] paramters)
        {
            int data = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(ChuoiKetNoi))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(query, connection);

                    if (paramters != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, paramters[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteNonQuery();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery AUTOWH");
                data = -1;
            }
            return data;
        }
        public object GetOneValue(string sql, string[] name, object[] values, int parameter)
        {
            using (SqlConnection cnn = new SqlConnection(ChuoiKetNoi))
            {
                cnn.Open();
                SqlCommand cmd = new SqlCommand(sql, cnn);
                cmd.CommandType = CommandType.StoredProcedure;
                for (int i = 0; i < parameter; i++)
                {
                    cmd.Parameters.AddWithValue(name[i], values[i]);
                }
                return cmd.ExecuteScalar();
            }
        }
    }
}
