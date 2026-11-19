using PCTP.Infrastructure.Security;
using PCTP.Security;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Component demand list generated from a FIRM plan and the synchronized IFS BOM.
    /// DevExpress GridControl/GridView are used for both plan and demand details.
    /// </summary>
    public sealed class FormYeuCauVaXuatLinhKien : XtraForm
    {
        private readonly IComponentDemandService _service;
        private readonly GridControl _plans = new GridControl();
        private readonly GridControl _demands = new GridControl();
        private readonly GridView _planView;
        private readonly GridView _demandView;
        private readonly TextEdit _site = new TextEdit();
        private readonly TextEdit _issueNo = new TextEdit();
        private readonly LookUpEdit _lots = new LookUpEdit();
        private readonly LookUpEdit _wipSlots = new LookUpEdit();
        private readonly SpinEdit _qty = new SpinEdit();
        private readonly LabelControl _status = new LabelControl();
        private readonly SimpleButton _postButton = new SimpleButton();

        public FormYeuCauVaXuatLinhKien()
            : this(new ComponentDemandService(new ComponentDemandRepository()))
        {
        }

        public FormYeuCauVaXuatLinhKien(IComponentDemandService service)
        {
            AuthorizationGuard.Demand("MES.COMPONENT_DEMAND", "VIEW");
            AuthorizationGuard.Demand("MES.COMPONENT_ISSUE", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _planView = new GridView(_plans);
            _demandView = new GridView(_demands);
            Text = "Yêu cầu linh kiện và điều chuyển PART → WIP";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1360, 850);
            MinimumSize = new Size(1050, 680);
            BuildUi();
            Load += (s, e) => LoadPlans();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 39));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2) };
            top.Controls.Add(new LabelControl { Text = "Site", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            _site.Width = 110;
            top.Controls.Add(_site);
            var reload = new SimpleButton { Text = "Tải kế hoạch", Width = 120, Height = 30 };
            reload.Click += (s, e) => LoadPlans();
            top.Controls.Add(reload);
            var reconcile = new SimpleButton
            {
                Text = "ĐỒNG BỘ YÊU CẦU BOM",
                Width = 190,
                Height = 30,
                Enabled = UserSession.Current != null &&
                    UserSession.Current.HasPermission("MES.COMPONENT_DEMAND", "CREATE")
            };
            reconcile.Click += GenerateClick;
            top.Controls.Add(reconcile);

            ConfigureGrid(_plans, _planView);
            ConfigureGrid(_demands, _demandView);
            _planView.SelectionChanged += (s, e) => LoadDemand();
            _demandView.SelectionChanged += (s, e) => LoadIssueOptions();

            var issue = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(2) };
            issue.Controls.Add(new LabelControl { Text = "LOT / Slot nguồn", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            ConfigureLookup(_lots, 330);
            issue.Controls.Add(_lots);
            issue.Controls.Add(new LabelControl { Text = "Slot đích WIP", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            ConfigureLookup(_wipSlots, 260);
            issue.Controls.Add(_wipSlots);
            issue.Controls.Add(new LabelControl { Text = "Số lượng", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            _qty.Width = 110;
            _qty.Properties.IsFloatValue = true;
            _qty.Properties.Mask.EditMask = "n3";
            _qty.Properties.Mask.UseMaskAsDisplayFormat = true;
            _qty.Properties.MinValue = 0.001M;
            _qty.Properties.MaxValue = 1000000000M;
            _qty.Properties.Increment = 1M;
            _qty.EditValue = 1M;
            issue.Controls.Add(_qty);
            issue.Controls.Add(new LabelControl { Text = "Số phiếu", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            _issueNo.Width = 180;
            _issueNo.Text = "WIP-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            issue.Controls.Add(_issueNo);

            _postButton.Text = "XUẤT PART → WIP";
            _postButton.Width = 175;
            _postButton.Height = 32;
            _postButton.Enabled = UserSession.Current != null &&
                UserSession.Current.HasPermission("MES.COMPONENT_ISSUE", "CONFIRM");
            _postButton.Click += PostClick;
            issue.Controls.Add(_postButton);

            _status.Dock = DockStyle.Fill;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _status.AutoSizeMode = LabelAutoSizeMode.None;

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(_plans, 0, 1);
            root.Controls.Add(_demands, 0, 2);
            root.Controls.Add(issue, 0, 3);
            root.Controls.Add(_status, 0, 4);
            Controls.Add(root);
        }

        private static void ConfigureGrid(GridControl grid, GridView view)
        {
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsBehavior.ReadOnly = true;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.ShowIndicator = true;
            view.OptionsSelection.MultiSelect = false;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            view.FocusRectStyle = DrawFocusRectStyle.RowFocus;
            view.OptionsView.ShowAutoFilterRow = true;
        }

        private static void ConfigureLookup(LookUpEdit editor, int width)
        {
            editor.Width = width;
            editor.Properties.NullText = "";
            editor.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            editor.Properties.PopupFilterMode = DevExpress.XtraEditors.PopupFilterMode.Contains;
            editor.Properties.BestFitMode = DevExpress.XtraEditors.Controls.BestFitMode.BestFitResizePopup;
        }

        private static long CellLong(DataRow row, string name)
        {
            return row == null || row[name] == DBNull.Value ? 0 : Convert.ToInt64(row[name]);
        }

        private static decimal CellDecimal(DataRow row, string name)
        {
            return row == null || row[name] == DBNull.Value ? 0 : Convert.ToDecimal(row[name]);
        }

        private static string CellText(DataRow row, string name)
        {
            return row == null || row[name] == DBNull.Value ? "" : Convert.ToString(row[name]);
        }

        private DataRow SelectedPlan() => _planView.GetFocusedDataRow();
        private DataRow SelectedDemand() => _demandView.GetFocusedDataRow();

        private void LoadPlans()
        {
            try
            {
                _plans.DataSource = _service.GetPlanLines(_site.Text);
                _planView.BestFitColumns();
                _status.Text = "Danh sách kế hoạch theo ca. Yêu cầu linh kiện được tạo tự động khi kế hoạch chuyển sang FIRM.";
                LoadDemand();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void GenerateClick(object sender, EventArgs e)
        {
            try
            {
                var row = SelectedPlan();
                var id = CellLong(row, "ProductionPlanLineId");
                if (id <= 0) throw new InvalidOperationException("Chọn một dòng kế hoạch FIRM.");
                var demandId = _service.GenerateDemand(id, CellText(row, "SiteCode"), Environment.UserName);
                _status.Text = "Đã kiểm tra/đồng bộ yêu cầu BOM #" + demandId + ". Thao tác có tính idempotent.";
                LoadDemand();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void LoadDemand()
        {
            try
            {
                var plan = SelectedPlan();
                var id = CellLong(plan, "ProductionPlanLineId");
                _demands.DataSource = id > 0
                    ? _service.GetDemandLines(id, CellText(plan, "SiteCode"))
                    : new DataTable();
                _demandView.BestFitColumns();
                LoadIssueOptions();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void LoadIssueOptions()
        {
            try
            {
                var plan = SelectedPlan();
                var demand = SelectedDemand();
                var site = CellText(plan, "SiteCode");
                var part = CellText(demand, "ComponentPartNo");
                var supplyMode = CellText(demand, "SupplyMode");
                var areaCode = CellText(demand, "SourceAreaCode");
                if (string.IsNullOrWhiteSpace(areaCode)) areaCode = supplyMode == "MAKE" ? "WIP" : "PART";

                var lots = !string.IsNullOrWhiteSpace(site) && !string.IsNullOrWhiteSpace(part)
                    ? _service.GetPartLots(site, part, areaCode)
                    : new DataTable();
                _lots.Properties.DataSource = lots;
                _lots.Properties.DisplayMember = "LotSlotDisplay";
                _lots.Properties.ValueMember = "MaterialLotId";
                _lots.Properties.PopulateColumns();
                if (_lots.Properties.Columns["MaterialLotId"] != null)
                    _lots.Properties.Columns["MaterialLotId"].Visible = false;

                _postButton.Enabled = supplyMode != "MAKE" && UserSession.Current != null &&
                    UserSession.Current.HasPermission("MES.COMPONENT_ISSUE", "CONFIRM");
                _postButton.Text = supplyMode == "MAKE"
                    ? "MAKE: GHI NHẬN TẠI MES"
                    : "XUẤT PART → WIP";
                _wipSlots.Enabled = supplyMode != "MAKE";

                if (supplyMode == "MAKE")
                    _status.Text = "Linh kiện MAKE: không xuất từ PART; ghi nhận LOT và tiêu hao tại Điều hành sản xuất MES.";

                var slots = !string.IsNullOrWhiteSpace(site) ? _service.GetWipSlots(site) : new DataTable();
                _wipSlots.Properties.DataSource = slots;
                _wipSlots.Properties.DisplayMember = "SlotDisplay";
                _wipSlots.Properties.ValueMember = "SlotId";
                _wipSlots.Properties.PopulateColumns();
                if (_wipSlots.Properties.Columns["SlotId"] != null)
                    _wipSlots.Properties.Columns["SlotId"].Visible = false;

                var outstanding = CellDecimal(demand, "OutstandingQty");
                var lot = _lots.GetSelectedDataRow() as DataRowView;
                var available = lot == null ? 0M : Convert.ToDecimal(lot["AvailableQty"]);
                var suggested = Math.Min(outstanding, available);
                if (suggested >= _qty.Properties.MinValue)
                    _qty.Value = Math.Min(_qty.Properties.MaxValue, suggested);
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void PostClick(object sender, EventArgs e)
        {
            try
            {
                var plan = SelectedPlan();
                var demand = SelectedDemand();
                if (demand == null) throw new InvalidOperationException("Chọn dòng yêu cầu linh kiện.");
                var lot = _lots.GetSelectedDataRow() as DataRowView;
                if (lot == null) throw new InvalidOperationException("Không có LOT khả dụng trong khu vực nguồn đã cấu hình.");
                if (CellText(demand, "SupplyMode") == "MAKE")
                    throw new InvalidOperationException("LOT sản xuất MAKE không xuất từ PART; hãy ghi nhận tiêu hao trong Điều hành sản xuất MES.");
                if (_wipSlots.EditValue == null || _wipSlots.EditValue == DBNull.Value)
                    throw new InvalidOperationException("Chọn Slot đích WIP.");

                var result = _service.PostIssue(
                    CellText(plan, "SiteCode"),
                    _issueNo.Text,
                    CellLong(demand, "ComponentDemandLineId"),
                    Convert.ToInt64(lot["MaterialLotId"]),
                    Convert.ToInt32(lot["SlotId"]),
                    Convert.ToInt32(_wipSlots.EditValue),
                    _qty.Value,
                    Environment.UserName);

                _status.Text = "Đã chuyển " + _qty.Value.ToString("N3") +
                    " từ PART sang WIP. Giao dịch MES #" + result.Rows[0]["InventoryTransactionId"] + ".";
                LoadDemand();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowError(Exception ex)
        {
            _status.Text = ex.Message;
            XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
