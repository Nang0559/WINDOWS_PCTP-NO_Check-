using PCTP.Infrastructure.Security;
using PCTP.Security;
using PCTP.Shared.MesControls;
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
        private readonly FvnSiteLookupEdit _site = new FvnSiteLookupEdit();
        private FvnConsoleScreenFrame _frame;
        private readonly TextEdit _issueNo = new TextEdit();
        private readonly LookUpEdit _lots = new LookUpEdit();
        private readonly LookUpEdit _wipSlots = new LookUpEdit();
        private readonly SpinEdit _qty = new SpinEdit();
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
            WindowState = FormWindowState.Maximized;
            Size = new Size(1360, 850);
            MinimumSize = new Size(1050, 680);
            BuildUi();
            Load += (s, e) =>
            {
                // Site chuẩn: dbo.vFVN_Site ∩ phạm vi của cả nhu cầu linh kiện lẫn xuất PART → WIP.
                _site.LoadForFunctions(new[] { "MES.COMPONENT_DEMAND", "MES.COMPONENT_ISSUE" });
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
            _site.Width = 150;
            _site.SiteChanged += (s, e) => LoadPlans();
            var reload = new SimpleButton();
            FvnConsoleScreen.StyleButton(reload, "TẢI KẾ HOẠCH", 130, (s, e) => LoadPlans());
            var reconcile = new SimpleButton();
            FvnConsoleScreen.StyleButton(reconcile, "ĐỒNG BỘ YÊU CẦU BOM", 190, GenerateClick,
                UserSession.Current != null && UserSession.Current.HasPermission("MES.COMPONENT_DEMAND", "CREATE"));
            var filter = FvnConsoleScreen.CreateFilterCard(
                FvnConsoleScreen.Caption("Site"), _site, reload, reconcile);

            ConfigureGrid(_plans, _planView);
            ConfigureGrid(_demands, _demandView);
            _planView.SelectionChanged += (s, e) => LoadDemand();
            _demandView.SelectionChanged += (s, e) => LoadIssueOptions();

            var issue = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, BackColor = Color.White };
            issue.Controls.Add(FvnConsoleScreen.Caption("LOT / Slot nguồn"));
            ConfigureLookup(_lots, 330);
            issue.Controls.Add(_lots);
            issue.Controls.Add(FvnConsoleScreen.Caption("Slot đích WIP"));
            ConfigureLookup(_wipSlots, 260);
            issue.Controls.Add(_wipSlots);
            issue.Controls.Add(FvnConsoleScreen.Caption("Số lượng"));
            _qty.Width = 110;
            _qty.Properties.IsFloatValue = true;
            _qty.Properties.Mask.EditMask = "n3";
            _qty.Properties.Mask.UseMaskAsDisplayFormat = true;
            _qty.Properties.MinValue = 0.001M;
            _qty.Properties.MaxValue = 1000000000M;
            _qty.Properties.Increment = 1M;
            _qty.EditValue = 1M;
            issue.Controls.Add(_qty);
            issue.Controls.Add(FvnConsoleScreen.Caption("Số phiếu"));
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

            var issueCard = new FvnCardPanel { Dock = DockStyle.Fill };
            issueCard.Controls.Add(issue);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = FvnConsoleTheme.Background };
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            body.Controls.Add(Card(_plans), 0, 0);
            body.Controls.Add(Card(_demands), 0, 1);
            body.Controls.Add(issueCard, 0, 2);

            _frame = FvnConsoleScreen.Compose(this,
                "YÊU CẦU LINH KIỆN & XUẤT PART → WIP",
                "Kế hoạch FIRM → nhu cầu linh kiện theo BOM IFS → xuất LOT từ PART sang slot WIP | Site theo phạm vi tài khoản",
                filter, FvnConsoleScreen.FilterCardHeight, body);
        }

        private static Control Card(Control grid)
        {
            var card = new FvnCardPanel { Dock = DockStyle.Fill, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 8) };
            card.Controls.Add(grid);
            return card;
        }

        private void SetStatus(string text, bool warning)
        {
            if (_frame != null) _frame.SetStatus(text, warning);
        }

        private static void ConfigureGrid(GridControl grid, GridView view)
        {
            FvnConsoleScreen.StyleGrid(grid, view);
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

        private static DataRow SelectedLookupRow(LookUpEdit editor)
        {
            var selected = editor.GetSelectedDataRow();
            var view = selected as DataRowView;
            return view != null ? view.Row : selected as DataRow;
        }

        private void LoadPlans()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_site.SiteCode))
                    throw new InvalidOperationException("Chọn Site để tải đúng phạm vi kế hoạch được phân quyền.");
                _plans.DataSource = _service.GetPlanLines(_site.SiteCode);
                _planView.BestFitColumns();
                _frame.SetRuntime("Site " + _site.SiteCode, true);
                SetStatus("Danh sách kế hoạch theo ca. Yêu cầu linh kiện được tạo tự động khi kế hoạch chuyển sang FIRM.", false);
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
                SetStatus("✔  Đã kiểm tra/đồng bộ yêu cầu BOM #" + demandId + ". Thao tác có tính idempotent.", false);
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
                    SetStatus("Linh kiện MAKE: không xuất từ PART; ghi nhận LOT và tiêu hao tại Điều hành sản xuất MES.", true);

                var slots = !string.IsNullOrWhiteSpace(site) ? _service.GetWipSlots(site) : new DataTable();
                _wipSlots.Properties.DataSource = slots;
                _wipSlots.Properties.DisplayMember = "SlotDisplay";
                _wipSlots.Properties.ValueMember = "SlotId";
                _wipSlots.Properties.PopulateColumns();
                if (_wipSlots.Properties.Columns["SlotId"] != null)
                    _wipSlots.Properties.Columns["SlotId"].Visible = false;

                var outstanding = CellDecimal(demand, "OutstandingQty");
                var lot = SelectedLookupRow(_lots);
                var available = lot == null ? 0M : Convert.ToDecimal(lot["AvailableQty"]);
                var suggested = Math.Min(outstanding, available);
                if (suggested >= _qty.Properties.MinValue)
                    _qty.Value = Math.Min(_qty.Properties.MaxValue, suggested);
            }
            catch (Exception ex) { SetStatus(ex.Message, true); }
        }

        private void PostClick(object sender, EventArgs e)
        {
            try
            {
                var plan = SelectedPlan();
                var demand = SelectedDemand();
                if (demand == null) throw new InvalidOperationException("Chọn dòng yêu cầu linh kiện.");
                var lot = SelectedLookupRow(_lots);
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

                SetStatus("✔  Đã chuyển " + _qty.Value.ToString("N3") +
                    " từ PART sang WIP. Giao dịch MES #" + result.Rows[0]["InventoryTransactionId"] + ".", false);
                LoadDemand();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowError(Exception ex)
        {
            SetStatus(ex.Message, true);
            XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
