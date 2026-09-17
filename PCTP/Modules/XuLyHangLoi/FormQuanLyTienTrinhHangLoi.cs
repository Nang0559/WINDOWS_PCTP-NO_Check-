using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.KhoVatLy;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
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
    /// Màn hình điều phối workflow Xử lý hàng lỗi.
    /// UI chỉ quyết định hành động được phép theo dữ liệu hiện tại;
    /// validation nghiệp vụ vẫn nằm ở Application Service/Workflow Service.
    /// </summary>
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

        private readonly Button[] _stepButtons = new Button[8];
        private int _activeStep = 1;
        private GridControl _grid;
        private GridView _gridView;
        private TextEdit _txtSearch;
        private SimpleButton _btnSecondary;
        private SimpleButton _btnPrimary;
        private SimpleButton _btnExport;
        private LabelControl _lblHint;
        private List<WorkflowRow> _rows = new List<WorkflowRow>();

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
            IInitialQCService initialQCService = null)
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

            var timeline = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(240, 243, 246)
            };

            var labels = new[]
            {
                "1. Tiếp nhận",
                "2. Tạo phiếu",
                "3. Truy vết LOT",
                "4. QC định hướng",
                "5. Initial QC",
                "6. Rework",
                "7. QC Rework / Disposition",
                "8. Giao bù / Hoàn tất"
            };

            for (int i = 0; i < labels.Length; i++)
            {
                int step = i + 1;
                var button = new Button
                {
                    Width = 160,
                    Height = 80,
                    Text = labels[i] + "\r\n(0)",
                    Tag = step,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(3),
                    Font = new Font("Tahoma", 9F, FontStyle.Bold),
                    BackColor = Color.White
                };
                button.Click += (s, e) => SetActiveStep((int)((Button)s).Tag);
                _stepButtons[i] = button;
                timeline.Controls.Add(button);

                if (i < labels.Length - 1)
                {
                    timeline.Controls.Add(new LabelControl
                    {
                        Text = "▶",
                        Width = 22,
                        Height = 70,
                        Padding = new Padding(2, 25, 2, 0),
                        Appearance = { Font = new Font("Tahoma", 11F, FontStyle.Bold), ForeColor = Color.Gray }
                    });
                }
            }
            root.Controls.Add(timeline, 0, 0);

            var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Padding = new Padding(3) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

            _txtSearch = new TextEdit { Dock = DockStyle.Fill };
            _txtSearch.Properties.NullValuePrompt = "Tìm số phiếu, model, mã hàng, LOT...";
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyFilter(); };
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

            _btnExport = new SimpleButton { Text = "Xuất Excel", Dock = DockStyle.Fill };
            _btnExport.Click += (s, e) => _gridView.ExportToXlsx($"HangLoi_Buoc{_activeStep}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            toolbar.Controls.Add(_btnExport, 4, 0);
            root.Controls.Add(toolbar, 0, 1);

            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.RowAutoHeight = true;
            _gridView.DoubleClick += (s, e) => ExecuteActionByStep(_activeStep);
            root.Controls.Add(_grid, 0, 2);

            _lblHint = new LabelControl
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 5, 0, 0),
                Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Italic), ForeColor = Color.DimGray }
            };
            root.Controls.Add(_lblHint, 0, 3);
            Controls.Add(root);
        }

        private int ResolveInitialStep()
        {
            if (!_preselectPhieuXuLyId.HasValue) return 1;
            var p = _phieuXuLyRepo.GetById(_preselectPhieuXuLyId.Value);
            if (p == null) return 1;
            return ResolveStep(p);
        }

        private int ResolveStep(PhieuXuLyBatThuong p)
        {
            var status = _phieuXuLyRepo.GetStatus(p.Id) ?? p.Status;
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
            if (status == QTChungStatus.DaXuatKhoRework || status == QTChungStatus.DaGiaoSanXuat)
                return 6;
            if (status == QTChungStatus.DaQCXacNhanCuoi)
            {
                var qc = _qtChungRepo.GetQC(p.Id);
                return qc != null && qc.SoLuongNG > 0 ? 7 : 8;
            }
            if (status == QTChungStatus.DaNhapLaiKho)
                return 8;
            if (status == QTChungStatus.ChoGiaoBu || status == QTChungStatus.DaGiaoBu || status == QTChungStatus.TuChoiGiaoBu)
                return 8;
            return 2;
        }

        private void SetActiveStep(int step)
        {
            _activeStep = Math.Max(1, Math.Min(8, step));
            for (int i = 0; i < _stepButtons.Length; i++)
            {
                _stepButtons[i].BackColor = i + 1 == _activeStep ? Color.LightSteelBlue : Color.White;
            }

            switch (_activeStep)
            {
                case 1:
                    _btnSecondary.Visible = true;
                    _btnPrimary.Text = "Tiếp nhận phiếu khách";
                    _lblHint.Text = "Bước 1: tiếp nhận nguồn hàng trả/khách trả.";
                    break;
                case 2:
                    _btnSecondary.Visible = true;
                    _btnPrimary.Text = "Tạo phiếu bất thường";
                    _lblHint.Text = "Bước 2: tạo PhieuXuLyBatThuong từ phiếu trả.";
                    break;
                case 3:
                    _btnSecondary.Visible = false;
                    _btnPrimary.Text = "Truy vết LOT";
                    _lblHint.Text = "Bước 3: truy vết kho thành phẩm, WIP/sản xuất và khách trả; snapshot trước khi QC.";
                    break;
                case 4:
                    _btnSecondary.Visible = false;
                    _btnPrimary.Text = "QC định hướng";
                    _lblHint.Text = "Bước 4: QC chọn Từ chối giao bù / Chỉ giao bù / Có Rework.";
                    break;
                case 5:
                    _btnSecondary.Visible = false;
                    _btnPrimary.Text = "Initial QC";
                    _lblHint.Text = "Bước 5: kiểm đủ toàn bộ snapshot. DaKiemTra = OK + NG; NG = Rework + LoaiBoBanDau.";
                    break;
                case 6:
                    _btnSecondary.Visible = false;
                    _btnPrimary.Text = "Xuất / Giao Rework";
                    _lblHint.Text = "Bước 6: chỉ nhánh CanRework; xuất không vượt Initial QC Rework, sau đó giao sản xuất.";
                    break;
                case 7:
                    _btnSecondary.Visible = false;
                    _btnPrimary.Text = "QC Rework / Disposition";
                    _lblHint.Text = "Bước 7: QC Rework OK + NG = lượng Rework; NG Rework cộng vào loại bỏ cuối.";
                    break;
                case 8:
                    _btnSecondary.Visible = false;
                    _btnPrimary.Text = "Giao bù / Hoàn tất";
                    _lblHint.Text = "Bước 8: Giao bù là nghĩa vụ độc lập; không suy ra từ NG/Rework. Phiếu chỉ hoàn tất khi đủ điều kiện workflow.";
                    break;
            }
            LoadCurrentStep();
        }

        private void RefreshAll()
        {
            RefreshBadges();
            LoadCurrentStep();
        }

        private List<PhieuTraHang> GetHeaders()
        {
            var list = new List<PhieuTraHang>();
            list.AddRange(_khachTraHangService.GetChoXuLy() ?? new List<PhieuTraHang>());
            list.AddRange(_traNoiBoService.GetChoXuLy() ?? new List<PhieuTraHang>());
            return list.GroupBy(x => x.Id).Select(g => g.First()).ToList();
        }

        private void RefreshBadges()
        {
            try
            {
                var rows = BuildWorkflowRows();
                var counts = new int[8];
                foreach (var row in rows)
                {
                    int step = ResolveRowStep(row);
                    if (step >= 1 && step <= 8) counts[step - 1]++;
                }
                for (int i = 0; i < 8; i++)
                    _stepButtons[i].Text = _stepButtons[i].Text.Split('\r')[0] + "\r\n(" + counts[i] + ")";
            }
            catch (Exception ex)
            {
                ShowWarning("Không thể cập nhật tiến trình.\r\n\r\n" + ex.Message);
            }
        }

        private int ResolveRowStep(WorkflowRow row)
        {
            if (!row.PhieuXuLyId.HasValue) return row.PhieuTraHangStatus == PhieuTraHangStatus.Moi ? 1 : 2;
            var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
            return p == null ? 2 : ResolveStep(p);
        }

        private void LoadCurrentStep()
        {
            try
            {
                var all = BuildWorkflowRows();
                _rows = all.Where(x => ResolveRowStep(x) == _activeStep).ToList();
                _grid.DataSource = _rows;
                ConfigureGrid();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                ShowWarning("Không thể tải dữ liệu tiến trình.\r\n\r\n" + ex.Message);
            }
        }

        private List<WorkflowRow> BuildWorkflowRows()
        {
            var result = new List<WorkflowRow>();
            foreach (var header in GetHeaders())
            {
                var row = new WorkflowRow
                {
                    PhieuTraHangId = header.Id,
                    SoPhieu = header.SoPhieu,
                    PhieuTraHangStatus = header.Status,
                    Nguon = header.Nguon.ToString(),
                    BoPhanPhatHienLoi = header.BoPhanPhatHienLoi
                };

                var p = _phieuXuLyRepo.GetByPhieuTraHangId(header.Id);
                if (p != null)
                {
                    row.PhieuXuLyId = p.Id;
                    row.Model = p.Model;
                    row.PhanLoaiXuLy = p.PhanLoaiXuLy;
                    row.HuongXuLy = p.HuongXuLy.ToString();
                    row.QTStatus = _phieuXuLyRepo.GetStatus(p.Id) ?? p.Status;
                    row.SoLuongLoi = p.SoLuongLoi;
                    row.MaSanPham = p.MaSanPham;
                    row.SoLo = p.SoLo;
                    if (_initialQCService != null)
                    {
                        var initial = _initialQCService.Get(p.Id);
                        if (initial != null)
                        {
                            row.InitialQC = $"QC {initial.SoLuongDaKiemTra:n0}/{initial.SoLuongAnhHuong:n0} | OK {initial.SoLuongOK:n0} | NG {initial.SoLuongNG:n0} | RW {initial.SoLuongRework:n0} | Loại bỏ {initial.SoLuongLoaiBoBanDau:n0}";
                        }
                    }
                }
                result.Add(row);
            }
            return result;
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
            var col = new DevExpress.XtraGrid.Columns.GridColumn { FieldName = field, Caption = caption, Visible = visible, Width = width };
            _gridView.Columns.Add(col);
        }

        private void ApplyFilter()
        {
            var kw = _txtSearch.Text == null ? string.Empty : _txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(kw)) { _gridView.ActiveFilterString = string.Empty; return; }
            var safe = kw.Replace("'", "''");
            _gridView.ActiveFilterString =
                $"Contains([SoPhieu], '{safe}') Or Contains([Model], '{safe}') Or Contains([MaSanPham], '{safe}') Or Contains([SoLo], '{safe}') Or Contains([Nguon], '{safe}') Or Contains([HuongXuLy], '{safe}')";
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
            using (var f = new FormTiepNhanPhieuKhachTra(_khachTraHangService))
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void TaoPhieuNoiBo()
        {
            using (var f = new FormTaoPhieuTraNoiBo(_traNoiBoService))
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void TaoPhieuTuSlot()
        {
            using (var f = new FormChonSlotNoiBo(_traNoiBoService, _phieuTraHangRepo, _qtChungService, _slotService))
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void TaoPhieuXuLyBatThuong()
        {
            var row = GetFocusedRow();
            if (row == null) return;
            var items = _phieuTraHangRepo.GetItems(row.PhieuTraHangId);
            if (items == null || items.Count == 0) { ShowWarning("Phiếu trả hàng chưa có dòng chi tiết."); return; }

            int created = 0;
            foreach (var item in items)
            {
                if (_phieuXuLyRepo.GetByPhieuTraHangId(row.PhieuTraHangId) != null) break;
                _qtChungService.TaoPhieuXuLyBatThuong(item.Id, row.Model, row.PhanLoaiXuLy ?? "Hàng lỗi", Environment.UserName, Environment.UserName);
                created++;
            }
            if (created > 0) RefreshAfterAction(); else ShowWarning("Phiếu này đã có PhieuXuLyBatThuong.");
        }

        private void TruyVetLOT()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            if (_affectedLotTraceService == null) { ShowWarning("MainApp chưa inject IAffectedLotTraceService."); return; }

            var result = _affectedLotTraceService.TruyVetLOT(row.PhieuXuLyId.Value, Environment.UserName);
            var message = $"LOT: {result.LotNo}\r\nMã hàng: {result.MaSanPham}\r\nTổng ảnh hưởng: {result.TotalAffectedQuantity:n0}\r\nSnapshot: {result.Items.Count} dòng\r\n" +
                          (result.Warnings.Count == 0 ? "Truy vết hoàn tất." : "Cảnh báo:\r\n- " + string.Join("\r\n- ", result.Warnings));
            XtraMessageBox.Show(this, message, result.IsComplete ? "Truy vết LOT" : "Truy vết chưa hoàn tất", MessageBoxButtons.OK, result.IsComplete ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            RefreshAfterAction();
        }

        private void QCDinhHuong()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            using (var f = new FormQCDinhHuong(_qtChungService, row.PhieuXuLyId.Value))
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void InitialQC()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            if (_initialQCService == null || _affectedLotTraceService == null) { ShowWarning("MainApp chưa inject InitialQCService/AffectedLotTraceService."); return; }
            using (var f = new FormInitialQC(_initialQCService, _affectedLotTraceService, row.PhieuXuLyId.Value))
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void XuLyRework()
        {
            var row = GetFocusedRow();
            if (row == null || !row.PhieuXuLyId.HasValue) return;
            var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
            if (p == null) { ShowWarning("Không tìm thấy phiếu xử lý."); return; }
            if (p.HuongXuLy != HuongXuLyBatThuong.CanRework) { ShowWarning("Phiếu không thuộc nhánh Rework."); return; }
            if (_initialQCService == null || _initialQCService.Get(p.Id) == null) { ShowWarning("Chưa xác nhận Initial QC."); return; }
            using (var f = new FormReworkProcess(_qtChungService, _reworkStockService, row.PhieuXuLyId.Value))
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
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
                using (var f = new FormQCXacNhanCuoi(_qtChungService, p.Id))
                    if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
                return;
            }
            if (qc.SoLuongNG > 0)
            {
                using (var f = new FormNhapLaiHangNG(_reworkStockService, p.Id, qc.SoLuongNG))
                    if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
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
                case HuongXuLyBatThuong.ChiGiaoBu:
                    XuLyGiaoBu(row);
                    break;
                case HuongXuLyBatThuong.TuChoiGiaoBu:
                    var result = _qtChungService.HoanTat(p.Id, Environment.UserName);
                    ShowScanResult(result);
                    RefreshAfterAction();
                    break;
                case HuongXuLyBatThuong.CanRework:
                    var qc = _qtChungRepo.GetQC(p.Id);
                    if (qc != null && qc.SoLuongNG > 0)
                    {
                        using (var f = new FormNhapLaiHangNG(_reworkStockService, p.Id, qc.SoLuongNG))
                            if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
                    }
                    else
                    {
                        var done = _qtChungService.HoanTat(p.Id, Environment.UserName);
                        ShowScanResult(done);
                        RefreshAfterAction();
                    }
                    break;
                default:
                    ShowWarning("Phiếu chưa có hướng xử lý hợp lệ.");
                    break;
            }
        }

        private void XuLyGiaoBu(WorkflowRow row)
        {
            var phieu = _phieuTraHangRepo.GetById(row.PhieuTraHangId);
            if (phieu == null) { ShowWarning("Không tìm thấy phiếu trả hàng."); return; }
            using (var f = new FormGiaoBuNG(_giaoBuNGService, phieu.Id, phieu.SoPhieu))
                if (f.ShowDialog(this) == DialogResult.OK) RefreshAfterAction();
        }

        private void RefreshAfterAction()
        {
            RefreshBadges();
            var row = GetFocusedRow();
            LoadCurrentStep();
            if (row != null && row.PhieuXuLyId.HasValue)
            {
                var p = _qtChungService.GetById(row.PhieuXuLyId.Value);
                if (p != null) SetActiveStep(ResolveStep(p));
            }
        }

        private void FocusPreselectedRow()
        {
            if (!_preselectPhieuXuLyId.HasValue) return;
            var handle = _gridView.LocateByValue("PhieuXuLyId", _preselectPhieuXuLyId.Value);
            if (handle >= 0) _gridView.FocusedRowHandle = handle;
        }

        private void ShowWarning(string message)
        {
            XtraMessageBox.Show(this, message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowScanResult(ScanResult result)
        {
            if (result == null) { ShowWarning("Không nhận được kết quả từ Service."); return; }
            XtraMessageBox.Show(this, result.Message, result.IsOK ? "Thành công" : "Không thể thực hiện", MessageBoxButtons.OK, result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private sealed class WorkflowRow
        {
            public int PhieuTraHangId { get; set; }
            public int? PhieuXuLyId { get; set; }
            public string SoPhieu { get; set; }
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
        }
    }
}