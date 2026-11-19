using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Read-only, role-scoped MES Control Tower. All commands navigate to the owning
    /// workflow forms; this screen has no transition, posting or editing operation.
    /// </summary>
    public sealed class FormMesProductionControlTower : XtraForm
    {
        private readonly IProductionFlowService _service;
        private readonly TextEdit _site = new TextEdit();
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly MemoEdit _details = new MemoEdit();
        private readonly ProgressBarControl _componentProgress = new ProgressBarControl();
        private readonly ProgressBarControl _operationProgress = new ProgressBarControl();
        private readonly LabelControl _status = new LabelControl();
        private readonly LabelControl _refreshStamp = new LabelControl();
        private readonly PanelControl[] _kpiCards = new PanelControl[5];
        private readonly LabelControl[] _kpiValues = new LabelControl[5];
        private readonly LabelControl[] _kpiCaptions = new LabelControl[5];
        private readonly SimpleButton _refresh = new SimpleButton();
        private readonly SimpleButton _openPlan = new SimpleButton();
        private readonly SimpleButton _openPart = new SimpleButton();
        private readonly SimpleButton _openProduction = new SimpleButton();
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private readonly Timer _refreshTimer = new Timer();
        private DataTable _data = new DataTable();

        public FormMesProductionControlTower()
            : this(MesProductionFlowComposition.Create())
        {
        }

        public FormMesProductionControlTower(IProductionFlowService service)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_FLOW", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _view = new GridView(_grid);
            Text = "MES CONTROL TOWER - Dòng chảy sản xuất";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1180, 720);
            BuildUi();
            ConfigureWorker();
            Load += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_site.Text))
                {
                    var scope = UserSession.Current.DataScopes.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.SiteCode));
                    if (scope != null) _site.Text = scope.SiteCode;
                }
                LoadFlow();
            };
            FormClosed += (s, e) => { _refreshTimer.Stop(); };
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(12),
                ColumnCount = 1, RowCount = 4, BackColor = Color.FromArgb(245, 247, 250)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var header = new PanelControl { Dock = DockStyle.Fill, BorderStyle = BorderStyles.NoBorder };
            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = 700, WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2, 10, 0, 0)
            };
            filters.Controls.Add(MakeCaption("SITE"));
            _site.Width = 110; _site.Properties.NullValuePrompt = "SiteCode";
            filters.Controls.Add(_site);
            filters.Controls.Add(MakeCaption("TỪ NGÀY"));
            ConfigureDate(_from, DateTime.Today.AddDays(-7));
            filters.Controls.Add(_from);
            filters.Controls.Add(MakeCaption("ĐẾN NGÀY"));
            ConfigureDate(_to, DateTime.Today.AddDays(30));
            filters.Controls.Add(_to);
            _refresh.Text = "LÀM MỚI"; _refresh.Width = 112; _refresh.Height = 32;
            _refresh.ImageOptions.SvgImageSize = new Size(16, 16);
            _refresh.Click += (s, e) => LoadFlow();
            filters.Controls.Add(_refresh);
            header.Controls.Add(filters);

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, Width = 440, WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0)
            };
            ConfigureAction(_openProduction, "TÁC VỤ SẢN XUẤT", UserSession.Current.HasPermission("MES.PRODUCTION_EXECUTION", "VIEW"));
            _openProduction.Click += (s, e) => OpenProduction();
            ConfigureAction(_openPart, "TÁC VỤ LINH KIỆN", UserSession.Current.HasPermission("MES.COMPONENT_DEMAND", "VIEW") && UserSession.Current.HasPermission("MES.COMPONENT_ISSUE", "VIEW"));
            _openPart.Click += (s, e) => OpenPart();
            ConfigureAction(_openPlan, "KẾ HOẠCH", UserSession.Current.HasPermission("MES.PLAN_UPLOAD", "VIEW"));
            _openPlan.Click += (s, e) => OpenPlan();
            actionPanel.Controls.Add(_openProduction);
            actionPanel.Controls.Add(_openPart);
            actionPanel.Controls.Add(_openPlan);
            header.Controls.Add(actionPanel);

            var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 4, 0, 4) };
            for (int i = 0; i < 5; i++)
            {
                kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
                _kpiCards[i] = new PanelControl
                {
                    Dock = DockStyle.Fill, Margin = new Padding(4),
                    BorderStyle = BorderStyles.Simple,
                    Appearance = { BackColor = Color.White, Options = { UseBackColor = true } }
                };
                _kpiCaptions[i] = new LabelControl
                {
                    Dock = DockStyle.Top, Height = 26,
                    Appearance = { ForeColor = Color.FromArgb(95, 107, 122), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }
                };
                _kpiValues[i] = new LabelControl
                {
                    Dock = DockStyle.Fill,
                    Appearance = { ForeColor = Color.FromArgb(28, 48, 73), Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold) }
                };
                _kpiCards[i].Controls.Add(_kpiValues[i]);
                _kpiCards[i].Controls.Add(_kpiCaptions[i]);
                kpis.Controls.Add(_kpiCards[i], i, 0);
            }
            SetKpiCaptions();
            Color[] stageColors =
            {
                Color.FromArgb(239, 246, 255), Color.FromArgb(255, 248, 230),
                Color.FromArgb(244, 246, 249), Color.FromArgb(235, 243, 255),
                Color.FromArgb(232, 247, 238)
            };
            for (int i = 0; i < _kpiCards.Length; i++)
            {
                _kpiCards[i].Appearance.BackColor = stageColors[i];
                _kpiCards[i].Appearance.Options.UseBackColor = true;
            }

            var split = new SplitContainerControl { Dock = DockStyle.Fill, Horizontal = false, SplitterPosition = 840 };
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ShowIndicator = true;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsSelection.MultiSelect = false;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.FocusRectStyle = DrawFocusRectStyle.RowFocus;
            _view.RowCellStyle += View_RowCellStyle;
            _view.CustomColumnDisplayText += (s, e) =>
            {
                if (e.Column.FieldName == "IsRelatedDepartment")
                    e.DisplayText = Convert.ToBoolean(e.Value) ? "Liên quan · chỉ xem" : "Phạm vi của tôi";
                else if (e.Column.FieldName == "FlowStage")
                {
                    string stage = Convert.ToString(e.Value);
                    e.DisplayText = stage == "PLAN" ? "Kế hoạch" :
                        stage == "COMPONENTS" ? "Cấp linh kiện" :
                        stage == "ORDER" ? "Chờ lệnh SX" :
                        stage == "PRODUCTION" ? "Đang sản xuất" :
                        stage == "DONE" ? "Hoàn thành" : stage;
                }
                else if (e.Column.FieldName == "WaitingAt")
                {
                    string waiting = Convert.ToString(e.Value);
                    e.DisplayText = waiting == "PLANNING" ? "Kế hoạch" :
                        waiting == "PART" ? "Kho linh kiện" :
                        waiting == "PRODUCTION" ? "Sản xuất" : waiting;
                }
            };
            _view.FocusedRowChanged += (s, e) => ShowSelectedDetails();
            _view.DoubleClick += (s, e) => OpenSelectedTask();
            _details.Dock = DockStyle.Fill;
            _details.Properties.ReadOnly = true;
            _details.Properties.ScrollBars = ScrollBars.Vertical;
            _details.Properties.Appearance.Font = new Font("Segoe UI", 9.25f);
            _details.EditValue = "Chọn một dòng để xem chi tiết dòng chảy, bộ phận liên quan và điểm đang chờ.";
            var detailPanel = new PanelControl { Dock = DockStyle.Fill, BorderStyle = BorderStyles.NoBorder, Padding = new Padding(8) };
            var detailTitle = new LabelControl
            {
                Dock = DockStyle.Top, Height = 28, Text = "CHI TIẾT DÒNG CHẢY / ĐIỂM CHỜ",
                Appearance = { Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(45, 65, 88) }
            };
            var progressPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 84, WrapContents = false,
                FlowDirection = FlowDirection.TopDown, Padding = new Padding(2, 3, 2, 2)
            };
            ConfigureProgress(_componentProgress);
            ConfigureProgress(_operationProgress);
            progressPanel.Controls.Add(new LabelControl { Text = "CẤP LINH KIỆN", Width = 260, Height = 18 });
            progressPanel.Controls.Add(_componentProgress);
            progressPanel.Controls.Add(new LabelControl { Text = "CÔNG ĐOẠN", Width = 260, Height = 18 });
            progressPanel.Controls.Add(_operationProgress);
            detailPanel.Controls.Add(_details);
            detailPanel.Controls.Add(progressPanel);
            detailPanel.Controls.Add(detailTitle);
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(detailPanel);

            var footer = new PanelControl { Dock = DockStyle.Fill, BorderStyle = BorderStyles.NoBorder };
            _status.Dock = DockStyle.Fill;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _refreshStamp.Dock = DockStyle.Right;
            _refreshStamp.Width = 240;
            _refreshStamp.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Far;
            _refreshStamp.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            footer.Controls.Add(_status);
            footer.Controls.Add(_refreshStamp);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(kpis, 0, 1);
            root.Controls.Add(split, 0, 2);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
        }

        private void ConfigureWorker()
        {
            _worker.DoWork += (s, e) =>
            {
                var args = (LoadArgs)e.Argument;
                e.Result = _service.GetDashboard(args.Site, args.From, args.To);
            };
            _worker.RunWorkerCompleted += (s, e) =>
            {
                _refresh.Enabled = true;
                if (e.Error != null)
                {
                    _status.Text = "Không tải được dòng chảy: " + e.Error.GetBaseException().Message;
                    _status.Appearance.ForeColor = Color.Firebrick;
                    return;
                }
                _data = e.Result as DataTable ?? new DataTable();
                EnrichActionableTasks();
                _grid.DataSource = _data;
                ConfigureColumns();
                UpdateKpis();
                _refreshStamp.Text = "Cập nhật: " + DateTime.Now.ToString("HH:mm:ss");
                _status.Text = "Đang theo dõi " + _data.Rows.Count.ToString("N0") +
                    " dòng kế hoạch trong phạm vi được cấp. Màn hình chỉ đọc.";
                _status.Appearance.ForeColor = Color.FromArgb(70, 85, 100);
                ShowSelectedDetails();
            };
            _refreshTimer.Interval = 30000;
            _refreshTimer.Tick += (s, e) => LoadFlow();
            _refreshTimer.Start();
        }

        private void LoadFlow()
        {
            if (_worker.IsBusy) return;
            if (string.IsNullOrWhiteSpace(_site.Text))
            {
                _status.Text = "Nhập SiteCode để xem dòng chảy thuộc phạm vi của bạn.";
                return;
            }
            _refresh.Enabled = false;
            _status.Text = "Đang đồng bộ trạng thái kế hoạch, linh kiện, lệnh và sản xuất...";
            _status.Appearance.ForeColor = Color.SteelBlue;
            _worker.RunWorkerAsync(new LoadArgs
            {
                Site = _site.Text.Trim(),
                From = _from.EditValue == null || _from.EditValue == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(_from.EditValue).Date,
                To = _to.EditValue == null || _to.EditValue == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(_to.EditValue).Date
            });
        }

        private void ConfigureColumns()
        {
            string[] hidden = { "ProductionPlanLineId", "ProductionPlanId", "ProductionOrderId", "ComponentDemandId", "PlanWorkflowStatusCode", "OrderWorkflowStatusCode" };
            foreach (var name in hidden)
                if (_view.Columns[name] != null) _view.Columns[name].Visible = false;
            SetCaption("IsRelatedDepartment", "Phạm vi");
            SetCaption("PlanNo", "Kế hoạch");
            SetCaption("ProductionDate", "Ngày SX");
            SetCaption("ShiftCode", "Ca");
            SetCaption("ProductionLineCode", "Chuyền");
            SetCaption("DepartmentCode", "Bộ phận");
            SetCaption("ProductPartNo", "Mã sản phẩm");
            SetCaption("ProductDescription", "Tên sản phẩm");
            SetCaption("PlannedQty", "SL kế hoạch");
            SetCaption("PlanStatus", "Trạng thái KH");
            SetCaption("DemandStatus", "Trạng thái linh kiện");
            SetCaption("ComponentCount", "Số mã LK");
            SetCaption("RequiredComponentQty", "SL LK yêu cầu");
            SetCaption("IssuedComponentQty", "SL LK đã cấp");
            SetCaption("OutstandingComponentQty", "SL LK còn thiếu");
            SetCaption("OrderNo", "Lệnh SX");
            SetCaption("OrderStatus", "Trạng thái lệnh");
            SetCaption("OrderPlannedQty", "SL lệnh");
            SetCaption("CompletedQty", "SL hoàn thành");
            SetCaption("ProductionLotCount", "Số LOT");
            SetCaption("ProducedQty", "SL LOT đã SX");
            SetCaption("OperationCount", "Số công đoạn");
            SetCaption("CompletedOperationCount", "CĐ hoàn thành");
            SetCaption("FlowStage", "Giai đoạn");
            SetCaption("MyTask", "Việc thuộc quyền xử lý");
            SetCaption("WaitingAt", "Đang chờ tại");
            SetCaption("WaitingReason", "Điểm chờ / cảnh báo");
            SetCaption("LastEventAtUtc", "Cập nhật workflow");
            SetCaption("LastEventAction", "Thao tác gần nhất");
            SetCaption("LastEventActor", "Người thao tác");
            if (_view.Columns["IsRelatedDepartment"] != null)
                _view.Columns["IsRelatedDepartment"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.Custom;
            _view.BestFitColumns();
            if (_view.Columns["MyTask"] != null) _view.Columns["MyTask"].VisibleIndex = 0;
            if (_view.Columns["IsRelatedDepartment"] != null) _view.Columns["IsRelatedDepartment"].VisibleIndex = 1;
            if (_view.Columns["FlowStage"] != null) _view.Columns["FlowStage"].VisibleIndex = 2;
            if (_view.Columns["WaitingAt"] != null) _view.Columns["WaitingAt"].VisibleIndex = 3;
            if (_view.Columns["WaitingReason"] != null) _view.Columns["WaitingReason"].Width = 260;
            if (_view.Columns["ProductDescription"] != null) _view.Columns["ProductDescription"].Width = 220;
        }

        private void SetCaption(string field, string caption)
        {
            if (_view.Columns[field] != null) _view.Columns[field].Caption = caption;
        }

        private void EnrichActionableTasks()
        {
            if (!_data.Columns.Contains("MyTask"))
                _data.Columns.Add("MyTask", typeof(string));
            var session = UserSession.Current;
            foreach (DataRow row in _data.Rows)
            {
                row["MyTask"] = "Theo dõi";
                // Cross-department dependencies are context only, never actionable.
                if (Convert.ToBoolean(row["IsRelatedDepartment"])) continue;

                string stage = Convert.ToString(row["FlowStage"]);
                string planStatus = Convert.ToString(row["PlanStatus"]);
                string site = Convert.ToString(row["SiteCode"]);
                string department = Convert.ToString(row["DepartmentCode"]);
                bool productionDepartmentScope = !string.IsNullOrWhiteSpace(department) &&
                    session.HasDataScope("MES.PRODUCTION_EXECUTION", "DEPARTMENT", department, site);

                if (stage == "PLAN" && planStatus == "IMPORTED" &&
                    session.HasPermission("MES.PLAN_UPLOAD", "VIEW") &&
                    session.HasPermission("MES.PLAN_UPLOAD", "CONFIRM"))
                    row["MyTask"] = "Kiểm tra kế hoạch";
                else if (stage == "PLAN" && planStatus == "VALIDATED" &&
                    session.HasPermission("MES.PLAN_UPLOAD", "VIEW") &&
                    session.HasPermission("MES.PLAN_UPLOAD", "APPROVE"))
                    row["MyTask"] = "FIRM kế hoạch";
                else if (stage == "COMPONENTS" &&
                    session.HasPermission("MES.COMPONENT_DEMAND", "VIEW") &&
                    session.HasPermission("MES.COMPONENT_ISSUE", "VIEW") &&
                    session.HasPermission("MES.COMPONENT_ISSUE", "CONFIRM") &&
                    session.HasDataScope("MES.COMPONENT_ISSUE", "SITE", site, site))
                    row["MyTask"] = "Cấp linh kiện";
                else if (stage == "ORDER" && productionDepartmentScope &&
                    session.HasPermission("MES.PRODUCTION_EXECUTION", "VIEW") &&
                    session.HasPermission("MES.PRODUCTION_EXECUTION", "CREATE"))
                    row["MyTask"] = "Tạo lệnh sản xuất";
                else if (stage == "PRODUCTION" && productionDepartmentScope &&
                    session.HasPermission("MES.PRODUCTION_EXECUTION", "VIEW") &&
                    session.HasPermission("MES.PRODUCTION_EXECUTION", "CONFIRM"))
                    row["MyTask"] = "Thực hiện công đoạn";
            }
        }

        private void UpdateKpis()
        {
            int[] values = new int[5];
            foreach (DataRow row in _data.Rows)
            {
                switch (Convert.ToString(row["FlowStage"]))
                {
                    case "PLAN": values[0]++; break;
                    case "COMPONENTS": values[1]++; break;
                    case "ORDER": values[2]++; break;
                    case "PRODUCTION": values[3]++; break;
                    case "DONE": values[4]++; break;
                }
            }
            for (int i = 0; i < values.Length; i++) _kpiValues[i].Text = values[i].ToString("N0");
        }

        private void SetKpiCaptions()
        {
            string[] captions = { "01  KẾ HOẠCH  →", "02  CẤP LINH KIỆN  →", "03  LỆNH SẢN XUẤT  →", "04  SẢN XUẤT THỰC TẾ  →", "05  HOÀN THÀNH" };
            for (int i = 0; i < captions.Length; i++) _kpiCaptions[i].Text = captions[i];
        }

        private void View_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            var row = _view.GetDataRow(e.RowHandle);
            if (row == null || !row.Table.Columns.Contains("FlowStage")) return;
            if (Convert.ToBoolean(row["IsRelatedDepartment"]))
            {
                e.Appearance.BackColor = Color.FromArgb(244, 246, 248);
                e.Appearance.ForeColor = Color.FromArgb(105, 115, 125);
                return;
            }
            if (e.Column.FieldName == "MyTask" && Convert.ToString(row["MyTask"]) != "Theo dõi")
            {
                e.Appearance.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                e.Appearance.ForeColor = Color.FromArgb(23, 89, 154);
            }
            string stage = Convert.ToString(row["FlowStage"]);
            if (stage == "COMPONENTS")
            {
                e.Appearance.BackColor = Color.FromArgb(255, 247, 230);
                e.Appearance.ForeColor = Color.FromArgb(145, 82, 12);
            }
            else if (stage == "DONE")
            {
                e.Appearance.BackColor = Color.FromArgb(232, 247, 238);
                e.Appearance.ForeColor = Color.FromArgb(25, 105, 62);
            }
            else if (stage == "PRODUCTION")
            {
                e.Appearance.BackColor = Color.FromArgb(235, 243, 255);
                e.Appearance.ForeColor = Color.FromArgb(35, 77, 130);
            }
        }

        private static void ConfigureProgress(ProgressBarControl progress)
        {
            progress.Width = 260;
            progress.Height = 20;
            progress.Properties.Minimum = 0;
            progress.Properties.Maximum = 100;
            progress.Properties.ShowTitle = true;
            progress.Position = 0;
        }

        private void ShowSelectedDetails()
        {
            var row = _view.GetFocusedDataRow();
            if (row == null)
            {
                _componentProgress.Position = 0;
                _operationProgress.Position = 0;
                return;
            }
            decimal required = SafeDecimal(row["RequiredComponentQty"]);
            decimal issued = SafeDecimal(row["IssuedComponentQty"]);
            int componentPercent = required <= 0 ? 0 : (int)Math.Min(100m, Math.Max(0m, issued / required * 100m));
            int operationCount = SafeInt(row["OperationCount"]);
            int completedOperations = SafeInt(row["CompletedOperationCount"]);
            _componentProgress.Position = componentPercent;
            _operationProgress.Position = operationCount <= 0 ? 0 :
                (int)Math.Min(100m, Math.Max(0m, completedOperations * 100m / operationCount));
            string related = Convert.ToBoolean(row["IsRelatedDepartment"])
                ? "BỘ PHẬN LIÊN QUAN (chỉ xem)" : "BỘ PHẬN TRONG PHẠM VI";
            _details.Text =
                related + Environment.NewLine +
                "Kế hoạch: " + Convert.ToString(row["PlanNo"]) + Environment.NewLine +
                "Sản phẩm: " + Convert.ToString(row["ProductPartNo"]) + " - " + Convert.ToString(row["ProductDescription"]) + Environment.NewLine +
                "Bộ phận / chuyền: " + Convert.ToString(row["DepartmentCode"]) + " / " + Convert.ToString(row["ProductionLineCode"]) + Environment.NewLine +
                "Kế hoạch: " + Convert.ToString(row["PlanStatus"]) + " | SL: " + Convert.ToString(row["PlannedQty"]) + Environment.NewLine +
                "Linh kiện: " + Convert.ToString(row["DemandStatus"]) + " | Mã linh kiện: " + Convert.ToString(row["ComponentCount"]) + Environment.NewLine +
                "Linh kiện còn thiếu: " + Convert.ToString(row["OutstandingComponentQty"]) + Environment.NewLine +
                "Lệnh: " + Convert.ToString(row["OrderNo"]) + " | " + Convert.ToString(row["OrderStatus"]) + Environment.NewLine +
                "LOT: " + Convert.ToString(row["ProductionLotCount"]) + " | SL đã SX: " + Convert.ToString(row["ProducedQty"]) + Environment.NewLine +
                "Công đoạn: " + Convert.ToString(row["CompletedOperationCount"]) + "/" + Convert.ToString(row["OperationCount"]) + Environment.NewLine +
                "Việc thuộc quyền: " + Convert.ToString(row["MyTask"]) + Environment.NewLine +
                "Đang chờ tại: " + Convert.ToString(row["WaitingAt"]) + Environment.NewLine +
                "Lý do: " + Convert.ToString(row["WaitingReason"]) + Environment.NewLine +
                "Cập nhật gần nhất: " + Convert.ToString(row["LastEventAtUtc"]) + " | " +
                Convert.ToString(row["LastEventAction"]) + " | " + Convert.ToString(row["LastEventActor"]);
        }

        private void OpenSelectedTask()
        {
            var row = _view.GetFocusedDataRow();
            if (row == null || Convert.ToBoolean(row["IsRelatedDepartment"])) return;
            string task = Convert.ToString(row["MyTask"]);
            if (task == "Kiểm tra kế hoạch" || task == "FIRM kế hoạch")
                OpenPlan();
            else if (task == "Cấp linh kiện")
                OpenPart();
            else if (task == "Tạo lệnh sản xuất" || task == "Thực hiện công đoạn")
                OpenProduction();
        }

        private void OpenPlan()
        {
            using (var form = new FormProductionPlanWorkflow()) form.ShowDialog(this);
            LoadFlow();
        }
        private void OpenPart()
        {
            using (var form = new FormYeuCauVaXuatLinhKien()) form.ShowDialog(this);
            LoadFlow();
        }
        private void OpenProduction()
        {
            using (var form = new FormDieuHanhSanXuat()) form.ShowDialog(this);
            LoadFlow();
        }

        private static decimal SafeDecimal(object value)
        {
            if (value == null || value == DBNull.Value) return 0m;
            decimal result;
            return decimal.TryParse(Convert.ToString(value), out result) ? result : 0m;
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            int result;
            return int.TryParse(Convert.ToString(value), out result) ? result : 0;
        }

        private static LabelControl MakeCaption(string text)
        {
            return new LabelControl
            {
                Text = text, AutoSizeMode = LabelAutoSizeMode.None, Width = 68, Height = 30,
                Padding = new Padding(2, 7, 2, 0),
                Appearance = { Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.FromArgb(82, 96, 112) }
            };
        }

        private static void ConfigureDate(DateEdit edit, DateTime value)
        {
            edit.Width = 116;
            edit.Properties.Mask.EditMask = "dd/MM/yyyy";
            edit.Properties.Mask.UseMaskAsDisplayFormat = true;
            edit.EditValue = value;
        }

        private static void ConfigureAction(SimpleButton button, string text, bool allowed)
        {
            button.Text = text;
            button.Width = 136;
            button.Height = 32;
            button.Enabled = allowed;
            button.ToolTip = allowed ? "Mở màn hình nghiệp vụ theo quyền hiện có." : "Bạn chỉ được theo dõi; không có quyền mở nghiệp vụ này.";
        }

        private sealed class LoadArgs
        {
            public string Site;
            public DateTime? From;
            public DateTime? To;
        }
    }
}