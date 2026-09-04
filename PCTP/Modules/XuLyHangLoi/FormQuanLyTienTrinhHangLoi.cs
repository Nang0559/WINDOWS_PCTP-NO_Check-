using DevExpress.XtraCharts.Native;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Modules.XuLyHangLoi;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.Shared.Helpers;
using PCTP.UserControls;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Repository;
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



    public partial class FormQuanLyTienTrinhHangLoi : XtraForm
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly IKhachTraHangService _khachTraHangService;
        private readonly ITraNoiBoService _traNoiBoService;
        private readonly IQTChungService _qtChungService;
        private readonly IReworkStockService _reworkStockService;
        private readonly IGiaoBuNGService _giaoBuNGService;

        // ============================================================
        // REPOSITORIES - CHỈ DÙNG ĐỌC / HIỂN THỊ DỮ LIỆU
        // ============================================================

        private readonly IPhieuTraHangRepository _phieuTraHangRepo;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly ITraHangQTChungRepository _qtChungRepo;
        private readonly IPhieuGiaoRepository _phieuGiaoRepo;

        // ============================================================
        // TIMELINE
        // ============================================================

        private TableLayoutPanel _pnlTimeline;
        private TimelineStepButton[] _steps;
        private int _activeStep = 1;

        // ============================================================
        // GRID
        // ============================================================

        private TextEdit _txtSearch;
        private GridControl _grid;
        private GridView _gridView;

        private SimpleButton _btnActionPrimary;
        private SimpleButton _btnActionSecondary;
        private SimpleButton _btnExportExcel;

        private LabelControl _lblHint;

        // ============================================================
        // DATA
        // ============================================================

        private List<WorkflowRow> _rows = new List<WorkflowRow>();


        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public FormQuanLyTienTrinhHangLoi(
            IKhachTraHangService khachTraHangService,
            ITraNoiBoService traNoiBoService,
            IQTChungService qtChungService,
            IReworkStockService reworkStockService,
            IGiaoBuNGService giaoBuNGService,
            IPhieuTraHangRepository phieuTraHangRepo,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            ITraHangQTChungRepository qtChungRepo,
            IPhieuGiaoRepository phieuGiaoRepo)
        {
            _khachTraHangService = khachTraHangService
                ?? throw new ArgumentNullException(nameof(khachTraHangService));

            _traNoiBoService = traNoiBoService
                ?? throw new ArgumentNullException(nameof(traNoiBoService));

            _qtChungService = qtChungService
                ?? throw new ArgumentNullException(nameof(qtChungService));

            _reworkStockService = reworkStockService
                ?? throw new ArgumentNullException(nameof(reworkStockService));

            _giaoBuNGService = giaoBuNGService
                ?? throw new ArgumentNullException(nameof(giaoBuNGService));

            _phieuTraHangRepo = phieuTraHangRepo
                ?? throw new ArgumentNullException(nameof(phieuTraHangRepo));

            _phieuXuLyRepo = phieuXuLyRepo
                ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));

            _qtChungRepo = qtChungRepo
                ?? throw new ArgumentNullException(nameof(qtChungRepo));

            _phieuGiaoRepo = phieuGiaoRepo
                ?? throw new ArgumentNullException(nameof(phieuGiaoRepo));

            BuildUI();

            RefreshAll();

            SetActiveStep(1);
        }


        // ============================================================
        // UI
        // ============================================================

        private void BuildUI()
        {
            Text = "Quản lý tiến trình hàng lỗi";
            Size = new Size(1450, 800);
            StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };

            mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 115));

            mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 42));

            mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 40));


            // ========================================================
            // TIMELINE
            // ========================================================

            _pnlTimeline = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 11,
                BackColor = Color.FromArgb(240, 243, 246)
            };

            for (int i = 0; i < 11; i++)
            {
                if (i % 2 == 0)
                {
                    _pnlTimeline.ColumnStyles.Add(
                        new ColumnStyle(
                            SizeType.Percent,
                            100f / 6f));
                }
                else
                {
                    _pnlTimeline.ColumnStyles.Add(
                        new ColumnStyle(
                            SizeType.AutoSize));
                }
            }

            _steps = new[]
            {
            new TimelineStepButton(
                1,
                "1. Tiếp nhận",
                "Chờ tiếp nhận",
                Color.FromArgb(220, 53, 69)),

            new TimelineStepButton(
                2,
                "2. Phiếu bất thường",
                "Chờ tạo phiếu",
                Color.FromArgb(253, 126, 20)),

            new TimelineStepButton(
                3,
                "3. QC định hướng",
                "Chờ QC",
                Color.FromArgb(111, 66, 193)),

            new TimelineStepButton(
                4,
                "4. Rework / Giao SX",
                "Đang xử lý",
                Color.FromArgb(23, 162, 184)),

            new TimelineStepButton(
                5,
                "5. QC xác nhận cuối",
                "Chờ QC cuối",
                Color.FromArgb(255, 193, 7)),

            new TimelineStepButton(
                6,
                "6. Hoàn tất / Giao lại",
                "Chờ xử lý cuối",
                Color.FromArgb(40, 167, 69))
        };

            for (int i = 0; i < _steps.Length; i++)
            {
                var step = _steps[i];

                step.Dock = DockStyle.Fill;

                step.StepClicked +=
                    (s, e) => SetActiveStep(step.StepIndex);

                _pnlTimeline.Controls.Add(
                    step,
                    i * 2,
                    0);

                if (i < _steps.Length - 1)
                {
                    var arrow = new LabelControl
                    {
                        Text = "▶",
                        AutoSizeMode =
                            DevExpress.XtraEditors.LabelAutoSizeMode.Horizontal,
                        Dock = DockStyle.Fill,
                        Appearance =
                    {
                        ForeColor = Color.FromArgb(150, 160, 170),
                        Font = new Font(
                            "Tahoma",
                            12,
                            FontStyle.Bold)
                    }
                    };

                    _pnlTimeline.Controls.Add(
                        arrow,
                        i * 2 + 1,
                        0);
                }
            }

            mainLayout.Controls.Add(
                _pnlTimeline,
                0,
                0);


            // ========================================================
            // TOOLBAR
            // ========================================================

            var searchPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                Padding = new Padding(3)
            };

            searchPanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100));

            searchPanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    190));

            searchPanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    210));

            searchPanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    130));


            _txtSearch = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtSearch.Properties.NullValuePrompt =
                "🔍 Tìm mã hàng, số phiếu, lot...";

            _txtSearch.Properties.Appearance.Font =
                new Font("Tahoma", 9.5F);

            _txtSearch.KeyDown +=
                (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                        ApplyFilter();
                };

            searchPanel.Controls.Add(
                _txtSearch,
                0,
                0);


            _btnActionSecondary = new SimpleButton
            {
                Text = "➕ Tạo phiếu nội bộ",
                Dock = DockStyle.Fill
            };

            _btnActionSecondary.Appearance.Font =
                new Font("Tahoma", 9F, FontStyle.Bold);

            _btnActionSecondary.Appearance.ForeColor =
                Color.DarkBlue;

            _btnActionSecondary.Click +=
                (s, e) => TaoPhieuNoiBo();

            searchPanel.Controls.Add(
                _btnActionSecondary,
                1,
                0);


            _btnActionPrimary = new SimpleButton
            {
                Dock = DockStyle.Fill
            };

            _btnActionPrimary.Appearance.Font =
                new Font("Tahoma", 9.5F, FontStyle.Bold);

            _btnActionPrimary.Click +=
                BtnActionPrimary_Click;

            searchPanel.Controls.Add(
                _btnActionPrimary,
                2,
                0);


            _btnExportExcel = new SimpleButton
            {
                Text = "📥 Xuất Excel",
                Dock = DockStyle.Fill
            };

            _btnExportExcel.Click +=
                (s, e) =>
                {
                    string fileName =
                        $"HangLoi_Buoc{_activeStep}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                    _gridView.ExportToXlsx(fileName);
                };

            searchPanel.Controls.Add(
                _btnExportExcel,
                3,
                0);


            mainLayout.Controls.Add(
                searchPanel,
                0,
                1);


            // ========================================================
            // GRID
            // ========================================================

            _grid = new GridControl
            {
                Dock = DockStyle.Fill
            };

            _gridView = new GridView(_grid);

            _grid.MainView = _gridView;

            _gridView.OptionsBehavior.Editable = false;

            _gridView.OptionsView.ShowGroupPanel = false;

            _gridView.OptionsView.RowAutoHeight = true;

            _gridView.OptionsSelection.MultiSelect = true;

            _gridView.OptionsSelection.MultiSelectMode =
                GridMultiSelectMode.CheckBoxRowSelect;

            _gridView.DoubleClick +=
                GridView_DoubleClick;

            mainLayout.Controls.Add(
                _grid,
                0,
                2);


            // ========================================================
            // HINT
            // ========================================================

            _lblHint = new LabelControl
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 0, 0),
                Appearance =
            {
                Font = new Font(
                    "Tahoma",
                    9F,
                    FontStyle.Italic),

                ForeColor = Color.DimGray
            }
            };

            mainLayout.Controls.Add(
                _lblHint,
                0,
                3);

            Controls.Add(mainLayout);
        }


        // ============================================================
        // REFRESH
        // ============================================================

        private void RefreshAll()
        {
            RefreshBadges();
            LoadCurrentStep();
        }


        private void RefreshBadges()
        {
            try
            {
                var tatCa = new List<PhieuTraHang>();

                tatCa.AddRange(
                    _khachTraHangService.GetChoXuLy()
                    ?? new List<PhieuTraHang>());

                tatCa.AddRange(
                    _traNoiBoService.GetChoXuLy()
                    ?? new List<PhieuTraHang>());

                // Không để duplicate header
                tatCa = tatCa
                    .GroupBy(x => x.Id)
                    .Select(g => g.First())
                    .ToList();


                int buoc1 = tatCa.Count(
                    x => x.Status == PhieuTraHangStatus.Moi);


                int buoc2 = tatCa.Count(
                    x => CoPhieuDangChoTaoBatThuong(x.Id));


                int buoc3 = tatCa.Count(
                    x => CoQTState(
                        x.Id,
                        QTChungStatus.DaTaoPhieuBatThuong));


                int buoc4 = tatCa.Count(
                    x => CoQTState(
                        x.Id,
                        QTChungStatus.DaDinhHuong));


                int buoc5 = tatCa.Count(
                    x => CoQTState(
                        x.Id,
                        QTChungStatus.DaGiaoSanXuat));


                int buoc6 = tatCa.Count(
                    x => CoQTState(
                        x.Id,
                        QTChungStatus.DaQCXacNhanCuoi)
                    ||
                    CoQTState(
                        x.Id,
                        QTChungStatus.DaNhapLaiKho));


                _steps[0].Count = buoc1;
                _steps[1].Count = buoc2;
                _steps[2].Count = buoc3;
                _steps[3].Count = buoc4;
                _steps[4].Count = buoc5;
                _steps[5].Count = buoc6;
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể cập nhật số lượng tiến trình.\r\n\r\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        private bool CoPhieuDangChoTaoBatThuong(
            int phieuTraHangId)
        {
            var p = _phieuXuLyRepo
                .GetByPhieuTraHangId(phieuTraHangId);

            return p == null;
        }


        private bool CoQTState(
     int phieuTraHangId,
     QTChungStatus status)
        {
            var p = _phieuXuLyRepo
                .GetByPhieuTraHangId(phieuTraHangId);

            if (p == null)
                return false;
            var trangthai=_phieuXuLyRepo.GetStatus(p.Id);
            return trangthai == status;
        }


        // ============================================================
        // STEP
        // ============================================================

        private void SetActiveStep(int stepIndex)
        {
            _activeStep = stepIndex;

            foreach (var step in _steps)
            {
                step.SetActive(
                    step.StepIndex == stepIndex);
            }

            switch (stepIndex)
            {
                case 1:
                    _btnActionSecondary.Visible = true;
                    _btnActionPrimary.Text =
                        "➕ Tiếp nhận phiếu khách";
                    _lblHint.Text =
                        "Bước 1: Tiếp nhận Phiếu trả hàng khách hoặc tạo Phiếu trả nội bộ.";
                    break;

                case 2:
                    _btnActionSecondary.Visible = true;
                    _btnActionPrimary.Text =
                        "📄 Tạo phiếu bất thường";
                    _lblHint.Text =
                        "Bước 2: Tạo PhieuXuLyBatThuong từ các dòng PhieuTraHangCT.";
                    break;

                case 3:
                    _btnActionSecondary.Visible = false;
                    _btnActionPrimary.Text =
                        "✍ QC định hướng";
                    _lblHint.Text =
                        "Bước 3: QC xác định hướng TuChoiGiaoBu / ChiGiaoBu / CanRework.";
                    break;

                case 4:
                    _btnActionSecondary.Visible = false;
                    _btnActionPrimary.Text =
                        "⚙ Xử lý Rework";
                    _lblHint.Text =
                        "Bước 4: Với CanRework, thực hiện xuất kho và giao cho sản xuất.";
                    break;

                case 5:
                    _btnActionSecondary.Visible = false;
                    _btnActionPrimary.Text =
                        "🔍 QC xác nhận cuối";
                    _lblHint.Text =
                        "Bước 5: QC xác nhận OK/NG. Nếu NG > 0 sẽ chuyển sang nhập lại kho.";
                    break;

                case 6:
                    _btnActionSecondary.Visible = false;
                    _btnActionPrimary.Text =
                        "↩ Xử lý hoàn tất";
                    _lblHint.Text =
                        "Bước 6: Nhập NG, giao bù, giao lại bộ phận phát hiện hoặc hoàn tất QT chung.";
                    break;
            }

            LoadCurrentStep();
        }


        // ============================================================
        // LOAD CURRENT STEP
        // ============================================================

        private void LoadCurrentStep()
        {
            try
            {
                var rows = BuildWorkflowRows();

                IEnumerable<WorkflowRow> result;

                switch (_activeStep)
                {
                    case 1:
                        result = rows.Where(
                            x => x.PhieuTraHangStatus
                                 == PhieuTraHangStatus.Moi);
                        break;

                    case 2:
                        result = rows.Where(
                            x => x.PhieuXuLyId == null);
                        break;

                    case 3:
                        result = rows.Where(
                            x => x.QTStatus
                                 == QTChungStatus.DaTaoPhieuBatThuong);
                        break;

                    case 4:
                        result = rows.Where(
                            x => x.QTStatus
                                 == QTChungStatus.DaDinhHuong);
                        break;

                    case 5:
                        result = rows.Where(
                            x => x.QTStatus
                                 == QTChungStatus.DaGiaoSanXuat);
                        break;

                    case 6:
                        result = rows.Where(
                            x =>
                                x.QTStatus
                                == QTChungStatus.DaQCXacNhanCuoi

                                ||

                                x.QTStatus
                                == QTChungStatus.DaNhapLaiKho

                                ||

                                x.QTStatus
                                == QTChungStatus.DaGiaoBu

                                ||

                                x.QTStatus
                                == QTChungStatus.TuChoiGiaoBu);
                        break;

                    default:
                        result = rows;
                        break;
                }

                _rows = result.ToList();

                _grid.DataSource = _rows;

                ConfigureGrid();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể tải dữ liệu tiến trình.\r\n\r\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // ============================================================
        // BUILD WORKFLOW ROW
        // ============================================================

        private List<WorkflowRow> BuildWorkflowRows()
        {
            var result = new List<WorkflowRow>();

            var headers = new List<PhieuTraHang>();

            headers.AddRange(
                _khachTraHangService.GetChoXuLy()
                ?? new List<PhieuTraHang>());

            headers.AddRange(
                _traNoiBoService.GetChoXuLy()
                ?? new List<PhieuTraHang>());


            headers = headers
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();


            foreach (var header in headers)
            {
                var row = new WorkflowRow
                {
                    PhieuTraHangId = header.Id,

                    SoPhieu = header.SoPhieu,

                    PhieuTraHangStatus =
                        header.Status,

                    Nguon = header.Nguon.ToString(),

                    BoPhanPhatHienLoi =
                        header.BoPhanPhatHienLoi
                };


                var pht =
                    _phieuXuLyRepo
                        .GetByPhieuTraHangId(header.Id);


                if (pht != null)
                {
                    row.PhieuXuLyId = pht.Id;

                    row.Model = pht.Model;

                    row.PhanLoaiXuLy =
                        pht.PhanLoaiXuLy;

                    row.HuongXuLy =
                        pht.HuongXuLy.ToString();

                    row.QTStatus =
                         _phieuXuLyRepo.GetStatus(pht.Id)
                         ?? QTChungStatus.Moi; // hoặc giá trị mặc định phù hợp với nghiệp vụ

                    row.SoLuongLoi =
                        pht.SoLuongLoi;

                    row.MaSanPham =
                        pht.MaSanPham;

                    row.SoLo =
                        pht.SoLo;
                }


                result.Add(row);
            }

            return result;
        }


        // ============================================================
        // GRID
        // ============================================================

        private void ConfigureGrid()
        {
            _gridView.Columns.Clear();

            AddColumn(
                "PhieuTraHangId",
                "PT Id",
                false);

            AddColumn(
                "PhieuXuLyId",
                "QT Id",
                false);

            AddColumn(
                "SoPhieu",
                "Số phiếu",
                true,
                120);

            AddColumn(
                "Nguon",
                "Nguồn",
                true,
                100);

            AddColumn(
                "Model",
                "Model",
                true,
                90);

            AddColumn(
                "MaSanPham",
                "Mã sản phẩm",
                true,
                140);

            AddColumn(
                "SoLo",
                "Số lô",
                true,
                100);

            AddColumn(
                "SoLuongLoi",
                "SL lỗi",
                true,
                70);

            AddColumn(
                "PhanLoaiXuLy",
                "Phân loại",
                true,
                130);

            AddColumn(
                "HuongXuLy",
                "Hướng xử lý",
                true,
                130);

            AddColumn(
                "PhieuTraHangStatus",
                "Trạng thái trả hàng",
                true,
                150);

            AddColumn(
                "QTStatus",
                "QT Chung",
                true,
                150);

            AddColumn(
                "BoPhanPhatHienLoi",
                "Bộ phận phát hiện",
                true,
                150);


            _gridView.BestFitColumns();
        }


        private void AddColumn(
            string fieldName,
            string caption,
            bool visible,
            int width = 100)
        {
            var col = new GridColumn
            {
                FieldName = fieldName,
                Caption = caption,
                Visible = visible,
                Width = width
            };

            _gridView.Columns.Add(col);
        }


        // ============================================================
        // SEARCH
        // ============================================================

        private void ApplyFilter()
        {
            string kw =
                _txtSearch.Text?
                    .Trim();

            if (string.IsNullOrWhiteSpace(kw))
            {
                _gridView.ActiveFilterString = "";
                return;
            }

            string safe =
                kw.Replace(
                    "'",
                    "''");

            _gridView.ActiveFilterString =
                $"Contains([SoPhieu], '{safe}') " +
                $"Or Contains([Model], '{safe}') " +
                $"Or Contains([MaSanPham], '{safe}') " +
                $"Or Contains([SoLo], '{safe}') " +
                $"Or Contains([Nguon], '{safe}') " +
                $"Or Contains([HuongXuLy], '{safe}')";
        }


        // ============================================================
        // ACTION
        // ============================================================

        private void BtnActionPrimary_Click(
            object sender,
            EventArgs e)
        {
            ExecuteActionByStep(
                _activeStep);
        }


        private void GridView_DoubleClick(
            object sender,
            EventArgs e)
        {
            ExecuteActionByStep(
                _activeStep);
        }


        private void ExecuteActionByStep(
            int step)
        {
            switch (step)
            {
                case 1:
                    TiepNhanKhach();
                    break;

                case 2:
                    TaoPhieuXuLyBatThuong();
                    break;

                case 3:
                    QCDinhHuong();
                    break;

                case 4:
                    XuLyRework();
                    break;

                case 5:
                    QCXacNhanCuoi();
                    break;

                case 6:
                    XuLyBuocCuoi();
                    break;
            }
        }


        // ============================================================
        // STEP 1
        // ============================================================

        private void TiepNhanKhach()
        {
            using (var f =
                new FormTiepNhanPhieuKhachTra(_khachTraHangService))
            {
                if (f.ShowDialog(this)
                    != DialogResult.OK)
                    return;

                RefreshAfterAction();
            }
        }


        // ============================================================
        // STEP 2
        // ============================================================

        private void TaoPhieuXuLyBatThuong()
        {
            var row =
                GetFocusedRow();

            if (row == null)
                return;

            if (row.PhieuTraHangId <= 0)
            {
                ShowWarning(
                    "Không xác định được PhieuTraHang.");
                return;
            }

            var items =
                _phieuTraHangRepo
                    .GetItems(
                        row.PhieuTraHangId);

            if (items == null ||
                items.Count == 0)
            {
                ShowWarning(
                    "Phiếu trả hàng chưa có dòng chi tiết.");
                return;
            }


            int created = 0;

            foreach (var item in items)
            {
                try
                {
                    var existing =
                        _phieuXuLyRepo
                            .GetByPhieuTraHangId(
                                row.PhieuTraHangId);

                    if (existing != null)
                        continue;


                    _qtChungService.TaoPhieuXuLyBatThuong(
                        item.Id,
                        row.Model,
                        row.PhanLoaiXuLy
                            ?? "Hàng lỗi",
                        Environment.UserName,
                        Environment.UserName);

                    created++;
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(
                        $"Không thể tạo phiếu cho dòng {item.Id}.\r\n\r\n" +
                        ex.Message,
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }
            }


            if (created == 0)
            {
                ShowWarning(
                    "Phiếu này đã có PhieuXuLyBatThuong.");
                return;
            }


            XtraMessageBox.Show(
                $"Đã tạo {created} phiếu xử lý bất thường.",
                "Thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            RefreshAfterAction();
        }


        // ============================================================
        // STEP 3
        // ============================================================

        private void QCDinhHuong()
        {
            var row =
                GetFocusedRow();

            if (row == null)
                return;

            if (!row.PhieuXuLyId.HasValue)
            {
                ShowWarning(
                    "Phiếu chưa có PhieuXuLyBatThuong.");
                return;
            }


            using (var f =
                new FormQCDinhHuong(
                    _qtChungService,
                    row.PhieuXuLyId.Value))
            {
                if (f.ShowDialog(this)
                    == DialogResult.OK)
                {
                    RefreshAfterAction();
                }
            }
        }


        // ============================================================
        // STEP 4
        // ============================================================

        private void XuLyRework()
        {
            var row =
                GetFocusedRow();

            if (row == null)
                return;

            if (!row.PhieuXuLyId.HasValue)
            {
                ShowWarning(
                    "Không xác định được phiếu xử lý.");
                return;
            }


            var p =
                _qtChungService.GetById(
                    row.PhieuXuLyId.Value);

            if (p == null)
            {
                ShowWarning(
                    "Không tìm thấy PhieuXuLyBatThuong.");
                return;
            }


            if (p.HuongXuLy
                != HuongXuLyBatThuong.CanRework)
            {
                XtraMessageBox.Show(
                    "Phiếu này không thuộc nhánh Rework.\r\n\r\n" +
                    "Hướng xử lý hiện tại: " +
                    p.HuongXuLy,
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }


            using (var f =
                new FormReworkProcess(
                    _qtChungService,
                    _reworkStockService,
                    row.PhieuXuLyId.Value))
            {
                if (f.ShowDialog(this)
                    == DialogResult.OK)
                {
                    RefreshAfterAction();
                }
            }
        }


        // ============================================================
        // STEP 5
        // ============================================================

        private void QCXacNhanCuoi()
        {
            var row =
                GetFocusedRow();

            if (row == null)
                return;

            if (!row.PhieuXuLyId.HasValue)
            {
                ShowWarning(
                    "Không xác định được phiếu xử lý.");
                return;
            }


            using (var f =
                new FormQCXacNhanCuoi(
                    _qtChungService,
                    row.PhieuXuLyId.Value))
            {
                if (f.ShowDialog(this)
                    == DialogResult.OK)
                {
                    RefreshAfterAction();
                }
            }
        }


        // ============================================================
        // STEP 6
        // ============================================================

        private void XuLyBuocCuoi()
        {
            var row =
                GetFocusedRow();

            if (row == null)
                return;

            if (!row.PhieuXuLyId.HasValue)
            {
                ShowWarning(
                    "Không xác định được phiếu xử lý.");
                return;
            }


            var p =
                _qtChungService.GetById(
                    row.PhieuXuLyId.Value);

            if (p == null)
            {
                ShowWarning(
                    "Không tìm thấy phiếu xử lý.");
                return;
            }


            switch (p.HuongXuLy)
            {
                case HuongXuLyBatThuong.ChiGiaoBu:

                    XuLyGiaoBu(row);

                    break;


                case HuongXuLyBatThuong.TuChoiGiaoBu:

                    HoanTatTuChoi(row);

                    break;


                case HuongXuLyBatThuong.CanRework:

                    XuLySauQCRework(row);

                    break;


                default:

                    ShowWarning(
                        "Phiếu chưa có hướng xử lý hợp lệ.");

                    break;
            }
        }


        // ============================================================
        // GIAO BÙ
        // ============================================================

        private void XuLyGiaoBu(
            WorkflowRow row)
        {
            if (!row.PhieuXuLyId.HasValue)
                return;


            var phieu =
                _phieuTraHangRepo
                    .GetById(
                        row.PhieuTraHangId);

            if (phieu == null)
            {
                ShowWarning(
                    "Không tìm thấy phiếu trả hàng.");
                return;
            }


            string soPhieuKhachTra =
                phieu.SoPhieu;


            using (var f =
                new FormGiaoBuNG(
                    _giaoBuNGService,
                    phieu.Id,
                    soPhieuKhachTra))
            {
                if (f.ShowDialog(this)
                    == DialogResult.OK)
                {
                    RefreshAfterAction();
                }
            }
        }


        // ============================================================
        // TU CHỐI GIAO BÙ
        // ============================================================

        private void HoanTatTuChoi(
            WorkflowRow row)
        {
            if (!row.PhieuXuLyId.HasValue)
                return;


            var confirm =
                XtraMessageBox.Show(
                    "Phiếu đang ở nhánh Từ chối giao bù.\r\n\r\n" +
                    "Bạn có muốn hoàn tất QT chung?",
                    "Xác nhận",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;


            var result =
                _qtChungService.HoanTat(
                    row.PhieuXuLyId.Value,
                    Environment.UserName);


            ShowScanResult(
                result);

            RefreshAfterAction();
        }


        // ============================================================
        // SAU QC REWORK
        // ============================================================

        private void XuLySauQCRework(
            WorkflowRow row)
        {
            if (!row.PhieuXuLyId.HasValue)
                return;


            var qc =
                _qtChungRepo.GetQC(
                    row.PhieuXuLyId.Value);

            if (qc == null)
            {
                ShowWarning(
                    "Chưa có kết quả QC cuối.");
                return;
            }


            if (qc.SoLuongNG <= 0)
            {
                var result =
                    _qtChungService.HoanTat(
                        row.PhieuXuLyId.Value,
                        Environment.UserName);

                ShowScanResult(result);

                RefreshAfterAction();

                return;
            }


            using (var f =
                new FormNhapLaiHangNG(
                    _reworkStockService,
                    row.PhieuXuLyId.Value,
                    qc.SoLuongNG))
            {
                if (f.ShowDialog(this)
                    == DialogResult.OK)
                {
                    RefreshAfterAction();
                }
            }
        }


        // ============================================================
        // TẠO PHIẾU NỘI BỘ
        // ============================================================

        private void TaoPhieuNoiBo()
        {
            using (var f = new FormTaoPhieuTraNoiBo(_traNoiBoService))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshBadges();
                    SetActiveStep(3);
                }
            }
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private WorkflowRow GetFocusedRow()
        {
            int handle =
                _gridView.FocusedRowHandle;

            if (handle < 0)
            {
                ShowWarning(
                    "Vui lòng chọn một dòng.");

                return null;
            }


            return _gridView
                .GetRow(handle)
                as WorkflowRow;
        }


        private void RefreshAfterAction()
        {
            RefreshBadges();
            LoadCurrentStep();
        }


        private void ShowWarning(
            string message)
        {
            XtraMessageBox.Show(
                message,
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }


        private void ShowScanResult(
            ScanResult result)
        {
            if (result == null)
            {
                XtraMessageBox.Show(
                    "Không nhận được kết quả từ Service.",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }


            if (result.IsOK)
            {
                XtraMessageBox.Show(
                    result.Message,
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                XtraMessageBox.Show(
                    result.Message,
                    "Không thể thực hiện",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }


        // ============================================================
        // WORKFLOW ROW
        // ============================================================

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

            public PhieuTraHangStatus
                PhieuTraHangStatus
            { get; set; }

            public QTChungStatus
                QTStatus
            { get; set; }
        }
    }
}