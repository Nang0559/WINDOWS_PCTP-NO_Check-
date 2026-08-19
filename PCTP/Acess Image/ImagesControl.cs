using DevExpress.Xpo.DB.Helpers;
using DevExpress.XtraEditors;
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
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.ImagesControl
{
    /// <summary>
    /// Quản lý ảnh sản phẩm (B20ImageStore) — chọn mã hàng, xem/thêm/sửa/xoá ảnh.
    /// Toàn bộ thao tác DB dùng tham số hoá, có try/catch, có xác nhận trước khi xoá.
    /// </summary>
    public partial class ImagesControl : DevExpress.XtraEditors.XtraUserControl
    {
        private enum Mode { None, View, Add, Edit }

        private readonly SQLPROVIDER _sql = new SQLPROVIDER();
        private Mode _mode = Mode.None;

        // Giới hạn an toàn khi upload — tránh nhét file khổng lồ vào DB
        private const int MAX_IMAGE_SIZE_MB = 5;

        public ImagesControl()
        {
            InitializeComponent();
        }

        // ════════════════════════════════════════════════════════════════════
        // LOAD DANH SÁCH MÃ HÀNG
        // ════════════════════════════════════════════════════════════════════
        private void UserControlnew_Load(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb,
                    "SELECT id, code, name, model FROM B20item ORDER BY code");

                gridControl1.DataSource = dt;
                lokupItemCode.Properties.DataSource = dt;
                lokupItemCode.Properties.DisplayMember = "code";
                lokupItemCode.Properties.ValueMember = "id";

                SetMode(Mode.None);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi tải danh sách mã hàng:\n{ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void gridView1_RowClick(object sender, RowClickEventArgs e)
        {
            var gv = sender as GridView;
            if (gv == null || e.RowHandle < 0) return;

            object id = gv.GetRowCellValue(e.RowHandle, "id");
            if (id == null || id == DBNull.Value) return;

            lokupItemCode.EditValue = id;
            LoadData();
        }

        private void lokupItemCode_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                LoadData();
        }

        // ════════════════════════════════════════════════════════════════════
        // LOAD ẢNH THEO MÃ HÀNG ĐÃ CHỌN
        // ════════════════════════════════════════════════════════════════════
        public void LoadData()
        {
            if (lokupItemCode.EditValue == null)
            {
                XtraMessageBox.Show("Vui lòng chọn mã hàng trước.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(lokupItemCode.EditValue.ToString(), out int parentId))
                return;

            ShowLoading(true);
            try
            {
                imageSlider1.Images.Clear();
                textEdit1.Text = string.Empty;

                DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb,
                    "SELECT id, Image FROM B20ImageStore WHERE ParentId = @ParentId ORDER BY id",
                    new SqlParameter("@ParentId", SqlDbType.Int) { Value = parentId });

                bool coAnh = false;

                foreach (DataRow row in dt.Rows)
                {
                    coAnh = true;
                    string id = row["id"].ToString();
                    byte[] raw = row["Image"] == DBNull.Value ? Array.Empty<byte>() : (byte[])row["Image"];

                    Image img;
                    if (raw.Length > 0)
                    {
                        using (var stream = new MemoryStream(raw))
                            img = Image.FromStream(stream);
                    }
                    else
                    {
                        img = TextToBitmap("Không có ảnh", imageSlider1.Size);
                    }

                    img.Tag = id;
                    imageSlider1.Images.Add(img);
                }

                if (imageSlider1.Images.Count > 0)
                    textEdit1.Text = imageSlider1.CurrentImage?.Tag?.ToString() ?? "";

                cmdEdit.Enabled = coAnh;
                SetMode(Mode.View);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi tải ảnh:\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void imageSlider1_ImageChanged(object sender,
            DevExpress.XtraEditors.Controls.ImageChangedEventArgs e)
        {
            if (imageSlider1.Images.Count != 0 && imageSlider1.CurrentImage?.Tag != null)
                textEdit1.Text = imageSlider1.CurrentImage.Tag.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        // THÊM ẢNH MỚI
        // ════════════════════════════════════════════════════════════════════
        private void cmdAdd_Click(object sender, EventArgs e)
        {
            if (lokupItemCode.EditValue == null)
            {
                XtraMessageBox.Show("Vui lòng chọn mã hàng trước khi thêm ảnh.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var picked = PickImageFromDisk();
            if (picked == null) return;

            pictureEdit1.Image = picked;
            SetMode(Mode.Add);
        }

        // ════════════════════════════════════════════════════════════════════
        // SỬA ẢNH (thay ảnh khác cho dòng đang chọn)
        // ════════════════════════════════════════════════════════════════════
        private void cmdEdit_Click(object sender, EventArgs e)
        {
            if (imageSlider1.CurrentImage == null)
            {
                XtraMessageBox.Show("Chưa có ảnh nào để sửa.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var picked = PickImageFromDisk();
            if (picked == null) return; // huỷ chọn -> không đổi gì

            pictureEdit1.Image = picked;
            SetMode(Mode.Edit);
        }

        /// <summary>Xoá ảnh hiện đang xem — có xác nhận, tách khỏi cmdSave để tránh nhầm lẫn.</summary>
        private void cmdDelete_Click(object sender, EventArgs e)
        {
            if (imageSlider1.CurrentImage == null || string.IsNullOrEmpty(textEdit1.Text))
            {
                XtraMessageBox.Show("Chưa có ảnh nào để xoá.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(textEdit1.Text, out int imageId)) return;

            if (XtraMessageBox.Show("Xoá ảnh này khỏi hệ thống?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            ShowLoading(true);
            try
            {
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                    "DELETE FROM B20ImageStore WHERE id = @Id",
                    new SqlParameter("@Id", SqlDbType.Int) { Value = imageId });

                LoadData();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi xoá ảnh:\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // LƯU (Thêm mới hoặc Cập nhật)
        // ════════════════════════════════════════════════════════════════════
        private void cmdSave_Click(object sender, EventArgs e)
        {
            if (pictureEdit1.Image == null)
            {
                XtraMessageBox.Show("Chưa có ảnh để lưu.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowLoading(true);
            try
            {
                byte[] data = ImageToByteArray(pictureEdit1.Image);

                switch (_mode)
                {
                    case Mode.Add:
                        if (lokupItemCode.EditValue == null) return;
                        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                            "INSERT INTO B20ImageStore ([ParentId],[Image],[Type],[CreatedAt]) " +
                            "VALUES (@ParentId,@Image,@Type,@CreatedAt)",
                            new SqlParameter("@ParentId", SqlDbType.Int) { Value = int.Parse(lokupItemCode.EditValue.ToString()) },
                            new SqlParameter("@Image", SqlDbType.VarBinary, -1) { Value = data },
                            new SqlParameter("@Type", SqlDbType.NVarChar, 50) { Value = "ITEM" },
                            new SqlParameter("@CreatedAt", SqlDbType.DateTime) { Value = DateTime.Now });
                        break;

                    case Mode.Edit:
                        if (!int.TryParse(textEdit1.Text, out int editId)) return;
                        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                            "UPDATE B20ImageStore SET [Image]=@Image,[Type]=@Type,[CreatedAt]=@CreatedAt " +
                            "WHERE id = @Id",
                            new SqlParameter("@Image", SqlDbType.VarBinary, -1) { Value = data },
                            new SqlParameter("@Type", SqlDbType.NVarChar, 50) { Value = "ITEM" },
                            new SqlParameter("@CreatedAt", SqlDbType.DateTime) { Value = DateTime.Now },
                            new SqlParameter("@Id", SqlDbType.Int) { Value = editId });
                        break;

                    default:
                        return;
                }

                pictureEdit1.Image = null;
                LoadData();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi lưu ảnh:\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            pictureEdit1.Image = null;
            SetMode(Mode.View);
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Mở hộp thoại chọn ảnh từ máy tính — kiểm tra định dạng + kích thước.</summary>
        private Image PickImageFromDisk()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh sản phẩm",
                Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = false
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return null;

                var fileInfo = new FileInfo(dlg.FileName);
                if (fileInfo.Length > MAX_IMAGE_SIZE_MB * 1024 * 1024)
                {
                    XtraMessageBox.Show($"Ảnh vượt quá {MAX_IMAGE_SIZE_MB}MB, vui lòng chọn ảnh nhỏ hơn.",
                        "Ảnh quá lớn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                try
                {
                    using (var fs = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read))
                    using (var temp = Image.FromStream(fs))
                        return new Bitmap(temp); // clone để giải phóng file ngay
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show($"Không đọc được file ảnh:\n{ex.Message}", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
            }
        }

        private byte[] ImageToByteArray(Image img)
        {
            using (var ms = new MemoryStream())
            {
                // JPEG nén tốt hơn BMP nhiều — giảm dung lượng lưu DB đáng kể
                img.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        private Bitmap TextToBitmap(string text, Size size)
        {
            if (size.Width <= 0) size.Width = 200;
            if (size.Height <= 0) size.Height = 150;

            var bmp = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.WhiteSmoke);
                using (var font = new Font("Tahoma", 12))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(text, font, Brushes.Gray, new RectangleF(0, 0, bmp.Width, bmp.Height), format);
            }
            return bmp;
        }

        /// <summary>Chuyển UI sang đúng trạng thái xem/thêm/sửa — tập trung logic ẩn/hiện 1 chỗ.</summary>
        private void SetMode(Mode mode)
        {
            _mode = mode;

            bool isEditing = mode == Mode.Add || mode == Mode.Edit;

            pictureEdit1.Visible = isEditing;
            imageSlider1.Visible = !isEditing;

            cmdSave.Enabled = isEditing;
            cmdCancel.Enabled = isEditing;
            cmdAdd.Enabled = !isEditing && lokupItemCode.EditValue != null;
            cmdEdit.Enabled = !isEditing && imageSlider1.Images.Count > 0;
            cmdDelete.Enabled = !isEditing && imageSlider1.Images.Count > 0;
        }

        private void ShowLoading(bool show)
        {
            if (show)
            {
                if (!splashScreenManager1.IsSplashFormVisible)
                    splashScreenManager1.ShowWaitForm();
            }
            else
            {
                splashScreenManager1.CloseWaitForm();
            }
        }
        // ════════════════════════════════════════════════════════════════════
        // PHÍM TẮT khi focus vào ImageSlider — hỗ trợ Delete để xoá nhanh
        // ════════════════════════════════════════════════════════════════════
        private void imageSlider1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && cmdDelete.Enabled)
            {
                cmdDelete_Click(sender, EventArgs.Empty);
            }
        }
    }
}
