using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Repositories;
using PCTP.Modules.BaoCao.UI;
using PCTP.Shared.Lookup;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.XuLyHangLoi
{
    /// <summary>Một lần giao gốc được chọn + SL khách trả thuộc lần giao đó.</summary>
    public sealed class PhieuGiaoChon
    {
        public PhieuGiaoUngVienInfo Phieu { get; set; }

        /// <summary>SL của LOT trong phiếu giao (nếu tra theo LOT), ngược lại tổng SL dòng giao.</summary>
        public int SoLuongDaGiao { get; set; }

        /// <summary>SL khách trả thuộc lần giao này (có thể khác SL đã giao).</summary>
        public int SoLuongTra { get; set; }
    }

    public enum ChedoDoiChieuPhieuGiao
    {
        /// <summary>Khách trả: tìm các lần giao gốc, tick + nhập SL trả, đồng thời xem luồng đi của LOT.</summary>
        DoiChieuVaGan,

        /// <summary>Trả nội bộ / xem lại: chỉ xem luồng đi của LOT (SX → kho → giao), không có phiếu giao để gán.</summary>
        ChiXemLuongDi
    }

    /// <summary>
    /// Tóm tắt luồng đi của LOT (timeline truy vết dùng chung với báo cáo truy xuất).
    /// Chỉ đọc; chỉ dùng cho hiển thị trên phiếu, không lưu xuống DB.
    /// </summary>
    public static class LuongHangLoiTomTat
    {
        private const int ToiDaSuKien = 12;

        public static string TomTat(IEnumerable<TraceTimelineNode> nodes)
        {
            if (nodes == null)
                return string.Empty;

            var parts = new List<string>();

            foreach (var n in nodes)
            {
                if (n == null || n.Kind == TraceNodeKind.DeliveryLot || n.Kind == TraceNodeKind.Invalid)
                    continue;

                string ngay = n.EventTime.HasValue ? n.EventTime.Value.ToString("dd/MM/yy") : "?";
                string sl = n.Quantity.HasValue ? " SL " + n.Quantity.Value.ToString("N0") : string.Empty;

                switch (n.Kind)
                {
                    case TraceNodeKind.Production:
                        parts.Add("SX " + ngay + sl);
                        break;
                    case TraceNodeKind.StockIn:
                        parts.Add("Nhập kho " + ngay + sl + DenViTri(n));
                        break;
                    case TraceNodeKind.StockOut:
                        parts.Add("Xuất kho " + ngay + sl + (string.IsNullOrWhiteSpace(n.DocumentNo) ? string.Empty : " (" + n.DocumentNo.Trim() + ")"));
                        break;
                    case TraceNodeKind.StockOther:
                        parts.Add((string.IsNullOrWhiteSpace(n.EventType) ? "Kho" : n.EventType.Trim()) + " " + ngay + sl);
                        break;
                    case TraceNodeKind.Delivery:
                        parts.Add("Giao " + ngay + sl +
                            (string.IsNullOrWhiteSpace(n.CustomerName) ? string.Empty : " (" + n.CustomerName.Trim() + ")"));
                        break;
                    case TraceNodeKind.Missing:
                        parts.Add("⚠ " + n.Title);
                        break;
                }
            }

            if (parts.Count == 0)
                return string.Empty;

            if (parts.Count > ToiDaSuKien)
            {
                int con = parts.Count - ToiDaSuKien;
                parts = parts.Take(ToiDaSuKien).ToList();
                parts.Add("… (+" + con + " sự kiện)");
            }

            return string.Join(" → ", parts);
        }

        private static string DenViTri(TraceTimelineNode n)
        {
            return string.IsNullOrWhiteSpace(n.ToLocation) ? string.Empty : " → " + n.ToLocation.Trim();
        }
    }

    /// <summary>
    /// Đối chiếu dòng khách trả với thông tin HÀNG ĐÃ GIAO.
    ///
    /// Cùng một mã hàng / LOT có thể được giao nhiều lần, khách lại thường chỉ báo
    /// khoảng thời gian → form liệt kê mọi lần giao trong [Từ ngày, Đến ngày]
    /// (lọc theo khách, mã hàng, LOT nếu có). Người tiếp nhận tick một hoặc nhiều
    /// lần giao và nhập SL khách trả cho từng lần (SL trả được phép khác SL đã giao).
    /// </summary>
    public sealed class FormDoiChieuPhieuGiao : XtraForm
    {
        public sealed class DongDoiChieu
        {
            public bool Chon { get; set; }
            public DateTime? NgayGiao { get; set; }
            public string GioGiao { get; set; }
            public string NhaMay { get; set; }
            public string PoNo { get; set; }
            public string Lot { get; set; }
            public int SoLuongPhieu { get; set; }
            public int? SoLuongLot { get; set; }
            public int SoLuongDaGiao { get; set; }
            public int SoLuongTra { get; set; }
            public string Note { get; set; }

            [Browsable(false)]
            public PhieuGiaoUngVienInfo Phieu { get; set; }
        }

        private readonly IKhachTraHangService _service;
        private readonly string _customerNo;
        private readonly string _maHang;
        private readonly int _soLuongKhachBao;
        private readonly ChedoDoiChieuPhieuGiao _mode;

        // Luồng đi của LOT (timeline truy vết dùng chung với báo cáo truy xuất giao hàng).
        private LotTraceTimelineBuilder _timelineBuilder;
        private SplitContainer _split;
        private TraceTimelineControl _timeline;
        private MemoEdit _memoChiTiet;
        private LabelControl _lblLuong;
        private LabelControl _lblTu;
        private LabelControl _lblDen;
        private SimpleButton _btnTim;
        private SimpleButton _btnOk;
        private int _traceVersion;
        private bool _dangNapLuoi;

        private StockLookupControl _lookup; // chế độ ChiXemLuongDi: ô tìm chung với báo cáo
        private TextEdit _txtMaHang;
        private TextEdit _txtLot;
        private DateEdit _dtTu;
        private DateEdit _dtDen;
        private LabelControl _lblKetQua;
        private LabelControl _lblTong;
        private GridControl _grid;
        private GridView _view;
        private List<DongDoiChieu> _rows = new List<DongDoiChieu>();

        public List<PhieuGiaoChon> KetQua { get; private set; } = new List<PhieuGiaoChon>();

        /// <summary>LOT dùng để đối chiếu (người dùng có thể sửa trong form).</summary>
        public string LotNo { get; private set; }

        /// <summary>Tóm tắt luồng đi (SX → nhập kho → xuất → giao) của LOT vừa truy vết. Rỗng nếu chưa truy vết được.</summary>
        public string LuongDiTomTat { get; private set; } = string.Empty;

        /// <summary>Các node timeline của LOT vừa truy vết (chỉ đọc).</summary>
        public IReadOnlyList<TraceTimelineNode> LuongDi { get; private set; } = new List<TraceTimelineNode>();

        public FormDoiChieuPhieuGiao(
            IKhachTraHangService service,
            string customerNo,
            string maHang,
            string tenHang,
            string lotNo,
            DateTime? ngayGiaoKhachBao,
            int soLuongKhachBao)
            : this(
                ChedoDoiChieuPhieuGiao.DoiChieuVaGan,
                service,
                customerNo,
                maHang,
                tenHang,
                lotNo,
                ngayGiaoKhachBao,
                soLuongKhachBao)
        {
        }

        /// <summary>
        /// Chỉ xem luồng đi của LOT (dùng cho phiếu trả nội bộ hoặc xem lại phiếu khách trả):
        /// sản xuất → nhập/xuất/chuyển kho → các lần giao khách. Không gán phiếu giao.
        /// </summary>
        public static FormDoiChieuPhieuGiao ChoXemLuongDi(string maHang, string tenHang, string lotNo)
        {
            return new FormDoiChieuPhieuGiao(
                ChedoDoiChieuPhieuGiao.ChiXemLuongDi,
                null,
                null,
                maHang,
                tenHang,
                lotNo,
                null,
                0);
        }

        private FormDoiChieuPhieuGiao(
            ChedoDoiChieuPhieuGiao mode,
            IKhachTraHangService service,
            string customerNo,
            string maHang,
            string tenHang,
            string lotNo,
            DateTime? ngayGiaoKhachBao,
            int soLuongKhachBao)
        {
            _mode = mode;

            if (mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan)
                _service = service ?? throw new ArgumentNullException(nameof(service));

            _customerNo = customerNo;
            _maHang = (maHang ?? string.Empty).Trim();
            _soLuongKhachBao = soLuongKhachBao;

            BuildUi(tenHang, lotNo, ngayGiaoKhachBao);

            Load += (s, e) => DatViTriSplitter();

            Shown += (s, e) =>
            {
                if (_mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan)
                    TimKiem();
                else
                {
                    TraCuuLuongDi();
                    var _ = NapGoiYMaHangAsync();
                }
            };
        }

        private void BuildUi(string tenHang, string lotNo, DateTime? ngayGiaoKhachBao)
        {
            Text = _mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan
                ? "Đối chiếu phiếu giao gốc"
                : "Luồng đi của hàng (nguồn gốc lịch sử)";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1180;
            Height = 820;

            var top = new Panel { Dock = DockStyle.Top, Height = 108 };

            top.Controls.Add(new LabelControl { Text = "Mã hàng", Left = 12, Top = 16, Width = 70 });
            _txtMaHang = new TextEdit { Left = 86, Top = 12, Width = 230 };
            _txtMaHang.Text = _maHang;
            _txtMaHang.Properties.ReadOnly = _mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan || _maHang.Length > 0;
            top.Controls.Add(_txtMaHang);

            top.Controls.Add(new LabelControl
            {
                Text = string.IsNullOrWhiteSpace(tenHang) ? string.Empty : tenHang,
                Left = 324, Top = 16, Width = 460, AutoSizeMode = LabelAutoSizeMode.None
            });

            top.Controls.Add(new LabelControl { Text = "Lot (nếu có)", Left = 12, Top = 52, Width = 70 });
            _txtLot = new TextEdit { Left = 86, Top = 48, Width = 230 };
            _txtLot.Text = (lotNo ?? string.Empty).Trim();
            _txtLot.Properties.NullValuePrompt = "Để trống = mọi LOT của mã";
            _txtLot.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; ChayTraCuu(); } };
            top.Controls.Add(_txtLot);

            _lblTu = new LabelControl { Text = "Giao từ ngày", Left = 340, Top = 52, Width = 80 };
            top.Controls.Add(_lblTu);
            _dtTu = TaoDateEdit(424, 48);
            top.Controls.Add(_dtTu);

            _lblDen = new LabelControl { Text = "đến ngày", Left = 548, Top = 52, Width = 60 };
            top.Controls.Add(_lblDen);
            _dtDen = TaoDateEdit(612, 48);
            top.Controls.Add(_dtDen);

            // Khách báo ngày cụ thể → mặc định đúng ngày đó; không có → 30 ngày gần nhất
            // (90 ngày nếu đã biết LOT vì LOT thường giao sau khi sản xuất một thời gian).
            if (ngayGiaoKhachBao.HasValue)
            {
                _dtTu.EditValue = ngayGiaoKhachBao.Value.Date;
                _dtDen.EditValue = ngayGiaoKhachBao.Value.Date;
            }
            else
            {
                bool coLot = !string.IsNullOrWhiteSpace(lotNo);
                _dtTu.EditValue = DateTime.Today.AddDays(coLot ? -90 : -30);
                _dtDen.EditValue = DateTime.Today;
            }

            bool chiXem = _mode == ChedoDoiChieuPhieuGiao.ChiXemLuongDi;

            _btnTim = new SimpleButton
            {
                Text = chiXem ? "🔎 Tra cứu luồng đi" : "🔎 Tìm lần giao",
                Left = chiXem ? 340 : 740,
                Top = 46,
                Width = chiXem ? 160 : 130
            };
            _btnTim.Click += (s, e) => ChayTraCuu();
            top.Controls.Add(_btnTim);

            if (chiXem)
            {
                _lblTu.Visible = false;
                _lblDen.Visible = false;
                _dtTu.Visible = false;
                _dtDen.Visible = false;
            }

            if (chiXem)
            {
                // Dùng chung StockLookupControl với các báo cáo (Lịch sử kho / Truy xuất): chọn mã hàng từ danh mục thật,
                // hoặc nhập LOT / quét tem QR. Các ô nhập tay cũ chỉ còn giữ giá trị (ẩn) cho phần truy vết bên dưới.
                foreach (Control c in top.Controls)
                    c.Visible = false;

                _lookup = new StockLookupControl { Left = 12, Top = 2, Width = 640, Height = 80 };
                _lookup.SearchRequested += (s, e) => LookupTimKiem();
                top.Controls.Add(_lookup);

                if (_txtLot.Text.Length > 0)
                {
                    _lookup.SelectLotQrTab();
                    _lookup.SetInputText(_txtLot.Text);
                }
            }

            _lblKetQua = new LabelControl { Left = 12, Top = 84, Width = 1100, AutoSizeMode = LabelAutoSizeMode.None };
            top.Controls.Add(_lblKetQua);

            if (_mode == ChedoDoiChieuPhieuGiao.ChiXemLuongDi)
                _lblKetQua.Text = "Nhập LOT (13/20 ký tự, hoặc LOT ghép) rồi Enter để xem luồng đi: sản xuất → kho → giao khách.";

            // ---- grid
            _grid = new GridControl { Dock = DockStyle.Fill };
            _view = new GridView(_grid);
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);

            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsBehavior.Editable = true;
            _view.OptionsSelection.MultiSelect = false;

            var chk = new RepositoryItemCheckEdit();
            chk.EditValueChanged += (s, e) => _view.PostEditor();
            _grid.RepositoryItems.Add(chk);

            var colChon = ThemCot(nameof(DongDoiChieu.Chon), "Chọn", 55, true);
            colChon.ColumnEdit = chk;
            var colNgayGiao = ThemCot(nameof(DongDoiChieu.NgayGiao), "Ngày giao", 90, false);
            colNgayGiao.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            colNgayGiao.DisplayFormat.FormatString = "dd/MM/yyyy";
            ThemCot(nameof(DongDoiChieu.GioGiao), "Giờ giao", 80, false);
            ThemCot(nameof(DongDoiChieu.NhaMay), "Nhà máy", 200, false);
            ThemCot(nameof(DongDoiChieu.PoNo), "PO", 110, false);
            ThemCot(nameof(DongDoiChieu.Lot), "LOT trên phiếu giao", 260, false);
            ThemCot(nameof(DongDoiChieu.SoLuongPhieu), "SL dòng giao", 90, false);
            ThemCot(nameof(DongDoiChieu.SoLuongLot), "SL của LOT", 80, false);
            ThemCot(nameof(DongDoiChieu.SoLuongDaGiao), "SL đã giao", 85, false);
            ThemCot(nameof(DongDoiChieu.SoLuongTra), "SL khách trả", 95, true);
            ThemCot(nameof(DongDoiChieu.Note), "Ghi chú phiếu giao", 160, false);

            _view.CellValueChanged += View_CellValueChanged;
            _view.RowCellStyle += View_RowCellStyle;
            _view.FocusedRowChanged += View_FocusedRowChanged;

            // ---- bottom
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52 };

            _lblTong = new LabelControl { Left = 12, Top = 8, Width = 760, Height = 36, AutoSizeMode = LabelAutoSizeMode.None };
            bottom.Controls.Add(_lblTong);
            _lblTong.Visible = _mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan;

            _btnOk = new SimpleButton
            {
                Text = _mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan
                    ? "✔ Gán vào dòng khách trả"
                    : "✔ Ghi luồng đi vào dòng",
                Left = 800,
                Top = 10,
                Width = 190
            };
            _btnOk.Click += (s, e) => XacNhan();
            var btnHuy = new SimpleButton
            {
                Text = _mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan ? "Hủy" : "Đóng",
                Left = 1000,
                Top = 10,
                Width = 90,
                DialogResult = DialogResult.Cancel
            };
            bottom.Controls.Add(_btnOk);
            bottom.Controls.Add(btnHuy);

            // ---- luồng đi của LOT: sản xuất → kho → giao (cùng nguồn với báo cáo truy xuất)
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Horizontal
            };

            _split.Panel1.Controls.Add(_grid);

            _timeline = new TraceTimelineControl { Dock = DockStyle.Fill };
            _timeline.SelectedNodeChanged += (s, e) =>
            {
                var node = _timeline.SelectedNode;
                _memoChiTiet.Text = node == null ? string.Empty : node.DetailText();
            };

            _memoChiTiet = new MemoEdit { Dock = DockStyle.Bottom, Height = 120 };
            _memoChiTiet.Properties.ReadOnly = true;

            _lblLuong = new LabelControl
            {
                Dock = DockStyle.Top,
                Height = 26,
                AutoSizeMode = LabelAutoSizeMode.None,
                Padding = new Padding(6, 4, 0, 0)
            };
            _lblLuong.Appearance.ForeColor = Color.DimGray;

            _split.Panel2.Controls.Add(_timeline);
            _split.Panel2.Controls.Add(_memoChiTiet);
            _split.Panel2.Controls.Add(_lblLuong);

            if (chiXem)
                _split.Panel1Collapsed = true;

            Controls.Add(_split);
            Controls.Add(top);
            Controls.Add(bottom);

            CancelButton = btnHuy;
        }

        private DateEdit TaoDateEdit(int left, int top)
        {
            var d = new DateEdit { Left = left, Top = top, Width = 120 };
            d.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            d.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            d.Properties.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            d.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            d.Properties.Mask.EditMask = "dd/MM/yyyy";
            d.Properties.Mask.UseMaskAsDisplayFormat = true;
            return d;
        }

        private GridColumn ThemCot(string field, string caption, int width, bool editable)
        {
            var col = _view.Columns.AddField(field);
            col.Caption = caption;
            col.Visible = true;
            col.Width = width;
            col.OptionsColumn.AllowEdit = editable;
            return col;
        }

        private void TimKiem()
        {
            _view.CloseEditor();

            DateTime tu = (_dtTu.EditValue as DateTime?) ?? DateTime.Today.AddDays(-30);
            DateTime den = (_dtDen.EditValue as DateTime?) ?? DateTime.Today;

            if (tu.Date > den.Date)
            {
                XtraMessageBox.Show(
                    "Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.",
                    "Khoảng thời gian không hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string lot = _txtLot.Text.Trim();

            try
            {
                var list = _service.TimPhieuGiaoTheoKhoang(_customerNo, _maHang, lot, tu, den);

                _rows = list.Select(p => new DongDoiChieu
                {
                    Phieu = p,
                    NgayGiao = p.NGAYGIAO,
                    GioGiao = string.IsNullOrWhiteSpace(p.GIOGIAOFCC) ? p.GIOGIAO : p.GIOGIAOFCC,
                    NhaMay = p.NHAMAY,
                    PoNo = p.PO_NO,
                    Lot = p.LOT,
                    SoLuongPhieu = p.SOLUONG,
                    SoLuongLot = p.SoLuongLot,
                    SoLuongDaGiao = p.SoLuongLot ?? p.SOLUONG,
                    Note = p.Note
                }).ToList();

                _dangNapLuoi = true;
                _grid.DataSource = _rows;

                if (_rows.Count == 1)
                {
                    _rows[0].Chon = true;
                    _rows[0].SoLuongTra = MacDinhSoLuongTra(_rows[0]);
                    _grid.RefreshDataSource();
                }

                _lblKetQua.Text = _rows.Count == 0
                    ? "Không có lần giao nào khớp trong khoảng thời gian này. Mở rộng khoảng ngày hoặc bỏ trống LOT."
                    : $"Tìm thấy {_rows.Count} lần giao. Tick các lần giao khách đã trả và nhập SL trả của từng lần.";
                _lblKetQua.Appearance.ForeColor = _rows.Count == 0 ? Color.Firebrick : Color.DimGray;

                _dangNapLuoi = false;

                CapNhatTong();

                // Luồng đi của LOT (không chặn việc đối chiếu nếu truy vết lỗi).
                CapNhatLuongDiAsync(LotChoTruyVet());
            }
            catch (Exception ex)
            {
                _dangNapLuoi = false;

                XtraMessageBox.Show(
                    "Không thể tra cứu phiếu giao.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // LUỒNG ĐI CỦA LOT  (sản xuất → nhập/xuất kho → giao khách)
        //
        // Dùng chung timeline với báo cáo truy xuất giao hàng để phiếu tiếp nhận /
        // phiếu trả nội bộ thấy đúng nguồn gốc lịch sử của hàng đang báo lỗi.
        // Chỉ đọc; khóa tra cứu là từng LOT (13/20 ký tự), không dùng chuỗi LOT ghép.
        // ============================================================

        private void ChayTraCuu()
        {
            if (_mode == ChedoDoiChieuPhieuGiao.DoiChieuVaGan)
                TimKiem();
            else
                TraCuuLuongDi();
        }

        // Nạp danh mục mã hàng cho ô tìm chung (cùng nguồn với báo cáo Lịch sử kho).
        private async Task NapGoiYMaHangAsync()
        {
            if (_lookup == null)
                return;

            try
            {
                var codes = await Task.Run(() => new StockHistoryQueryService().GetItemCodesAsync(CancellationToken.None));

                if (IsDisposed)
                    return;

                var rows = new System.Data.DataTable();
                rows.Columns.Add("PART", typeof(string));
                foreach (string code in codes ?? new string[0])
                    if (!string.IsNullOrWhiteSpace(code))
                        rows.Rows.Add(code);

                _lookup.SetItemSuggestions(rows, "PART", null);
            }
            catch (Exception)
            {
                // Không có danh sách gợi ý vẫn nhập tay được.
            }
        }

        // Tab "Tìm theo mã": chỉ chọn mã hàng. Tab "LOT / QR": tem QR (Lot:Mã hàng:...) hoặc LOT thuần.
        private void LookupTimKiem()
        {
            string input = _lookup.InputText;

            if (input.Length == 0)
                return;

            if (_lookup.IsItemSearch)
            {
                _txtMaHang.Text = input;
                _lblKetQua.Text = "Đã chọn mã hàng " + input + ". Nhập LOT hoặc quét tem QR ở tab \"Tìm theo LOT / QR\" để xem luồng đi.";
                _lblKetQua.Appearance.ForeColor = Color.DimGray;
                _lookup.SelectLotQrTab();
                _lookup.FocusInput();
                return;
            }

            QrItemInfo info;
            string error;

            if (QrItemParser.TryParse(input, out info, out error))
            {
                _txtMaHang.Text = info.ItemCode ?? string.Empty;
                _txtLot.Text = info.Lot ?? string.Empty;
            }
            else
            {
                _txtLot.Text = input;
            }

            ChayTraCuu();
        }

        private void TraCuuLuongDi()
        {
            CapNhatLuongDiAsync(LotChoTruyVet());
        }

        private void DatViTriSplitter()
        {
            if (_split == null || _split.Panel1Collapsed)
                return;

            try
            {
                _split.SplitterDistance = Math.Max(120, Math.Min(230, _split.Height - 260));
            }
            catch (InvalidOperationException)
            {
                // Kích thước form quá nhỏ: giữ vị trí mặc định.
            }
        }

        private void View_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            if (_dangNapLuoi || _mode != ChedoDoiChieuPhieuGiao.DoiChieuVaGan)
                return;

            // Người dùng đã gõ LOT cụ thể → luồng đi theo LOT đó, không đổi theo dòng đang chọn.
            if (_txtLot.Text.Trim().Length > 0)
                return;

            CapNhatLuongDiAsync(LotChoTruyVet());
        }

        private string LotChoTruyVet()
        {
            string lot = _txtLot.Text.Trim();

            if (lot.Length > 0)
                return lot;

            var row = _view.GetFocusedRow() as DongDoiChieu;

            if (row == null && _rows.Count == 1)
                row = _rows[0];

            return row == null ? string.Empty : (row.Lot ?? string.Empty).Trim();
        }

        private LotTraceTimelineBuilder LayTimelineBuilder()
        {
            if (_timelineBuilder == null)
                _timelineBuilder = LotTraceTimelineBuilderFactory.CreateDefault();

            return _timelineBuilder;
        }

        private async Task CapNhatLuongDiAsync(string lot)
        {
            int version = ++_traceVersion;
            lot = (lot ?? string.Empty).Trim();

            LuongDiTomTat = string.Empty;
            LuongDi = new List<TraceTimelineNode>();

            if (lot.Length == 0)
            {
                _timeline.Clear("Chưa có LOT: nhập LOT hoặc chọn một lần giao để xem luồng đi.");
                _lblLuong.Text = "Chưa có LOT để truy vết luồng đi.";
                _lblLuong.Appearance.ForeColor = Color.DimGray;
                return;
            }

            string maHang = _txtMaHang.Text.Trim();

            _lblLuong.Text = "Đang truy vết luồng đi của LOT " + lot + "...";
            _lblLuong.Appearance.ForeColor = Color.DimGray;

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;

            try
            {
                // Không giới hạn ngày giao: cần thấy ĐỦ các lần giao của LOT để xác định đúng nguồn gốc.
                TraceTimelineResult result = await LayTimelineBuilder()
                    .BuildForLotAsync(lot, string.IsNullOrWhiteSpace(maHang) ? null : maHang, null, null, CancellationToken.None);

                // Có yêu cầu truy vết mới hơn (đổi dòng / đổi LOT) hoặc form đã đóng → bỏ kết quả cũ.
                if (version != _traceVersion || IsDisposed)
                    return;

                _timeline.SetNodes(result.Nodes, result.Summary ?? "Không có dữ liệu luồng đi cho LOT này.");
                LuongDi = result.Nodes;
                LuongDiTomTat = LuongHangLoiTomTat.TomTat(result.Nodes);

                _lblLuong.Text = result.Summary;
                _lblLuong.Appearance.ForeColor = result.Nodes.Count == 0 ? Color.Firebrick : Color.DimGray;
            }
            catch (Exception ex)
            {
                if (version != _traceVersion || IsDisposed)
                    return;

                _timeline.Clear("Không truy vết được luồng đi.");
                _lblLuong.Text = "Không truy vết được luồng đi của LOT " + lot + ": " + ex.Message;
                _lblLuong.Appearance.ForeColor = Color.Firebrick;
            }
            finally
            {
                if (!IsDisposed)
                    Cursor = previous;
            }
        }

        // SL mặc định khi tick: phần khách báo còn chưa phân bổ, tối đa bằng SL đã giao của lần đó.
        private int MacDinhSoLuongTra(DongDoiChieu row)
        {
            if (_soLuongKhachBao <= 0)
                return row.SoLuongDaGiao;

            int daPhanBo = _rows.Where(r => r != row && r.Chon).Sum(r => r.SoLuongTra);
            int conLai = _soLuongKhachBao - daPhanBo;

            return conLai > 0 ? Math.Min(row.SoLuongDaGiao, conLai) : 0;
        }

        private void View_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            var row = _view.GetRow(e.RowHandle) as DongDoiChieu;

            if (row == null)
                return;

            if (e.Column.FieldName == nameof(DongDoiChieu.Chon))
            {
                row.SoLuongTra = row.Chon ? MacDinhSoLuongTra(row) : 0;
                _view.RefreshRow(e.RowHandle);
            }
            else if (e.Column.FieldName == nameof(DongDoiChieu.SoLuongTra) && row.SoLuongTra > 0 && !row.Chon)
            {
                row.Chon = true;
                _view.RefreshRow(e.RowHandle);
            }

            CapNhatTong();
        }

        private void View_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (e.Column.FieldName != nameof(DongDoiChieu.SoLuongTra))
                return;

            var row = _view.GetRow(e.RowHandle) as DongDoiChieu;

            if (row != null && row.Chon && row.SoLuongTra > row.SoLuongDaGiao)
                e.Appearance.ForeColor = Color.Firebrick;
        }

        private void CapNhatTong()
        {
            int tong = _rows.Where(r => r.Chon).Sum(r => r.SoLuongTra);
            int soLan = _rows.Count(r => r.Chon);

            string text = $"Đã chọn {soLan} lần giao, tổng SL trả: {tong:n0}";

            if (_soLuongKhachBao > 0)
            {
                text += $"   |   Khách báo trả: {_soLuongKhachBao:n0}";

                int lech = tong - _soLuongKhachBao;
                if (soLan > 0 && lech != 0)
                    text += $"   |   Chênh lệch: {lech:+#,0;-#,0}";
            }

            _lblTong.Text = text;
            _lblTong.Appearance.ForeColor =
                _soLuongKhachBao > 0 && soLan > 0 && tong != _soLuongKhachBao
                    ? Color.DarkOrange
                    : Color.DimGray;
        }

        private void XacNhan()
        {
            if (_mode == ChedoDoiChieuPhieuGiao.ChiXemLuongDi)
            {
                if (string.IsNullOrWhiteSpace(LuongDiTomTat))
                {
                    XtraMessageBox.Show(
                        "Chưa có luồng đi của LOT để ghi vào dòng. Nhập LOT rồi tra cứu trước.",
                        "Chưa có luồng đi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                LotNo = _txtLot.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _view.CloseEditor();
            _view.UpdateCurrentRow();

            var chon = _rows.Where(r => r.Chon).ToList();

            if (chon.Count == 0)
            {
                XtraMessageBox.Show(
                    "Chọn ít nhất một lần giao mà khách đã trả.",
                    "Chưa chọn phiếu giao",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (chon.Any(r => r.SoLuongTra <= 0))
            {
                XtraMessageBox.Show(
                    "Mỗi lần giao đã chọn phải có SL khách trả lớn hơn 0.",
                    "Số lượng không hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // SL trả được phép KHÁC SL đã giao → chỉ cảnh báo, không chặn.
            var vuot = chon.Where(r => r.SoLuongTra > r.SoLuongDaGiao).ToList();

            if (vuot.Count > 0 &&
                XtraMessageBox.Show(
                    $"{vuot.Count} lần giao có SL khách trả LỚN HƠN SL đã giao.\r\n" +
                    "Vẫn tiếp tục gán?",
                    "SL trả lớn hơn SL đã giao",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int tong = chon.Sum(r => r.SoLuongTra);

            if (_soLuongKhachBao > 0 && tong != _soLuongKhachBao &&
                XtraMessageBox.Show(
                    $"Tổng SL trả theo các lần giao ({tong:n0}) khác SL khách báo ({_soLuongKhachBao:n0}).\r\n" +
                    "Dòng khách trả sẽ lấy theo tổng SL đã phân bổ. Tiếp tục?",
                    "Chênh lệch số lượng",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            KetQua = chon.Select(r => new PhieuGiaoChon
            {
                Phieu = r.Phieu,
                SoLuongDaGiao = r.SoLuongDaGiao,
                SoLuongTra = r.SoLuongTra
            }).ToList();

            LotNo = _txtLot.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
