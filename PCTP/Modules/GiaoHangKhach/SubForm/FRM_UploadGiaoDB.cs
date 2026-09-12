using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PCTP.Applications.Services;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.SubForm
{
    public class FRM_UploadGiaoDB : XtraForm
    {
        private readonly PhieuService _phieuSvc;
        private readonly DataTable _danhSachMaHang;

        private readonly HashSet<string> _maHangHopLe;
        private ExcelPackage _excelPkg;
        private DataTable _previewDt;

        private const int MODE_EXCEL = 0;
        private const int MODE_NHAPTAY = 1;

        // ── Thứ tự cột trong file Excel — dùng chung cho đọc file lẫn tạo file mẫu ──
        private static readonly string[] ExcelHeaders =
            { "Mã hàng", "Tên hàng", "Số lượng", "Giờ giao", "Cửa", "Truyền" };

        // Controls chung
        private LabelControl lblFile, lblSheet, lblHint, lblSoDong;
        private TextEdit txtDuongDan;
        private ComboBoxEdit cboSheet;
        private SimpleButton btnChonFile, btnTaiFileMau, btnXemTruoc, btnUpload, btnDong;
        private GridControl gridPreview;
        private GridView gridViewPreview;
        private DevExpress.XtraEditors.RadioGroup rdoCheDo;

        // Controls header phiếu — dùng chung cho CẢ 2 chế độ
        private GroupControl grpHeaderNhap;
        private LabelControl lblName, lblNgay, lblNhaMay, lblGhiChu;
        private TextEdit txtName;
        private DateEdit dateNgayLap;
        private ComboBoxEdit cboNhaMay;
        private MemoEdit txtGhiChu;

        // Controls chế độ nhập tay
        private SimpleButton btnThemDong, btnXoaDong;

        public FRM_UploadGiaoDB(PhieuService phieuSvc)
        {
            _phieuSvc = phieuSvc ?? throw new ArgumentNullException(nameof(phieuSvc));
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _danhSachMaHang = _phieuSvc.GetDanhSachMaHangGiaoDB() ?? new DataTable();
            _maHangHopLe = new HashSet<string>(
                _danhSachMaHang.AsEnumerable().Select(r => r["Code"]?.ToString().Trim() ?? ""),
                StringComparer.OrdinalIgnoreCase);
            BuildUI();
        }

        private void SetupMaHangLookup()
        {
            if (gridViewPreview.Columns["MaHang"] == null) return;

            var riLookup = new RepositoryItemLookUpEdit
            {
                DataSource = _danhSachMaHang,
                ValueMember = "Code",
                DisplayMember = "Code",
                BestFitMode = BestFitMode.BestFitResizePopup,
                SearchMode = SearchMode.AutoSuggest,
                NullText = ""
            };
            riLookup.Columns.Add(new LookUpColumnInfo("Code", "Mã Hàng"));
            riLookup.Columns.Add(new LookUpColumnInfo("Name", "Tên Hàng"));

            gridPreview.RepositoryItems.Add(riLookup);
            gridViewPreview.Columns["MaHang"].ColumnEdit = riLookup;
        }

        private void BuildUI()
        {
            this.Text = "Upload Đơn Hàng GIAO DB";
            this.Size = new System.Drawing.Size(1000, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int y = 15;

            rdoCheDo = new DevExpress.XtraEditors.RadioGroup
            {
                Location = new System.Drawing.Point(15, y),
                Size = new System.Drawing.Size(400, 30)
            };
            rdoCheDo.Properties.Items.AddRange(new[]
            {
                new DevExpress.XtraEditors.Controls.RadioGroupItem(MODE_EXCEL, "📂 Upload File Excel"),
                new DevExpress.XtraEditors.Controls.RadioGroupItem(MODE_NHAPTAY, "✏ Nhập Trực Tiếp")
            });
            rdoCheDo.SelectedIndex = MODE_EXCEL;                              // ✅ gán TRƯỚC — chưa wire nên không tự fire
            rdoCheDo.SelectedIndexChanged += RdoCheDo_SelectedIndexChanged;   // ✅ wire SAU
            y += 40;

            lblFile = Lbl("File Excel:", 15, y + 3, 80);
            txtDuongDan = new TextEdit { Location = new System.Drawing.Point(100, y), Size = new System.Drawing.Size(550, 28) };
            txtDuongDan.Properties.ReadOnly = true;
            btnChonFile = Btn("📂 Chọn File", 660, y, 140, 28);
            btnChonFile.Click += BtnChonFile_Click;
            btnTaiFileMau = Btn("📥 Tải File Mẫu", 810, y, 150, 28);
            btnTaiFileMau.Click += BtnTaiFileMau_Click;
            y += 40;

            lblSheet = Lbl("Sheet:", 15, y + 3, 80);
            cboSheet = new ComboBoxEdit { Location = new System.Drawing.Point(100, y), Size = new System.Drawing.Size(250, 28) };
            y += 40;

            lblHint = new LabelControl
            {
                Text = "📋 Thứ tự cột file Excel: [1] Mã hàng  [2] Tên hàng  [3] Số lượng  " +
                       "[4] Giờ giao  [5] Cửa  [6] Truyền.\n" +
                       "Tên phiếu / Ngày lập / Nhà máy / Ghi chú nhập ở khung bên dưới — áp dụng chung cho toàn bộ file.",
                Location = new System.Drawing.Point(15, y),
                AutoSizeMode = LabelAutoSizeMode.None,
                Size = new System.Drawing.Size(945, 34)
            };
            lblHint.Appearance.ForeColor = System.Drawing.Color.DarkBlue;
            y += 44;

            // ════════════════════════════════════════════════════════════
            // Header phiếu — dùng chung cho CẢ 2 chế độ (Excel & nhập tay)
            // ════════════════════════════════════════════════════════════
            grpHeaderNhap = new GroupControl
            {
                Text = "Thông tin phiếu (nhập 1 lần, áp dụng cho toàn bộ chi tiết bên dưới)",
                Location = new System.Drawing.Point(15, y),
                Size = new System.Drawing.Size(945, 100)
            };

            lblName = Lbl("Tên phiếu:", 15, 30, 70);
            txtName = new TextEdit { Location = new System.Drawing.Point(90, 27), Size = new System.Drawing.Size(230, 26) };

            lblNgay = Lbl("Ngày lập:", 335, 30, 65);
            dateNgayLap = new DateEdit { Location = new System.Drawing.Point(400, 27), Size = new System.Drawing.Size(110, 26) };
            dateNgayLap.EditValue = DateTime.Now;

            lblNhaMay = Lbl("Nhà máy:", 525, 30, 65);
            cboNhaMay = new ComboBoxEdit { Location = new System.Drawing.Point(595, 27), Size = new System.Drawing.Size(330, 26) };
            cboNhaMay.Properties.Items.AddRange(new[]
            {
                "HON DA - VIET NAM(NHA MAY VP)",
                "HON DA - VIET NAM(NHA MAY HA NAM)"
            });
            cboNhaMay.SelectedIndex = 0;

            lblGhiChu = Lbl("Ghi chú:", 15, 65, 70);
            txtGhiChu = new MemoEdit { Location = new System.Drawing.Point(90, 62), Size = new System.Drawing.Size(835, 28) };

            grpHeaderNhap.Controls.AddRange(new Control[]
            {
                lblName, txtName, lblNgay, dateNgayLap, lblNhaMay, cboNhaMay,
                lblGhiChu, txtGhiChu
            });
            y += 110;

            btnXemTruoc = Btn("👁 Xem Trước", 15, y, 130, 32);
            btnXemTruoc.Click += BtnXemTruoc_Click;

            btnUpload = Btn("💾 Lưu Phiếu", 155, y, 130, 32);
            btnUpload.Appearance.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
            btnUpload.Appearance.ForeColor = System.Drawing.Color.White;
            btnUpload.Click += BtnUpload_Click;

            btnThemDong = Btn("➕ Thêm Dòng", 295, y, 120, 32);
            btnThemDong.Click += BtnThemDong_Click;
            btnThemDong.Visible = false;

            btnXoaDong = Btn("➖ Xóa Dòng", 425, y, 110, 32);
            btnXoaDong.Click += BtnXoaDong_Click;
            btnXoaDong.Visible = false;

            btnDong = Btn("✕ Đóng", 830, y, 100, 32);
            btnDong.Click += (s, e) => this.Close();

            lblSoDong = new LabelControl { Text = "", Location = new System.Drawing.Point(550, y + 8) };
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
            gridViewPreview.CellValueChanged += GridViewPreview_CellValueChanged;

            this.Controls.AddRange(new Control[]
            {
                rdoCheDo, lblFile, txtDuongDan, btnChonFile, btnTaiFileMau,
                lblSheet, cboSheet, lblHint, grpHeaderNhap,
                btnXemTruoc, btnUpload, btnThemDong, btnXoaDong,
                btnDong, lblSoDong, gridPreview
            });

            // ✅ Đồng bộ trạng thái ban đầu — event wire trước SelectedIndex nên
            // dòng dưới đây thực ra không bắt buộc nữa, giữ lại cho rõ ý & an toàn
            // nếu sau này có ai đổi lại thứ tự wiring.
            RdoCheDo_SelectedIndexChanged(this, EventArgs.Empty);
        }

        // ══════════════════ Chuyển chế độ ══════════════════
        private void RdoCheDo_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool laExcel = rdoCheDo.SelectedIndex == MODE_EXCEL;

            lblFile.Visible = laExcel; txtDuongDan.Visible = laExcel;
            btnChonFile.Visible = laExcel; btnTaiFileMau.Visible = laExcel;
            lblSheet.Visible = laExcel; cboSheet.Visible = laExcel; lblHint.Visible = laExcel;
            btnXemTruoc.Visible = laExcel;

            btnThemDong.Visible = !laExcel;
            btnXoaDong.Visible = !laExcel;
            gridViewPreview.OptionsBehavior.Editable = !laExcel;

            if (!laExcel)
            {
                _previewDt = TaoBang();
                gridPreview.DataSource = _previewDt;
                SetupMaHangLookup();
                lblSoDong.Text = "";
            }
            else
            {
                _previewDt = null;
                gridPreview.DataSource = null;
                lblSoDong.Text = "";
            }
        }

        private void GridViewPreview_CellValueChanged(object sender,
            DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            if (e.Column.FieldName != "MaHang") return;

            DataRow row = gridViewPreview.GetDataRow(e.RowHandle);
            if (row == null) return;

            string ma = row["MaHang"]?.ToString().Trim() ?? "";
            var found = _danhSachMaHang.AsEnumerable().FirstOrDefault(r =>
                string.Equals(r["Code"]?.ToString().Trim(), ma, StringComparison.OrdinalIgnoreCase));

            row["TenHang"] = found?["Name"]?.ToString() ?? "";
            ValidateRowMaHang(row);
        }

        // ══════════════════ Tải file mẫu Excel ══════════════════
        private void BtnTaiFileMau_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Lưu file mẫu GIAO DB";
                dlg.Filter = "Excel|*.xlsx";
                dlg.FileName = "MauUploadGiaoDB.xlsx";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    using (var pkg = new ExcelPackage())
                    {
                        var ws = pkg.Workbook.Worksheets.Add("GiaoDB");

                        for (int c = 0; c < ExcelHeaders.Length; c++)
                        {
                            ws.Cells[1, c + 1].Value = ExcelHeaders[c];
                            ws.Cells[1, c + 1].Style.Font.Bold = true;
                            ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(
                                System.Drawing.Color.FromArgb(0, 120, 212));
                            ws.Cells[1, c + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                        }

                        // 1 dòng ví dụ minh họa
                        var vd = _danhSachMaHang.Rows.Count > 0 ? _danhSachMaHang.Rows[0] : null;
                        ws.Cells[2, 1].Value = vd?["Code"]?.ToString() ?? "VD00000001";
                        ws.Cells[2, 2].Value = vd?["Name"]?.ToString() ?? "Tên hàng ví dụ";
                        ws.Cells[2, 3].Value = 100;
                        ws.Cells[2, 4].Value = "08";
                        ws.Cells[2, 5].Value = "1";
                        ws.Cells[2, 6].Value = "A";

                        // ✅ Bỏ AutoFit() — EPPlus 7.x cần font measurer riêng
                        // (SixLabors/GenericFontMetricsTextMeasurer) chưa được cấu hình
                        // đủ trên môi trường .NET Framework hiện tại, ném lỗi
                        // "does not have an implementation". Set độ rộng cố định thay thế.
                        double[] widths = { 16, 30, 12, 12, 10, 10 };
                        for (int c = 1; c <= ExcelHeaders.Length; c++)
                            ws.Column(c).Width = widths[c - 1];

                        pkg.SaveAs(new FileInfo(dlg.FileName));
                    }

                    if (XtraMessageBox.Show(
                            "✅ Đã tạo file mẫu. Mở file ngay?",
                            "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Information)
                        == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(dlg.FileName);
                    }
                }
                catch (Exception ex) { ShowErr($"Lỗi tạo file mẫu: {ex.Message}"); }
            }
        }

        // ══════════════════ Chọn file / đọc Excel ══════════════════
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

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    _excelPkg = new ExcelPackage(fs);

                foreach (var ws in _excelPkg.Workbook.Worksheets)
                    cboSheet.Properties.Items.Add(ws.Name);

                if (cboSheet.Properties.Items.Count > 0)
                    cboSheet.SelectedIndex = 0;
            }
            catch (Exception ex) { ShowErr($"Lỗi đọc file: {ex.Message}"); }
        }

        private void BtnXemTruoc_Click(object sender, EventArgs e)
        {
            try
            {
                _previewDt = DocDuLieu();
                if (_previewDt == null) return;
                gridPreview.DataSource = _previewDt;

                int soLoi = _previewDt.AsEnumerable()
                    .Count(r => !string.IsNullOrEmpty(r.GetColumnError("MaHang")));

                lblSoDong.Text = soLoi > 0
                    ? $"⚠ {_previewDt.Rows.Count} dòng — {soLoi} dòng SAI mã hàng"
                    : $"✅ {_previewDt.Rows.Count} dòng — tất cả mã hàng hợp lệ";
                lblSoDong.Appearance.ForeColor = soLoi > 0
                    ? System.Drawing.Color.Red : System.Drawing.Color.DarkGreen;
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

            for (int r = 2; r <= rEnd; r++)   // bỏ dòng header
            {
                string G(int col) => ws.Cells[r, col].Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(G(1))) continue;   // dòng trống

                var row = dt.NewRow();
                row["MaHang"] = Trunc(G(1), 50);    // cột 1
                row["TenHang"] = Trunc(G(2), 200);   // cột 2
                row["SoLuong"] = ToInt(G(3));         // cột 3
                row["GioGiao"] = Trunc(G(4), 10);    // cột 4
                row["CUA"] = Trunc(G(5), 20);    // cột 5
                row["TRUYEN"] = Trunc(G(6), 20);    // cột 6
                dt.Rows.Add(row);
                ValidateRowMaHang(row);
            }
            return dt;
        }

        // ══════════════════ Chế độ nhập tay ══════════════════
        private void BtnThemDong_Click(object sender, EventArgs e)
        {
            if (_previewDt == null) return;

            var row = _previewDt.NewRow();
            row["MaHang"] = ""; row["TenHang"] = ""; row["SoLuong"] = 0;
            row["GioGiao"] = ""; row["CUA"] = ""; row["TRUYEN"] = "";
            _previewDt.Rows.Add(row);
            ValidateRowMaHang(row);
            lblSoDong.Text = $"✅ {_previewDt.Rows.Count} dòng";
        }

        private void BtnXoaDong_Click(object sender, EventArgs e)
        {
            if (gridViewPreview.FocusedRowHandle < 0) return;
            gridViewPreview.DeleteRow(gridViewPreview.FocusedRowHandle);
            lblSoDong.Text = $"✅ {_previewDt.Rows.Count} dòng";
        }

        // ══════════════════ Lưu — qua Repository, không còn SQL trong form ══════════════════
        private void BtnUpload_Click(object sender, EventArgs e)
        {
            bool laNhapTay = rdoCheDo.SelectedIndex == MODE_NHAPTAY;

            if (_previewDt == null || _previewDt.Rows.Count == 0)
            { ShowWarn("Chưa có dữ liệu. Bấm Xem Trước (hoặc Thêm Dòng) trước!"); return; }

            int soLoi = _previewDt.AsEnumerable()
                .Count(r => !string.IsNullOrEmpty(r.GetColumnError("MaHang")));
            if (soLoi > 0)
            {
                ShowWarn($"Có {soLoi} dòng mã hàng không hợp lệ (xem icon ⚠ đỏ ở đầu dòng).\n" +
                         "Vui lòng sửa hoặc xóa các dòng này trước khi Lưu.");
                return;
            }

            if (laNhapTay)
            {
                bool thieuMaHang = _previewDt.AsEnumerable()
                    .Any(r => string.IsNullOrWhiteSpace(r["MaHang"]?.ToString()));
                if (thieuMaHang) { ShowWarn("Có dòng chưa nhập Mã Hàng!"); return; }
            }

            if (string.IsNullOrWhiteSpace(txtName.Text))
            { ShowWarn("Vui lòng nhập Tên phiếu!"); return; }

            if (cboNhaMay.SelectedIndex < 0)
            { ShowWarn("Vui lòng chọn Nhà máy!"); return; }

            if (XtraMessageBox.Show(
                    $"Tạo phiếu GIAO DB với {_previewDt.Rows.Count} dòng?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            try
            {
                Cursor = Cursors.WaitCursor;

                string nhaMayName = cboNhaMay.Text;
                int nhaMay = nhaMayName.Contains("HA NAM") ? 2 : 1;
                string ghiChu = string.IsNullOrWhiteSpace(txtGhiChu.Text) ? null : txtGhiChu.Text.Trim();

                int idp = _phieuSvc.TaoPhieuVaChiTietGiaoDB(
                    txtName.Text.Trim(),
                    dateNgayLap.DateTime,
                    nhaMay,
                    nhaMayName,
                    note: ghiChu,
                    chiTiet: _previewDt);

                XtraMessageBox.Show(
                    $"✅ Tạo phiếu GIAO DB #{idp} thành công với {_previewDt.Rows.Count} dòng!",
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { ShowErr($"Lỗi tạo phiếu: {ex.Message}"); }
            finally { Cursor = Cursors.Default; }
        }

        // ══════════════════ Helpers ══════════════════
        private static DataTable TaoBang()
        {
            var dt = new DataTable();
            dt.Columns.Add("MaHang", typeof(string));
            dt.Columns.Add("TenHang", typeof(string));
            dt.Columns.Add("SoLuong", typeof(int));
            dt.Columns.Add("GioGiao", typeof(string));
            dt.Columns.Add("CUA", typeof(string));
            dt.Columns.Add("TRUYEN", typeof(string));
            return dt;
        }

        private static int ToInt(string s) =>
            int.TryParse(s?.Replace(",", "").Trim(), out int v) ? v : 0;

        private static string Trunc(string s, int maxLen) =>
            string.IsNullOrEmpty(s) ? s : s.Length <= maxLen ? s : s.Substring(0, maxLen);

        private static LabelControl Lbl(string text, int x, int y, int w) =>
            new LabelControl { Text = text, Location = new System.Drawing.Point(x, y), Size = new System.Drawing.Size(w, 20) };

        private static SimpleButton Btn(string text, int x, int y, int w, int h) =>
            new SimpleButton { Text = text, Location = new System.Drawing.Point(x, y), Size = new System.Drawing.Size(w, h) };

        private void ShowErr(string msg) => XtraMessageBox.Show(msg, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        private void ShowWarn(string msg) => XtraMessageBox.Show(msg, "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _excelPkg?.Dispose();
            base.OnFormClosed(e);
        }

        private void ValidateRowMaHang(DataRow row)
        {
            string ma = row["MaHang"]?.ToString().Trim() ?? "";
            bool hopLe = !string.IsNullOrEmpty(ma) && _maHangHopLe.Contains(ma);
            row.SetColumnError("MaHang",
                hopLe ? "" : "Mã hàng không tồn tại trong B20 (hoặc đang để trống)");
        }
    }
}