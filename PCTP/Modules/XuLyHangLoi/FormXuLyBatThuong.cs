using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraReports.UI;
using PCTP.Domain.Entities;
using PCTP.Models;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.RpIn;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    /// <summary>
    /// Mốc 3 — QC kiểm tra & phê duyệt "Phiếu xử lý bất thường".
    /// Layout: Grid danh sách CHO_QC bên trái | Panel chi tiết + form QC bên phải.
    /// Sau khi CapNhatQCDuyet thành công: TrangThai chuyển 0 -> 1 (QC_DA_DUYET),
    /// dòng biến mất khỏi danh sách (LoadData lọc lại TrangThai=0), in phiếu A
    /// (ảnh 2 mẫu giấy), rồi refresh badge ở form cha.
    /// </summary>
    public enum XuLyBatThuongMode { DinhHuong, XacNhanCuoi }

    public partial class FormXuLyBatThuong : XtraForm
    {
        private readonly IPhieuTraHangRepository _repo;
        private readonly XuLyBatThuongMode _mode;
        private readonly int? _preselectId;

        // ── Grid trái ────────────────────────────────────────────────────
        private GridControl _grid;
        private GridView _gridView;
        private List<PhieuXuLyBatThuong> _dsPhieu;

        // ── Panel chi tiết phải — vùng thông tin KHO (read-only) ────────
        private LabelControl _lblSoPhieu, _lblModel, _lblMaSP, _lblSoLo, _lblSoLuong;
        private LabelControl _lblPhanLoai, _lblNoiDung, _lblNguoiTH, _lblBoPhan, _lblNgayTao;

        // ── Cụm "Định hướng QC" (Mode DinhHuong) ──────────────────────────
        private TextEdit _txtLoaiLoi;
        private MemoEdit _txtPhuongPhapDinhHuong;

        // ── Cụm "Phương pháp kiểm tra" ────────────────────────────────────
        private MemoEdit _txtPhuongPhapKiemTra;
        private ComboBoxEdit _cboKetQuaKiemTra;
        private SpinEdit _spinSlKiemTra;

        // ── Cụm "Phương pháp sửa" ──────────────────────────────────────────
        private MemoEdit _txtPhuongPhapSua;
        private ComboBoxEdit _cboKetQuaSua;
        private SpinEdit _spinSlSua;

        // ── Cụm "Xác nhận lần cuối (phòng chất lượng)" ───────────────────
        private ComboBoxEdit _cboXacNhanCuoi;
        private TextEdit _txtNguoiDanhGia;
        private TextEdit _txtNguoiThucHienQC;
        private MemoEdit _txtGhiChuQC;

        // ── Bảng chữ ký 4 cột ──────────────────────────────────────────────
        private DateEdit _dateBoPhanPhatSinh, _dateQCTiepNhan, _dateBPPHXacNhan, _dateQCDuyet;
        private TextEdit _txtHoTenBoPhanPhatSinh, _txtHoTenQCTiepNhan, _txtHoTenBPPHXacNhan, _txtHoTenQCDuyet;

        private SimpleButton _btnDuyet, _btnInPhieu, _btnRefresh, _btnClose;
        private LabelControl _lblTrangThaiRong;

        private PanelControl _pnlChiTiet;

        // ── Constructor chính ─────────────────────────────────────────────
        public FormXuLyBatThuong(IPhieuTraHangRepository repo, XuLyBatThuongMode mode, int? preselectId = null)
        {
            _repo = repo;
            _mode = mode;
            _preselectId = preselectId;
            BuildUI();
            ApplyModeUI();
            LoadData();
        }

        // Constructor quá tải tương thích ngược (mặc định về DinhHuong)
        public FormXuLyBatThuong(IPhieuTraHangRepository repo, int? preselectId = null)
            : this(repo, XuLyBatThuongMode.DinhHuong, preselectId)
        {
        }

        // ════════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = _mode == XuLyBatThuongMode.DinhHuong
                ? "QC — Định hướng xử lý bất thường"
                : "QC — Xác nhận lần cuối & Duyệt phiếu";
            Size = new Size(1250, 780);
            StartPosition = FormStartPosition.CenterParent;

            var split = new SplitContainerControl
            {
                Dock = DockStyle.Fill,
                SplitterPosition = 220,
                Horizontal = false
            };

            // ── LEFT: Grid danh sách chờ ─────────────────────────────────
            var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(5) };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblTitle = new LabelControl
            {
                Text = _mode == XuLyBatThuongMode.DinhHuong ? "📋 Danh sách chờ QC định hướng" : "📋 Danh sách chờ QC chốt cuối",
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
            _gridView.DoubleClick += (s, e) => { if (_btnDuyet.Enabled) _btnDuyet.PerformClick(); };

            _gridView.Columns.Add(new GridColumn { FieldName = "SoPhieu", Caption = "Số phiếu", Width = 130, VisibleIndex = 0 });
            _gridView.Columns.Add(new GridColumn { FieldName = "Model", Caption = "Model", Width = 80, VisibleIndex = 1 });
            _gridView.Columns.Add(new GridColumn { FieldName = "MaSanPham", Caption = "Mã sản phẩm", Width = 150, VisibleIndex = 2 });
            _gridView.Columns.Add(new GridColumn { FieldName = "SoLo", Caption = "Số lô", Width = 100, VisibleIndex = 3 });
            var colSl = new GridColumn { FieldName = "SoLuongLoi", Caption = "SL lỗi", Width = 70, VisibleIndex = 4 };
            colSl.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            colSl.DisplayFormat.FormatString = "n0";
            _gridView.Columns.Add(colSl);
            _gridView.Columns.Add(new GridColumn { FieldName = "PhanLoaiXuLy", Caption = "Phân loại", Width = 130, VisibleIndex = 5 });
            _gridView.Columns.Add(new GridColumn { FieldName = "NgayTao", Caption = "Ngày tạo", Width = 110, VisibleIndex = 6 });
            _gridView.Columns["NgayTao"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            _gridView.Columns["NgayTao"].DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";

            leftLayout.Controls.Add(_grid, 0, 1);
            split.Panel1.Controls.Add(leftLayout);
            split.Panel1.MinSize = 120;

            // ── RIGHT: Panel chi tiết + thao tác QC ─────────────────────
            var rightLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(5) };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblTitle2 = new LabelControl
            {
                Text = _mode == XuLyBatThuongMode.DinhHuong ? "✍️ Chi tiết & Định hướng QC" : "✍️ Chi tiết & Xác nhận lần cuối",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9.5F, FontStyle.Bold), ForeColor = Color.Navy }
            };
            rightLayout.Controls.Add(lblTitle2, 0, 0);

            BuildPanelChiTiet();

            var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scrollHost.Controls.Add(_pnlChiTiet);
            rightLayout.Controls.Add(scrollHost, 0, 1);

            split.Panel2.Controls.Add(rightLayout);
            Controls.Add(split);
        }

        private void BuildPanelChiTiet()
        {
            _pnlChiTiet = new PanelControl { Width = 640, Height = 1250, Dock = DockStyle.Top };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = false,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int r = 0;
            void AddInfoRow(string label, out LabelControl valueCtrl)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
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

            // ── Vùng KHO — read-only ─────────────────────────────────────
            AddInfoRow("Số phiếu:", out _lblSoPhieu);
            AddInfoRow("Model:", out _lblModel);
            AddInfoRow("Mã sản phẩm:", out _lblMaSP);
            AddInfoRow("Số lô:", out _lblSoLo);
            AddInfoRow("Số lượng lỗi:", out _lblSoLuong);
            AddInfoRow("Phân loại xử lý:", out _lblPhanLoai);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.Controls.Add(new LabelControl { Text = "Nội dung bất thường:", Dock = DockStyle.Fill }, 0, r);
            _lblNoiDung = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = { TextOptions = { WordWrap = DevExpress.Utils.WordWrap.Wrap } }
            };
            layout.Controls.Add(_lblNoiDung, 1, r);
            r++;

            AddInfoRow("Bộ phận phát hành:", out _lblBoPhan);
            AddInfoRow("Người thực hiện:", out _lblNguoiTH);
            AddInfoRow("Ngày tạo:", out _lblNgayTao);

            AddSeparator(layout, ref r);

            // ── Cụm Định hướng QC (Dành riêng cho Mode DinhHuong) ────────
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblDH = new LabelControl
            {
                Text = "── QC Định hướng ban đầu ──",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkCyan }
            };
            layout.Controls.Add(lblDH, 0, r);
            layout.SetColumnSpan(lblDH, 2);
            r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.Controls.Add(new LabelControl { Text = "Loại lỗi:", Dock = DockStyle.Fill }, 0, r);
            _txtLoaiLoi = new TextEdit { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtLoaiLoi, 1, r);
            r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.Controls.Add(new LabelControl { Text = "Phương pháp định hướng:", Dock = DockStyle.Fill }, 0, r);
            _txtPhuongPhapDinhHuong = new MemoEdit { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtPhuongPhapDinhHuong, 1, r);
            r++;

            AddSeparator(layout, ref r);

            // ── VÙNG QC CHI TIẾT (Kiểm tra, Sửa, Chốt cuối, Chữ ký) ──────
            BuildVungQC(layout, ref r);

            // ── Buttons ──────────────────────────────────────────────────
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            _btnDuyet = new SimpleButton { Text = _mode == XuLyBatThuongMode.DinhHuong ? "🚀 Lưu Định Hướng" : "✅ Duyệt & In phiếu", Width = 160, Height = 36 };
            _btnDuyet.Appearance.BackColor = Color.SeaGreen;
            _btnDuyet.Appearance.ForeColor = Color.White;
            _btnDuyet.Appearance.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _btnDuyet.Click += BtnDuyet_Click;

            _btnInPhieu = new SimpleButton { Text = "🖨 In lại phiếu", Width = 120, Height = 36 };
            _btnInPhieu.Click += BtnInPhieu_Click;

            _btnRefresh = new SimpleButton { Text = "🔄 Làm mới", Width = 100, Height = 36 };
            _btnRefresh.Click += (s, e) => LoadData();

            btnPanel.Controls.Add(_btnDuyet);
            btnPanel.Controls.Add(_btnInPhieu);
            btnPanel.Controls.Add(_btnRefresh);
            layout.Controls.Add(btnPanel, 0, r);
            layout.SetColumnSpan(btnPanel, 2);
            r++;

            _pnlChiTiet.Controls.Add(layout);
            SetPanelEnabled(false);
        }

        private void AddSeparator(TableLayoutPanel layout, ref int r)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
            var sep = new Panel { Dock = DockStyle.Fill, Height = 1, BackColor = Color.Silver };
            layout.Controls.Add(sep, 0, r);
            layout.SetColumnSpan(sep, 2);
            r++;
        }

        private void BuildVungQC(TableLayoutPanel layout, ref int r)
        {
            // ── Phương pháp kiểm tra ─────────────────────────────────────
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblKT = new LabelControl { Text = "── Phương pháp kiểm tra ──", Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkSlateBlue } };
            layout.Controls.Add(lblKT, 0, r); layout.SetColumnSpan(lblKT, 2); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.Controls.Add(new LabelControl { Text = "Nội dung KT:", Dock = DockStyle.Fill }, 0, r);
            _txtPhuongPhapKiemTra = new MemoEdit { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtPhuongPhapKiemTra, 1, r); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            var panelKT = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            panelKT.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
            panelKT.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            panelKT.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            _cboKetQuaKiemTra = new ComboBoxEdit { Dock = DockStyle.Fill };
            _cboKetQuaKiemTra.Properties.Items.AddRange(new object[] { "OK", "NG", "Cải" });
            _cboKetQuaKiemTra.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            _spinSlKiemTra = new SpinEdit { Dock = DockStyle.Fill, Properties = { MinValue = 0 } };
            panelKT.Controls.Add(new LabelControl { Text = "KQ:", Dock = DockStyle.Fill }, 0, 0);
            panelKT.Controls.Add(_cboKetQuaKiemTra, 1, 0);
            panelKT.Controls.Add(_spinSlKiemTra, 2, 0);
            layout.Controls.Add(panelKT, 1, r); r++;

            // ── Phương pháp sửa ──────────────────────────────────────────
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblSua = new LabelControl { Text = "── Phương pháp sửa ──", Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkSlateBlue } };
            layout.Controls.Add(lblSua, 0, r); layout.SetColumnSpan(lblSua, 2); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.Controls.Add(new LabelControl { Text = "Nội dung sửa:", Dock = DockStyle.Fill }, 0, r);
            _txtPhuongPhapSua = new MemoEdit { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtPhuongPhapSua, 1, r); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            var panelSua = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            panelSua.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
            panelSua.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            panelSua.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            _cboKetQuaSua = new ComboBoxEdit { Dock = DockStyle.Fill };
            _cboKetQuaSua.Properties.Items.AddRange(new object[] { "OK", "NG", "Cải" });
            _cboKetQuaSua.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            _spinSlSua = new SpinEdit { Dock = DockStyle.Fill, Properties = { MinValue = 0 } };
            panelSua.Controls.Add(new LabelControl { Text = "KQ:", Dock = DockStyle.Fill }, 0, 0);
            panelSua.Controls.Add(_cboKetQuaSua, 1, 0);
            panelSua.Controls.Add(_spinSlSua, 2, 0);
            layout.Controls.Add(panelSua, 1, r); r++;

            // ── Xác nhận lần cuối ────────────────────────────────────────
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblCuoi = new LabelControl { Text = "── Xác nhận lần cuối (phòng chất lượng) ──", Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkRed } };
            layout.Controls.Add(lblCuoi, 0, r); layout.SetColumnSpan(lblCuoi, 2); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(new LabelControl { Text = "Kết luận (*):", Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold) } }, 0, r);
            _cboXacNhanCuoi = new ComboBoxEdit { Dock = DockStyle.Fill };
            _cboXacNhanCuoi.Properties.Items.AddRange(new object[] { "OK", "NG" });
            _cboXacNhanCuoi.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            layout.Controls.Add(_cboXacNhanCuoi, 1, r); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.Controls.Add(new LabelControl { Text = "Người đánh giá:", Dock = DockStyle.Fill }, 0, r);
            _txtNguoiDanhGia = new TextEdit { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtNguoiDanhGia, 1, r); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.Controls.Add(new LabelControl { Text = "Người thực hiện QC:", Dock = DockStyle.Fill }, 0, r);
            _txtNguoiThucHienQC = new TextEdit { Dock = DockStyle.Fill, Text = Environment.UserName };
            layout.Controls.Add(_txtNguoiThucHienQC, 1, r); r++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.Controls.Add(new LabelControl { Text = "Ghi chú:", Dock = DockStyle.Fill }, 0, r);
            _txtGhiChuQC = new MemoEdit { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtGhiChuQC, 1, r); r++;

            AddSeparator(layout, ref r);

            // ── Bảng chữ ký ──────────────────────────────────────────────
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var lblKy = new LabelControl { Text = "── Chữ ký xác nhận các bên ──", Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkSlateBlue } };
            layout.Controls.Add(lblKy, 0, r); layout.SetColumnSpan(lblKy, 2); r++;

            var kyTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
            for (int c = 0; c < 4; c++) kyTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            kyTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            kyTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            kyTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            string[] headers = { "BP phát sinh", "QC tiếp nhận", "BPPH xác nhận", "QC duyệt (MG/QM)" };
            for (int c = 0; c < 4; c++)
                kyTbl.Controls.Add(new LabelControl { Text = headers[c], Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 7.5F, FontStyle.Bold) }, AutoEllipsis = true }, c, 0);

            _dateBoPhanPhatSinh = new DateEdit { Dock = DockStyle.Fill };
            _dateQCTiepNhan = new DateEdit { Dock = DockStyle.Fill };
            _dateBPPHXacNhan = new DateEdit { Dock = DockStyle.Fill };
            _dateQCDuyet = new DateEdit { Dock = DockStyle.Fill };
            kyTbl.Controls.Add(_dateBoPhanPhatSinh, 0, 1);
            kyTbl.Controls.Add(_dateQCTiepNhan, 1, 1);
            kyTbl.Controls.Add(_dateBPPHXacNhan, 2, 1);
            kyTbl.Controls.Add(_dateQCDuyet, 3, 1);

            _txtHoTenBoPhanPhatSinh = new TextEdit { Dock = DockStyle.Fill };
            _txtHoTenQCTiepNhan = new TextEdit { Dock = DockStyle.Fill };
            _txtHoTenBPPHXacNhan = new TextEdit { Dock = DockStyle.Fill };
            _txtHoTenQCDuyet = new TextEdit { Dock = DockStyle.Fill, Text = Environment.UserName };
            kyTbl.Controls.Add(_txtHoTenBoPhanPhatSinh, 0, 2);
            kyTbl.Controls.Add(_txtHoTenQCTiepNhan, 1, 2);
            kyTbl.Controls.Add(_txtHoTenBPPHXacNhan, 2, 2);
            kyTbl.Controls.Add(_txtHoTenQCDuyet, 3, 2);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.Controls.Add(kyTbl, 0, r); layout.SetColumnSpan(kyTbl, 2); r++;
        }

        // ── Ẩn/Hiện control tùy thuộc vào Mode ────────────────────────────
        private void ApplyModeUI()
        {
            bool isDinhHuong = (_mode == XuLyBatThuongMode.DinhHuong);

            // Mode DinhHuong: Chỉ hiện ô định hướng, ẩn các cụm kiểm tra/sửa/chốt cuối
            // Mode XacNhanCuoi: Ẩn ô định hướng, hiện toàn bộ cụm kiểm tra/sửa/chốt cuối
            _txtLoaiLoi.Visible = isDinhHuong;
            _txtLoaiLoi.Parent.Visible = isDinhHuong; // Hoặc ẩn dòng chứa nó tùy theo TableLayout của bạn

            // Cụm định hướng
            _txtLoaiLoi.Enabled = isDinhHuong;
            _txtPhuongPhapDinhHuong.Enabled = isDinhHuong;

            // Cụm kiểm tra, sửa, kết luận cuối (chỉ cho phép ở XacNhanCuoi)
            bool isXacNhan = !isDinhHuong;
            _txtPhuongPhapKiemTra.Enabled = isXacNhan;
            _cboKetQuaKiemTra.Enabled = isXacNhan;
            _spinSlKiemTra.Enabled = isXacNhan;

            _txtPhuongPhapSua.Enabled = isXacNhan;
            _cboKetQuaSua.Enabled = isXacNhan;
            _spinSlSua.Enabled = isXacNhan;

            _cboXacNhanCuoi.Enabled = isXacNhan;
            _txtNguoiDanhGia.Enabled = isXacNhan;
            _txtNguoiThucHienQC.Enabled = isXacNhan;
            _txtGhiChuQC.Enabled = isXacNhan;
        }

        // ════════════════════════════════════════════════════════════════
        // Load / Bind
        // ════════════════════════════════════════════════════════════════
        private void LoadData()
        {
            try
            {
                if (_mode == XuLyBatThuongMode.DinhHuong)
                    _dsPhieu = _repo.ge(); // TrangThai = ChoQC
                else
                    _dsPhieu = _repo.GetDanhSachChoQCXacNhanCuoi(); // TrangThai = ChoQCXacNhanCuoi
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
            ApplyModeUI(); // Giữ đúng trạng thái enable/disable theo mode

            // ── Vùng KHO ──────────────────────────────────────────────────
            _lblSoPhieu.Text = row.SoPhieu ?? "";
            _lblModel.Text = row.Model ?? "";
            _lblMaSP.Text = row.MaSanPham ?? "";
            _lblSoLo.Text = row.SoLo ?? "";
            _lblSoLuong.Text = row.SoLuongLoi.ToString("n0");
            _lblPhanLoai.Text = row.PhanLoaiXuLy ?? "";
            _lblNoiDung.Text = row.NoiDungBatThuong ?? "";
            _lblBoPhan.Text = row.BoPhanPhatHanh ?? "";
            _lblNguoiTH.Text = row.NguoiThucHien ?? "";
            _lblNgayTao.Text = row.NgayTao.ToString("dd/MM/yyyy HH:mm");

            // Bind dữ liệu định hướng
            _txtLoaiLoi.Text = row.LoaiLoi ?? "";
            _txtPhuongPhapDinhHuong.Text = row.PhuongPhapDinhHuong ?? "";

            // Bind dữ liệu QC cuối
            _txtPhuongPhapKiemTra.Text = row.PhuongPhapKiemTra ?? "";
            _cboKetQuaKiemTra.EditValue = row.KetQuaKiemTra;
            _spinSlKiemTra.EditValue = row.SoLuongKiemTra ?? (object)0;

            _txtPhuongPhapSua.Text = row.PhuongPhapSua ?? "";
            _cboKetQuaSua.EditValue = row.KetQuaSua;
            _spinSlSua.EditValue = row.SoLuongSua ?? (object)0;

            _cboXacNhanCuoi.EditValue = row.XacNhanCuoiKetQua;
            _txtNguoiDanhGia.Text = row.NguoiDanhGia ?? "";
            _txtNguoiThucHienQC.Text = string.IsNullOrWhiteSpace(row.NguoiThucHienQC) ? Environment.UserName : row.NguoiThucHienQC;
            _txtGhiChuQC.Text = row.GhiChuQC ?? "";
        }

        private void SetPanelEnabled(bool enabled)
        {
            _btnDuyet.Enabled = enabled;
            _btnInPhieu.Enabled = enabled;
        }

        // ════════════════════════════════════════════════════════════════
        // Xử lý Sự kiện nút Lưu / Duyệt
        // ════════════════════════════════════════════════════════════════
        private void BtnDuyet_Click(object sender, EventArgs e)
        {
            var row = _gridView.GetFocusedRow() as PhieuXuLyBatThuong;
            if (row == null) return;

            if (_mode == XuLyBatThuongMode.DinhHuong)
            {
                // ── Xử lý lưu Định hướng ─────────────────────────────────
                if (string.IsNullOrWhiteSpace(_txtPhuongPhapDinhHuong.Text))
                {
                    XtraMessageBox.Show("Vui lòng nhập phương pháp định hướng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtPhuongPhapDinhHuong.Focus();
                    return;
                }

                try
                {
                    _repo.CapNhatQCDinhHuong(row.Id, _txtLoaiLoi.Text.Trim(), _txtPhuongPhapDinhHuong.Text.Trim(), Environment.UserName);
                    XtraMessageBox.Show("Đã lưu định hướng và chuyển sang bộ phận sản xuất xử lý.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show($"Lỗi lưu định hướng:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // ── Xử lý Xác nhận cuối (Duyệt) ──────────────────────────
                if (_cboXacNhanCuoi.EditValue == null)
                {
                    XtraMessageBox.Show("Vui lòng chọn Kết luận (*) cuối cùng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    _repo.CapNhatQCDuyet(new QCDuyetInput
                    {
                        Id = row.Id,
                        PhuongPhapKiemTra = _txtPhuongPhapKiemTra.Text.Trim(),
                        KetQuaKiemTra = _cboKetQuaKiemTra.Text,
                        SoLuongKiemTra = (int?)_spinSlKiemTra.Value,
                        PhuongPhapSua = _txtPhuongPhapSua.Text.Trim(),
                        KetQuaSua = _cboKetQuaSua.Text,
                        SoLuongSua = (int?)_spinSlSua.Value,
                        XacNhanCuoiKetQua = _cboXacNhanCuoi.Text,
                        NguoiDanhGia = _txtNguoiDanhGia.Text.Trim(),
                        NguoiThucHienQC = _txtNguoiThucHienQC.Text.Trim(),
                        GhiChuQC = _txtGhiChuQC.Text.Trim(),
                        NgayQCDuyet = DateTime.Now,
                        HoTenQCDuyet = _txtHoTenQCDuyet.Text.Trim()
                    });

                    InPhieu(row.Id, false);
                    XtraMessageBox.Show("Đã duyệt thành công phiếu xử lý bất thường.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show($"Lỗi duyệt phiếu:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnInPhieu_Click(object sender, EventArgs e)
        {
            var row = _gridView.GetFocusedRow() as PhieuXuLyBatThuong;
            if (row != null) InPhieu(row.Id, true);
        }

        private void InPhieu(int id, bool showErrorIfMissing)
        {
            try
            {
                var updated = _repo.GetPhieuXuLyBatThuong(id);
                if (updated == null) return;
                new ReportPrintTool(new RpPhieuXuLyBatThuong(updated)).ShowPreviewDialog();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi in phiếu:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}