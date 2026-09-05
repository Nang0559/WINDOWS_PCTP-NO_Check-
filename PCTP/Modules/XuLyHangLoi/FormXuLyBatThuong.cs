using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.XuLyHangLoi
{
    /// <summary>
    /// Mốc 3 — QC kiểm tra & định hướng / xác nhận cuối "Phiếu xử lý bất thường".
    ///
    /// Viết lại hoàn toàn theo kiến trúc QTChungStatus (không còn phụ thuộc
    /// IPhieuTraHangRepository/PhieuLoiKhachTra như bản cũ).
    ///
    /// Form này CHỈ đóng vai trò danh sách + xem nhanh thông tin (grid trái /
    /// panel đọc phải). Hành động QC thật sự (chọn hướng xử lý, nhập OK/NG,
    /// chọn Slot...) được giao cho 2 form chuyên biệt đã đúng chuẩn:
    ///     FormQCDinhHuong(IQTChungService, int phieuXuLyId)
    ///     FormQCXacNhanCuoi(IQTChungService, int phieuXuLyId)
    /// để tránh viết lại (và có nguy cơ làm sai) logic nghiệp vụ Slot/LOT
    /// vốn đã được kiểm chứng đúng trong 2 form đó.
    /// </summary>
    public enum XuLyBatThuongMode { DinhHuong, XacNhanCuoi }

    public partial class FormXuLyBatThuong : XtraForm
    {
        private readonly IQTChungService _qtChungService;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly XuLyBatThuongMode _mode;
        private readonly int? _preselectId;

        // ── Grid trái ────────────────────────────────────────────────
        private GridControl _grid;
        private GridView _gridView;
        private List<PhieuXuLyBatThuong> _dsPhieu;

        // ── Panel chi tiết phải — CHỈ đọc, không còn nhập liệu QC ─────
        private LabelControl _lblSoPhieu, _lblModel, _lblMaSP, _lblSoLo, _lblSoLuong;
        private LabelControl _lblPhanLoai, _lblNoiDung, _lblBoPhan, _lblNguoiTao, _lblNgayTao;
        private LabelControl _lblHuongXuLy, _lblTrangThai;

        private SimpleButton _btnXuLy, _btnRefresh;
        private PanelControl _pnlChiTiet;

        // ── Constructor chính ─────────────────────────────────────────
        public FormXuLyBatThuong(
            IQTChungService qtChungService,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            XuLyBatThuongMode mode,
            int? preselectId = null)
        {
            _qtChungService = qtChungService ?? throw new ArgumentNullException(nameof(qtChungService));
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
            _mode = mode;
            _preselectId = preselectId;

            BuildUI();
            LoadData();
        }

        // Constructor tương thích ngược (mặc định về DinhHuong)
        public FormXuLyBatThuong(
            IQTChungService qtChungService,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            int? preselectId = null)
            : this(qtChungService, phieuXuLyRepo, XuLyBatThuongMode.DinhHuong, preselectId)
        {
        }

        // ════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = _mode == XuLyBatThuongMode.DinhHuong
                ? "QC — Định hướng xử lý bất thường"
                : "QC — Xác nhận lần cuối";
            Size = new Size(1100, 650);
            StartPosition = FormStartPosition.CenterParent;

            var split = new SplitContainerControl
            {
                Dock = DockStyle.Fill,
                SplitterPosition = 550,
                Horizontal = false
            };

            // ── LEFT: Grid danh sách chờ xử lý ─────────────────────
            var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(5) };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblTitle = new LabelControl
            {
                Text = _mode == XuLyBatThuongMode.DinhHuong
                    ? "📋 Danh sách chờ QC định hướng (DaTaoPhieuBatThuong)"
                    : "📋 Danh sách chờ QC xác nhận cuối (DaGiaoSanXuat)",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9.5F, FontStyle.Bold), ForeColor = Color.DarkOrange }
            };
            leftLayout.Controls.Add(lblTitle, 0, 0);

            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.FocusedRowChanged += (s, e) => BindChiTietPanel();
            _gridView.DoubleClick += (s, e) => { if (_btnXuLy.Enabled) _btnXuLy.PerformClick(); };

            _gridView.Columns.Add(new GridColumn { FieldName = "SoPhieu", Caption = "Số phiếu", Width = 130, VisibleIndex = 0 });
            _gridView.Columns.Add(new GridColumn { FieldName = "Model", Caption = "Model", Width = 80, VisibleIndex = 1 });
            _gridView.Columns.Add(new GridColumn { FieldName = "MaSanPham", Caption = "Mã sản phẩm", Width = 150, VisibleIndex = 2 });
            _gridView.Columns.Add(new GridColumn { FieldName = "SoLo", Caption = "Số lô", Width = 100, VisibleIndex = 3 });

            var colSl = new GridColumn { FieldName = "SoLuongLoi", Caption = "SL lỗi", Width = 70, VisibleIndex = 4 };
            colSl.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            colSl.DisplayFormat.FormatString = "n0";
            _gridView.Columns.Add(colSl);

            _gridView.Columns.Add(new GridColumn { FieldName = "PhanLoaiXuLy", Caption = "Phân loại", Width = 130, VisibleIndex = 5 });
            _gridView.Columns.Add(new GridColumn { FieldName = "CreatedAt", Caption = "Ngày tạo", Width = 110, VisibleIndex = 6 });
            _gridView.Columns["CreatedAt"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            _gridView.Columns["CreatedAt"].DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";

            leftLayout.Controls.Add(_grid, 0, 1);
            split.Panel1.Controls.Add(leftLayout);
            split.Panel1.MinSize = 120;

            // ── RIGHT: Panel chi tiết (đọc) + nút mở form xử lý ─────
            BuildPanelChiTiet();
            split.Panel2.Controls.Add(_pnlChiTiet);

            Controls.Add(split);
        }

        private void BuildPanelChiTiet()
        {
            _pnlChiTiet = new PanelControl { Dock = DockStyle.Fill };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int r = 0;
            void AddRow(string label, out LabelControl valueCtrl)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                layout.Controls.Add(new LabelControl { Text = label, Dock = DockStyle.Fill }, 0, r);
                valueCtrl = new LabelControl
                {
                    Dock = DockStyle.Fill,
                    AutoSizeMode = LabelAutoSizeMode.None,
                    Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold) }
                };
                layout.Controls.Add(valueCtrl, 1, r);
                r++;
            }

            AddRow("Số phiếu:", out _lblSoPhieu);
            AddRow("Model:", out _lblModel);
            AddRow("Mã sản phẩm:", out _lblMaSP);
            AddRow("Số lô:", out _lblSoLo);
            AddRow("Số lượng lỗi:", out _lblSoLuong);
            AddRow("Phân loại xử lý:", out _lblPhanLoai);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.Controls.Add(new LabelControl { Text = "Nội dung bất thường:", Dock = DockStyle.Fill }, 0, r);
            _lblNoiDung = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = { TextOptions = { WordWrap = DevExpress.Utils.WordWrap.Wrap } }
            };
            layout.Controls.Add(_lblNoiDung, 1, r);
            r++;

            AddRow("Bộ phận phát hành:", out _lblBoPhan);
            AddRow("Người tạo:", out _lblNguoiTao);
            AddRow("Ngày tạo:", out _lblNgayTao);
            AddRow("Hướng xử lý:", out _lblHuongXuLy);
            AddRow("Trạng thái QTChung:", out _lblTrangThai);

            // ── Nút mở form xử lý chuyên biệt ───────────────────────
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            r++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            _btnXuLy = new SimpleButton
            {
                Text = _mode == XuLyBatThuongMode.DinhHuong ? "🚀 Định hướng xử lý..." : "✅ Xác nhận cuối...",
                Width = 180,
                Height = 36
            };
            _btnXuLy.Appearance.BackColor = Color.SeaGreen;
            _btnXuLy.Appearance.ForeColor = Color.White;
            _btnXuLy.Appearance.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _btnXuLy.Click += BtnXuLy_Click;

            _btnRefresh = new SimpleButton { Text = "🔄 Làm mới", Width = 100, Height = 36 };
            _btnRefresh.Click += (s, e) => LoadData();

            btnPanel.Controls.Add(_btnXuLy);
            btnPanel.Controls.Add(_btnRefresh);
            layout.Controls.Add(btnPanel, 0, r);
            layout.SetColumnSpan(btnPanel, 2);

            _pnlChiTiet.Controls.Add(layout);
            SetPanelEnabled(false);
        }

        // ════════════════════════════════════════════════════════════
        // Load / Bind
        // ════════════════════════════════════════════════════════════
        private QTChungStatus TrangThaiChoXuLy =>
            _mode == XuLyBatThuongMode.DinhHuong
                ? QTChungStatus.DaTaoPhieuBatThuong   // mốc 2 → 3a
                : QTChungStatus.DaGiaoSanXuat;         // mốc 3b — chờ QC chốt cuối

        private void LoadData()
        {
            try
            {
                _dsPhieu = _phieuXuLyRepo.GetByStatus(TrangThaiChoXuLy);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi tải danh sách phiếu:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _dsPhieu = new List<PhieuXuLyBatThuong>();
            }

            _grid.DataSource = _dsPhieu;
            _gridView.BestFitColumns();

            if (_dsPhieu.Count == 0)
            {
                SetPanelEnabled(false);
                return;
            }

            if (_preselectId.HasValue)
            {
                var target = _dsPhieu.FirstOrDefault(x => x.Id == _preselectId.Value);
                if (target != null)
                {
                    int handle = _gridView.LocateByValue("Id", target.Id);
                    if (handle >= 0)
                    {
                        _gridView.FocusedRowHandle = handle;
                        return;
                    }
                }
            }

            _gridView.FocusedRowHandle = 0;
        }

        private void BindChiTietPanel()
        {
            var row = _gridView.GetFocusedRow() as PhieuXuLyBatThuong;
            if (row == null)
            {
                SetPanelEnabled(false);
                return;
            }

            SetPanelEnabled(true);

            _lblSoPhieu.Text = row.SoPhieu ?? "";
            _lblModel.Text = row.Model ?? "";
            _lblMaSP.Text = row.MaSanPham ?? "";
            _lblSoLo.Text = row.SoLo ?? "";
            _lblSoLuong.Text = row.SoLuongLoi.ToString("n0");
            _lblPhanLoai.Text = row.PhanLoaiXuLy ?? "";
            _lblNoiDung.Text = row.NoiDungBatThuong ?? "";
            _lblBoPhan.Text = row.BoPhanPhatHanh ?? "";
            _lblNguoiTao.Text = row.CreatedBy ?? "";
            _lblNgayTao.Text = row.CreatedAt.ToString("dd/MM/yyyy HH:mm");
            _lblHuongXuLy.Text = row.HuongXuLy.ToString();
            _lblTrangThai.Text = row.Status.ToString();
        }

        private void SetPanelEnabled(bool enabled)
        {
            _btnXuLy.Enabled = enabled;
        }

        // ════════════════════════════════════════════════════════════
        // Mở form xử lý chuyên biệt
        // ════════════════════════════════════════════════════════════
        private void BtnXuLy_Click(object sender, EventArgs e)
        {
            var row = _gridView.GetFocusedRow() as PhieuXuLyBatThuong;
            if (row == null) return;

            try
            {
                DialogResult result;
                if (_mode == XuLyBatThuongMode.DinhHuong)
                {
                    using (var f = new FormQCDinhHuong(_qtChungService, row.Id))
                        result = f.ShowDialog(this);
                }
                else
                {
                    using (var f = new FormQCXacNhanCuoi(_qtChungService, row.Id))
                        result = f.ShowDialog(this);
                }

                // Sau khi xử lý xong (dù OK hay Cancel), tải lại danh sách —
                // phiếu đã xử lý sẽ tự động biến mất vì không còn khớp TrangThaiChoXuLy.
                LoadData();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi mở form xử lý:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}