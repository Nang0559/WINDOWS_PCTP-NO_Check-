using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace PCTP.Acess_Image
{
    public partial class FRMSHOW : DevExpress.XtraEditors.XtraForm
    {
        SQLPROVIDER BRV = new SQLPROVIDER();
       
        string _idsp = string.Empty;    
        public FRMSHOW(string idsp)
        {
            InitializeComponent();
            _idsp     = idsp;
            LoadData();
        }
        private void FRMSHOW_Load(object sender, EventArgs e)
        {
            
        }
        public void LoadData()
        {
            if (_idsp != string.Empty)
            {
                string sql = "select * from B20ImageStore where ParentId = " + int.Parse(_idsp) + "";
                byte[] _value = new byte[0];
                string id = "";
                Image image = null;
                // try
                // {
                using (SqlConnection connection = new SqlConnection(BRV.B7R2_FCCdb))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(sql, connection);


                    SqlDataReader MyReader = command.ExecuteReader();



                    while (MyReader.Read())
                    {
                        id = MyReader["id"].ToString();
                        //_value = String.IsNullOrEmpty(MyReader[_feild].ToString()) ? (Byte?)null : Byte.Parse(MyReader[_feild].ToString());
                        _value = DBNull.Value.Equals(MyReader["Image"]) ? new byte[0] : (byte[])MyReader["Image"];
                        MemoryStream stream = new MemoryStream(_value);
                        if (_value.Length > 0)
                        {
                            image = Image.FromStream(stream);
                            image.Tag = id.ToString();
                            imageSliderShow.Images.Add(image);
                            imageSliderShow.ToolTipTitle = id.ToString();
                        }
                        else
                        {

                            image = TextToBitmap("NoImage", imageSliderShow.Size);
                            image.Tag = id.ToString();
                            imageSliderShow.Images.Add(image);
                        }
                        //_value = ;
                    }


                    connection.Close();

                }
            }
            else
                MessageBox.Show("Không có dữ liệu !");
           
        }
        public Bitmap TextToBitmap(string text, Size size)
        {
            Bitmap bmp = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(text, new Font("Times New Roman", 16), Brushes.Black, new RectangleF(0, 0, bmp.Width, bmp.Height));
                g.Flush();
            }
            return bmp;
        }
    }
}