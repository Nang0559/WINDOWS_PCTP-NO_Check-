using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using OfficeOpenXml;
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

        // Controls chung
        private LabelControl lblFile, lblSheet, lblHint, lblSoDong;
        private TextEdit txtDuongDan;
        private ComboBoxEdit cboSheet;
        private CheckEdit chkXoaCu;
        private SimpleButton btnChonFile, btnXemTruoc, btnUpload, btnDong;
        private GridControl gridPreview;
        private GridView gridViewPreview;
        private DevExpress.XtraEditors.RadioGroup rdoCheDo;

        // Controls chế độ nhập tay
        private GroupControl grpHeaderNhap;
        private LabelControl lblIDP, lblName, lblNgay, lblNhaMay;
        private TextEdit txtIDP, txtName;
        private DateEdit dateNgayLap;
        private ComboBoxEdit cboNhaMay;
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
            rdoCheDo.SelectedIndex = MODE_EXCEL;
            rdoCheDo.SelectedIndexChanged += RdoCheDo_SelectedIndexChanged;
            y += 40;

            lblFile = Lbl("File Excel:", 15, y + 3, 80);
            txtDuongDan = new TextEdit { Location = new System.Drawing.Point(100, y), Size = new System.Drawing.Size(680, 28) };
            txtDuongDan.Properties.ReadOnly = true;
            btnChonFile = Btn("📂 Chọn File", 790, y, 170, 28);
            btnChonFile.Click += BtnChonFile_Click;
            y += 40;

            lblSheet = Lbl("Sheet:", 15, y + 3, 80);
            cboSheet = new ComboBoxEdit { Location = new System.Drawing.Point(100, y), Size = new System.Drawing.Size(250, 28) };
            y += 40;

            lblHint = new LabelControl
            {
                Text = "📋 Thứ tự cột: [1] IDP [2] Tên phiếu [3] Ngày lập " +
                       "[4] Mã hàng [5] Tên hàng [6] Số lượng " +
                       "[7] Giờ giao [8] Nhà máy [9] Cửa [10] Truyền",
                Location = new System.Drawing.Point(15, y)
            };
            lblHint.Appearance.ForeColor = System.Drawing.Color.DarkBlue;
            y += 30;

            grpHeaderNhap = new GroupControl
            {
                Text = "Thông tin phiếu (nhập 1 lần, áp dụng cho mọi dòng chi tiết bên dưới)",
                Location = new System.Drawing.Point(15, y),
                Size = new System.Drawing.Size(945, 70),
                Visible = false
            };
            lblIDP = Lbl("Số Phiếu (IDP):", 15, 30, 100);
            txtIDP = new TextEdit { Location = new System.Drawing.Point(120, 27), Size = new System.Drawing.Size(80, 26) };
            txtIDP.Properties.ReadOnly = true;
            lblName = Lbl("Tên phiếu:", 215, 30, 70);
            txtName = new TextEdit { Location = new System.Drawing.Point(290, 27), Size = new System.Drawing.Size(180, 26) };
            lblNgay = Lbl("Ngày lập:", 485, 30, 70);
            dateNgayLap = new DateEdit { Location = new System.Drawing.Point(555, 27), Size = new System.Drawing.Size(110, 26) };
            dateNgayLap.EditValue = DateTime.Now;
            lblNhaMay = Lbl("Nhà máy:", 680, 30, 65);
            cboNhaMay = new ComboBoxEdit { Location = new System.Drawing.Point(750, 27), Size = new System.Drawing.Size(180, 26) };
            cboNhaMay.Properties.Items.AddRange(new[]
            {
                "HON DA - VIET NAM(NHA MAY VP)",
                "HON DA - VIET NAM(NHA MAY HA NAM)"
            });
            cboNhaMay.SelectedIndex = 0;
            grpHeaderNhap.Controls.AddRange(new Control[]
            { lblIDP, txtIDP, lblName, txtName, lblNgay, dateNgayLap, lblNhaMay, cboNhaMay });
            y += 80;

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
            btnUpload.Appearance.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
            btnUpload.Appearance.ForeColor = System.Drawing.Color.White;
            btnUpload.Click += BtnUpload_Click;

            btnThemDong = Btn("➕ Thêm Dòng", 580, y, 120, 32);
            btnThemDong.Click += BtnThemDong_Click;
            btnThemDong.Visible = false;

            btnXoaDong = Btn("➖ Xóa Dòng", 710, y, 110, 32);
            btnXoaDong.Click += BtnXoaDong_Click;
            btnXoaDong.Visible = false;

            btnDong = Btn("✕ Đóng", 830, y, 100, 32);
            btnDong.Click += (s, e) => this.Close();

            lblSoDong = new LabelControl { Text = "", Location = new System.Drawing.Point(300, y + 40) };
            lblSoDong.Appearance.ForeColor = System.Drawing.Color.DarkGreen;
            y += 75;

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
                rdoCheDo, lblFile, txtDuongDan, btnChonFile,
                lblSheet, cboSheet, lblHint, grpHeaderNhap,
                chkXoaCu, btnXemTruoc, btnUpload, btnThemDong, btnXoaDong,
                btnDong, lblSoDong, gridPreview
            });
        }

        // ══════════════════ Chuyển chế độ ══════════════════
        private void RdoCheDo_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool laExcel = rdoCheDo.SelectedIndex == MODE_EXCEL;

            lblFile.Visible = laExcel; txtDuongDan.Visible = laExcel; btnChonFile.Visible = laExcel;
            lblSheet.Visible = laExcel; cboSheet.Visible = laExcel; lblHint.Visible = laExcel;
            btnXemTruoc.Visible = laExcel;

            grpHeaderNhap.Visible = !laExcel;
            btnThemDong.Visible = !laExcel;
            btnXoaDong.Visible = !laExcel;
            gridViewPreview.OptionsBehavior.Editable = !laExcel;

            if (!laExcel)
            {
                txtIDP.Text = _phieuSvc.SinhIDPMoi().ToString();
                _previewDt = TaoBang();
                gridPreview.DataSource = _previewDt;
                ApplyColumnVisibility(laExcel: false);
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
        // Ẩn 4 cột IDP/Name/NgayLap/NhaMay khi nhập tay (đã có ở khối header,
        // hiển thị lặp lại từng dòng gây rối) — vẫn hiện đủ khi xem trước Excel.
        private void ApplyColumnVisibility(bool laExcel)
        {
            foreach (string col in new[] { "IDP", "Name", "NgayLap", "NhaMay" })
                if (gridViewPreview.Columns[col] != null)
                    gridViewPreview.Columns[col].Visible = laExcel;
        }

        // ══════════════════ Chọn file / đọc Excel (giữ nguyên) ══════════════════
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
                ApplyColumnVisibility(laExcel: true);

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

            for (int r = 2; r <= rEnd; r++)
            {
                string G(int col) => ws.Cells[r, col].Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(G(1)) && string.IsNullOrWhiteSpace(G(4))) continue;

                var row = dt.NewRow();
                row["IDP"] = Trunc(G(1), 20);
                row["Name"] = Trunc(G(2), 100);
                row["NgayLap"] = ParseDate(G(3));
                row["MaHang"] = Trunc(G(4), 50);
                row["TenHang"] = Trunc(G(5), 200);
                row["SoLuong"] = ToInt(G(6));
                row["GioGiao"] = Trunc(G(7), 10);
                row["NhaMay"] = Trunc(G(8), 100);
                row["CUA"] = Trunc(G(9), 20);
                row["TRUYEN"] = Trunc(G(10), 20);
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
            row["IDP"] = txtIDP.Text;
            row["Name"] = txtName.Text;
            row["NgayLap"] = dateNgayLap.EditValue ?? DateTime.Now;
            row["NhaMay"] = cboNhaMay.Text;
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

        // Trước khi upload ở chế độ nhập tay: đồng bộ lại header (phòng khi
        // người dùng đổi Name/Ngày/Nhà máy SAU khi đã thêm 1 vài dòng).
        private void DongBoHeaderVaoTatCaDong()
        {
            if (_previewDt == null) return;
            foreach (DataRow row in _previewDt.Rows)
            {
                row["IDP"] = txtIDP.Text;
                row["Name"] = txtName.Text;
                row["NgayLap"] = dateNgayLap.EditValue ?? DateTime.Now;
                row["NhaMay"] = cboNhaMay.Text;
            }
        }

        // ══════════════════ Upload — QUA REPOSITORY, không còn SQL trong form ══════════════════
        private void BtnUpload_Click(object sender, EventArgs e)
        {
            bool laNhapTay = rdoCheDo.SelectedIndex == MODE_NHAPTAY;
            int soLoi = _previewDt.AsEnumerable()
            .Count(r => !string.IsNullOrEmpty(r.GetColumnError("MaHang")));
            if (soLoi > 0)
            {
                ShowWarn($"Có {soLoi} dòng mã hàng không hợp lệ (xem icon ⚠ đỏ ở đầu dòng).\n" +
                         "Vui lòng sửa hoặc xóa các dòng này trước khi Upload.");
                return;
            }
            if (laNhapTay)
                DongBoHeaderVaoTatCaDong();

            if (_previewDt == null || _previewDt.Rows.Count == 0)
            { ShowWarn("Chưa có dữ liệu. Bấm Xem Trước (hoặc Thêm Dòng) trước!"); return; }

            if (laNhapTay)
            {
                var thieuMaHang = _previewDt.AsEnumerable()
                    .Any(r => string.IsNullOrWhiteSpace(r["MaHang"]?.ToString()));
                if (thieuMaHang)
                { ShowWarn("Có dòng chưa nhập Mã Hàng!"); return; }
            }

            if (XtraMessageBox.Show(
                    $"Upload {_previewDt.Rows.Count} dòng vào TMPPHIEUGIAOHANGDBCT?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                _phieuSvc.UploadChiTietGiaoDB(_previewDt, chkXoaCu.Checked);

                XtraMessageBox.Show($"✅ Upload thành công {_previewDt.Rows.Count} dòng!",
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { ShowErr($"Lỗi Upload: {ex.Message}"); }
            finally { Cursor = Cursors.Default; }
        }

        // ══════════════════ Helpers ══════════════════
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