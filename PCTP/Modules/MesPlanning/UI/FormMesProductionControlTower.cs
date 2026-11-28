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
using DevExpress.XtraLayout;
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
        private readonly PCTP.Shared.MesControls.FvnSiteDateRangeFilterControl _filters =
            new PCTP.Shared.MesControls.FvnSiteDateRangeFilterControl();
        private readonly PCTP.Shared.MesControls.FvnDepartmentTaskGridControl _taskGrid =
            new PCTP.Shared.MesControls.FvnDepartmentTaskGridControl();
        private readonly LabelControl _details = new LabelControl();
        private readonly PCTP.Shared.MesControls.FvnFlowStageCardsControl _stageStrip = new PCTP.Shared.MesControls.FvnFlowStageCardsControl();
        private readonly PCTP.Shared.MesControls.FvnProgressSummaryControl _progressSummary = new PCTP.Shared.MesControls.FvnProgressSummaryControl();
        private readonly PCTP.Shared.MesControls.FvnScopeBadgeControl _scopeBadge = new PCTP.Shared.MesControls.FvnScopeBadgeControl();
        private readonly PCTP.Shared.MesControls.FvnHoldAlertControl _holdAlert = new PCTP.Shared.MesControls.FvnHoldAlertControl();
        private readonly PCTP.Shared.MesControls.FvnFlowStepperControl _flowStepper = new PCTP.Shared.MesControls.FvnFlowStepperControl();
        private readonly PCTP.Shared.MesControls.FvnHoldListControl _holdList = new PCTP.Shared.MesControls.FvnHoldListControl();
        private readonly LabelControl _status = new LabelControl();
        private readonly LabelControl _refreshStamp = new LabelControl();
        private readonly CheckEdit _onlyMyTasks = new CheckEdit();
        private readonly SimpleButton _openPlan = new SimpleButton();
        private readonly SimpleButton _openPart = new SimpleButton();
        private readonly SimpleButton _openProduction = new SimpleButton();
        private readonly SimpleButton _openTask = new SimpleButton();
        private readonly SimpleButton _openTimeline = new SimpleButton();
        private readonly SimpleButton _selectFirmPlan = new SimpleButton();
        private readonly bool _selectionMode;
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private readonly Timer _refreshTimer = new Timer();
        private DataTable _data = new DataTable();
        private long _lastChangeVersion = -1;

        public long SelectedPlanLineId { get; private set; }

        public FormMesProductionControlTower()
            : this(MesProductionFlowComposition.Create(), false)
        {
        }

        public FormMesProductionControlTower(bool selectionMode)
            : this(MesProductionFlowComposition.Create(), selectionMode)
        {
        }

        public FormMesProductionControlTower(IProductionFlowService service)
            : this(service, false)
        {
        }

        public FormMesProductionControlTower(IProductionFlowService service, bool selectionMode)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_FLOW", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _selectionMode = selectionMode;
            Text = "MES CONTROL TOWER - Dòng chảy sản xuất";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1180, 720);
            _filters.RefreshRequested += (s, e) => LoadFlow();
            BuildUi();
            ConfigureWorker();
            Load += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_filters.SiteCode))
                {
                    var scope = UserSession.Current.DataScopes.FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x.SiteCode) &&
                        (string.IsNullOrWhiteSpace(x.FunctionCode) ||
                         string.Equals(x.FunctionCode, "MES.PRODUCTION_FLOW", StringComparison.OrdinalIgnoreCase)));
                    if (scope != null) _filters.SiteCode = scope.SiteCode;
                }
                LoadFlow();
            };
            FormClosed += (s, e) => { _refreshTimer.Stop(); };
        }

        private void BuildUi()
        {
            // Control Tower: bộ lọc + 5 chặng KPI + lưới dòng Firm + trạng thái.
            // Không thực hiện posting/transition tại màn hình giám sát.
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var header = new PanelControl
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyles.NoBorder,
                Padding = new Padding(4, 4, 4, 0)
            };
            _filters.Dock = DockStyle.Left;
            _filters.Width = 760;
            header.Controls.Add(_filters);

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = _selectionMode ? 250 : 690,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 12, 0, 0)
            };
            if (_selectionMode)
            {
                ConfigureAction(_selectFirmPlan, "CHỌN DÒNG FIRM", false);
                _selectFirmPlan.Click += (s, e) => SelectCurrentFirmPlan();
                actionPanel.Controls.Add(_selectFirmPlan);
            }
            else
            {
                ConfigureAction(_selectFirmPlan, "CHỌN DÒNG FIRM", false);
                _selectFirmPlan.Click += (s, e) => OpenSelectedInExecution();
                ConfigureAction(_openProduction, "ĐIỀU HÀNH SX", UserSession.Current.HasPermission("MES.PRODUCTION_EXECUTION", "VIEW"));
                _openProduction.Click += (s, e) => OpenProduction();
                ConfigureAction(_openPart, "DÒNG LINH KIỆN", UserSession.Current.HasPermission("MES.COMPONENT_DEMAND", "VIEW") && UserSession.Current.HasPermission("MES.COMPONENT_ISSUE", "VIEW"));
                _openPart.Click += (s, e) => OpenPart();
                ConfigureAction(_openPlan, "KẾ HOẠCH", UserSession.Current.HasPermission("MES.PLAN_UPLOAD", "VIEW"));
                _openPlan.Click += (s, e) => OpenPlan();
                ConfigureAction(_openTask, "MỞ TÁC VỤ", false);
                _openTask.Click += (s, e) => OpenSelectedTask();
                ConfigureAction(_openTimeline, "LỊCH SỬ", false);
                _openTimeline.Click += (s, e) => OpenSelectedTimeline();
                actionPanel.Controls.Add(_selectFirmPlan);
                actionPanel.Controls.Add(_openTimeline);
                actionPanel.Controls.Add(_openTask);
                actionPanel.Controls.Add(_openProduction);
                actionPanel.Controls.Add(_openPart);
                actionPanel.Controls.Add(_openPlan);
            }
            header.Controls.Add(actionPanel);

            _stageStrip.Dock = DockStyle.Fill;
            _stageStrip.CardClicked += (s, e) => ApplyStageFilter();
            _taskGrid.Dock = DockStyle.Fill;
            _taskGrid.View.RowCellStyle += View_RowCellStyle;
            _taskGrid.View.CustomUnboundColumnData += View_CustomUnboundColumnData;
            _taskGrid.View.CustomColumnDisplayText += (s, e) =>
            {
                if (e.Column.FieldName == "IsRelatedDepartment")
                    e.DisplayText = Convert.ToBoolean(e.Value) ? "Liên quan · chỉ xem" : "Phạm vi của tôi";
                else if (e.Column.FieldName == "FlowStage")
                {
                    string stage = Convert.ToString(e.Value);
                    e.DisplayText = stage == "PLAN" ? "01 Kế hoạch" :
                        stage == "COMPONENTS" ? "02 Cấp linh kiện" :
                        stage == "ORDER" ? "03 Lệnh sản xuất" :
                        stage == "PRODUCTION" ? "04 Sản xuất thực tế" :
                        stage == "DONE" ? "05 Hoàn thành" : stage;
                }
                else if (e.Column.FieldName == "WaitingAt")
                {
                    string waiting = Convert.ToString(e.Value);
                    e.DisplayText = waiting == "PLANNING" ? "Kế hoạch" :
                        waiting == "PART" ? "Kho linh kiện" :
                        waiting == "PRODUCTION" ? "Sản xuất" : waiting;
                }
            };
            _taskGrid.View.FocusedRowChanged += (s, e) =>
            {
                ShowSelectedDetails();
                UpdateSelectionButton();
                UpdateNavigationButtons();
            };
            _taskGrid.View.DoubleClick += (s, e) =>
            {
                if (_selectionMode) SelectCurrentFirmPlan();
                else OpenSelectedTimeline();
            };

            var footer = new PanelControl { Dock = DockStyle.Fill, BorderStyle = BorderStyles.NoBorder };
            _onlyMyTasks.Text = "Chỉ việc của tôi";
            _onlyMyTasks.Dock = DockStyle.Left;
            _onlyMyTasks.Width = 145;
            _onlyMyTasks.CheckedChanged += (s, e) => ApplyStageFilter();
            _status.Dock = DockStyle.Fill;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _refreshStamp.Dock = DockStyle.Right;
            _refreshStamp.Width = 220;
            _refreshStamp.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Far;
            _refreshStamp.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            footer.Controls.Add(_status);
            footer.Controls.Add(_refreshStamp);
            footer.Controls.Add(_onlyMyTasks);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_stageStrip, 0, 1);
            root.Controls.Add(_taskGrid, 0, 2);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
        }

        private void ConfigureWorker()
        {
            _worker.DoWork += (s, e) =>
            {
                var args = (LoadArgs)e.Argument;
                long version = _service.GetChangeVersion(args.Site);
                if (!args.Force && version == _lastChangeVersion)
                {
                    e.Result = new LoadResult { Version = version, Data = null };
                    return;
                }
                e.Result = new LoadResult
                {
                    Version = version,
                    Data = _service.GetDashboard(args.Site, args.From, args.To)
                };
            };
            _worker.RunWorkerCompleted += (s, e) =>
            {
                _filters.SetBusy(false);
                if (e.Error != null)
                {
                    _status.Text = "Không tải được dòng chảy: " + e.Error.GetBaseException().Message;
                    _status.Appearance.Options.UseForeColor = false;
                    return;
                }
                var result = e.Result as LoadResult;
                if (result == null) return;
                _lastChangeVersion = result.Version;
                if (result.Data == null)
                {
                    _refreshStamp.Text = "Không đổi · " + DateTime.Now.ToString("HH:mm:ss");
                    return;
                }
                _data = result.Data;
                PrepareColumns(_data);
                _taskGrid.Bind(_data);
                ConfigureColumns();
                ApplyStageFilter();
                UpdateKpis();
                _refreshStamp.Text = "Cập nhật: " + DateTime.Now.ToString("HH:mm:ss");
                _status.Text = "Đang theo dõi " + _data.Rows.Count.ToString("N0") +
                    " dòng kế hoạch trong phạm vi được cấp. Màn hình chỉ đọc.";
                _status.Appearance.Options.UseForeColor = false;
                ShowSelectedDetails();
                UpdateSelectionButton();
                UpdateNavigationButtons();
            };
            _refreshTimer.Interval = 5000;
            _refreshTimer.Tick += (s, e) => PollFlow();
            _refreshTimer.Start();
        }

        private void LoadFlow()
        {
            StartLoad(true);
        }

        private void PollFlow()
        {
            StartLoad(false);
        }

        private void StartLoad(bool force)
        {
            if (_worker.IsBusy) return;
            if (string.IsNullOrWhiteSpace(_filters.SiteCode))
            {
                _status.Text = "Không tìm thấy Site trong phạm vi được cấp cho tài khoản.";
                return;
            }
            _filters.SetBusy(true);
            _status.Text = "Đang đồng bộ trạng thái kế hoạch, linh kiện, lệnh và sản xuất...";
            _status.Appearance.Options.UseForeColor = false;
            _worker.RunWorkerAsync(new LoadArgs
            {
                Site = _filters.SiteCode,
                From = _filters.FromDate,
                To = _filters.ToDate,
                Force = force
            });
        }

        private static string StageName(string stage)
        {
            switch (stage)
            {
                case "PLAN": return "01 Kế hoạch";
                case "COMPONENTS": return "02 Cấp linh kiện";
                case "ORDER": return "03 Lệnh sản xuất";
                case "PRODUCTION": return "04 Sản xuất thực tế";
                case "DONE": return "05 Hoàn thành";
                default: return stage;
            }
        }

        /// <summary>Thêm cột hiển thị dẫn xuất (chỉ để trình bày; không thay đổi dữ liệu nguồn).</summary>
        private void PrepareColumns(DataTable table)
        {
            foreach (var name in new[] { "FlowCode", "FlowName", "SiteText", "StageText", "MaterialText", "WarnText", "StatusText" })
                if (!table.Columns.Contains(name)) table.Columns.Add(name, typeof(string));
            if (!table.Columns.Contains("HasWarning")) table.Columns.Add("HasWarning", typeof(bool));
            string site = _filters.SiteCode ?? string.Empty;
            foreach (DataRow row in table.Rows)
            {
                string stage = Convert.ToString(row["FlowStage"]);
                bool demandExists = !string.IsNullOrWhiteSpace(Convert.ToString(row["DemandStatus"]));
                decimal outstanding = SafeDecimal(row["OutstandingComponentQty"]);
                int paused = SafeInt(row["PausedOperationCount"]);
                string reason = Convert.ToString(row["WaitingReason"]);
                bool warn = paused > 0 || outstanding > 0m || !string.IsNullOrWhiteSpace(reason);
                row["FlowCode"] = Convert.ToString(row["PlanNo"]);
                row["FlowName"] = Convert.ToString(row["ProductDescription"]) +
                    (string.IsNullOrWhiteSpace(Convert.ToString(row["ProductionLineCode"])) ? string.Empty :
                        " - " + Convert.ToString(row["ProductionLineCode"]));
                row["SiteText"] = site;
                row["StageText"] = StageName(stage);
                row["MaterialText"] = !demandExists ? "Chưa có yêu cầu linh kiện" :
                    outstanding > 0m ? "Chưa cấp đủ linh kiện" : "Đã cấp đủ";
                row["HasWarning"] = warn;
                row["WarnText"] = !warn ? "—" :
                    !string.IsNullOrWhiteSpace(reason) ? reason :
                    paused > 0 ? "Công đoạn tạm dừng" : "Còn thiếu linh kiện";
                row["StatusText"] = stage == "DONE" ? "Đã hoàn thành" :
                    paused > 0 ? "Tạm dừng" :
                    stage == "PRODUCTION" ? "Đang chạy" :
                    stage == "ORDER" ? "Chờ sản xuất" :
                    stage == "COMPONENTS" ? "Chờ linh kiện" : "Chuẩn bị";
            }
        }

        private void ConfigureColumns()
        {
            var view = _taskGrid.View;
            foreach (DevExpress.XtraGrid.Columns.GridColumn column in view.Columns) column.Visible = false;
            if (view.Columns["RowNo"] == null)
                view.Columns.AddField("RowNo").UnboundType = DevExpress.Data.UnboundColumnType.Integer;

            string[,] layout =
            {
                { "RowNo", "STT", "56" }, { "FlowCode", "Mã dòng chảy", "150" }, { "FlowName", "Tên dòng chảy / Tuyến", "230" },
                { "SiteText", "Site", "100" }, { "StageText", "Công đoạn", "150" }, { "MaterialText", "Trạng thái vật tư", "190" },
                { "OrderNo", "Lệnh sản xuất", "130" }, { "ProducedQty", "Số lượng OK", "100" }, { "NgQty", "Số lượng NG", "100" },
                { "ProductionDate", "Ngày bắt đầu", "110" }, { "PlannedEndDate", "Ngày hoàn thành", "120" },
                { "WarnText", "Cảnh báo / Điểm nghẽn", "230" }, { "StatusText", "Trạng thái", "120" }
            };
            for (int i = 0; i < layout.GetLength(0); i++)
            {
                var column = view.Columns[layout[i, 0]];
                if (column == null) continue;
                column.Caption = layout[i, 1];
                column.Width = int.Parse(layout[i, 2]);
                column.Visible = true;
                column.VisibleIndex = i;
            }
            foreach (var name in new[] { "ProducedQty", "NgQty" })
            {
                var q = view.Columns[name];
                if (q == null) continue;
                q.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
                q.DisplayFormat.FormatString = "n0";
            }
            foreach (var name in new[] { "ProductionDate", "PlannedEndDate" })
            {
                var d = view.Columns[name];
                if (d == null) continue;
                d.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                d.DisplayFormat.FormatString = "dd/MM/yyyy";
            }
        }

        private void SetCaption(string field, string caption)
        {
            if (_taskGrid.View.Columns[field] != null) _taskGrid.View.Columns[field].Caption = caption;
        }

        private void UpdateKpis()
        {
            int plan = 0, planWaiting = 0, planLate = 0;
            int comp = 0, compFull = 0, compShort = 0;
            int order = 0, orderOpen = 0, orderNoLot = 0;
            int prod = 0, prodBlocked = 0, done = 0;
            DateTime today = DateTime.Today;
            foreach (DataRow row in _data.Rows)
            {
                bool related = row["IsRelatedDepartment"] != DBNull.Value && Convert.ToBoolean(row["IsRelatedDepartment"]);
                decimal outstanding = SafeDecimal(row["OutstandingComponentQty"]);
                bool demandExists = !string.IsNullOrWhiteSpace(Convert.ToString(row["DemandStatus"]));
                bool orderExists = row["ProductionOrderId"] != DBNull.Value;
                int lots = SafeInt(row["ProductionLotCount"]);
                int paused = SafeInt(row["PausedOperationCount"]);
                switch (Convert.ToString(row["FlowStage"]))
                {
                    case "PLAN":
                        plan++;
                        planWaiting++;
                        if (row["ProductionDate"] != DBNull.Value && Convert.ToDateTime(row["ProductionDate"]).Date < today) planLate++;
                        break;
                    case "COMPONENTS": comp++; break;
                    case "ORDER": order++; break;
                    case "PRODUCTION": prod++; if (paused > 0) prodBlocked++; break;
                    case "DONE": done++; break;
                }
                if (demandExists)
                {
                    if (outstanding > 0m) compShort++; else compFull++;
                }
                if (orderExists) { if (lots > 0) orderOpen++; else orderNoLot++; }
                if (related) { /* đếm cả dòng liên quan: Control Tower là màn theo dõi liên phòng ban */ }
            }
            var info = PCTP.Shared.MesControls.FvnCardBadgeKind.Info;
            var warn = PCTP.Shared.MesControls.FvnCardBadgeKind.Warning;
            var danger = PCTP.Shared.MesControls.FvnCardBadgeKind.Danger;
            var none = PCTP.Shared.MesControls.FvnCardBadgeKind.None;
            _stageStrip.SetCards(new System.Collections.Generic.List<PCTP.Shared.MesControls.FvnFlowStageCard>
            {
                new PCTP.Shared.MesControls.FvnFlowStageCard { Caption = "01  KẾ HOẠCH", Value = plan.ToString("N0"), Unit = "Lệnh",
                    Glyph = "☰", Accent = Color.FromArgb(46, 110, 190), SubText = planWaiting + " Đang chờ",
                    BadgeText = planLate > 0 ? planLate + " Trễ hạn" : string.Empty, BadgeKind = danger },
                new PCTP.Shared.MesControls.FvnFlowStageCard { Caption = "02  CẤP LINH KIỆN", Value = (compFull + compShort).ToString("N0"), Unit = "Lệnh",
                    Glyph = "▣", Accent = Color.FromArgb(0, 150, 160), SubText = "Cấp đủ: " + compFull + " | Thiếu: " + compShort,
                    BadgeText = compShort > 0 ? compShort + " Chưa cấp đủ linh kiện" : string.Empty, BadgeKind = warn },
                new PCTP.Shared.MesControls.FvnFlowStageCard { Caption = "03  LỆNH SẢN XUẤT", Value = (orderOpen + orderNoLot).ToString("N0"), Unit = "Lệnh",
                    Glyph = "✎", Accent = Color.FromArgb(92, 84, 200), SubText = orderOpen + " Đã mở | " + orderNoLot + " Chờ tạo LOT",
                    BadgeText = string.Empty, BadgeKind = none },
                new PCTP.Shared.MesControls.FvnFlowStageCard { Caption = "04  SẢN XUẤT THỰC TẾ", Value = prod.ToString("N0"), Unit = "Lệnh",
                    Glyph = "▤", Accent = Color.FromArgb(232, 130, 20), SubText = "Đang chạy",
                    BadgeText = prodBlocked > 0 ? prodBlocked + " Tắc nghẽn / Dừng" : string.Empty, BadgeKind = warn },
                new PCTP.Shared.MesControls.FvnFlowStageCard { Caption = "05  HOÀN THÀNH", Value = done.ToString("N0"), Unit = "Lệnh",
                    Glyph = "✔", Accent = Color.FromArgb(34, 160, 84), SubText = "Đã hoàn thành",
                    BadgeText = string.Empty, BadgeKind = info }
            });
        }

        private void ApplyStageFilter()
        {
            string[] keys = { "PLAN", "COMPONENTS", "ORDER", "PRODUCTION", "DONE" };
            int selected = _stageStrip.SelectedIndex;
            string filter = _onlyMyTasks.Checked ? "[IsActionable] = true" : string.Empty;
            if (selected >= 0 && selected < keys.Length)
                filter = (filter.Length > 0 ? filter + " AND " : string.Empty) + "[FlowStage] = '" + keys[selected] + "'";
            _taskGrid.View.ActiveFilterString = filter;
        }

        private void View_CustomUnboundColumnData(object sender, DevExpress.XtraGrid.Views.Base.CustomColumnDataEventArgs e)
        {
            if (e.Column.FieldName == "RowNo" && e.IsGetData) e.Value = e.ListSourceRowIndex + 1;
        }

        private void View_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            var row = _taskGrid.View.GetDataRow(e.RowHandle);
            if (row != null && row.Table.Columns.Contains("HasWarning") && !(row["HasWarning"] is DBNull) &&
                Convert.ToBoolean(row["HasWarning"]) && e.RowHandle != _taskGrid.View.FocusedRowHandle)
            {
                e.Appearance.BackColor = Color.FromArgb(255, 243, 214);
                e.Appearance.Options.UseBackColor = true;
            }
            if (row == null || !row.Table.Columns.Contains("TaskCode")) return;
            if (e.Column.FieldName == "TaskLabel" &&
                row["TaskCode"] != DBNull.Value &&
                (ProductionFlowTaskCode)row["TaskCode"] != ProductionFlowTaskCode.None)
                e.Appearance.Font = new Font(e.Appearance.Font, FontStyle.Bold);
        }

        private void ShowSelectedDetails()
        {
            var row = _taskGrid.View.GetFocusedDataRow();
            if (row == null)
            {
                _progressSummary.SetProgress(0, 0);
                _scopeBadge.SetRelated(false);
                _holdAlert.SetDependency(string.Empty, string.Empty, false);
                _flowStepper.SetStates(
                    PCTP.Shared.MesControls.FvnFlowStepState.Pending,
                    PCTP.Shared.MesControls.FvnFlowStepState.Pending,
                    PCTP.Shared.MesControls.FvnFlowStepState.Pending,
                    PCTP.Shared.MesControls.FvnFlowStepState.Pending,
                    PCTP.Shared.MesControls.FvnFlowStepState.Pending);
                _flowStepper.SetDetails(null, null, null, null, null);
                _holdList.Bind(new DataTable());
                return;
            }
            decimal required = SafeDecimal(row["RequiredComponentQty"]);
            decimal issued = SafeDecimal(row["IssuedComponentQty"]);
            int componentPercent = required <= 0 ? 0 : (int)Math.Min(100m, Math.Max(0m, issued / required * 100m));
            int operationCount = SafeInt(row["OperationCount"]);
            int completedOperations = SafeInt(row["CompletedOperationCount"]);
            int operationPercent = operationCount <= 0 ? 0 :
                (int)Math.Min(100m, Math.Max(0m, completedOperations * 100m / operationCount));
            _progressSummary.SetProgress(componentPercent, operationPercent);
            bool isRelated = Convert.ToBoolean(row["IsRelatedDepartment"]);
            _scopeBadge.SetRelated(isRelated);
            _holdAlert.SetDependency(Convert.ToString(row["WaitingAt"]), Convert.ToString(row["WaitingReason"]), isRelated);
            UpdateFlowStepper(row, isRelated);
            _holdList.Bind(BuildHoldRows(row, isRelated));
            string related = isRelated ? "BỘ PHẬN LIÊN QUAN (chỉ xem)" : "BỘ PHẬN TRONG PHẠM VI";
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
                "Công đoạn: " + Convert.ToString(row["CompletedOperationCount"]) + "/" + Convert.ToString(row["OperationCount"]) +
                    " | Tạm dừng: " + Convert.ToString(row["PausedOperationCount"]) + Environment.NewLine +
                "Việc thuộc quyền: " + Convert.ToString(row["TaskLabel"]) + " (" + Convert.ToString(row["AccessMode"]) + ")" + Environment.NewLine +
                "Đang chờ tại: " + Convert.ToString(row["WaitingAt"]) + Environment.NewLine +
                "Lý do: " + Convert.ToString(row["WaitingReason"]) + Environment.NewLine +
                "Cập nhật gần nhất: " + Convert.ToString(row["LastEventAtUtc"]) + " | " +
                Convert.ToString(row["LastEventAction"]) + " | " + Convert.ToString(row["LastEventActor"]);
        }

        private void UpdateFlowStepper(DataRow row, bool related)
        {
            var relatedState = PCTP.Shared.MesControls.FvnFlowStepState.Related;
            var pending = PCTP.Shared.MesControls.FvnFlowStepState.Pending;
            var complete = PCTP.Shared.MesControls.FvnFlowStepState.Complete;
            var active = PCTP.Shared.MesControls.FvnFlowStepState.Active;
            var hold = PCTP.Shared.MesControls.FvnFlowStepState.Hold;
            var blocked = PCTP.Shared.MesControls.FvnFlowStepState.Blocked;
            string stage = Convert.ToString(row["FlowStage"]);
            string planStatus = Convert.ToString(row["PlanStatus"]);
            bool demandExists = !string.IsNullOrWhiteSpace(Convert.ToString(row["DemandStatus"]));
            decimal outstanding = SafeDecimal(row["OutstandingComponentQty"]);
            int lots = SafeInt(row["ProductionLotCount"]);
            int operations = SafeInt(row["OperationCount"]);
            int completed = SafeInt(row["CompletedOperationCount"]);
            int paused = SafeInt(row["PausedOperationCount"]);
            bool orderExists = row["ProductionOrderId"] != DBNull.Value;
            var plan = planStatus == "FIRM" ? complete : stage == "PLAN" ? active : pending;
            var components = !demandExists ? (stage == "COMPONENTS" ? blocked : pending) :
                outstanding > 0m ? blocked : complete;
            var order = orderExists ? (lots > 0 ? complete : blocked) :
                (stage == "ORDER" ? blocked : pending);
            var production = paused > 0 ? hold :
                operations > 0 && completed >= operations ? complete :
                lots > 0 ? active : pending;
            var done = stage == "DONE" ? complete : pending;
            if (related)
            {
                if (plan != complete) plan = relatedState;
                if (components != complete) components = relatedState;
                if (order != complete) order = relatedState;
                if (production != complete) production = relatedState;
                if (done != complete) done = relatedState;
            }
            _flowStepper.SetStates(plan, components, order, production, done);
            decimal requiredQty = SafeDecimal(row["RequiredComponentQty"]);
            decimal issuedQty = SafeDecimal(row["IssuedComponentQty"]);
            string suffix = related ? " (chỉ xem)" : string.Empty;
            _flowStepper.SetDetails(
                Convert.ToString(row["PlanNo"]) + " · " + planStatus,
                demandExists ? "Đã cấp " + issuedQty.ToString("#,##0.##") + "/" + requiredQty.ToString("#,##0.##") +
                    (outstanding > 0m ? Environment.NewLine + "Còn thiếu " + outstanding.ToString("#,##0.##") : string.Empty)
                    : "Chưa có yêu cầu linh kiện",
                orderExists ? Convert.ToString(row["OrderNo"]) + " · " + Convert.ToString(row["OrderStatus"]) +
                    Environment.NewLine + "LOT: " + lots : "Chưa tạo lệnh",
                operations > 0 ? completed + "/" + operations + " công đoạn" +
                    (paused > 0 ? Environment.NewLine + "Tạm dừng: " + paused : string.Empty) : "Chưa có công đoạn",
                "SL đã SX: " + SafeDecimal(row["ProducedQty"]).ToString("#,##0.##") + suffix);
        }

        private static DataTable BuildHoldRows(DataRow row, bool related)
        {
            var table = new DataTable("FlowHolds");
            table.Columns.Add("HoldType", typeof(string));
            table.Columns.Add("OwnerDepartment", typeof(string));
            table.Columns.Add("BlockedDepartment", typeof(string));
            table.Columns.Add("Reason", typeof(string));
            table.Columns.Add("HoldStatus", typeof(string));
            table.Columns.Add("IsRelated", typeof(string));
            string department = Convert.ToString(row["DepartmentCode"]);
            string blocked = related ? "Bộ phận của tôi" : "Dòng chảy kế tiếp";
            Action<string, string> add = (type, reason) =>
                table.Rows.Add(type, department, blocked, reason, "Đang chặn",
                    related ? "Chỉ xem" : "Trong phạm vi");
            if (Convert.ToString(row["PlanStatus"]) != "FIRM" &&
                Convert.ToString(row["FlowStage"]) == "PLAN")
                add("PLAN_NOT_FIRM", "Kế hoạch chưa được xác nhận FIRM");
            if (string.IsNullOrWhiteSpace(Convert.ToString(row["DemandStatus"])))
                add("DEMAND_NOT_CREATED", "Chưa phát sinh yêu cầu linh kiện từ BOM");
            else if (SafeDecimal(row["OutstandingComponentQty"]) > 0m)
                add("COMPONENT_SHORTAGE", "Còn thiếu " + Convert.ToString(row["OutstandingComponentQty"]) + " linh kiện");
            if (row["ProductionOrderId"] == DBNull.Value)
                add("ORDER_NOT_CREATED", "Chưa tạo lệnh sản xuất");
            if (SafeInt(row["PausedOperationCount"]) > 0)
                add("OPERATION_PAUSED", "Có " + Convert.ToString(row["PausedOperationCount"]) + " công đoạn đang tạm dừng");
            if (SafeInt(row["OperationCount"]) > SafeInt(row["CompletedOperationCount"]))
                add("OPERATION_REMAINING", "Còn công đoạn chưa hoàn thành");
            return table;
        }

        private void UpdateSelectionButton()
        {
            if (!_selectionMode) return;
            var row = _taskGrid.View.GetFocusedDataRow();
            _selectFirmPlan.Enabled = row != null &&
                !Convert.ToBoolean(row["IsRelatedDepartment"]) &&
                string.Equals(Convert.ToString(row["PlanStatus"]), "FIRM", StringComparison.OrdinalIgnoreCase) &&
                SafeDecimal(row["OutstandingComponentQty"]) <= 0m &&
                row["ProductionOrderId"] == DBNull.Value;
        }

        private void SelectCurrentFirmPlan()
        {
            var row = _taskGrid.View.GetFocusedDataRow();
            if (row == null || Convert.ToBoolean(row["IsRelatedDepartment"]) ||
                !string.Equals(Convert.ToString(row["PlanStatus"]), "FIRM", StringComparison.OrdinalIgnoreCase) ||
                SafeDecimal(row["OutstandingComponentQty"]) > 0m ||
                row["ProductionOrderId"] != DBNull.Value)
            {
                XtraMessageBox.Show(this,
                    "Chỉ được chọn dòng FIRM thuộc bộ phận của bạn, đã đủ linh kiện và chưa tạo lệnh sản xuất.",
                    "Chọn kế hoạch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SelectedPlanLineId = Convert.ToInt64(row["ProductionPlanLineId"]);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateNavigationButtons()
        {
            var row = _taskGrid.View.GetFocusedDataRow();
            bool hasRow = row != null;
            if (!_selectionMode)
            {
                _openTimeline.Enabled = hasRow && UserSession.Current.HasPermission("MES.PRODUCTION_FLOW", "VIEW");
                _selectFirmPlan.Enabled = hasRow && !Convert.ToBoolean(row["IsRelatedDepartment"]) &&
                    UserSession.Current.HasPermission("MES.PRODUCTION_EXECUTION", "VIEW");
                _openTask.Enabled = hasRow && row["TaskCode"] != DBNull.Value &&
                    (ProductionFlowTaskCode)row["TaskCode"] != ProductionFlowTaskCode.None;
            }
        }

        private void OpenSelectedInExecution()
        {
            var row = _taskGrid.View.GetFocusedDataRow();
            if (row == null || Convert.ToBoolean(row["IsRelatedDepartment"]))
            {
                XtraMessageBox.Show(this, "Chọn một dòng thuộc bộ phận của bạn để chuyển sang Điều hành sản xuất MES.",
                    "Chọn dòng FIRM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                using (var form = new FormDieuHanhSanXuat())
                {
                    form.PreloadContext(SafeLong(row["ProductionPlanLineId"]), SafeLong(row["ProductionOrderId"]),
                        row.Table.Columns.Contains("LatestProductionLotId") ? SafeLong(row["LatestProductionLotId"]) : 0L);
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Điều hành sản xuất MES", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            LoadFlow();
        }

        private void OpenSelectedTimeline()
        {
            var row = _taskGrid.View.GetFocusedDataRow();
            if (row == null) return;
            try
            {
                using (var timeline = new FormProductionWorkflowTimeline(MesWorkflowComposition.Create()))
                {
                    long planId = SafeLong(row["ProductionPlanId"]);
                    long orderId = SafeLong(row["ProductionOrderId"]);
                    long lotId = row.Table.Columns.Contains("LatestProductionLotId") ? SafeLong(row["LatestProductionLotId"]) : 0L;
                    if (lotId > 0) timeline.SetEntity("OPERATION", lotId);
                    else if (orderId > 0) timeline.SetEntity("ORDER", orderId);
                    else timeline.SetEntity("PLAN", planId);
                    timeline.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Lịch sử workflow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static long SafeLong(object value)
        {
            long result;
            return value != null && value != DBNull.Value && long.TryParse(Convert.ToString(value), out result) ? result : 0L;
        }

        private void OpenSelectedTask()
        {
            var row = _taskGrid.View.GetFocusedDataRow();
            if (row == null || Convert.ToBoolean(row["IsRelatedDepartment"])) return;
            var task = row["TaskCode"] == DBNull.Value
                ? ProductionFlowTaskCode.None : (ProductionFlowTaskCode)row["TaskCode"];
            switch (task)
            {
                case ProductionFlowTaskCode.ValidatePlan:
                case ProductionFlowTaskCode.FirmPlan: OpenPlan(); break;
                case ProductionFlowTaskCode.IssueComponents: OpenPart(); break;
                case ProductionFlowTaskCode.CreateOrder:
                case ProductionFlowTaskCode.ReleaseOrder:
                case ProductionFlowTaskCode.CreateLot:
                case ProductionFlowTaskCode.ResumeOrder:
                case ProductionFlowTaskCode.ResumeOperation:
                case ProductionFlowTaskCode.CompleteOrder:
                case ProductionFlowTaskCode.CloseOrder:
                case ProductionFlowTaskCode.ExecuteOperation: OpenProduction(); break;
            }
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
            public bool Force;
        }

        private sealed class LoadResult
        {
            public long Version;
            public DataTable Data;
        }
    }
}