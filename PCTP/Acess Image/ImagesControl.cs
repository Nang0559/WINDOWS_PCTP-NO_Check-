using DevExpress.Xpo.DB.Helpers;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraSplashScreen;
using PCTP.Acess_Image;
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
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.ImagesControl
{
    public partial class ImagesControl : DevExpress.XtraEditors.XtraUserControl
    {
        public ImagesControl()
        {
            InitializeComponent();

        }
        SQLPROVIDER BRV = new SQLPROVIDER();
        int add_edit = 0;
        private void UserControlnew_Load(object sender, EventArgs e)
        {
            DataTable dt = new DataTable();
            dt = BRV.ExecuteQuery(BRV.B7R2_FCCdb,"select id,code,name,model from B20item");
            gridControl1.DataSource= dt;    
            lokupItemCode.Properties.DataSource= dt;
            pictureEdit1.Visible= false;
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            
            using (SqlConnection conn = new SqlConnection(@"Data Source=FCCIT\SQLEXPRESS;Initial Catalog=A;User ID=sa;Password=Abc@123;Encrypt=False"))
            {
                SqlCommand CmdSql = new SqlCommand("SELECT * FROM [Emply]", conn);
                conn.Open();
                CmdSql.ExecuteNonQuery();

                SqlDataReader reader = CmdSql.ExecuteReader();
                while (reader.Read())
                {
                    this.pictureEdit1.EditValue = reader["Photo"];
                }
                conn.Close();
            }
        }

        private void gridView1_RowClick(object sender, DevExpress.XtraGrid.Views.Grid.RowClickEventArgs e)
        {
            string ma = (sender as GridView).GetFocusedRowCellValue("Id").ToString();
            textEdit1.Text = ma;
            string sq = "select * from  [Emply] where Id= '"+ int.Parse(ma) +"'";
            byte[] imag = BRV.ExecuteReaderByte(sq, "Photo");
            MemoryStream stream = new MemoryStream(imag);
            pictureEdit1.Image = Image.FromStream(stream);

            //imageSlider1.Images.Add(Image.FromStream(stream));
            //using (SqlConnection conn = new SqlConnection(@"Data Source=FCCIT\SQLEXPRESS;Initial Catalog=A;User ID=sa;Password=Abc@123;Encrypt=False"))
            //{
            //    SqlCommand CmdSql = new SqlCommand(sq, conn);
            //    conn.Open();
            //    CmdSql.ExecuteNonQuery();

            //    SqlDataReader reader = CmdSql.ExecuteReader();
            //    while (reader.Read())
            //    {
            //        this.pictureEdit1.EditValue = reader["Photo"];
            //    }
            //    conn.Close();
            //}
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            MemoryStream fullstream = new MemoryStream();
            MemoryStream ms = new MemoryStream();
            //foreach (Image im in imageSlider1.ImageList)
            //{

            //}
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
        bool ttanh = false;
        public void LoadData()
        {
            if (!splashScreenManager1.IsSplashFormVisible)
                splashScreenManager1.ShowWaitForm();
            ttanh = false;
            //int idsp = int.Parse(lokupItemCode.EditValue.ToString());
            if (lokupItemCode.EditValue != null)
            {
                string sql = "select * from B20ImageStore where ParentId = " + int.Parse(lokupItemCode.EditValue.ToString()) + "";
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
                        ttanh = true;
                        id = MyReader["id"].ToString();
                        //_value = String.IsNullOrEmpty(MyReader[_feild].ToString()) ? (Byte?)null : Byte.Parse(MyReader[_feild].ToString());
                        _value = DBNull.Value.Equals(MyReader["Image"]) ? new byte[0] : (byte[])MyReader["Image"];
                        MemoryStream stream = new MemoryStream(_value);
                        if (_value.Length > 0)
                        {
                            image = Image.FromStream(stream);
                            image.Tag = id.ToString();
                            imageSlider1.Images.Add(image);
                            textEdit1.Text = id.ToString();
                        }
                        else
                        {

                            image = TextToBitmap("NoImage", imageSlider1.Size);
                            image.Tag = id.ToString();
                            imageSlider1.Images.Add(image);
                        }
                        //_value = ;
                    }


                    connection.Close();
                }
            }
            splashScreenManager1.CloseWaitForm();
            if(ttanh==false)
                cmdEdit.Enabled = false;
            else
                cmdEdit.Enabled = true;
        }
        private void lokupItemCode_KeyPress(object sender, KeyPressEventArgs e)
        {
            if(e.KeyChar == 13)
            {

                LoadData();
               
               
            }    
        }

        private void cmdEdit_Click(object sender, EventArgs e)
        {
            add_edit = 1;
            pictureEdit1.Image = imageSlider1.CurrentImage;
            imageSlider1.Visible = false;
            pictureEdit1.Visible=true;
        }
        public byte[] imageToByteArray(System.Drawing.Image imageIn)
        {
            MemoryStream ms = new MemoryStream();
            imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            return ms.ToArray();
        }
        private void cmdSave_Click(object sender, EventArgs e)
        {
            if (!splashScreenManager1.IsSplashFormVisible)
                splashScreenManager1.ShowWaitForm();
            if (add_edit == 1)
            {
                if (pictureEdit1.Image != null && textEdit1.Text != "")
                {
                    using (SqlConnection conn = new SqlConnection(BRV.B7R2_FCCdb))
                    {
                        SqlCommand CmdSql = new SqlCommand("UPDATE B20ImageStore set [Image]=@Image,[Type]=@Type,[CreatedAt]=@CreatedAt" +
                           " where id = " + int.Parse(textEdit1.Text) + "", conn);
                        conn.Open();
                        CmdSql.Parameters.AddWithValue("@Image", imageToByteArray(pictureEdit1.Image));

                        CmdSql.Parameters.AddWithValue("@Type", "ITEM");
                        CmdSql.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        //CmdSql.Parameters.AddWithValue("@Password", textBox10.Text);

                        CmdSql.ExecuteNonQuery();
                        conn.Close();

                        //SqlCommand CmdSql2 = new SqlCommand("INSERT INTO [Resources] (ResourceID,ResourceName) " +
                        //   "VALUES (@ResourceID,@ResourceName)", conn);

                        //conn.Open();
                        //CmdSql2.Parameters.AddWithValue("@ResourceID", label36.Text);
                        //CmdSql2.Parameters.AddWithValue("@ResourceName", textBox2.Text + "," + textBox1.Text);

                        //CmdSql2.ExecuteNonQuery();
                        //conn.Close();
                    }
                }
                else
                {
                    using (SqlConnection conn = new SqlConnection(BRV.B7R2_FCCdb))
                    {
                        SqlCommand CmdSql = new SqlCommand("Delete B20ImageStore " +
                           " where id = " + int.Parse(textEdit1.Text) + "", conn);
                        conn.Open();
                        

                        CmdSql.ExecuteNonQuery();
                        conn.Close();
                        textEdit1.Text = string.Empty;
                        //SqlCommand CmdSql2 = new SqlCommand("INSERT INTO [Resources] (ResourceID,ResourceName) " +
                        //   "VALUES (@ResourceID,@ResourceName)", conn);

                        //conn.Open();
                        //CmdSql2.Parameters.AddWithValue("@ResourceID", label36.Text);
                        //CmdSql2.Parameters.AddWithValue("@ResourceName", textBox2.Text + "," + textBox1.Text);

                        //CmdSql2.ExecuteNonQuery();
                        //conn.Close();
                    }
                }
            }
            else if (add_edit == 2)
            {
                using (SqlConnection conn = new SqlConnection(BRV.B7R2_FCCdb))
                {
                    SqlCommand CmdSql = new SqlCommand("insert into B20ImageStore ([ParentId], [Image],[Type],[CreatedAt]) " +
                       " values (@ParentId,@Image,@Type,@CreatedAt)", conn);
                    conn.Open();
                    CmdSql.Parameters.AddWithValue("@Image", imageToByteArray(pictureEdit1.Image));

                    CmdSql.Parameters.AddWithValue("@Type", "ITEM");
                    CmdSql.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                    CmdSql.Parameters.AddWithValue("@ParentId", lokupItemCode.EditValue);

                    CmdSql.ExecuteNonQuery();
                    conn.Close();

                    //SqlCommand CmdSql2 = new SqlCommand("INSERT INTO [Resources] (ResourceID,ResourceName) " +
                    //   "VALUES (@ResourceID,@ResourceName)", conn);

                    //conn.Open();
                    //CmdSql2.Parameters.AddWithValue("@ResourceID", label36.Text);
                    //CmdSql2.Parameters.AddWithValue("@ResourceName", textBox2.Text + "," + textBox1.Text);

                    //CmdSql2.ExecuteNonQuery();
                    //conn.Close();
                }
            }
            
            imageSlider1.Images.Clear();
            if(pictureEdit1.Image != null)
            pictureEdit1.Image.Clone();
            splashScreenManager1.CloseWaitForm();
            LoadData();
            cmdEdit.Enabled = true;
            imageSlider1.Visible = true;
            pictureEdit1.Visible = false;
            
        }

        private void imageSlider1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            string a = "";
        }

        private void imageSlider1_ImageChanged(object sender, DevExpress.XtraEditors.Controls.ImageChangedEventArgs e)
        {
            if(imageSlider1.Images.Count!=0)
            textEdit1.Text =  imageSlider1.CurrentImage.Tag.ToString();
        }

        private void cmdAdd_Click(object sender, EventArgs e)
        {
            add_edit = 2;
            pictureEdit1.Visible = true;
            imageSlider1.Visible = false;
        }

        private void gridView1_RowClick_1(object sender, RowClickEventArgs e)
        {
            GridView gv = (GridView)sender;
            if (e.RowHandle != 0)
            {
                var id = gv.GetRowCellValue(e.RowHandle, "id");
                var code = gv.GetRowCellValue(e.RowHandle, "code");
                lokupItemCode.EditValue = id;
                //lokupItemCode = code.ToString();
            }

        }
    }
}
