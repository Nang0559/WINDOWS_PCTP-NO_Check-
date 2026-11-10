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
        private readonly IHangLoiPhase5To9Service _phase5To9Service;
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
        // Toàn bộ dòng của mọi bước (đọc DB một lần mỗi lần làm mới); đổi bước / đổi bộ lọc chỉ lọc lại trên bộ nhớ.
        private List<WorkflowRow> _allRows;
        private readonly IWaitFormService _waitForm;
        // Bộ lọc trạng thái phiếu (dưới ô tìm kiếm). Mặc định chỉ hiện phiếu MỚI TẠO.
        private FlowLayoutPanel _pnlLoc;
        private CheckEdit _chkMoi;
        private CheckEdit _chkDang;
        private CheckEdit _chkHoanTat;
        private LabelControl _lblLoc;
        private bool _dangDatLoc;
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
            INoiBoLookupRepository noiBoLookupRepo = null,
            IHangLoiPhase5To9Service phase5To9Service = null)
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
            _phase5To9Service = phase5To9Service;
            _waitForm = new WaitFormService(this);
            BuildUI();

            // Tự động tải danh sách phiếu khi form hiện (có wait form), không cần nhập tìm kiếm.
            Shown += (s, e) => NapDuLieuBanDau();
        }

        private void NapDuLieuBanDau()
        {
            RefreshBadges();
            SetActiveStep(ResolveInitialStep());

            if (_preselectPhieuXuLyId.HasValue)
                HienThiDongNeuBiLoc(_preselectPhieuXuLyId.Value);

            FocusPreselectedRow();
        }

        private void BuildUI()
        {
            Text = "Quản lý tiến trình Xử lý hàng lỗi";
            Size = new Size(1500, 850);
            StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Name = "rework_rootLayout", Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var timeline = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), WrapContents = false, AutoScroll = true, BackColor = Color.FromArgb(240, 243, 246) };
            var labels = new[] { "1. Tiếp nhận", "2. Tạo phiếu", "3. Truy vết LOT", "4. QC định hướng", "5. Initial QC", "6. Rework", "7. QC Rework / Disposition", "8. Giao bù / Hoàn tất" };
            for (int i = 0; i < labels.Length; i++)
            {
                int step = i + 1;
                var button = new Button { Name = "rework_step_" + step.ToString("00"), Width = 160, Height = 80, Text = labels[i] + "\r\n(0)", Tag = step, FlatStyle = FlatStyle.Flat, Margin = new Padding(3), Font = new Font("Tahoma", 9F, FontStyle.Bold), BackColor = Color.White };
                button.Click += (s, e) => { _searchMode = false; _txtSearch.Text = string.Empty; RefreshBadges(); SetActiveStep((int)((Button)s).Tag); };
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
            _txtSearch = new TextEdit { Name = "rework_search", Dock = DockStyle.Fill };
            _txtSearch.Properties.NullValuePrompt = "Tìm số phiếu, model, mã hàng, LOT...";
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; ExecuteSearch(); } };
            toolbar.Controls.Add(_txtSearch, 0, 0);
            _btnSecondary = new SimpleButton { Name = "rework_btnCreateInternal", Text = "Tạo phiếu nội bộ", Dock = DockStyle.Fill };
            _btnSecondary.Click += (s, e) => TaoPhieuNoiBo();
            toolbar.Controls.Add(_btnSecondary, 1, 0);
            var btnSlot = new SimpleButton { Name = "rework_btnCreateFromSlot", Text = "Tạo từ Slot", Dock = DockStyle.Fill };
            btnSlot.Click += (s, e) => TaoPhieuTuSlot();
            toolbar.Controls.Add(btnSlot, 2, 0);
            _btnPrimary = new SimpleButton { Name = "rework_btnPrimaryAction", Dock = DockStyle.Fill };
            _btnPrimary.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            _btnPrimary.Click += BtnActionPrimary_Click;
            toolbar.Controls.Add(_btnPrimary, 3, 0);
            _btnCreateXuLy = new SimpleButton { Name = "rework_btnCreateAbnormal", Text = "Tạo phiếu bất thường", Dock = DockStyle.Fill };
            _btnCreateXuLy.Click += (s, e) => TaoPhieuXuLyBatThuong();
            toolbar.Controls.Add(_btnCreateXuLy, 4, 0);
            _btnExport = new SimpleButton { Name = "rework_btnExportExcel", Text = "Xuất Excel", Dock = DockStyle.Fill };
            _btnExport.Click += (s, e) => _gridView.ExportToXlsx($"HangLoi_Buoc{_activeStep}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            toolbar.Controls.Add(_btnExport, 5, 0);
            root.Controls.Add(toolbar, 0, 1);

            // Bộ lọc trạng thái phiếu ngay dưới ô tìm kiếm.
            _pnlLoc = new FlowLayoutPanel { Name = "rework_statusFilter", Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(6, 3, 3, 0) };
            _pnlLoc.Controls.Add(new LabelControl { Text = "Lọc trạng thái phiếu:", AutoSizeMode = LabelAutoSizeMode.Horizontal, Margin = new Padding(3, 5, 8, 0) });
            _chkMoi = TaoCheckLoc("rework_chkMoi", "Mới tạo (chờ xử lý)", 150);
            _chkDang = TaoCheckLoc("rework_chkDang", "Đang xử lý", 110);
            _chkHoanTat = TaoCheckLoc("rework_chkHoanTat", "Đã hoàn tất", 110);
            _lblLoc = new LabelControl { Name = "rework_statusFilterInfo", AutoSizeMode = LabelAutoSizeMode.Horizontal, Margin = new Padding(16, 5, 0, 0), Appearance = { ForeColor = Color.DimGray } };
            _pnlLoc.Controls.Add(_lblLoc);
            DatLocMacDinh();
            root.Controls.Add(_pnlLoc, 0, 2);

            _grid = new GridControl { Name = "rework_worklistGrid", Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.RowAutoHeight = true;
            _gridView.DoubleClick += (s, e) => OnGridDoubleClick();
            root.Controls.Add(_grid, 0, 3);
            _lblHint = new LabelControl { Name = "rework_workflowHint", Dock = DockStyle.Fill, Padding = new Padding(10, 5, 0, 0), Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Italic), ForeColor = Color.DimGray } };
            root.Controls.Add(_lblHint, 0, 4);
            Controls.Add(root);
        }

        private CheckEdit TaoCheckLoc(string name, string caption, int width)
        {
            var chk = new CheckEdit { Name = name, Width = width, Margin = new Padding(3, 0, 3, 0) };
            chk.Properties.Caption = caption;
            chk.CheckedChanged += (s, e) => { if (!_dangDatLoc) LoadCurrentStep(); };
            _pnlLoc.Controls.Add(chk);
            return chk;
        }

        // Mặc định mỗi bước: chỉ phiếu MỚI TẠO. 'Đang xử lý' chỉ có ở bước 2 và 6-8; 'Đã hoàn tất' chỉ ở bước 8.
        private void DatLocMacDinh()
        {
            _dangDatLoc = true;
            try
            {
                _chkMoi.Checked = true;
                _chkDang.Checked = false;
                _chkHoanTat.Checked = false;
                _chkDang.Visible = _activeStep == 2 || _activeStep >= 6;
                _chkHoanTat.Visible = _activeStep == 8;
            }
            finally { _dangDatLoc = false; }
        }

        // Các ô trạng thái là bộ lọc THU HẸP: không tích ô nào = không lọc = hiện TOÀN BỘ phiếu của bước.
        private bool CoLocTrangThai()
        {
            return _chkMoi.Checked || _chkDang.Checked || _chkHoanTat.Checked;
        }

        private bool TrangThaiDuocChon(WorkflowRow row)
        {
            if (!CoLocTrangThai()) return true;

            if (row.DaHoanTat) return _chkHoanTat.Checked;
            if (IsInProgress(row)) return _chkDang.Checked;
            return _chkMoi.Checked;
        }

        // Phiếu cần chọn sẵn (mở từ nơi khác / vừa tạo) đang bị bộ lọc ẩn → tự bật đúng nhóm trạng thái của nó.
        private void HienThiDongNeuBiLoc(int phieuXuLyId)
        {
            if (_allRows == null || _searchMode) return;
            if (_rows.Any(r => r.PhieuXuLyId == phieuXuLyId)) return;

            var row = _allRows.FirstOrDefault(r => r.PhieuXuLyId == phieuXuLyId);
            if (row == null) return;

            CheckEdit chk = row.DaHoanTat ? _chkHoanTat : (IsInProgress(row) ? _chkDang : _chkMoi);
            chk.Visible = true;
            chk.Checked = true; // CheckedChanged → LoadCurrentStep()
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

            // Phiếu đã HoanTat: trước đây rơi xuống 'return 2' nên biến mất khỏi bước 7 rồi hiện nhầm ở
            // bước 2 ('Tạo phiếu'), còn bước 8 luôn trống. Giờ phiếu hoàn tất nằm ở bước 8 (chỉ xem) trong
            // SoNgayHienThiPhieuHoanTat ngày gần nhất; cũ hơn thì ẩn khỏi hàng đợi (vẫn tìm được bằng ô tìm kiếm).
            if (status == QTChungStatus.HoanTat)
            {
                var hoanTatLuc = p.UpdatedAt ?? p.CreatedAt;
                return (DateTime.Now - hoanTatLuc).TotalDays <= SoNgayHienThiPhieuHoanTat ? 8 : 0;
            }

            // Chỉ còn trạng thái Moi (chưa hoàn tất bước tạo phiếu).
            return 2;
        }

        private const int SoNgayHienThiPhieuHoanTat = 30;

        private void SetActiveStep(int step)
        {
            _activeStep = Math.Max(1, Math.Min(8, step));
            for (int i = 0; i < _stepButtons.Length; i++) _stepButtons[i].BackColor = i + 1 == _activeStep ? Color.LightSteelBlue : Color.White;
            switch (_activeStep)
            {
                case 1: _btnSecondary.Visible = true; _btnPrimary.Text = "Tiếp nhận phiếu khách"; _lblHint.Text = "Bước 1: danh sách phiếu đã tiếp nhận (khách trả / nội bộ) chưa tạo phiếu bất thường. Bấm đúp để mở phiếu; chọn dòng rồi bấm 'Tạo phiếu bất thường' để chuyển thẳng sang bước 3 (Truy vết LOT). Bước 2 chỉ hiện khi phiếu mới được tạo một phần."; break;
                case 2: _btnSecondary.Visible = true; _btnPrimary.Text = "Tạo phiếu bất thường"; _lblHint.Text = "Bước 2: gồm dòng phiếu trả CHƯA tạo phiếu bất thường (mới tạo một phần; chọn rồi bấm 'Tạo phiếu bất thường') và các phiếu bất thường ĐÃ TẠO để theo dõi (chọn dòng + bấm nút hoặc bấm đúp để nhảy tới bước hiện tại của phiếu)."; break;
                case 3: _btnSecondary.Visible = false; _btnPrimary.Text = "Truy vết LOT"; _lblHint.Text = "Bước 3: truy vết kho thành phẩm, WIP/sản xuất và khách trả; snapshot trước khi QC."; break;
                case 4: _btnSecondary.Visible = false; _btnPrimary.Text = "QC định hướng"; _lblHint.Text = "Bước 4: QC chọn Từ chối giao bù / Chỉ giao bù / Có Rework."; break;
                case 5: _btnSecondary.Visible = false; _btnPrimary.Text = "Initial QC"; _lblHint.Text = "Bước 5: kiểm đủ snapshot. Chỉ nhập Đã kiểm tra, Rework, Loại bỏ; NG = Rework + Loại bỏ và OK = Đã kiểm tra - NG được tự tính. Hàng cần rework thì nhập vào cột Rework (không nhập vào OK)."; break;
                case 6: _btnSecondary.Visible = false; _btnPrimary.Text = "Xuất / Giao Rework"; _lblHint.Text = "Bước 6: chỉ nhánh CanRework; xuất không vượt Initial QC Rework, giao sản xuất. 'Ghi nhận tiến độ Rework' chỉ ghi chú, không xác nhận sửa xong. Sửa xong thì đóng form Rework và chọn bước 7."; break;
                case 7: _btnSecondary.Visible = false; _btnPrimary.Text = "QC Rework / Disposition"; _lblHint.Text = "Bước 7: QC Rework OK + NG = lượng Rework; NG Rework cộng vào loại bỏ cuối."; break;
                case 8: _btnSecondary.Visible = false; _btnPrimary.Text = "Giao bù / Hoàn tất"; _lblHint.Text = "Bước 8: phiếu còn chờ giao bù / hoàn tất nằm trên; phiếu ĐÃ HOÀN TẤT (" + SoNgayHienThiPhieuHoanTat + " ngày gần nhất) nằm dưới, chọn dòng và bấm nút để xem kết quả. Giao bù là nghĩa vụ độc lập; không suy ra từ NG/Rework."; break;
            }
            _btnCreateXuLy.Visible = _activeStep == 1 || _activeStep == 2;
            DatLocMacDinh();
            LoadCurrentStep();
        }

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
                // Mỗi bước hiển thị: (mới | đang xử lý) + đã chuyển sang bước sau.
                //  - mới       : đang nằm ở bước, chưa có thao tác dở.
                //  - đang xử lý: đang nằm ở bước nhưng đã có thao tác dở (đã xuất rework, đã giao SX, chờ giao bù).
                //  - đã chuyển : đã đi qua bước này (đang ở bước sau hoặc đã hoàn tất). Bước 1 đếm theo phiếu trả,
                //                các bước còn lại đếm theo phiếu xử lý bất thường. Bước 8: đã xong = hoàn tất.
                var moi = new int[8];
                var dang = new int[8];
                var chuyen = new int[8];
                var phieuTraDaTao = new HashSet<int>();

                // Đọc DB một lần (wait form chuẩn), các bước/bộ lọc dùng lại bộ nhớ đệm này.
                _allRows = _waitForm.Run(() => { return BuildWorkflowRows(); }, "Đang tải danh sách phiếu xử lý hàng lỗi...");

                foreach (var row in _allRows)
                {
                    var step = StepOf(row);
                    bool hoanTat = row.DaHoanTat;

                    if (row.PhieuXuLyId.HasValue)
                    {
                        phieuTraDaTao.Add(row.PhieuTraHangId);
                        int eff = hoanTat ? 9 : step;           // 0 = đã hủy/ẩn → không tính
                        for (int k = 2; k <= 8; k++)
                            if (eff > k) chuyen[k - 1]++;
                    }

                    if (hoanTat) continue;
                    if (step >= 1 && step <= 8)
                    {
                        if (IsInProgress(row)) dang[step - 1]++; else moi[step - 1]++;
                    }
                }
                chuyen[0] = phieuTraDaTao.Count;

                var labels = new[] { "1. Tiếp nhận", "2. Tạo phiếu", "3. Truy vết LOT", "4. QC định hướng", "5. Initial QC", "6. Rework", "7. QC Rework / Disposition", "8. Giao bù / Hoàn tất" };
                for (int i = 0; i < 8; i++)
                    _stepButtons[i].Text = labels[i] + "\r\n(mới " + moi[i] + " | đang " + dang[i] + ")\r\n"
                                         + (i == 7 ? "đã xong " : "đã chuyển ") + chuyen[i];
            }
            catch (Exception ex) { ShowWarning("Không thể cập nhật tiến trình.\r\n\r\n" + ex.Message); }
        }

        private static bool IsInProgress(WorkflowRow row)
        {
            return row.PhieuXuLyId.HasValue
                && (row.QTStatus == QTChungStatus.DaXuatKhoRework
                    || row.QTStatus == QTChungStatus.DaGiaoSanXuat
                    || row.QTStatus == QTChungStatus.ChoGiaoBu);
        }

        /// <summary>Phiếu bất thường đã tạo và còn đang xử lý (bước 3..8, chưa hoàn tất) → hiện ở bước 2 như danh sách theo dõi.</summary>
        private static bool IsCreatedInfoRow(WorkflowRow row, int step)
        {
            return row.PhieuXuLyId.HasValue && step >= 3 && step <= 8 && !row.DaHoanTat;
        }

        private void GoToCurrentStepOf(WorkflowRow row)
        {
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            int step = ResolveRowStep(row);
            if (step < 1 || step > 8) { ShowWarning("Phiếu này không còn trong hàng đợi xử lý."); return; }
            _searchMode = false;
            _txtSearch.Text = string.Empty;
            SetActiveStep(step);
            HienThiDongNeuBiLoc(row.PhieuXuLyId.Value);
            var handle = _gridView.LocateByValue("PhieuXuLyId", row.PhieuXuLyId.Value);
            if (handle >= 0) _gridView.FocusedRowHandle = handle;
        }

        // Bước đã tính sẵn lúc nạp dữ liệu (không truy vấn lại DB); dòng cấp phiếu trả dùng HeaderStep.
        private int StepOf(WorkflowRow row)
        {
            return row.BuocDaTinh ?? ResolveRowStep(row);
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
            _pnlLoc.Enabled = true;

            try
            {
                // Tự động nạp phiếu của bước đang chọn (từ bộ nhớ đệm), lọc theo trạng thái được tích.
                if (_allRows == null)
                    _allRows = _waitForm.Run(() => { return BuildWorkflowRows(); }, "Đang tải danh sách phiếu xử lý hàng lỗi...");

                var thuocBuoc = _allRows.Where(x =>
                {
                    int st = StepOf(x);
                    return st == _activeStep || (_activeStep == 2 && IsCreatedInfoRow(x, st));
                }).ToList();

                _rows = thuocBuoc.Where(TrangThaiDuocChon).ToList();
                // Bước 8: phiếu còn việc phải làm lên trước, phiếu đã hoàn tất (mới nhất trước) xếp sau.
                if (_activeStep == 8)
                    _rows = _rows.OrderBy(x => x.DaHoanTat ? 1 : 0)
                                 .ThenByDescending(x => x.HoanTatLuc ?? DateTime.MinValue)
                                 .ToList();
                _grid.DataSource = _rows;
                ConfigureGrid();
                ApplyFilter();

                _lblLoc.Text = !CoLocTrangThai()
                    ? $"Không lọc trạng thái: hiển thị toàn bộ {_rows.Count} phiếu ở bước này"
                    : $"Hiển thị {_rows.Count}/{thuocBuoc.Count} phiếu ở bước này";
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
                row.BuocDaTinh = ResolveStep(p);
                if (row.QTStatus == QTChungStatus.DaQCXacNhanCuoi || row.QTStatus == QTChungStatus.DaNhapLaiKho || row.QTStatus == QTChungStatus.HoanTat)
                {
                    row.KetQuaCuoi = BuildKetQuaCuoi(p);
                    if (row.QTStatus == QTChungStatus.HoanTat) row.HoanTatLuc = p.UpdatedAt;
                }
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
            _pnlLoc.Enabled = false; // kết quả tìm kiếm gồm cả phiếu đã đóng, không áp dụng bộ lọc trạng thái
            _lblLoc.Text = "Đang hiển thị kết quả tìm kiếm (không áp dụng bộ lọc trạng thái). Xóa ô tìm kiếm và Enter để quay lại danh sách.";
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
            AddColumn("KetQuaCuoi", "Kết quả cuối", true, 260);
            AddColumn("HoanTatLuc", "Hoàn tất lúc", true, 120);
            AddColumn("BoPhanPhatHienLoi", "Bộ phận phát hiện", true, 150);
            var colHoanTat = _gridView.Columns["HoanTatLuc"];
            if (colHoanTat != null)
            {
                colHoanTat.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                colHoanTat.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";
            }
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
            using (var f = new FormTiepNhanPhieuKhachTra(_khachTraHangService, _noiBoLookupRepo, _slotService, _reworkStockService)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterIntake();
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
                // Phiếu đã tạo (xem ở bước 2) → chuyển tới bước hiện tại của phiếu thay vì báo lỗi.
                GoToCurrentStepOf(row);
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
                List<int> createdIds;
                if (row.HeaderStep == 2)
                {
                    if (!row.PhieuTraHangCTId.HasValue)
                        throw new InvalidOperationException("Dòng bước 2 không có PhieuTraHangCTId; hãy tải lại danh sách.");
                    int id = _qtChungService.TaoPhieuXuLyBatThuong(
                        row.PhieuTraHangCTId.Value, model, "Hàng lỗi", null, Environment.UserName);
                    createdIds = new List<int> { id };
                }
                else
                {
                    createdIds = _qtChungService.TaoPhieuXuLyTuPhieuTra(row.PhieuTraHangId, model, "Hàng lỗi", Environment.UserName)
                                 ?? new List<int>();
                }

                // Dòng bước 1/2 chưa có PhieuXuLyId nên RefreshAfterAction() không biết phải nhảy đi đâu
                // (người dùng bị kẹt ở bước 1). Chuyển sang đúng bước của phiếu vừa tạo và chọn sẵn dòng đó.
                int targetStep = 0;
                int? firstId = createdIds.Count > 0 ? (int?)createdIds[0] : null;
                if (firstId.HasValue)
                {
                    var created = _qtChungService.GetById(firstId.Value);
                    if (created != null) targetStep = ResolveStep(created);
                }

                RefreshBadges();
                if (targetStep >= 1 && targetStep <= 8)
                {
                    _searchMode = false;
                    _txtSearch.Text = string.Empty;
                    SetActiveStep(targetStep);
                    HienThiDongNeuBiLoc(firstId.Value);
                    var handle = _gridView.LocateByValue("PhieuXuLyId", firstId.Value);
                    if (handle >= 0) _gridView.FocusedRowHandle = handle;
                    XtraMessageBox.Show(this, $"Đã tạo {createdIds.Count} phiếu xử lý bất thường cho phiếu {row.SoPhieu}.\r\nPhiếu đã chuyển sang Bước {targetStep}: {_stepButtons[targetStep - 1].Text.Split('\r')[0].Substring(3).Trim()}.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    LoadCurrentStep();
                    XtraMessageBox.Show(this, $"Đã tạo {createdIds.Count} phiếu xử lý bất thường cho phiếu {row.SoPhieu}.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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
                using (var f = new FormQCXacNhanCuoi(_qtChungService, p.Id, _slotService, _reworkStockService)) if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
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

        /// <summary>Tóm tắt kết quả QC cuối + nhập lại NG cho cột 'Kết quả cuối' (chỉ để hiển thị, không ném lỗi).</summary>
        private string BuildKetQuaCuoi(PhieuXuLyBatThuong p)
        {
            try
            {
                var parts = new List<string>();

                var qc = _qtChungRepo.GetQC(p.Id);
                if (qc != null)
                    parts.Add($"QC cuối: OK {qc.SoLuongOK:n0} | NG {qc.SoLuongNG:n0}");

                var nhapNg = _qtChungRepo.GetNhapNG(p.Id);
                if (nhapNg != null && nhapNg.Count > 0)
                    parts.Add($"Nhập lại NG {nhapNg.Sum(x => x.SoLuongNG):n0}");

                if (parts.Count == 0)
                {
                    if (p.HuongXuLy == HuongXuLyBatThuong.TuChoiGiaoBu) parts.Add("Từ chối giao bù");
                    else if (p.HuongXuLy == HuongXuLyBatThuong.ChiGiaoBu) parts.Add("Giao bù");
                }

                return string.Join(" | ", parts);
            }
            catch (Exception ex)
            {
                // Cột chỉ để xem: lỗi đọc không được làm hỏng cả dashboard.
                return "(không đọc được kết quả: " + ex.Message + ")";
            }
        }

        private void ShowKetQuaPhieuHoanTat(WorkflowRow row, PhieuXuLyBatThuong p)
        {
            var lines = new List<string>
            {
                "Số phiếu xử lý: " + p.SoPhieu,
                "Nguồn: " + row.Nguon + " | Hướng xử lý: " + p.HuongXuLy,
                "Mã sản phẩm: " + p.MaSanPham + " | LOT: " + p.SoLo + " | SL lỗi: " + p.SoLuongLoi.ToString("n0")
            };

            if (!string.IsNullOrWhiteSpace(row.InitialQC)) lines.Add("Initial QC: " + row.InitialQC);
            if (!string.IsNullOrWhiteSpace(row.KetQuaCuoi)) lines.Add("Kết quả cuối: " + row.KetQuaCuoi);

            lines.Add("Hoàn tất lúc: " + (p.UpdatedAt.HasValue ? p.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "-")
                      + (string.IsNullOrWhiteSpace(p.UpdatedBy) ? "" : " | Người thực hiện: " + p.UpdatedBy));

            XtraMessageBox.Show(this, string.Join("\r\n", lines), "Phiếu đã hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private int TongCanScrap(PhieuXuLyBatThuong p)
        {
            var disposition = _phase5To9Service == null ? null : _phase5To9Service.GetDisposition(p.Id);
            if (disposition != null)
                return disposition.SoLuongLoaiBoCuoi;

            var initial = _initialQCService == null ? null : _initialQCService.Get(p.Id);
            return initial == null ? 0 : initial.SoLuongLoaiBoBanDau;
        }

        private int DaScrap(int phieuXuLyId)
        {
            return (_qtChungRepo.GetXuat(phieuXuLyId) ?? new List<TraHangQTChungXuat>())
                .Where(x => x != null && string.Equals(x.LoaiXuat, "Scrap", StringComparison.OrdinalIgnoreCase))
                .Sum(x => Math.Max(0, x.SoLuongXuat));
        }

        /// <summary>
        /// Trước khi hoàn tất: số lượng loại bỏ theo kết luận QC phải được ghi nhận ra khỏi kho. Còn thiếu thì mở hộp thoại
        /// ghi nhận scrap; người dùng vẫn có thể hoàn tất không ghi nhận kho nhưng phải xác nhận rõ.
        /// </summary>
        private bool EnsureScrapRecorded(PhieuXuLyBatThuong p)
        {
            int tong, da;
            try
            {
                tong = TongCanScrap(p);
                if (tong <= 0 || _reworkStockService == null || _noiBoLookupRepo == null)
                    return true;
                da = DaScrap(p.Id);
            }
            catch (Exception)
            {
                return true; // không đọc được dữ liệu: không chặn thao tác hoàn tất
            }

            if (da >= tong)
                return true;

            using (var f = new FormGhiNhanScrap(_reworkStockService, _noiBoLookupRepo, p, tong, da))
                f.ShowDialog(this);

            try { da = DaScrap(p.Id); } catch (Exception) { }

            if (da >= tong)
                return true;

            return XtraMessageBox.Show(
                this,
                string.Format("Còn {0:n0} chưa ghi nhận loại bỏ khỏi kho (cần {1:n0}, đã ghi nhận {2:n0}).\r\n\r\nVẫn hoàn tất mà KHÔNG ghi nhận kho?", tong - da, tong, da),
                "Chưa ghi nhận loại bỏ",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void XuLyBuocCuoi()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
            if (p == null) return;

            // Phiếu đã hoàn tất: không còn thao tác nào — chỉ cho xem kết quả cuối
            // (trước đây gọi HoanTat lần nữa sẽ báo lỗi 'không thể chuyển HoanTat → HoanTat').
            if (p.Status == QTChungStatus.HoanTat)
            {
                ShowKetQuaPhieuHoanTat(row, p);
                return;
            }

            // Kết quả cuối chỉ có OK hoặc loại bỏ: phần loại bỏ phải được ghi nhận khỏi kho (cả phiếu nội bộ lẫn khách trả).
            if (!EnsureScrapRecorded(p))
                return;

            switch (p.HuongXuLy)
            {
                case HuongXuLyBatThuong.ChiGiaoBu: XuLyGiaoBu(row); break;
                case HuongXuLyBatThuong.TuChoiGiaoBu:
                    var result = _qtChungService.HoanTat(p.Id, Environment.UserName);
                    ShowScanResult(result);
                    RefreshAfterAction();
                    break;
                case HuongXuLyBatThuong.CanRework:
                    // Initial QC xác nhận 100% OK (không có NG/Rework) → không còn gì để rework hay giao bù: hoàn tất luôn.
                    // Phiếu còn ở DaDinhHuong nên cần transition QT_CHUNG 20 → 100 (xem PHASE7_WorkflowSeed.sql).
                    if (p.Status == QTChungStatus.DaDinhHuong)
                    {
                        var initial = _initialQCService == null ? null : _initialQCService.Get(p.Id);
                        if (initial == null) { ShowWarning("Chưa xác nhận Initial QC."); break; }
                        if (initial.SoLuongRework > 0 || initial.SoLuongNG > 0)
                        {
                            ShowWarning("Phiếu còn số lượng NG/Rework sau Initial QC; hãy xử lý ở Bước 6 (Rework) trước.");
                            break;
                        }
                        var allOk = _qtChungService.HoanTat(p.Id, Environment.UserName);
                        ShowScanResult(allOk);
                        RefreshAfterAction();
                        break;
                    }
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
                if (p != null)
                {
                    int step = ResolveStep(p);
                    if (step >= 1 && step <= 8) _activeStep = step;   // 0 = phiếu đã hủy/ẩn → giữ bước hiện tại
                }
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
            /// <summary>Tóm tắt kết quả QC cuối / nhập lại NG (chỉ có khi đã QC cuối hoặc đã hoàn tất).</summary>
            public string KetQuaCuoi { get; set; }
            public DateTime? HoanTatLuc { get; set; }
            public bool DaHoanTat { get { return PhieuXuLyId.HasValue && QTStatus == QTChungStatus.HoanTat; } }
            // Dòng cấp phiếu trả (chưa có PhieuXuLyId): 1 = Tiếp nhận, 2 = đã tạo một phần.
            public int HeaderStep { get; set; } = 1;
            /// <summary>Bước hiện tại của phiếu xử lý, tính một lần lúc nạp dữ liệu (null với dòng cấp phiếu trả).</summary>
            public int? BuocDaTinh { get; set; }
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
            // Chỉ nhập: Đã kiểm tra, Rework, Loại bỏ. NG = Rework + Loại bỏ; OK = Đã kiểm tra - NG (tự tính, không cho sửa)
            // để không thể nhập sai quy tắc "NG = Rework + Loại bỏ ban đầu" và "Đã kiểm tra = OK + NG".
            AddNumberColumn("SoLuongDaKiemTra", "Đã kiểm tra", 95);
            AddNumberColumn("SoLuongOK", "OK (tự tính)", 95, true);
            AddNumberColumn("SoLuongNG", "NG (tự tính)", 95, true);
            AddNumberColumn("SoLuongRework", "Rework", 90);
            AddNumberColumn("SoLuongLoaiBo", "Loại bỏ", 90);
            _grid.CellEndEdit += (s, e) => { if (e.RowIndex >= 0) RecalcRow(e.RowIndex); UpdateSummary(); };
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
        private void AddNumberColumn(string name, string caption, int width, bool readOnly = false)
        {
            var column = new DataGridViewTextBoxColumn { Name = name, HeaderText = caption, Width = width, ValueType = typeof(int), ReadOnly = readOnly };
            if (readOnly) column.DefaultCellStyle.BackColor = Color.WhiteSmoke;
            _grid.Columns.Add(column);
        }

        private void RecalcRow(int rowIndex)
        {
            var row = _grid.Rows[rowIndex];
            int affected = Convert.ToInt32(row.Cells["SoLuongAnhHuong"].Value);
            int inspected = CellInt(rowIndex, "SoLuongDaKiemTra");
            int rework = CellInt(rowIndex, "SoLuongRework");
            int scrap = CellInt(rowIndex, "SoLuongLoaiBo");
            int ng = rework + scrap;
            row.Cells["SoLuongNG"].Value = ng;
            row.Cells["SoLuongOK"].Value = inspected - ng;
            if (inspected > affected) row.ErrorText = "Đã kiểm tra vượt số lượng ảnh hưởng.";
            else if (ng > inspected) row.ErrorText = "Rework + Loại bỏ vượt số đã kiểm tra.";
            else row.ErrorText = string.Empty;
        }

        private void LoadSnapshot()
        {
            if (_initialQCService.Get(_phieuXuLyId) != null) throw new InvalidOperationException("Phiếu đã có Initial QC; không cho ghi đè kết quả đã xác nhận.");
            var snapshot = _traceService.GetSnapshot(_phieuXuLyId);
            if (snapshot == null || snapshot.Count == 0) throw new InvalidOperationException("Chưa có snapshot LOT. Hãy thực hiện Truy vết LOT trước.");
            foreach (var item in snapshot) _grid.Rows.Add(item.Id, item.SourceType.ToString(), item.LotNo, item.MaSanPham, item.SoLuongAnhHuong, item.SoLuongAnhHuong, item.SoLuongAnhHuong, 0, 0, 0);
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
                    RecalcRow(row.Index);
                foreach (DataGridViewRow row in _grid.Rows)
                    if (!string.IsNullOrEmpty(row.ErrorText))
                        throw new InvalidOperationException("LOT " + Convert.ToString(row.Cells["LotNo"].Value) + ": " + row.ErrorText);
                UpdateSummary();
                foreach (DataGridViewRow row in _grid.Rows)
                    list.Add(new InitialQCLotResult { AffectedLotId = Convert.ToInt32(row.Cells["AffectedLotId"].Value), SoLuongDaKiemTra = CellInt(row.Index, "SoLuongDaKiemTra"), SoLuongOK = CellInt(row.Index, "SoLuongOK"), SoLuongNG = CellInt(row.Index, "SoLuongNG"), SoLuongRework = CellInt(row.Index, "SoLuongRework"), SoLuongLoaiBo = CellInt(row.Index, "SoLuongLoaiBo") });

                var result = _initialQCService.Confirm(_phieuXuLyId, list, _txtNoiDung.Text, _txtKetLuan.Text, Environment.UserName);
                XtraMessageBox.Show(this, $"Đã xác nhận Initial QC.\r\nOK: {result.SoLuongOK:n0}\r\nNG: {result.SoLuongNG:n0}\r\nRework: {result.SoLuongRework:n0}\r\nLoại bỏ ban đầu: {result.SoLuongLoaiBoBanDau:n0}", "Initial QC", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { XtraMessageBox.Show(this, ex.Message, "Không thể xác nhận Initial QC", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    /// <summary>Hộp thoại ghi nhận loại bỏ (scrap) khỏi kho: chọn SlotLot nguồn (NG cách ly / đang hoạt động) và số lượng.</summary>
    internal sealed class FormGhiNhanScrap : XtraForm
    {
        private sealed class DongScrap
        {
            public int SlotLotId { get; set; }
            public string ViTri { get; set; }
            public string LotNo { get; set; }
            public string TrangThai { get; set; }
            public int TonKho { get; set; }
            public int SoLuongScrap { get; set; }
        }

        private readonly IReworkStockService _stock;
        private readonly PhieuXuLyBatThuong _phieu;
        private readonly int _tongCanScrap;
        private int _daScrap;
        private readonly List<DongScrap> _rows = new List<DongScrap>();
        private readonly GridControl _grid;
        private readonly GridView _view;
        private readonly LabelControl _lblInfo;
        private readonly TextEdit _txtLyDo;

        public FormGhiNhanScrap(
            IReworkStockService stock,
            INoiBoLookupRepository lookup,
            PhieuXuLyBatThuong phieu,
            int tongCanScrap,
            int daScrap)
        {
            _stock = stock;
            _phieu = phieu;
            _tongCanScrap = tongCanScrap;
            _daScrap = daScrap;

            Text = "Ghi nhận loại bỏ (scrap)";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 480);

            _lblInfo = new LabelControl { Dock = DockStyle.Top, Height = 50, AutoSizeMode = LabelAutoSizeMode.None, Padding = new Padding(10, 8, 0, 0) };

            _grid = new GridControl { Dock = DockStyle.Fill };
            _view = new GridView(_grid);
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsBehavior.Editable = true;

            var pnl = new Panel { Dock = DockStyle.Bottom, Height = 80 };
            _txtLyDo = new TextEdit { Left = 12, Top = 8, Width = 876 };
            _txtLyDo.Properties.NullValuePrompt = "Lý do loại bỏ (không bắt buộc)";
            var btnGhi = new SimpleButton { Text = "Ghi nhận loại bỏ", Left = 640, Top = 40, Width = 150 };
            var btnDong = new SimpleButton { Text = "Đóng", Left = 800, Top = 40, Width = 88, DialogResult = DialogResult.Cancel };
            btnGhi.Click += (s, e) => Ghi();
            pnl.Controls.AddRange(new Control[] { _txtLyDo, btnGhi, btnDong });

            Controls.Add(_grid);
            Controls.Add(pnl);
            Controls.Add(_lblInfo);
            CancelButton = btnDong;

            List<ScrapSourceLot> nguon = null;
            try { nguon = lookup.FindScrapSourceLots(phieu.MaSanPham, phieu.SoLo); }
            catch (Exception ex) { XtraMessageBox.Show(this, "Không tải được danh sách hàng để loại bỏ: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

            foreach (var n in nguon ?? new List<ScrapSourceLot>())
                _rows.Add(new DongScrap
                {
                    SlotLotId = n.SlotLotId,
                    ViTri = n.ViTri,
                    LotNo = n.LotNo,
                    TrangThai = n.PhieuStatus == 3 ? "NG cách ly" : "Đang hoạt động",
                    TonKho = n.Quantity,
                    SoLuongScrap = 0
                });

            _grid.DataSource = _rows;
            _view.PopulateColumns();
            _view.Columns["SlotLotId"].Visible = false;
            _view.Columns["ViTri"].Caption = "Vị trí";
            _view.Columns["LotNo"].Caption = "LOT";
            _view.Columns["TrangThai"].Caption = "Trạng thái";
            _view.Columns["TonKho"].Caption = "Tồn";
            _view.Columns["SoLuongScrap"].Caption = "SL loại bỏ";
            foreach (DevExpress.XtraGrid.Columns.GridColumn c in _view.Columns)
                c.OptionsColumn.AllowEdit = c.FieldName == "SoLuongScrap";

            CapNhatThongTin();
        }

        private void CapNhatThongTin()
        {
            _lblInfo.Text = string.Format(
                "Phiếu {0} | Mã {1} | LOT {2}\r\nCần loại bỏ: {3:n0} | Đã ghi nhận: {4:n0} | Còn lại: {5:n0}  (nhập SL loại bỏ ở cột cuối của dòng muốn lấy hàng)",
                _phieu.SoPhieu, _phieu.MaSanPham, string.IsNullOrWhiteSpace(_phieu.SoLo) ? "(toàn bộ LOT của mã)" : _phieu.SoLo,
                _tongCanScrap, _daScrap, Math.Max(0, _tongCanScrap - _daScrap));
        }

        private void Ghi()
        {
            _view.CloseEditor();
            _view.UpdateCurrentRow();

            var chon = _rows.Where(r => r.SoLuongScrap > 0).ToList();
            if (chon.Count == 0)
            {
                XtraMessageBox.Show(this, "Nhập số lượng loại bỏ ở ít nhất một dòng.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int tong = chon.Sum(r => r.SoLuongScrap);
            if (tong > _tongCanScrap - _daScrap)
            {
                XtraMessageBox.Show(this, string.Format("Tổng {0:n0} vượt số còn phải loại bỏ ({1:n0}).", tong, _tongCanScrap - _daScrap), "Vượt hạn mức", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string lyDo = (_txtLyDo.Text ?? string.Empty).Trim();
            if (lyDo.Length == 0) lyDo = "Loại bỏ theo kết luận QC";

            string loi = null;
            foreach (var r in chon)
            {
                var kq = _stock.GhiNhanScrap(_phieu.Id, r.SlotLotId, r.LotNo, r.SoLuongScrap, _tongCanScrap, lyDo, Environment.UserName, Guid.NewGuid());
                if (!kq.IsOK)
                {
                    loi = "LOT " + r.LotNo + " (" + r.ViTri + "): " + kq.Message;
                    break;
                }
                _daScrap += r.SoLuongScrap;
                r.TonKho -= r.SoLuongScrap;
                r.SoLuongScrap = 0;
            }

            _grid.RefreshDataSource();
            CapNhatThongTin();

            if (loi != null)
            {
                XtraMessageBox.Show(this, loi, "Không ghi nhận được", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_daScrap >= _tongCanScrap)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
