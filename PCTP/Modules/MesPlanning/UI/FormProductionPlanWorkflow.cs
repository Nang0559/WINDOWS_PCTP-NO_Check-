using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Security;
using PCTP.Shared.MesControls;

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
        private readonly FvnSiteLookupEdit _site = new FvnSiteLookupEdit();
        private readonly TextEdit _reason = new TextEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private FvnConsoleScreenFrame _frame;
        private const string Function = "MES.PLAN_UPLOAD";
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
            Load += (s, e) =>
            {
                // Site chuẩn: dbo.vFVN_Site (FVN_PCTPSite liên kết IFS.FVN_Site) ∩ phạm vi tài khoản của MES.PLAN_UPLOAD.
                _site.LoadForFunction(Function);
                if (_site.SiteCount == 0)
                {
                    SetStatus(string.IsNullOrEmpty(_site.LastError)
                        ? "Không tìm thấy Site trong phạm vi được cấp cho tài khoản."
                        : _site.LastError, true);
                    return;
                }
                LoadPlans();
            };
        }

        private void BuildUi()
        {
            var session = UserSession.Current;
            _site.Width = 150;
            _site.SiteChanged += (s, e) => LoadPlans();
            _reason.Width = 280;
            _reason.Properties.NullValuePrompt = "Lý do / ghi chú (nếu có)";
            var refresh = new SimpleButton();
            FvnConsoleScreen.StyleButton(refresh, "TẢI KẾ HOẠCH", 130, (s, e) => LoadPlans());
            var filter = FvnConsoleScreen.CreateFilterCard(
                FvnConsoleScreen.Caption("Site"), _site, refresh,
                FvnConsoleScreen.Caption("Ghi chú"), _reason);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 44, WrapContents = false, BackColor = FvnConsoleTheme.Background,
                Padding = new Padding(0, 6, 0, 0)
            };
            AddButton(actions, "1. KIỂM TRA / VALIDATE", () => Transition(10), session.HasPermission(Function, "CONFIRM"));
            AddButton(actions, "2. FIRM + SINH NHU CẦU BOM", () => Transition(20), session.HasPermission(Function, "APPROVE"));
            AddButton(actions, "HỦY KẾ HOẠCH", () => Transition(900), session.HasPermission(Function, "CANCEL"));
            AddButton(actions, "LỊCH SỬ WORKFLOW", OpenTimeline, true);
            AddButton(actions, "CHỌN DÒNG KẾ HOẠCH", SelectPlanLine, true);

            FvnConsoleScreen.StyleGrid(_grid, _view);
            var gridCard = new FvnCardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            gridCard.Controls.Add(_grid);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = FvnConsoleTheme.Background };
            body.Controls.Add(gridCard);
            body.Controls.Add(actions);

            _frame = FvnConsoleScreen.Compose(this,
                "QUẢN LÝ KẾ HOẠCH SẢN XUẤT & WORKFLOW",
                "Kế hoạch theo ca: IMPORTED → VALIDATED → FIRM (sinh nhu cầu linh kiện từ BOM) | Site theo phạm vi tài khoản",
                filter, FvnConsoleScreen.FilterCardHeight, body);
            SetStatus("Chọn một dòng kế hoạch. FIRM gọi thủ tục SQL chuyên biệt để sinh yêu cầu linh kiện từ BOM.", false);
        }

        private void SetStatus(string text, bool warning)
        {
            if (_frame != null) _frame.SetStatus(text, warning);
        }

        private static void AddButton(Control parent, string caption, Action action, bool enabled)
        {
            var button = new SimpleButton
            {
                Text = caption, Width = 190, Height = 32, Margin = new Padding(0, 0, 8, 0),
                Enabled = enabled,
                ToolTip = enabled ? caption : "Bạn chỉ có quyền theo dõi, không có quyền thực hiện thao tác này."
            };
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
                if (string.IsNullOrWhiteSpace(_site.SiteCode))
                    throw new InvalidOperationException("Chọn Site để tải đúng phạm vi kế hoạch được phân quyền.");
                _grid.DataSource = _plans.GetPlanLines(_site.SiteCode);
                _view.BestFitColumns();
                _frame.SetRuntime("Site " + _site.SiteCode, true);
                SetStatus("Đã tải " + _view.RowCount.ToString("N0") +
                    " dòng kế hoạch. Trạng thái IMPORTED → VALIDATED → FIRM.", false);
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
                SetStatus("✔  Đã gửi lệnh " + action + " cho kế hoạch " + Convert.ToString(row["PlanNo"]) + ".", false);
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
            SetStatus(ex.Message, true);
            XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
