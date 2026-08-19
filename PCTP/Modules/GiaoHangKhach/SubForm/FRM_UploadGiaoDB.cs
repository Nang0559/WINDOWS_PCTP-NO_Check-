using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using OfficeOpenXml;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.SubForm
{
    public class FRM_UploadGiaoDB : DevExpress.XtraEditors.XtraForm
    {
        private readonly SQLPROVIDER _sql;
        private ExcelPackage _excelPkg;
        private DataTable _previewDt;

        // ── Controls (build bằng code như FRM_UploadMikrun) ─────────────
        private LabelControl lblFile, lblSheet, lblHint, lblSoDong;
        private TextEdit txtDuongDan;
        private ComboBoxEdit cboSheet;
        private CheckEdit chkXoaCu;
        private SimpleButton btnChonFile, btnXemTruoc, btnUpload, btnDong;
        private GridControl gridPreview;
        private GridView gridViewPreview;

        public FRM_UploadGiaoDB(SQLPROVIDER sql)
        {
            _sql = sql;
            OfficeOpenXml.ExcelPackage.LicenseContext =
                OfficeOpenXml.LicenseContext.NonCommercial;
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Upload Đơn Hàng GIAO DB";
            this.Size = new System.Drawing.Size(1000, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int y = 15;

            lblFile = Lbl("File Excel:", 15, y + 3, 80);
            txtDuongDan = new TextEdit
            {
                Location = new System.Drawing.Point(100, y),
                Size = new System.Drawing.Size(680, 28)
            };
            txtDuongDan.Properties.ReadOnly = true;

            btnChonFile = Btn("📂 Chọn File", 790, y, 170, 28);
            btnChonFile.Click += BtnChonFile_Click;
            y += 40;

            lblSheet = Lbl("Sheet:", 15, y + 3, 80);
            cboSheet = new ComboBoxEdit
            {
                Location = new System.Drawing.Point(100, y),
                Size = new System.Drawing.Size(250, 28)
            };
            y += 40;

            // ✅ Hint thứ tự cột
            lblHint = new LabelControl
            {
                Text = "📋 Thứ tự cột: [1] IDP  [2] Tên phiếu  [3] Ngày lập  " +
                        "[4] Mã hàng  [5] Tên hàng  [6] Số lượng  " +
                        "[7] Giờ giao  [8] Nhà máy  [9] Cửa  [10] Truyền",
                Location = new System.Drawing.Point(15, y)
            };
            lblHint.Appearance.ForeColor = System.Drawing.Color.DarkBlue;
            y += 30;

            chkXoaCu = new CheckEdit
            {
                Text = "Xóa dữ liệu cũ trước khi upload",
                Location = new System.Drawing.Point(15, y + 5),
                Size = new System.Drawing.Size(280, 25)
            };
            chkXoaCu.Checked = true;

            btnXemTruoc = Btn("👁 Xem Trước", 300, y, 130, 32);
            btnXemTruoc.Click += BtnXemTruoc_Click;

            btnUpload = Btn("⬆ Upload DB", 440, y, 130, 32);
            btnUpload.Appearance.BackColor =
                System.Drawing.Color.FromArgb(0, 120, 212);
            btnUpload.Appearance.ForeColor = System.Drawing.Color.White;
            btnUpload.Click += BtnUpload_Click;

            btnDong = Btn("✕ Đóng", 580, y, 100, 32);
            btnDong.Click += (s, e) => this.Close();

            lblSoDong = new LabelControl
            {
                Text = "",
                Location = new System.Drawing.Point(700, y + 8)
            };
            lblSoDong.Appearance.ForeColor = System.Drawing.Color.DarkGreen;
            y += 45;

            gridViewPreview = new GridView();
            gridViewPreview.OptionsBehavior.Editable = false;
            gridViewPreview.OptionsView.ShowGroupPanel = false;

            gridPreview = new GridControl
            {
                Location = new System.Drawing.Point(15, y),
                Size = new System.Drawing.Size(965, 300),
                MainView = gridViewPreview
            };
            gridViewPreview.GridControl = gridPreview;

            this.Controls.AddRange(new System.Windows.Forms.Control[]
            {
            lblFile, txtDuongDan, btnChonFile,
            lblSheet, cboSheet,
            lblHint, chkXoaCu,
            btnXemTruoc, btnUpload, btnDong, lblSoDong,
            gridPreview
            });
        }

        // ════════════════════════════════════════════════════════════
        // Chọn file
        // ════════════════════════════════════════════════════════════
        private void BtnChonFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn file Excel Đơn hàng GIAO DB";
                dlg.Filter = "Excel|*.xlsx;*.xls|All|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                txtDuongDan.Text = dlg.FileName;
                LoadSheets(dlg.FileName);
            }
        }

        private void LoadSheets(string path)
        {
            try
            {
                _excelPkg?.Dispose();
                cboSheet.Properties.Items.Clear();
                cboSheet.Enabled = true;

                using (var fs = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite))
                    _excelPkg = new ExcelPackage(fs);

                foreach (var ws in _excelPkg.Workbook.Worksheets)
                    cboSheet.Properties.Items.Add(ws.Name);

                if (cboSheet.Properties.Items.Count > 0)
                    cboSheet.SelectedIndex = 0;
            }
            catch (Exception ex) { ShowErr($"Lỗi đọc file: {ex.Message}"); }
        }

        // ════════════════════════════════════════════════════════════
        // Xem trước
        // ════════════════════════════════════════════════════════════
        private void BtnXemTruoc_Click(object sender, EventArgs e)
        {
            try
            {
                _previewDt = DocDuLieu();
                if (_previewDt == null) return;
                gridPreview.DataSource = _previewDt;
                lblSoDong.Text = $"✅ Tìm thấy {_previewDt.Rows.Count} dòng";
            }
            catch (Exception ex) { ShowErr($"Lỗi xem trước: {ex.Message}"); }
        }

        private DataTable DocDuLieu()
        {
            if (_excelPkg == null) { ShowWarn("Chưa chọn file!"); return null; }

            var ws = _excelPkg.Workbook.Worksheets[cboSheet.Text];
            if (ws == null) { ShowWarn("Không tìm thấy sheet!"); return null; }

            var dt = TaoBang();
            int rEnd = ws.Dimension?.End.Row ?? 1;

            for (int r = 2; r <= rEnd; r++)  // ← bỏ dòng header
            {
                string G(int col) => ws.Cells[r, col].Text?.Trim() ?? "";

                // Bỏ dòng trống
                if (string.IsNullOrWhiteSpace(G(1)) &&
                    string.IsNullOrWhiteSpace(G(4))) continue;

                var row = dt.NewRow();
                row["IDP"] = Trunc(G(1), 20);   // cột 1
                row["Name"] = Trunc(G(2), 100);  // cột 2
                row["NgayLap"] = ParseDate(G(3));    // cột 3
                row["MaHang"] = Trunc(G(4), 50);   // cột 4
                row["TenHang"] = Trunc(G(5), 200);  // cột 5
                row["SoLuong"] = ToInt(G(6));        // cột 6
                row["GioGiao"] = Trunc(G(7), 10);   // cột 7
                row["NhaMay"] = Trunc(G(8), 100);  // cột 8
                row["CUA"] = Trunc(G(9), 20);   // cột 9
                row["TRUYEN"] = Trunc(G(10), 20);  // cột 10
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════
        // Upload
        // ════════════════════════════════════════════════════════════
        private void BtnUpload_Click(object sender, EventArgs e)
        {
            if (_previewDt == null || _previewDt.Rows.Count == 0)
            { ShowWarn("Chưa có dữ liệu. Bấm Xem Trước trước!"); return; }

            if (XtraMessageBox.Show(
                    $"Upload {_previewDt.Rows.Count} dòng vào TMPPHIEUGIAOHANGDBCT?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            try
            {
                Cursor = Cursors.WaitCursor;

                // ✅ Xóa cũ nếu chọn
                if (chkXoaCu.Checked)
                    _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                        "DELETE FROM TMPPHIEUGIAOHANGDBCT");

                // ✅ Insert từng dòng với SqlParameter
                int n = 0;
                foreach (DataRow row in _previewDt.Rows)
                {
                    _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                        "INSERT INTO TMPPHIEUGIAOHANGDBCT " +
                        "(IDP, MaHang, TenHang, SoLuong, GioGiao, NhaMay, CUA, TRUYEN, Status, TTNHAN) " +
                        "VALUES (@idp,@ma,@ten,@sl,@gio,@nm,@cua,@tr,'NG',1)",
                        new SqlParameter("@idp", row["IDP"]),
                        new SqlParameter("@ma", row["MaHang"]),
                        new SqlParameter("@ten", row["TenHang"]),
                        new SqlParameter("@sl", row["SoLuong"]),
                        new SqlParameter("@gio", row["GioGiao"]),
                        new SqlParameter("@nm", row["NhaMay"]),
                        new SqlParameter("@cua", row["CUA"]),
                        new SqlParameter("@tr", row["TRUYEN"]));
                    n++;
                }

                // ✅ Insert header vào TMPPHIEUNHANDB
                var idpGroups = _previewDt.AsEnumerable()
                    .GroupBy(r => r["IDP"].ToString())
                    .ToList();

                foreach (var grp in idpGroups)
                {
                    string idp = grp.Key;
                    string name = grp.First()["Name"].ToString();
                    object ngayLap = grp.First()["NgayLap"];
                    string nhaMay = grp.First()["NhaMay"].ToString();

                    // addNM từ tên nhà máy
                    int addNM = nhaMay.Contains("HA NAM") ? 2 : 1;

                    _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                        "IF NOT EXISTS (SELECT 1 FROM TMPPHIEUNHANDB WHERE IDP=@idp) " +
                        "INSERT INTO TMPPHIEUNHANDB (IDP, Name, NgayLap, NHAMAY) " +
                        "VALUES (@idp, @name, @ngay, @nm)",
                        new SqlParameter("@idp", idp),
                        new SqlParameter("@name", name),
                        new SqlParameter("@ngay", ngayLap == DBNull.Value
                            ? (object)DBNull.Value : ngayLap),
                        new SqlParameter("@nm", addNM));
                }

                XtraMessageBox.Show(
                    $"✅ Upload thành công {n} dòng!",
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { ShowErr($"Lỗi Upload: {ex.Message}"); }
            finally { Cursor = Cursors.Default; }
        }

        // ════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════
        private static DataTable TaoBang()
        {
            var dt = new DataTable();
            dt.Columns.Add("IDP", typeof(string));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("NgayLap", typeof(DateTime));
            dt.Columns.Add("MaHang", typeof(string));
            dt.Columns.Add("TenHang", typeof(string));
            dt.Columns.Add("SoLuong", typeof(int));
            dt.Columns.Add("GioGiao", typeof(string));
            dt.Columns.Add("NhaMay", typeof(string));
            dt.Columns.Add("CUA", typeof(string));
            dt.Columns.Add("TRUYEN", typeof(string));
            return dt;
        }

        private static object ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
            string[] fmts = { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyyMMdd" };
            return DateTime.TryParseExact(s, fmts,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime d)
                ? (object)d : DBNull.Value;
        }

        private static int ToInt(string s) =>
            int.TryParse(s?.Replace(",", "").Trim(), out int v) ? v : 0;

        private static string Trunc(string s, int maxLen) =>
            string.IsNullOrEmpty(s) ? s
            : s.Length <= maxLen ? s
            : s.Substring(0, maxLen);

        private static LabelControl Lbl(string text, int x, int y, int w) =>
            new LabelControl
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, 20)
            };

        private static TextEdit TxtNum(string val, int x, int y, int w) =>
            new TextEdit
            {
                Text = val,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, 28)
            };

        private static SimpleButton Btn(string text, int x, int y, int w, int h) =>
            new SimpleButton
            {
                Text = text,
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(w, h)
            };

        private void ShowErr(string msg) =>
            XtraMessageBox.Show(msg, "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void ShowWarn(string msg) =>
            XtraMessageBox.Show(msg, "Cảnh báo",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _excelPkg?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
