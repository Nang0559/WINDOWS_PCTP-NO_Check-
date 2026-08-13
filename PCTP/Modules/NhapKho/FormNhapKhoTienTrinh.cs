using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.UserControls;
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
    // PCTP/VIEWSTOCK/ViewForm/FormNhapKhoTienTrinh.cs
    public partial class FormNhapKhoTienTrinh : XtraForm
    {
        private readonly SQLPROVIDER _sql = new SQLPROVIDER();
        private readonly INhapKhoDashboardRepository _dashRepo;
        private readonly MainStockSV _mainStockForm;

        private TableLayoutPanel _pnlTimeline; // Dùng TableLayoutPanel thay FlowLayoutPanel để tự co giãn
        private TimelineStepButton[] _steps;
        private int _activeStep = 1; // Chỉ còn 3 mốc: 1 (Chờ nhập), 2 (Đã nhập), 3 (Lệch)
        private GridControl _grid;
        private GridView _gridView;
        private SimpleButton _btnAction;
        private SimpleButton _btnScanAction; // Nút mở màn hình quét nhập riêng biệt

        public FormNhapKhoTienTrinh(MainStockSV mainStockForm)
        {
            _dashRepo = new NhapKhoDashboardRepository(_sql);
            _mainStockForm = mainStockForm;
            BuildUI();
            RefreshBadges();
            SetActiveStep(1);
        }

        private void BuildUI()
        {
            Text = "Quản lý tiến trình Nhập kho";
            Size = new Size(1250, 720);
            StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // Row 0: Timeline (Tăng lên 120 để chứa trọn vẹn nút, không bị cắt)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Row 1: Toolbar cố định chiều cao chuẩn, không bị bóp méo
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Row 2: Grid chiếm phần còn lại

            // ── Row 0: Timeline tự co giãn 3 mốc ───────────────────────────
            _pnlTimeline = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 5, // 3 Bước + 2 Mũi tên xen kẽ
                BackColor = Color.FromArgb(240, 243, 246)
            };

            for (int i = 0; i < 5; i++)
            {
                if (i % 2 == 0) _pnlTimeline.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 / 3f));
                else _pnlTimeline.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            _steps = new[]
            {
        new TimelineStepButton(1, "1. Phiếu SX chờ nhập", "Chờ tiếp nhận", Color.FromArgb(220, 53, 69)),
        new TimelineStepButton(2, "2. Đã nhập hôm nay", "Đã hoàn tất", Color.FromArgb(40, 167, 69)),
        new TimelineStepButton(3, "3. Đối chiếu lệch A0/Slot", "Cần kiểm tra", Color.FromArgb(253, 126, 20)),
    };

            for (int i = 0; i < _steps.Length; i++)
            {
                var step = _steps[i];
                step.Dock = DockStyle.Fill;
                step.StepClicked += (s, e) => SetActiveStep(step.StepIndex);
                _pnlTimeline.Controls.Add(step, i * 2, 0);

                if (i < _steps.Length - 1)
                {
                    var arrow = new LabelControl
                    {
                        Text = "▶",
                        Appearance = { ForeColor = Color.FromArgb(150, 160, 170), Font = new Font("Tahoma", 12) },
                        AutoSizeMode = LabelAutoSizeMode.Horizontal,
                        Dock = DockStyle.Fill
                    };
                    _pnlTimeline.Controls.Add(arrow, i * 2 + 1, 0);
                }
            }
            mainLayout.Controls.Add(_pnlTimeline, 0, 0);

            // ── Row 1: Toolbar chứa nút Quét nhập và Làm mới (Dùng Panel chống bóp méo) ──
            var toolbarPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };

            _btnAction = new SimpleButton { Text = "🔄 Làm mới", Width = 115, Height = 33 };
            _btnAction.Location = new Point(toolbarPanel.Width - 125, 6);
            _btnAction.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnAction.Appearance.Font = new Font("Tahoma", 9F);
            _btnAction.Click += (s, e) => RefreshDataByActiveStep();

            _btnScanAction = new SimpleButton { Text = "📷 Quét nhập", Width = 160, Height = 33 };
            _btnScanAction.Location = new Point(toolbarPanel.Width - 295, 6);
            _btnScanAction.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnScanAction.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            _btnScanAction.Appearance.ForeColor = Color.DarkBlue;
            _btnScanAction.Click += (s, e) => MoFormScanNhap();

            toolbarPanel.Controls.Add(_btnScanAction);
            toolbarPanel.Controls.Add(_btnAction);
            mainLayout.Controls.Add(toolbarPanel, 0, 1);

            // ── Row 2: Grid ──────────────────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ColumnAutoWidth = true;
            _gridView.DoubleClick += (s, e) => MoFormScanNhap();
            mainLayout.Controls.Add(_grid, 0, 2);

            Controls.Add(mainLayout);
        }

        private void RefreshBadges()
        {
            _steps[0].Count = _dashRepo.DemPhieuChoNhap();
            _steps[1].Count = _dashRepo.DemDaNhapHomNay();
            _steps[2].Count = _dashRepo.DemLechDoiChieu();
        }

        private void SetActiveStep(int step)
        {
            _activeStep = step;
            foreach (var s in _steps) s.SetActive(s.StepIndex == step);

            RefreshDataByActiveStep();
        }

        private void RefreshDataByActiveStep()
        {
            switch (_activeStep)
            {
                case 1:
                    _grid.DataSource = _dashRepo.GetGridChoNhap();
                    _btnAction.Text = "🔄 Làm mới (Chờ)";
                    break;
                case 2:
                    _grid.DataSource = _dashRepo.GetGridDaNhapHomNay();
                    _btnAction.Text = "🔄 Làm mới (Đã nhập)";
                    break;
                case 3:
                    _grid.DataSource = _dashRepo.GetGridLechDoiChieu();
                    _btnAction.Text = "🔄 Làm mới (Lệch)";
                    break;
            }
            _gridView.PopulateColumns();
            _gridView.BestFitColumns();
        }

        private void MoFormScanNhap()
        {
            using (var f = new FormEnterItemSV(_mainStockForm))
                f.ShowDialog(this);

            RefreshBadges();
            RefreshDataByActiveStep(); // Cập nhật lại dữ liệu grid sau khi quét xong
        }
    }
}