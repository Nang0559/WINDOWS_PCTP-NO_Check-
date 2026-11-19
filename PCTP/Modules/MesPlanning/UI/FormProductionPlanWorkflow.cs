using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Worklist and workflow actions for imported production plans.
    /// The grid is read-only; all state changes go through ProductionWorkflowService
    /// and the shared workflow transition repository.
    /// </summary>
    public sealed class FormProductionPlanWorkflow : XtraForm
    {
        private readonly IProductionPlanService _plans;
        private readonly IProductionWorkflowService _workflow;
        private readonly TextEdit _site = new TextEdit();
        private readonly TextEdit _reason = new TextEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl();
        public long SelectedPlanLineId { get; private set; }

        public FormProductionPlanWorkflow()
            : this(new ProductionPlanService(new ProductionPlanRepository()), MesWorkflowComposition.Create())
        {
        }

        public FormProductionPlanWorkflow(IProductionPlanService plans, IProductionWorkflowService workflow)
        {
            AuthorizationGuard.Demand("MES.PLAN_UPLOAD", "VIEW");
            _plans = plans ?? throw new ArgumentNullException(nameof(plans));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _view = new GridView(_grid);
            Text = "MES - Quản lý kế hoạch và Workflow";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1050, 650);
            BuildUi();
            Load += (s, e) => LoadPlans();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(12),
                ColumnCount = 1, RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            toolbar.Controls.Add(new LabelControl { Text = "Site", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            _site.Width = 120;
            _site.Properties.NullValuePrompt = "Nhập SiteCode";
            toolbar.Controls.Add(_site);
            AddButton(toolbar, "TẢI KẾ HOẠCH", LoadPlans);
            AddButton(toolbar, "1. KIỂM TRA / VALIDATE", () => Transition(10));
            AddButton(toolbar, "2. FIRM + SINH NHU CẦU BOM", () => Transition(20));
            AddButton(toolbar, "HỦY KẾ HOẠCH", () => Transition(900));
            AddButton(toolbar, "LỊCH SỬ WORKFLOW", OpenTimeline);
            AddButton(toolbar, "CHỌN DÒNG KẾ HOẠCH", SelectPlanLine);

            _reason.Width = 220;
            _reason.Properties.NullValuePrompt = "Lý do / ghi chú (nếu có)";
            toolbar.Controls.Add(_reason);

            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsSelection.MultiSelect = false;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.FocusRectStyle = DrawFocusRectStyle.RowFocus;

            _status.Dock = DockStyle.Fill;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _status.Text = "Chọn một dòng kế hoạch. FIRM gọi thủ tục SQL chuyên biệt để sinh yêu cầu linh kiện từ BOM.";

            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(_grid, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);
        }

        private static void AddButton(Control parent, string caption, Action action)
        {
            var button = new SimpleButton { Text = caption, Width = 165, Height = 30, Margin = new Padding(4, 2, 4, 2) };
            button.Click += (s, e) => action();
            parent.Controls.Add(button);
        }

        private DataRow SelectedPlan()
        {
            return _view.GetFocusedDataRow();
        }

        private void LoadPlans()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_site.Text))
                    throw new InvalidOperationException("Nhập SiteCode để tải đúng phạm vi kế hoạch được phân quyền.");
                _grid.DataSource = _plans.GetPlanLines(_site.Text);
                _view.BestFitColumns();
                _status.Text = "Đã tải " + _view.RowCount.ToString("N0") +
                    " dòng kế hoạch. Trạng thái IMPORTED → VALIDATED → FIRM.";
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void Transition(int targetStatus)
        {
            try
            {
                var row = SelectedPlan();
                if (row == null) throw new InvalidOperationException("Chọn dòng kế hoạch cần xử lý.");
                long planId = Convert.ToInt64(row["ProductionPlanId"]);
                int current = StatusCode(Convert.ToString(row["Status"]));
                if (current == targetStatus)
                    throw new InvalidOperationException("Kế hoạch đã ở trạng thái đích.");

                string action = targetStatus == 10 ? "VALIDATE" : targetStatus == 20 ? "FIRM" : "CANCEL";
                string prompt = targetStatus == 20
                    ? "Chuyển kế hoạch sang FIRM? Hệ thống sẽ sinh nhu cầu linh kiện từ BOM trong SQL."
                    : targetStatus == 10 ? "Xác nhận kiểm tra kế hoạch?" : "Hủy kế hoạch đã chọn?";
                if (XtraMessageBox.Show(this, prompt, action, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                _workflow.TransitionPlan(planId, current, targetStatus, AuthorizationGuard.ActorCode, _reason.Text);
                _status.Text = "Đã gửi lệnh " + action + " cho kế hoạch " + Convert.ToString(row["PlanNo"]) + ".";
                LoadPlans();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void SelectPlanLine()
        {
            try
            {
                var row = SelectedPlan();
                if (row == null) throw new InvalidOperationException("Chọn một dòng kế hoạch.");
                if (StatusCode(Convert.ToString(row["Status"])) != 20)
                    throw new InvalidOperationException("Chỉ được tạo lệnh từ kế hoạch đã FIRM.");
                SelectedPlanLineId = Convert.ToInt64(row["ProductionPlanLineId"]);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void OpenTimeline()
        {
            try
            {
                var row = SelectedPlan();
                if (row == null) throw new InvalidOperationException("Chọn kế hoạch để xem lịch sử.");
                using (var form = new FormProductionWorkflowTimeline(_workflow))
                {
                    form.SetEntity("PLAN", Convert.ToInt64(row["ProductionPlanId"]));
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private static int StatusCode(string status)
        {
            switch ((status ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "IMPORTED": return 0;
                case "VALIDATED": return 10;
                case "FIRM": return 20;
                case "CANCELLED": return 900;
                default: throw new InvalidOperationException("Trạng thái kế hoạch không được hỗ trợ: " + status);
            }
        }

        private void ShowError(Exception ex)
        {
            _status.Text = ex.Message;
            XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
