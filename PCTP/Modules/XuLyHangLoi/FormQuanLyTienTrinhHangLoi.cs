using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.KhoVatLy;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.Shared.Enums;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.XuLyHangLoi
{
    public partial class FormQuanLyTienTrinhHangLoi : XtraForm
    {
        private readonly int? _preselectPhieuXuLyId;
        private readonly IKhachTraHangService _khachTraHangService;
        private readonly ITraNoiBoService _traNoiBoService;
        private readonly IQTChungService _qtChungService;
        private readonly IReworkStockService _reworkStockService;
        private readonly IGiaoBuNGService _giaoBuNGService;
        private readonly ISlotService _slotService;
        private readonly IPhieuTraHangRepository _phieuTraHangRepo;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly ITraHangQTChungRepository _qtChungRepo;
        private readonly IPhieuGiaoRepository _phieuGiaoRepo;
        private readonly IAffectedLotTraceService _affectedLotTraceService;
        private readonly IInitialQCService _initialQCService;
        private readonly INoiBoLookupRepository _noiBoLookupRepo;
        private readonly Button[] _stepButtons = new Button[8];
        private int _activeStep = 1;
        private GridControl _grid;
        private GridView _gridView;
        private TextEdit _txtSearch;
        private SimpleButton _btnSecondary;
        private SimpleButton _btnPrimary;
        private SimpleButton _btnCreateXuLy;
        private SimpleButton _btnExport;
        private LabelControl _lblHint;
        private List<WorkflowRow> _rows = new List<WorkflowRow>();
        // Đang hiển thị kết quả quét/tìm (mọi bước) → double-click mở phiếu đã tạo.
        private bool _searchMode;

        public FormQuanLyTienTrinhHangLoi(
            IKhachTraHangService khachTraHangService,
            ITraNoiBoService traNoiBoService,
            IQTChungService qtChungService,
            IReworkStockService reworkStockService,
            IGiaoBuNGService giaoBuNGService,
            IPhieuTraHangRepository phieuTraHangRepo,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            ITraHangQTChungRepository qtChungRepo,
            IPhieuGiaoRepository phieuGiaoRepo,
            ISlotService slotService,
            int? preselectPhieuXuLyId,
            IAffectedLotTraceService affectedLotTraceService = null,
            IInitialQCService initialQCService = null,
            INoiBoLookupRepository noiBoLookupRepo = null)
        {
            _khachTraHangService = khachTraHangService ?? throw new ArgumentNullException(nameof(khachTraHangService));
            _traNoiBoService = traNoiBoService ?? throw new ArgumentNullException(nameof(traNoiBoService));
            _qtChungService = qtChungService ?? throw new ArgumentNullException(nameof(qtChungService));
            _reworkStockService = reworkStockService ?? throw new ArgumentNullException(nameof(reworkStockService));
            _giaoBuNGService = giaoBuNGService ?? throw new ArgumentNullException(nameof(giaoBuNGService));
            _phieuTraHangRepo = phieuTraHangRepo ?? throw new ArgumentNullException(nameof(phieuTraHangRepo));
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
            _qtChungRepo = qtChungRepo ?? throw new ArgumentNullException(nameof(qtChungRepo));
            _phieuGiaoRepo = phieuGiaoRepo ?? throw new ArgumentNullException(nameof(phieuGiaoRepo));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _preselectPhieuXuLyId = preselectPhieuXuLyId;
            _affectedLotTraceService = affectedLotTraceService;
            _initialQCService = initialQCService;
            _noiBoLookupRepo = noiBoLookupRepo;
            BuildUI();
            RefreshAll();
            SetActiveStep(ResolveInitialStep());
            FocusPreselectedRow();
        }

        private void BuildUI()
        {
            Text = "Quản lý tiến trình Xử lý hàng lỗi";
            Size = new Size(1500, 850);
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var timeline = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), WrapContents = false, AutoScroll = true, BackColor = Color.FromArgb(240, 243, 246) };
            var labels = new[] { "1. Tiếp nhận", "2. Tạo phiếu", "3. Truy vết LOT", "4. QC định hướng", "5. Initial QC", "6. Rework", "7. QC Rework / Disposition", "8. Giao bù / Hoàn tất" };
            for (int i = 0; i < labels.Length; i++)
            {
                int step = i + 1;
                var button = new Button { Width = 160, Height = 80, Text = labels[i] + "\r\n(0)", Tag = step, FlatStyle = FlatStyle.Flat, Margin = new Padding(3), Font = new Font("Tahoma", 9F, FontStyle.Bold), BackColor = Color.White };
                button.Click += (s, e) => { _searchMode = false; _txtSearch.Text = string.Empty; SetActiveStep((int)((Button)s).Tag); };
                _stepButtons[i] = button;
                timeline.Controls.Add(button);
                if (i < labels.Length - 1)
                    timeline.Controls.Add(new LabelControl { Text = "▶", Width = 22, Height = 70, Padding = new Padding(2, 25, 2, 0), Appearance = { Font = new Font("Tahoma", 11F, FontStyle.Bold), ForeColor = Color.Gray } });
            }
            root.Controls.Add(timeline, 0, 0);

            var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, Padding = new Padding(3) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            _txtSearch = new TextEdit { Dock = DockStyle.Fill };
            _txtSearch.Properties.NullValuePrompt = "Tìm số phiếu, model, mã hàng, LOT...";
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; ExecuteSearch(); } };
            toolbar.Controls.Add(_txtSearch, 0, 0);
            _btnSecondary = new SimpleButton { Text = "Tạo phiếu nội bộ", Dock = DockStyle.Fill };
            _btnSecondary.Click += (s, e) => TaoPhieuNoiBo();
            toolbar.Controls.Add(_btnSecondary, 1, 0);
            var btnSlot = new SimpleButton { Text = "Tạo từ Slot", Dock = DockStyle.Fill };
            btnSlot.Click += (s, e) => TaoPhieuTuSlot();
            toolbar.Controls.Add(btnSlot, 2, 0);
            _btnPrimary = new SimpleButton { Dock = DockStyle.Fill };
            _btnPrimary.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            _btnPrimary.Click += BtnActionPrimary_Click;
            toolbar.Controls.Add(_btnPrimary, 3, 0);
            _btnCreateXuLy = new SimpleButton { Text = "Tạo phiếu bất thường", Dock = DockStyle.Fill };
            _btnCreateXuLy.Click += (s, e) => TaoPhieuXuLyBatThuong();
            toolbar.Controls.Add(_btnCreateXuLy, 4, 0);
            _btnExport = new SimpleButton { Text = "Xuất Excel", Dock = DockStyle.Fill };
            _btnExport.Click += (s, e) => _gridView.ExportToXlsx($"HangLoi_Buoc{_activeStep}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            toolbar.Controls.Add(_btnExport, 5, 0);
            root.Controls.Add(toolbar, 0, 1);

            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.RowAutoHeight = true;
            _gridView.DoubleClick += (s, e) => OnGridDoubleClick();
            root.Controls.Add(_grid, 0, 2);
            _lblHint = new LabelControl { Dock = DockStyle.Fill, Padding = new Padding(10, 5, 0, 0), Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Italic), ForeColor = Color.DimGray } };
            root.Controls.Add(_lblHint, 0, 3);
            Controls.Add(root);
        }

        private int ResolveInitialStep()
        {
            if (!_preselectPhieuXuLyId.HasValue) return 1;
            var p = _phieuXuLyRepo.GetById(_preselectPhieuXuLyId.Value);
            return p == null ? 1 : ResolveStep(p);
        }

        private int ResolveStep(PhieuXuLyBatThuong p)
        {
            var status = _phieuXuLyRepo.GetStatus(p.Id) ?? p.Status;

            // Phiếu đã hủy không còn nằm trong hàng đợi của bất kỳ bước xử lý nào.
            if (status == QTChungStatus.Huy) return 0;
            if (status == QTChungStatus.DaTaoPhieuBatThuong)
            {
                var snapshot = _affectedLotTraceService == null ? null : _affectedLotTraceService.GetSnapshot(p.Id);
                return snapshot == null || snapshot.Count == 0 ? 3 : 4;
            }
            if (status == QTChungStatus.DaDinhHuong)
            {
                var initial = _initialQCService == null ? null : _initialQCService.Get(p.Id);
                return initial == null ? 5 : (p.HuongXuLy == HuongXuLyBatThuong.CanRework && initial.SoLuongRework > 0 ? 6 : 8);
            }
            if (status == QTChungStatus.DaXuatKhoRework) return 6;
            // Sau khi đã giao Rework cho SX, phiếu phải xuất hiện ở bước 7 để QC xác nhận kết quả.
            if (status == QTChungStatus.DaGiaoSanXuat) return p.HuongXuLy == HuongXuLyBatThuong.CanRework ? 7 : 6;
            if (status == QTChungStatus.DaQCXacNhanCuoi)
            {
                var qc = _qtChungRepo.GetQC(p.Id);
                return qc != null && qc.SoLuongNG > 0 ? 7 : 8;
            }
            if (status == QTChungStatus.DaNhapLaiKho || status == QTChungStatus.ChoGiaoBu || status == QTChungStatus.DaGiaoBu || status == QTChungStatus.TuChoiGiaoBu) return 8;
            return 2;
        }

        private void SetActiveStep(int step)
        {
            _activeStep = Math.Max(1, Math.Min(8, step));
            for (int i = 0; i < _stepButtons.Length; i++) _stepButtons[i].BackColor = i + 1 == _activeStep ? Color.LightSteelBlue : Color.White;
            switch (_activeStep)
            {
                case 1: _btnSecondary.Visible = true; _btnPrimary.Text = "Tiếp nhận phiếu khách"; _lblHint.Text = "Bước 1: danh sách phiếu đã tiếp nhận (khách trả / nội bộ) chưa tạo phiếu bất thường. Bấm đúp để mở phiếu; chọn dòng rồi 'Tạo phiếu bất thường' để sang bước 2."; break;
                case 2: _btnSecondary.Visible = true; _btnPrimary.Text = "Tạo phiếu bất thường"; _lblHint.Text = "Bước 2: phiếu trả còn dòng chưa tạo PhieuXuLyBatThuong (đã tạo một phần). Chọn dòng rồi 'Tạo phiếu bất thường'."; break;
                case 3: _btnSecondary.Visible = false; _btnPrimary.Text = "Truy vết LOT"; _lblHint.Text = "Bước 3: truy vết kho thành phẩm, WIP/sản xuất và khách trả; snapshot trước khi QC."; break;
                case 4: _btnSecondary.Visible = false; _btnPrimary.Text = "QC định hướng"; _lblHint.Text = "Bước 4: QC chọn Từ chối giao bù / Chỉ giao bù / Có Rework."; break;
                case 5: _btnSecondary.Visible = false; _btnPrimary.Text = "Initial QC"; _lblHint.Text = "Bước 5: kiểm đủ snapshot. DaKiemTra = OK + NG; NG = Rework + LoaiBoBanDau."; break;
                case 6: _btnSecondary.Visible = false; _btnPrimary.Text = "Xuất / Giao Rework"; _lblHint.Text = "Bước 6: chỉ nhánh CanRework; xuất không vượt Initial QC Rework, giao sản xuất. 'Ghi nhận tiến độ Rework' chỉ ghi chú, không xác nhận sửa xong. Sửa xong thì đóng form Rework và chọn bước 7."; break;
                case 7: _btnSecondary.Visible = false; _btnPrimary.Text = "QC Rework / Disposition"; _lblHint.Text = "Bước 7: QC Rework OK + NG = lượng Rework; NG Rework cộng vào loại bỏ cuối."; break;
                case 8: _btnSecondary.Visible = false; _btnPrimary.Text = "Giao bù / Hoàn tất"; _lblHint.Text = "Bước 8: Giao bù là nghĩa vụ độc lập; không suy ra từ NG/Rework."; break;
            }
            _btnCreateXuLy.Visible = _activeStep == 1 || _activeStep == 2;
            LoadCurrentStep();
        }

        private void RefreshAll() { RefreshBadges(); LoadCurrentStep(); }

        private List<PhieuTraHang> GetHeaders()
        {
            // Tiến trình phải đọc đủ phiếu trả, không chỉ các header còn nằm trong
            // hàng đợi "chờ xử lý" của hai service. Nếu service đổi trạng thái header
            // sau khi tạo phiếu bất thường, chỉ đọc GetChoXuLy() sẽ làm mất phiếu
            // cha khỏi dashboard trong khi các phiếu con vẫn tồn tại.
            var list = new List<PhieuTraHang>();
            list.AddRange(_phieuTraHangRepo.GetByNguon(NguonXuLyBatThuong.KhachTra) ?? new List<PhieuTraHang>());
            list.AddRange(_phieuTraHangRepo.GetByNguon(NguonXuLyBatThuong.TraNoiBo) ?? new List<PhieuTraHang>());
            return list.Where(x => x != null).GroupBy(x => x.Id).Select(g => g.First()).ToList();
        }

        private void RefreshBadges()
        {
            try
            {
                var counts = new int[8];
                foreach (var row in BuildWorkflowRows())
                {
                    var step = ResolveRowStep(row);
                    if (step >= 1 && step <= 8) counts[step - 1]++;
                }
                var labels = new[] { "1. Tiếp nhận", "2. Tạo phiếu", "3. Truy vết LOT", "4. QC định hướng", "5. Initial QC", "6. Rework", "7. QC Rework / Disposition", "8. Giao bù / Hoàn tất" };
                for (int i = 0; i < 8; i++) _stepButtons[i].Text = labels[i] + "\r\n(" + counts[i] + ")";
            }
            catch (Exception ex) { ShowWarning("Không thể cập nhật tiến trình.\r\n\r\n" + ex.Message); }
        }

        private int ResolveRowStep(WorkflowRow row)
        {
            if (!row.PhieuXuLyId.HasValue) return row.HeaderStep;
            var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
            return p == null ? 2 : ResolveStep(p);
        }

        private void LoadCurrentStep()
        {
            if (_searchMode && !string.IsNullOrWhiteSpace(_txtSearch.Text))
            {
                ShowSearchResults();
                return;
            }

            _searchMode = false;

            try
            {
                _rows = BuildWorkflowRows().Where(x => ResolveRowStep(x) == _activeStep).ToList();
                _grid.DataSource = _rows;
                ConfigureGrid();
                ApplyFilter();
            }
            catch (Exception ex) { ShowWarning("Không thể tải dữ liệu tiến trình.\r\n\r\n" + ex.Message); }
        }

        private List<WorkflowRow> BuildWorkflowRows()
        {
            var result = new List<WorkflowRow>();
            foreach (var header in GetHeaders())
                result.AddRange(BuildRows(header, null));
            return result;
        }

        /// <summary>
        /// Đơn vị đếm được quy định rõ:
        ///  - Bước 1: một phiếu trả hàng (header) chưa có phiếu xử lý con nào.
        ///  - Bước 2: một dòng PhieuTraHangCT chưa có phiếu xử lý; tạo phiếu chỉ cho dòng được chọn.
        ///  - Bước 3..8: mỗi PhieuXuLyBatThuong là một công việc độc lập.
        /// Nhờ vậy tạo một phần không làm các dòng còn lại bị gộp/đếm sai.
        /// </summary>
        private List<WorkflowRow> BuildRows(PhieuTraHang header, List<PhieuTraHangCT> items)
        {
            var rows = new List<WorkflowRow>();
            if (items == null) items = _phieuTraHangRepo.GetItems(header.Id) ?? new List<PhieuTraHangCT>();

            var xuLyList = _phieuXuLyRepo.GetAllByPhieuTraHangId(header.Id) ?? new List<PhieuXuLyBatThuong>();

            foreach (var p in xuLyList)
            {
                var row = NewRow(header);
                row.PhieuXuLyId = p.Id;
                row.Model = p.Model;
                row.PhanLoaiXuLy = p.PhanLoaiXuLy;
                row.HuongXuLy = p.HuongXuLy.ToString();
                row.QTStatus = _phieuXuLyRepo.GetStatus(p.Id) ?? p.Status;
                row.SoLuongLoi = p.SoLuongLoi;
                row.MaSanPham = p.MaSanPham;
                row.SoLo = p.SoLo;
                row.SoPhieuXuLy = p.SoPhieu;
                if (_initialQCService != null)
                {
                    var initial = _initialQCService.Get(p.Id);
                    if (initial != null) row.InitialQC = $"QC {initial.SoLuongDaKiemTra:n0}/{initial.SoLuongAnhHuong:n0} | OK {initial.SoLuongOK:n0} | NG {initial.SoLuongNG:n0} | RW {initial.SoLuongRework:n0} | Loại bỏ {initial.SoLuongLoaiBoBanDau:n0}";
                }
                rows.Add(row);
            }

            var chuaIds = new HashSet<int>(_phieuTraHangRepo.GetItemIdsChuaCoPhieuXuLy(header.Id) ?? new List<int>());
            var chua = items.Where(i => chuaIds.Contains(i.Id)).ToList();

            if (chua.Count > 0 && xuLyList.Count == 0)
            {
                // Bước 1 đếm theo phiếu tiếp nhận, không đếm từng dòng hàng.
                var row = NewRow(header);
                row.HeaderStep = 1;
                row.MaSanPham = string.Join(", ", chua.Select(i => i.MaHang).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                row.SoLo = string.Join(", ", chua.Select(i => i.LotNo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                row.SoLuongLoi = chua.Sum(i => i.SoLuong);
                row.SoDongChuaXuLy = chua.Count;
                rows.Add(row);
            }
            else if (chua.Count > 0)
            {
                // Bước 2 đếm theo từng dòng còn thiếu phiếu xử lý; giữ CT.Id để tạo đúng dòng.
                foreach (var item in chua)
                {
                    var row = NewRow(header);
                    row.HeaderStep = 2;
                    row.PhieuTraHangCTId = item.Id;
                    row.MaSanPham = item.MaHang;
                    row.SoLo = item.LotNo;
                    row.SoLuongLoi = item.SoLuong;
                    row.Model = item.MaHang;
                    row.SoDongChuaXuLy = 1;
                    rows.Add(row);
                }
            }

            return rows;
        }

        private static WorkflowRow NewRow(PhieuTraHang header)
        {
            return new WorkflowRow { PhieuTraHangId = header.Id, SoPhieu = header.SoPhieu, PhieuTraHangStatus = header.Status, Nguon = header.Nguon.ToString(), BoPhanPhatHienLoi = header.BoPhanPhatHienLoi };
        }

        private void OnGridDoubleClick()
        {
            var handle = _gridView.FocusedRowHandle;
            var row = handle < 0 ? null : _gridView.GetRow(handle) as WorkflowRow;

            // Kết quả tìm kiếm, hoặc dòng cấp phiếu trả (Bước 1/2) → mở chính phiếu đã tạo.
            if (_searchMode || (row != null && !row.PhieuXuLyId.HasValue))
                MoPhieuDaTao();
            else
                ExecuteActionByStep(_activeStep);
        }

        // ============================================================
        // QUÉT / TÌM PHIẾU THEO MÃ HÀNG, LOT, SỐ PHIẾU (mọi bước, kể cả phiếu đã đóng)
        // ============================================================

        private void ExecuteSearch()
        {
            var kw = _txtSearch.Text == null ? string.Empty : _txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(kw))
            {
                _searchMode = false;
                LoadCurrentStep();
                return;
            }
            _searchMode = true;
            ShowSearchResults();
        }

        private static List<string> TachTuKhoa(string kw)
        {
            // Tem QR FCC: Lot:MãHàng:Ngày:SL:... → lấy Lot và Mã hàng (bỏ phần ngắn/ngày).
            if (kw.IndexOf(':') >= 0)
            {
                var parts = kw.Split(':');
                var tokens = new List<string>();
                if (parts.Length > 0 && parts[0].Trim().Length >= 4) tokens.Add(parts[0].Trim());
                if (parts.Length > 1 && parts[1].Trim().Length >= 4) tokens.Add(parts[1].Trim());
                if (tokens.Count > 0) return tokens;
            }
            return new List<string> { kw };
        }

        private static bool Khop(string token, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // Lot trên tem có thể dài hơn lot lưu trong Slot/phiếu.
            return value.Length >= 4 && token.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ShowSearchResults()
        {
            try
            {
                var kw = _txtSearch.Text.Trim();
                var tokens = TachTuKhoa(kw);

                var headers = new List<PhieuTraHang>();
                headers.AddRange(_phieuTraHangRepo.GetByNguon(NguonXuLyBatThuong.TraNoiBo) ?? new List<PhieuTraHang>());
                headers.AddRange(_phieuTraHangRepo.GetByNguon(NguonXuLyBatThuong.KhachTra) ?? new List<PhieuTraHang>());

                var found = new List<WorkflowRow>();
                foreach (var header in headers.GroupBy(x => x.Id).Select(g => g.First()))
                {
                    var items = _phieuTraHangRepo.GetItems(header.Id) ?? new List<PhieuTraHangCT>();
                    var rowsOfHeader = BuildRows(header, items);

                    bool match = tokens.Any(t =>
                        Khop(t, header.SoPhieu) ||
                        items.Any(i => Khop(t, i.MaHang) || Khop(t, i.LotNo)) ||
                        rowsOfHeader.Any(r => Khop(t, r.Model) || Khop(t, r.MaSanPham) || Khop(t, r.SoLo)));

                    if (match) found.AddRange(rowsOfHeader);
                }

                _rows = found;
                _grid.DataSource = _rows;
                ConfigureGrid();
                _gridView.ActiveFilterString = string.Empty;

                _lblHint.Text = found.Count == 0
                    ? "Không tìm thấy phiếu nào khớp mã hàng / LOT / số phiếu vừa quét."
                    : $"Tìm thấy {found.Count} phiếu - bấm đúp để mở phiếu (sửa / xóa nếu chưa đóng, chỉ xem nếu đã đóng).";
            }
            catch (Exception ex) { ShowWarning("Không thể tìm phiếu.\r\n\r\n" + ex.Message); }
        }

        // Double-click kết quả tìm kiếm → mở đúng loại phiếu với đầy đủ thông tin đã tạo.
        private void MoPhieuDaTao()
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0) return;
            var row = _gridView.GetRow(handle) as WorkflowRow;
            if (row == null) return;

            var header = _phieuTraHangRepo.GetById(row.PhieuTraHangId);
            if (header == null) { ShowWarning("Không tìm thấy phiếu."); return; }

            try
            {
                if (header.Nguon == NguonXuLyBatThuong.TraNoiBo)
                {
                    if (_noiBoLookupRepo == null) { ShowWarning("MainApp chưa inject INoiBoLookupRepository."); return; }
                    using (var f = new FormTaoPhieuTraNoiBo(_traNoiBoService, _noiBoLookupRepo, _slotService, _phieuTraHangRepo, header.Id))
                        if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
                }
                else
                {
                    using (var f = new FormTiepNhanPhieuKhachTra(_khachTraHangService, _phieuTraHangRepo, header.Id, _noiBoLookupRepo, _slotService))
                        if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
                }
            }
            catch (Exception ex) { ShowWarning("Không thể mở phiếu.\r\n\r\n" + ex.Message); }
        }

        private void ConfigureGrid()
        {
            _gridView.Columns.Clear();
            AddColumn("PhieuTraHangId", "PT Id", false);
            AddColumn("PhieuXuLyId", "QT Id", false);
            AddColumn("SoPhieu", "Số phiếu", true, 120);
            AddColumn("Nguon", "Nguồn", true, 100);
            AddColumn("Model", "Model", true, 90);
            AddColumn("MaSanPham", "Mã sản phẩm", true, 140);
            AddColumn("SoLo", "LOT", true, 110);
            AddColumn("SoLuongLoi", "SL lỗi", true, 75);
            AddColumn("PhanLoaiXuLy", "Phân loại", true, 120);
            AddColumn("HuongXuLy", "Hướng xử lý", true, 130);
            AddColumn("QTStatus", "Workflow", true, 150);
            AddColumn("InitialQC", "Initial QC", true, 320);
            AddColumn("BoPhanPhatHienLoi", "Bộ phận phát hiện", true, 150);
            _gridView.BestFitColumns();
        }

        private void AddColumn(string field, string caption, bool visible, int width = 100)
        {
            _gridView.Columns.Add(new DevExpress.XtraGrid.Columns.GridColumn { FieldName = field, Caption = caption, Visible = visible, Width = width });
        }

        private void ApplyFilter()
        {
            var kw = _txtSearch.Text == null ? string.Empty : _txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(kw)) { _gridView.ActiveFilterString = string.Empty; return; }
            var safe = kw.Replace("'", "''");
            _gridView.ActiveFilterString = $"Contains([SoPhieu], '{safe}') Or Contains([Model], '{safe}') Or Contains([MaSanPham], '{safe}') Or Contains([SoLo], '{safe}') Or Contains([Nguon], '{safe}') Or Contains([HuongXuLy], '{safe}')";
        }

        private void BtnActionPrimary_Click(object sender, EventArgs e) { ExecuteActionByStep(_activeStep); }

        private void ExecuteActionByStep(int step)
        {
            switch (step)
            {
                case 1: TiepNhanKhach(); break;
                case 2: TaoPhieuXuLyBatThuong(); break;
                case 3: TruyVetLOT(); break;
                case 4: QCDinhHuong(); break;
                case 5: InitialQC(); break;
                case 6: XuLyRework(); break;
                case 7: QCReworkDisposition(); break;
                case 8: XuLyBuocCuoi(); break;
            }
        }

        private WorkflowRow GetFocusedRow()
        {
            var handle = _gridView.FocusedRowHandle;
            if (handle < 0) { ShowWarning("Vui lòng chọn một dòng."); return null; }
            return _gridView.GetRow(handle) as WorkflowRow;
        }

        private void TiepNhanKhach()
        {
            using (var f = new FormTiepNhanPhieuKhachTra(_khachTraHangService, _noiBoLookupRepo, _slotService)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterIntake();
        }

        private void TaoPhieuNoiBo()
        {
            if (_noiBoLookupRepo == null) { ShowWarning("MainApp chưa inject INoiBoLookupRepository."); return; }
            using (var f = new FormTaoPhieuTraNoiBo(_traNoiBoService, _noiBoLookupRepo, _slotService)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterIntake();
        }

        private void TaoPhieuTuSlot()
        {
            if (_noiBoLookupRepo == null) { ShowWarning("MainApp chưa inject INoiBoLookupRepository."); return; }

            // Bước tắt: chọn nhanh Slot/LOT → mở ĐÚNG form "Tạo phiếu trả nội bộ" với các dòng đã điền sẵn.
            // Header (Phòng ban, BP phát hiện lỗi, Lý do, Số phiếu) và việc lưu do FormTaoPhieuTraNoiBo xử lý.
            List<SlotNguonChon> chon;
            using (var pick = new FormChonSlotNoiBo(_slotService))
            {
                if (pick.ShowDialog(this) != DialogResult.OK) return;
                chon = pick.KetQua;
            }

            using (var f = new FormTaoPhieuTraNoiBo(_traNoiBoService, _noiBoLookupRepo, _slotService))
            {
                f.ThemDongTuSlot(chon);
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterIntake();
            }
        }

        private void TaoPhieuXuLyBatThuong()
        {
            var row = GetFocusedRow();
            if (row == null) return;

            if (row.PhieuXuLyId.HasValue)
            {
                ShowWarning("Dòng này đã là phiếu xử lý bất thường. Hãy chọn dòng phiếu trả còn ở Bước 1/2.");
                return;
            }

            // Model KHÔNG bắt buộc: mặc định theo mã hàng; có thể nhập Model thực tế.
            string defaultModel = (row.MaSanPham ?? string.Empty).Split(',')[0].Trim();
            string model = XtraInputBox.Show(
                "Model của hàng lỗi (để trống = dùng mã hàng):",
                $"Tạo phiếu bất thường - {row.SoPhieu}",
                defaultModel);

            if (model == null) return; // người dùng bấm Cancel

            try
            {
                if (row.HeaderStep == 2)
                {
                    if (!row.PhieuTraHangCTId.HasValue)
                        throw new InvalidOperationException("Dòng bước 2 không có PhieuTraHangCTId; hãy tải lại danh sách.");
                    int id = _qtChungService.TaoPhieuXuLyBatThuong(
                        row.PhieuTraHangCTId.Value, model, "Hàng lỗi", null, Environment.UserName);
                    XtraMessageBox.Show(this, $"Đã tạo 1 phiếu xử lý bất thường (Id={id}) cho dòng hàng của phiếu {row.SoPhieu}.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    var ids = _qtChungService.TaoPhieuXuLyTuPhieuTra(row.PhieuTraHangId, model, "Hàng lỗi", Environment.UserName);
                    XtraMessageBox.Show(this, $"Đã tạo {ids.Count} phiếu xử lý bất thường cho phiếu {row.SoPhieu}.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                RefreshAfterAction();
            }
            catch (Exception ex) { ShowWarning("Không thể tạo phiếu xử lý bất thường.\r\n\r\n" + ex.Message); }
        }

        private void TruyVetLOT()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            if (_affectedLotTraceService == null) { ShowWarning("MainApp chưa inject IAffectedLotTraceService."); return; }
            var result = _affectedLotTraceService.TruyVetLOT(row.PhieuXuLyId.Value, Environment.UserName);
            var message = $"LOT: {result.LotNo}\r\nMã hàng: {result.MaSanPham}\r\nTổng ảnh hưởng: {result.TotalAffectedQuantity:n0}\r\nSnapshot: {result.Items.Count} dòng\r\n" + (result.Warnings.Count == 0 ? "Truy vết hoàn tất." : "Cảnh báo:\r\n- " + string.Join("\r\n- ", result.Warnings));
            XtraMessageBox.Show(this, message, result.IsComplete ? "Truy vết LOT" : "Truy vết chưa hoàn tất", MessageBoxButtons.OK, result.IsComplete ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            RefreshAfterAction();
        }

        private void QCDinhHuong()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            using (var f = new FormQCDinhHuong(_qtChungService, row.PhieuXuLyId.Value)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void InitialQC()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            if (_initialQCService == null || _affectedLotTraceService == null) { ShowWarning("MainApp chưa inject InitialQCService/AffectedLotTraceService."); return; }
            using (var f = new FormInitialQC(_initialQCService, _affectedLotTraceService, row.PhieuXuLyId.Value)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void XuLyRework()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
            if (p == null) { ShowWarning("Không tìm thấy phiếu xử lý."); return; }
            if (p.HuongXuLy != HuongXuLyBatThuong.CanRework) { ShowWarning("Phiếu không thuộc nhánh Rework."); return; }
            if (_initialQCService == null || _initialQCService.Get(p.Id) == null) { ShowWarning("Chưa xác nhận Initial QC."); return; }
            using (var f = new FormReworkProcess(_qtChungService, _reworkStockService, row.PhieuXuLyId.Value, _noiBoLookupRepo, _qtChungRepo)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void QCReworkDisposition()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
            if (p == null) return;
            var qc = _qtChungRepo.GetQC(p.Id);
            if (qc == null)
            {
                using (var f = new FormQCXacNhanCuoi(_qtChungService, p.Id, _slotService)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
                return;
            }
            if (qc.SoLuongNG > 0)
            {
                using (var f = new FormNhapLaiHangNG(_reworkStockService, p.Id, qc.SoLuongNG, _slotService)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
                return;
            }
            var result = _qtChungService.HoanTat(p.Id, Environment.UserName);
            ShowScanResult(result);
            RefreshAfterAction();
        }

        private void XuLyBuocCuoi()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
            if (p == null) return;
            switch (p.HuongXuLy)
            {
                case HuongXuLyBatThuong.ChiGiaoBu: XuLyGiaoBu(row); break;
                case HuongXuLyBatThuong.TuChoiGiaoBu:
                    var result = _qtChungService.HoanTat(p.Id, Environment.UserName);
                    ShowScanResult(result);
                    RefreshAfterAction();
                    break;
                case HuongXuLyBatThuong.CanRework:
                    var qc = _qtChungRepo.GetQC(p.Id);
                    if (qc != null && qc.SoLuongNG > 0)
                    {
                        using (var f = new FormNhapLaiHangNG(_reworkStockService, p.Id, qc.SoLuongNG, _slotService)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
                    }
                    else
                    {
                        var done = _qtChungService.HoanTat(p.Id, Environment.UserName);
                        ShowScanResult(done);
                        RefreshAfterAction();
                    }
                    break;
                default: ShowWarning("Phiếu chưa có hướng xử lý hợp lệ."); break;
            }
        }

        private void XuLyGiaoBu(WorkflowRow row)
        {
            var phieu = _phieuTraHangRepo.GetById(row.PhieuTraHangId);
            if (phieu == null) { ShowWarning("Không tìm thấy phiếu trả hàng."); return; }
            using (var f = new FormGiaoBuNG(_giaoBuNGService, phieu.Id, phieu.SoPhieu)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        // Phiếu mới tiếp nhận (khách trả / nội bộ / từ Slot) → về Bước 1 và hiện đầy đủ danh sách.
        private void RefreshAfterIntake()
        {
            _searchMode = false;
            _txtSearch.Text = string.Empty;
            RefreshBadges();
            SetActiveStep(1);
        }

        private void RefreshAfterAction()
        {
            int? selected = null;
            var row = _gridView == null ? null : _gridView.GetFocusedRow() as WorkflowRow;
            if (row != null) selected = row.PhieuXuLyId;
            RefreshBadges();
            if (selected.HasValue)
            {
                var p = _qtChungService.GetById(selected.Value);
                if (p != null) _activeStep = ResolveStep(p);
            }
            LoadCurrentStep();
            for (int i = 0; i < _stepButtons.Length; i++) _stepButtons[i].BackColor = i + 1 == _activeStep ? Color.LightSteelBlue : Color.White;
        }

        private void FocusPreselectedRow()
        {
            if (!_preselectPhieuXuLyId.HasValue) return;
            var handle = _gridView.LocateByValue("PhieuXuLyId", _preselectPhieuXuLyId.Value);
            if (handle >= 0) _gridView.FocusedRowHandle = handle;
        }

        private void ShowWarning(string message) { XtraMessageBox.Show(this, message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        private void ShowScanResult(ScanResult result)
        {
            if (result == null) { ShowWarning("Không nhận được kết quả từ Service."); return; }
            XtraMessageBox.Show(this, result.Message, result.IsOK ? "Thành công" : "Không thể thực hiện", MessageBoxButtons.OK, result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private sealed class WorkflowRow
        {
            public int PhieuTraHangId { get; set; }
            public int? PhieuTraHangCTId { get; set; }
            public int? PhieuXuLyId { get; set; }
            public string SoPhieu { get; set; }
            public string SoPhieuXuLy { get; set; }
            public int SoDongChuaXuLy { get; set; }
            public string Nguon { get; set; }
            public string Model { get; set; }
            public string MaSanPham { get; set; }
            public string SoLo { get; set; }
            public int SoLuongLoi { get; set; }
            public string PhanLoaiXuLy { get; set; }
            public string HuongXuLy { get; set; }
            public string BoPhanPhatHienLoi { get; set; }
            public string InitialQC { get; set; }
            public PhieuTraHangStatus PhieuTraHangStatus { get; set; }
            public QTChungStatus QTStatus { get; set; }
            // Dòng cấp phiếu trả (chưa có PhieuXuLyId): 1 = Tiếp nhận, 2 = đã tạo một phần.
            public int HeaderStep { get; set; } = 1;
        }
    }

    /// <summary>
    /// Initial QC theo snapshot LOT. Đặt cùng source file để không phải thêm Compile Include vào csproj legacy.
    /// </summary>
    internal sealed class FormInitialQC : XtraForm
    {
        private readonly IInitialQCService _initialQCService;
        private readonly IAffectedLotTraceService _traceService;
        private readonly int _phieuXuLyId;
        private readonly DataGridView _grid;
        private readonly TextEdit _txtNoiDung;
        private readonly TextEdit _txtKetLuan;
        private readonly LabelControl _lblSummary;

        public FormInitialQC(IInitialQCService initialQCService, IAffectedLotTraceService traceService, int phieuXuLyId)
        {
            _initialQCService = initialQCService ?? throw new ArgumentNullException(nameof(initialQCService));
            _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
            if (phieuXuLyId <= 0) throw new ArgumentException("PhieuXuLyId không hợp lệ.", nameof(phieuXuLyId));
            _phieuXuLyId = phieuXuLyId;
            Text = "Initial QC - Xử lý hàng lỗi";
            Size = new Size(1250, 720);
            StartPosition = FormStartPosition.CenterParent;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            _lblSummary = new LabelControl { Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 10F, FontStyle.Bold) } };
            root.Controls.Add(_lblSummary, 0, 0);

            _grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect, EditMode = DataGridViewEditMode.EditOnEnter };
            AddTextColumn("AffectedLotId", "Id", 70, false);
            AddTextColumn("SourceType", "Nguồn", 100, false);
            AddTextColumn("LotNo", "LOT", 120, true);
            AddTextColumn("MaSanPham", "Mã hàng", 130, true);
            AddTextColumn("SoLuongAnhHuong", "Ảnh hưởng", 90, true);
            AddNumberColumn("SoLuongDaKiemTra", "Đã kiểm tra", 95);
            AddNumberColumn("SoLuongOK", "OK", 80);
            AddNumberColumn("SoLuongNG", "NG", 80);
            AddNumberColumn("SoLuongRework", "Rework", 90);
            AddNumberColumn("SoLuongLoaiBo", "Loại bỏ", 90);
            _grid.CellEndEdit += (s, e) => UpdateSummary();
            root.Controls.Add(_grid, 0, 1);
            _txtNoiDung = new TextEdit { Dock = DockStyle.Fill };
            _txtNoiDung.Properties.NullValuePrompt = "Nội dung kiểm tra...";
            root.Controls.Add(_txtNoiDung, 0, 2);
            _txtKetLuan = new TextEdit { Dock = DockStyle.Fill };
            _txtKetLuan.Properties.NullValuePrompt = "Kết luận QC...";
            root.Controls.Add(_txtKetLuan, 0, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new SimpleButton { Text = "Đóng", Width = 100 };
            cancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            var confirm = new SimpleButton { Text = "Xác nhận Initial QC", Width = 170 };
            confirm.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            confirm.Click += Confirm_Click;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(confirm);
            root.Controls.Add(buttons, 0, 4);
            Controls.Add(root);
            LoadSnapshot();
        }

        private void AddTextColumn(string name, string caption, int width, bool visible) { _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = caption, Width = width, Visible = visible, ReadOnly = true }); }
        private void AddNumberColumn(string name, string caption, int width) { _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = caption, Width = width, ValueType = typeof(int) }); }

        private void LoadSnapshot()
        {
            if (_initialQCService.Get(_phieuXuLyId) != null) throw new InvalidOperationException("Phiếu đã có Initial QC; không cho ghi đè kết quả đã xác nhận.");
            var snapshot = _traceService.GetSnapshot(_phieuXuLyId);
            if (snapshot == null || snapshot.Count == 0) throw new InvalidOperationException("Chưa có snapshot LOT. Hãy thực hiện Truy vết LOT trước.");
            foreach (var item in snapshot) _grid.Rows.Add(item.Id, item.SourceType.ToString(), item.LotNo, item.MaSanPham, item.SoLuongAnhHuong, 0, 0, 0, 0, 0);
            UpdateSummary();
        }

        private int CellInt(int row, string name)
        {
            int value;
            return int.TryParse(Convert.ToString(_grid.Rows[row].Cells[name].Value), out value) ? value : 0;
        }

        private void UpdateSummary()
        {
            int affected = 0, inspected = 0, ok = 0, ng = 0, rework = 0, scrap = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                affected += Convert.ToInt32(row.Cells["SoLuongAnhHuong"].Value);
                inspected += CellInt(row.Index, "SoLuongDaKiemTra");
                ok += CellInt(row.Index, "SoLuongOK");
                ng += CellInt(row.Index, "SoLuongNG");
                rework += CellInt(row.Index, "SoLuongRework");
                scrap += CellInt(row.Index, "SoLuongLoaiBo");
            }
            _lblSummary.Text = $"Ảnh hưởng: {affected:n0} | Đã kiểm tra: {inspected:n0} | OK: {ok:n0} | NG: {ng:n0} | Rework: {rework:n0} | Loại bỏ ban đầu: {scrap:n0}";
        }

        private void Confirm_Click(object sender, EventArgs e)
        {
            try
            {
                var list = new List<InitialQCLotResult>();
                foreach (DataGridViewRow row in _grid.Rows)
                    list.Add(new InitialQCLotResult { AffectedLotId = Convert.ToInt32(row.Cells["AffectedLotId"].Value), SoLuongDaKiemTra = CellInt(row.Index, "SoLuongDaKiemTra"), SoLuongOK = CellInt(row.Index, "SoLuongOK"), SoLuongNG = CellInt(row.Index, "SoLuongNG"), SoLuongRework = CellInt(row.Index, "SoLuongRework"), SoLuongLoaiBo = CellInt(row.Index, "SoLuongLoaiBo") });

                var result = _initialQCService.Confirm(_phieuXuLyId, list, _txtNoiDung.Text, _txtKetLuan.Text, Environment.UserName);
                XtraMessageBox.Show(this, $"Đã xác nhận Initial QC.\r\nOK: {result.SoLuongOK:n0}\r\nNG: {result.SoLuongNG:n0}\r\nRework: {result.SoLuongRework:n0}\r\nLoại bỏ ban đầu: {result.SoLuongLoaiBoBanDau:n0}", "Initial QC", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { XtraMessageBox.Show(this, ex.Message, "Không thể xác nhận Initial QC", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}