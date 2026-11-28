using PCTP.Infrastructure.Security;
using PCTP.Security;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraTab;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Màn hình điều hành sản xuất MES. Luồng: chọn dòng kế hoạch FIRM → tạo lệnh → release → tạo LOT → start →
    /// ghi nhận công đoạn / tem / tiêu hao → complete → close.
    /// Form chỉ là lớp trình bày: nút được bật/tắt theo QUYỀN và theo TRẠNG THÁI workflow hiện tại để người dùng
    /// luôn thấy đúng bước tiếp theo; Service và SQL vẫn là nơi kiểm soát cuối cùng.
    /// </summary>
    public sealed class FormDieuHanhSanXuat : XtraForm
    {
        private enum StatusKind { Info, Success, Error }

        private sealed class Cmd
        {
            public SimpleButton Button;
            public bool Permitted;
            public string Caption;
        }

        private readonly IProductionExecutionService _service;
        private readonly IProductionWorkflowService _workflow;
        private IProductionFlowService _flow;

        // --- Tab "Lệnh và LOT"
        private readonly ButtonEdit _planLineId = new ButtonEdit();
        private readonly TextEdit _orderNo = new TextEdit();
        private readonly TextEdit _orderId = new TextEdit();
        private readonly TextEdit _lotId = new TextEdit();
        private readonly DateEdit _productionDate = new DateEdit();
        private readonly TextEdit _goodQty = new TextEdit();
        private readonly TextEdit _ngQty = new TextEdit();
        private readonly CheckEdit _finished = new CheckEdit();
        private readonly LabelControl _badge = new LabelControl();
        private readonly LabelControl _hint = new LabelControl();
        private readonly LabelControl[] _steps = new LabelControl[5];
        private readonly GridControl _work = new GridControl();
        private readonly GridView _workView = new GridView();
        private readonly SimpleButton _refreshWork = new SimpleButton();
        private readonly List<Cmd> _orderCmds = new List<Cmd>();
        private Cmd _cmdSelectPlan, _cmdCreateOrder, _cmdRelease, _cmdCreateLot, _cmdStart,
            _cmdPause, _cmdResume, _cmdComplete, _cmdClose, _cmdHistory;

        // --- Tab "Sơ đồ quy trình" (MesProductionFlowMapControl)
        private readonly MesProductionFlowMapControl _flowMap = new MesProductionFlowMapControl();
        private readonly LabelControl _mapBadge = new LabelControl();
        private readonly LabelControl _mapHint = new LabelControl();
        private XtraTabControl _tabs;
        private XtraTabPage _tabFlow, _tabOrder, _tabConsume;

        // --- Tab "Công đoạn"
        private readonly TextEdit _operationLotId = new TextEdit();
        private readonly TextEdit _operationNo = new TextEdit();
        private readonly TextEdit _routingStepId = new TextEdit();
        private readonly TextEdit _inputQty = new TextEdit();
        private readonly TextEdit _opGoodQty = new TextEdit();
        private readonly TextEdit _opNgQty = new TextEdit();
        private readonly TextEdit _inputSlot = new TextEdit();
        private readonly TextEdit _outputSlot = new TextEdit();
        private readonly GridControl _routingSteps = new GridControl();
        private readonly GridView _routingView = new GridView();
        private readonly GridControl _labels = new GridControl();
        private readonly GridView _labelView = new GridView();
        private readonly TextEdit _labelQty = new TextEdit();
        private readonly TextEdit _labelScan = new TextEdit();
        private readonly TextEdit _labelVoidReason = new TextEdit();
        private readonly LabelControl _opBadge = new LabelControl();
        private readonly List<Cmd> _opCmds = new List<Cmd>();
        private Cmd _opPrepare, _opReady, _opStart, _opPause, _opResume;

        // --- Tab "Tiêu hao LOT linh kiện"
        private readonly TextEdit _materialLineId = new TextEdit();
        private readonly TextEdit _consumeProductionLotId = new TextEdit();
        private readonly TextEdit _componentLotId = new TextEdit();
        private readonly TextEdit _consumeSlot = new TextEdit();
        private readonly TextEdit _consumeQty = new TextEdit();
        private readonly GridControl _materials = new GridControl();
        private readonly GridView _materialView = new GridView();

        // --- Chung
        private readonly LabelControl _status = new LabelControl();
        private DataTable _printRows;
        private int _printIndex;
        private int _orderStatus = -1;
        private int _operationStatus = -1;
        private bool _syncing;
        private bool _loadingWork;

        private static readonly Color Green = Color.FromArgb(34, 139, 84);
        private static readonly Color Red = Color.FromArgb(192, 57, 43);
        private static readonly Color Blue = Color.FromArgb(0, 102, 204);
        private static readonly Color Orange = Color.FromArgb(214, 122, 0);
        private static readonly Color Gray = Color.FromArgb(120, 120, 120);

        public FormDieuHanhSanXuat()
            : this(new ProductionExecutionService(new ProductionExecutionRepository()), MesWorkflowComposition.Create(), null)
        {
        }

        public FormDieuHanhSanXuat(IProductionExecutionService service)
            : this(service, MesWorkflowComposition.Create(), null)
        {
        }

        public FormDieuHanhSanXuat(IProductionExecutionService service, IProductionWorkflowService workflow)
            : this(service, workflow, null)
        {
        }

        public FormDieuHanhSanXuat(IProductionExecutionService service, IProductionWorkflowService workflow,
            IProductionFlowService flow)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _flow = flow;
            Text = "Điều hành sản xuất MES";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1280, 820);
            MinimumSize = new Size(1000, 680);
            KeyPreview = true;
            Font = new Font("Segoe UI", 9.5F);
            BuildUi();
            ApplyOrderState();
            ApplyOperationState();
            Load += (s, e) => LoadWorklist();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { LoadWorklist(); e.Handled = true; } };
        }

        // =====================================================================================================
        //  BUILD UI
        // =====================================================================================================

        private void BuildUi()
        {
            var tabs = new XtraTabControl { Dock = DockStyle.Fill };
            _tabs = tabs;
            _tabFlow = BuildFlowTab();
            _tabOrder = BuildOrderTab();
            _tabConsume = BuildConsumeTab();
            tabs.TabPages.Add(_tabFlow);
            tabs.TabPages.Add(_tabOrder);
            tabs.TabPages.Add(BuildOperationTab());
            tabs.TabPages.Add(_tabConsume);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            _status.Dock = DockStyle.Fill;
            _status.AutoSizeMode = LabelAutoSizeMode.None;
            _status.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            SetStatus("Bắt đầu bằng CHỌN KẾ HOẠCH FIRM, hoặc chọn một dòng trong danh sách bên dưới. " +
                "Lệnh/LOT/công đoạn/tiêu hao đều ghi vào MES; DIRECT không tạo tồn WIP, VIA_WIP dùng ledger chung.",
                StatusKind.Info);

            root.Controls.Add(tabs, 0, 0);
            root.Controls.Add(_status, 0, 1);
            Controls.Add(root);
        }

        // ---------------------------------------------------------------- Tab 0: Sơ đồ quy trình
        private XtraTabPage BuildFlowTab()
        {
            var page = new XtraTabPage { Text = "Sơ đồ quy trình" };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _mapHint.Dock = DockStyle.Fill;
            _mapHint.AutoSizeMode = LabelAutoSizeMode.None;
            _mapHint.Margin = new Padding(0, 0, 6, 6);
            _mapHint.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _mapHint.Appearance.BackColor = Color.FromArgb(232, 241, 252);
            _mapHint.Appearance.ForeColor = Color.FromArgb(20, 70, 130);
            _mapHint.Appearance.Options.UseBackColor = true;
            _mapHint.Appearance.Options.UseForeColor = true;
            _mapHint.Padding = new Padding(10, 0, 0, 0);

            _mapBadge.Dock = DockStyle.Fill;
            _mapBadge.AutoSizeMode = LabelAutoSizeMode.None;
            _mapBadge.Margin = new Padding(0, 0, 0, 6);
            _mapBadge.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            _mapBadge.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _mapBadge.Appearance.Font = new Font(Font, FontStyle.Bold);

            _flowMap.Dock = DockStyle.Fill;
            _flowMap.NodeClicked += FlowNodeClicked;
            _lotId.TextChanged += (s, e) => ApplyOrderState();

            root.Controls.Add(_mapHint, 0, 0);
            root.Controls.Add(_mapBadge, 1, 0);
            root.Controls.Add(_flowMap, 0, 1);
            root.SetColumnSpan(_flowMap, 2);
            page.Controls.Add(root);
            return page;
        }

        /// <summary>Bấm ô trên sơ đồ = bấm đúng nút lệnh tương ứng (cùng quyền, cùng điều kiện trạng thái, cùng hộp xác nhận).</summary>
        private void FlowNodeClicked(object sender, FlowNodeEventArgs e)
        {
            switch (e.Key)
            {
                case "select": ClickCmd(_cmdSelectPlan); break;
                case "create": ClickCmd(_cmdCreateOrder); break;
                case "release": ClickCmd(_cmdRelease); break;
                case "start": ClickCmd(_cmdStart); break;
                case "pause": ClickCmd(_cmdPause); break;
                case "resume": ClickCmd(_cmdResume); break;
                case "complete": ClickCmd(_cmdComplete); break;
                case "close": ClickCmd(_cmdClose); break;
                case "history": ClickCmd(_cmdHistory); break;
                case "lot":
                case "output":
                    if (_cmdCreateLot == null || !_cmdCreateLot.Button.Enabled) return;
                    if (_goodQty.Text.Trim().Length > 0) { _cmdCreateLot.Button.PerformClick(); break; }
                    _tabs.SelectedTabPage = _tabOrder;
                    _goodQty.Focus();
                    SetStatus("Nhập Số lượng OK / NG (và LOT thành phẩm nếu cần) rồi bấm TẠO LOT SẢN XUẤT.", StatusKind.Info);
                    break;
                case "consume":
                    _tabs.SelectedTabPage = _tabConsume;
                    break;
            }
        }

        private static void ClickCmd(Cmd cmd)
        {
            if (cmd != null && cmd.Button.Enabled) cmd.Button.PerformClick();
        }

        private void MapNode(string key, Cmd cmd, bool done, bool current)
        {
            bool enabled = cmd != null && cmd.Button.Enabled;
            FlowNodeVisual visual = done ? FlowNodeVisual.Done : current ? FlowNodeVisual.Current
                : enabled ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled;
            _flowMap.SetNode(key, visual, enabled, cmd == null ? null : cmd.Button.ToolTip);
        }

        private void UpdateFlowMap(bool hasPlan, bool hasOrder, int s)
        {
            long lot;
            bool hasLot = hasOrder && long.TryParse(_lotId.Text.Trim(), out lot) && lot > 0;
            bool live = hasOrder && s < 900;

            MapNode("select", _cmdSelectPlan, hasPlan || hasOrder, false);
            MapNode("create", _cmdCreateOrder, hasOrder, false);
            MapNode("release", _cmdRelease, live && s >= 10, false);
            MapNode("lot", _cmdCreateLot, hasLot, false);
            MapNode("output", _cmdCreateLot, false, false);
            bool canConsume = hasLot && (s == 10 || s == 20 || s == 30);
            _flowMap.SetNode("consume", canConsume ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled, canConsume,
                canConsume ? "Mở tab Tiêu hao LOT linh kiện theo BOM." : "Cần có LOT sản xuất và lệnh đang hoạt động.");
            MapNode("start", _cmdStart, live && s >= 20, false);
            MapNode("pause", _cmdPause, false, s == 30);
            MapNode("resume", _cmdResume, false, false);
            MapNode("complete", _cmdComplete, live && s >= 40, false);
            MapNode("close", _cmdClose, s == 50, false);
            MapNode("history", _cmdHistory, false, false);
            _flowMap.ApplyOrderStatus(hasOrder ? s : -1, hasLot);

            _mapHint.Text = _hint.Text;
            StyleBadge(_mapBadge, _badge.Text, _badge.Appearance.BackColor);
        }

        // ---------------------------------------------------------------- Tab 1: Lệnh và LOT
        private XtraTabPage BuildOrderTab()
        {
            var page = new XtraTabPage { Text = "Lệnh và LOT" };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 1) Thanh lệnh
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
            bool canFlow = UserSession.Current.HasPermission("MES.PRODUCTION_FLOW", "VIEW");
            _cmdSelectPlan = NewCmd(_orderCmds, bar, "CHỌN KẾ HOẠCH FIRM", "images/find/find_16x16.png", SelectPlanLineClick, canFlow, null,
                "Mở Control Tower để chọn dòng kế hoạch đã FIRM, đủ linh kiện, chưa có lệnh.");
            _cmdCreateOrder = NewCmd(_orderCmds, bar, "TẠO LỆNH TỪ KẾ HOẠCH", "images/actions/addfile_16x16.png", CreateOrderClick, Can("CREATE"), null,
                "Tạo lệnh sản xuất từ dòng kế hoạch đang chọn.");
            AddSeparator(bar);
            _cmdRelease = NewCmd(_orderCmds, bar, "RELEASE LỆNH", null, (s, e) => TransitionOrder(10, null), Can("APPROVE"), null,
                "Phát hành lệnh (Mới tạo → Đã phát hành).");
            _cmdCreateLot = NewCmd(_orderCmds, bar, "TẠO LOT SẢN XUẤT", null, CreateLotClick, Can("CREATE"), null,
                "Ghi nhận sản lượng OK/NG và tạo LOT sản xuất cho lệnh.");
            _cmdStart = NewCmd(_orderCmds, bar, "START LỆNH", null, (s, e) => TransitionOrder(20, null), Can("CONFIRM"), Color.FromArgb(214, 240, 222),
                "Bắt đầu sản xuất (Đã phát hành → Đang sản xuất).");
            _cmdPause = NewCmd(_orderCmds, bar, "PAUSE LỆNH", null, (s, e) => TransitionOrder(30, "Tạm dừng lệnh sản xuất?"), Can("CONFIRM"), Color.FromArgb(255, 236, 200),
                "Tạm dừng lệnh đang chạy.");
            _cmdResume = NewCmd(_orderCmds, bar, "RESUME LỆNH", null, (s, e) => TransitionOrder(20, null), Can("CONFIRM"), null,
                "Chạy tiếp lệnh đang tạm dừng.");
            _cmdComplete = NewCmd(_orderCmds, bar, "COMPLETE LỆNH", "images/actions/apply_16x16.png",
                (s, e) => TransitionOrder(40, "Hoàn thành lệnh? Hệ thống sẽ đối soát công đoạn và sản lượng. Thao tác không thể hoàn tác."),
                Can("APPROVE"), Color.FromArgb(214, 230, 250), "Hoàn thành lệnh khi các công đoạn và sản lượng đã khớp.");
            _cmdClose = NewCmd(_orderCmds, bar, "CLOSE LỆNH", "images/actions/cancel_16x16.png",
                (s, e) => TransitionOrder(50, "Đóng lệnh? Sau khi đóng chỉ còn xem lịch sử và in lại tem."),
                Can("APPROVE"), null, "Đóng lệnh sau khi đối soát vật tư, tem, NG và nhập kho.");
            AddSeparator(bar);
            _cmdHistory = NewCmd(_orderCmds, bar, "LỊCH SỬ LỆNH", "images/history/history_16x16.png",
                (s, e) => OpenTimeline("ORDER", _orderId), true, null, "Xem lịch sử workflow của lệnh.");

            _badge.AutoSizeMode = LabelAutoSizeMode.None;
            _badge.Size = new Size(170, 30);
            _badge.Margin = new Padding(14, 2, 0, 0);
            _badge.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            _badge.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _badge.Appearance.Font = new Font(Font, FontStyle.Bold);
            bar.Controls.Add(_badge);

            // 2) Gợi ý bước tiếp theo
            _hint.Dock = DockStyle.Fill;
            _hint.AutoSizeMode = LabelAutoSizeMode.None;
            _hint.Height = 30;
            _hint.Margin = new Padding(0, 0, 0, 6);
            _hint.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _hint.Appearance.BackColor = Color.FromArgb(232, 241, 252);
            _hint.Appearance.ForeColor = Color.FromArgb(20, 70, 130);
            _hint.Appearance.Options.UseBackColor = true;
            _hint.Appearance.Options.UseForeColor = true;
            _hint.Padding = new Padding(10, 0, 0, 0);

            // 3) Thanh tiến trình lệnh
            var stepper = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            string[] names = { "Mới tạo", "Đã phát hành", "Đang sản xuất", "Hoàn thành", "Đã đóng" };
            for (int i = 0; i < _steps.Length; i++)
            {
                _steps[i] = new LabelControl
                {
                    AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(150, 26), Text = (i + 1) + ". " + names[i],
                    Margin = new Padding(0, 0, 4, 0)
                };
                _steps[i].Appearance.TextOptions.HAlignment = HorzAlignment.Center;
                _steps[i].Appearance.TextOptions.VAlignment = VertAlignment.Center;
                stepper.Controls.Add(_steps[i]);
            }

            // 4) Hai nhóm nhập liệu
            var forms = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            forms.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            forms.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _planLineId.Properties.Buttons[0].Kind = ButtonPredefines.Search;
            _planLineId.Properties.NullValuePrompt = "Chọn dòng kế hoạch FIRM...";
            _planLineId.Properties.NullValuePromptShowForEmptyValue = true;
            _planLineId.ButtonClick += (s, e) => SelectPlanLineClick(s, e);
            _planLineId.ToolTip = "Nhập ID hoặc bấm biểu tượng kính lúp để chọn dòng kế hoạch FIRM.";
            _orderNo.Properties.NullValuePrompt = "Số lệnh sản xuất (nhập khi tạo lệnh)";
            _orderNo.Properties.NullValuePromptShowForEmptyValue = true;
            ReadOnly(_orderId, "Tự điền sau khi tạo lệnh");
            ReadOnly(_lotId, "Tự điền sau khi tạo LOT");

            var leftRows = FormRows(
                Row("ProductionPlanLineId", _planLineId),
                Row("OrderNo", _orderNo),
                Row("ProductionOrderId", _orderId),
                Row("ProductionLotId (sau khi tạo LOT)", _lotId));

            _productionDate.Properties.DisplayFormat.FormatType = FormatType.DateTime;
            _productionDate.Properties.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";
            _productionDate.Properties.EditFormat.FormatType = FormatType.DateTime;
            _productionDate.Properties.EditFormat.FormatString = "dd/MM/yyyy HH:mm";
            _productionDate.Properties.Mask.EditMask = "dd/MM/yyyy HH:mm";
            _productionDate.Properties.Mask.UseMaskAsDisplayFormat = true;
            _productionDate.DateTime = DateTime.Now;
            Numeric(_goodQty, "Số lượng đạt (OK) của LOT");
            Numeric(_ngQty, "Số lượng lỗi (NG) - để 0 nếu không có");
            _finished.Text = "LOT thành phẩm (FG)";
            _finished.ToolTip = "Chọn nếu là LOT thành phẩm cuối (FG). Bỏ chọn = bán thành phẩm (WIP).";
            _finished.Dock = DockStyle.Fill;

            var rightRows = FormRows(
                Row("Ngày/giờ sản xuất", _productionDate),
                Row("Số lượng OK", _goodQty),
                Row("Số lượng NG", _ngQty),
                Row(string.Empty, _finished));

            forms.Controls.Add(Group("Thông tin Kế hoạch & Lệnh", leftRows), 0, 0);
            forms.Controls.Add(Group("Ghi nhận Sản lượng & LOT", rightRows), 1, 0);

            // 5) Danh sách lệnh / kế hoạch FIRM
            var workPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            workPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            workPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var workBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            _refreshWork.Text = "TẢI LẠI DANH SÁCH (F5)";
            _refreshWork.Size = new Size(190, 28);
            _refreshWork.ImageOptions.Image = Icon("images/actions/refresh_16x16.png");
            _refreshWork.Click += (s, e) => LoadWorklist();
            workBar.Controls.Add(_refreshWork);
            workBar.Controls.Add(new LabelControl
            {
                AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(760, 28),
                Text = "Chọn một dòng để nạp thông tin vào form phía trên. Chỉ hiển thị dòng FIRM thuộc phạm vi của bạn.",
                Appearance = { TextOptions = { VAlignment = VertAlignment.Center } }
            });
            _work.Dock = DockStyle.Fill;
            _work.MainView = _workView;
            _work.ViewCollection.Add(_workView);
            ConfigureGrid(_workView);
            _workView.OptionsView.ShowFooter = true;
            _workView.CustomColumnDisplayText += WorkView_CustomColumnDisplayText;
            _workView.FocusedRowChanged += (s, e) => WorkRowChanged();
            workPanel.Controls.Add(workBar, 0, 0);
            workPanel.Controls.Add(_work, 0, 1);

            root.Controls.Add(bar, 0, 0);
            root.Controls.Add(_hint, 0, 1);
            root.Controls.Add(stepper, 0, 2);
            root.Controls.Add(forms, 0, 3);
            root.Controls.Add(workPanel, 0, 4);
            page.Controls.Add(root);
            return page;
        }

        // ---------------------------------------------------------------- Tab 2: Công đoạn
        private XtraTabPage BuildOperationTab()
        {
            var page = new XtraTabPage { Text = "Công đoạn" };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // --- Nhóm A: chọn LOT + công đoạn
            _operationLotId.Properties.NullValuePrompt = "ProductionLotId";
            _operationLotId.Properties.NullValuePromptShowForEmptyValue = true;
            ReadOnly(_routingStepId, "Chọn trong danh sách routing");
            ReadOnly(_operationNo, "Chọn trong danh sách routing");

            var aFields = FormRows(
                Row("ProductionLotId", _operationLotId),
                Row("OperationNo", _operationNo),
                Row("RoutingStepId (workflow)", _routingStepId));
            var aBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            NewCmd(null, aBar, "TẢI ROUTING THEO LOT", "images/actions/refresh_16x16.png", LoadRoutingClick, true, null,
                "Tải danh sách công đoạn của LOT, sau đó chọn công đoạn cần thao tác.");
            NewCmd(null, aBar, "LỊCH SỬ LOT", "images/history/history_16x16.png",
                (s, e) => OpenTimeline("OPERATION", _operationLotId), true, null, "Xem lịch sử workflow của công đoạn LOT.");
            _routingSteps.Dock = DockStyle.Fill;
            _routingSteps.MainView = _routingView;
            _routingSteps.ViewCollection.Add(_routingView);
            ConfigureGrid(_routingView);
            _routingView.FocusedRowChanged += (s, e) =>
            {
                var row = _routingView.GetFocusedDataRow();
                if (row == null) return;
                _routingStepId.Text = Convert.ToString(row["RoutingStepId"]);
                _operationNo.Text = Convert.ToString(row["OperationNo"]);
                RefreshOperationState();
            };
            var aPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            aPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            aPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            aPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            aPanel.Controls.Add(aFields, 0, 0);
            aPanel.Controls.Add(aBar, 0, 1);
            aPanel.Controls.Add(_routingSteps, 0, 2);

            // --- Nhóm B: điều khiển công đoạn + sản lượng
            var bBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            _opPrepare = NewCmd(_opCmds, bBar, "PREPARE", null, (s, e) => TransitionOperation(10), Can("CONFIRM"), null, "Bắt đầu chuẩn bị công đoạn.");
            _opReady = NewCmd(_opCmds, bBar, "READY", null, (s, e) => TransitionOperation(20), Can("CONFIRM"), null, "Vật tư và tem đã sẵn sàng.");
            _opStart = NewCmd(_opCmds, bBar, "START", null, (s, e) => TransitionOperation(30), Can("CONFIRM"), Color.FromArgb(214, 240, 222), "Bắt đầu công đoạn.");
            _opPause = NewCmd(_opCmds, bBar, "PAUSE", null, (s, e) => TransitionOperation(40), Can("CONFIRM"), Color.FromArgb(255, 236, 200), "Tạm dừng công đoạn.");
            _opResume = NewCmd(_opCmds, bBar, "RESUME", null, (s, e) => TransitionOperation(30), Can("CONFIRM"), null, "Chạy tiếp công đoạn.");
            _opBadge.AutoSizeMode = LabelAutoSizeMode.None;
            _opBadge.Size = new Size(150, 28);
            _opBadge.Margin = new Padding(12, 2, 0, 0);
            _opBadge.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            _opBadge.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _opBadge.Appearance.Font = new Font(Font, FontStyle.Bold);
            bBar.Controls.Add(_opBadge);

            Numeric(_inputQty, "Số lượng đầu vào (InputQty)");
            Numeric(_opGoodQty, "Số lượng đạt (GoodQty)");
            Numeric(_opNgQty, "Số lượng lỗi (NgQty)");
            Slot(_inputSlot, "Bắt buộc nếu công đoạn trước chạy VIA_WIP, ngược lại để trống.");
            Slot(_outputSlot, "Bắt buộc nếu công đoạn này chạy VIA_WIP, ngược lại để trống.");
            var bFields = FormRows(
                Row("InputQty", _inputQty),
                Row("GoodQty", _opGoodQty),
                Row("NgQty", _opNgQty),
                Row("Slot đầu vào (InputSlotId)", _inputSlot),
                Row("Slot đầu ra (OutputSlotId)", _outputSlot));
            var bComplete = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            NewCmd(null, bComplete, "XÁC NHẬN HOÀN THÀNH CÔNG ĐOẠN", "images/actions/apply_16x16.png", CompleteOperationClick,
                Can("CONFIRM"), Color.FromArgb(214, 230, 250), "Ghi nhận sản lượng và bàn giao theo cấu hình DIRECT/VIA_WIP.");
            var bPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            bPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bPanel.Controls.Add(bBar, 0, 0);
            bPanel.Controls.Add(bFields, 0, 1);
            bPanel.Controls.Add(bComplete, 0, 2);

            // --- Nhóm C: tem công đoạn
            Numeric(_labelQty, "Số sản phẩm cần chuẩn bị tem");
            _labelScan.Properties.NullValuePrompt = "Quét QR tem MES hoặc LabelCode";
            _labelScan.Properties.NullValuePromptShowForEmptyValue = true;
            _labelVoidReason.Properties.NullValuePrompt = "Lý do hủy tem chưa sử dụng";
            _labelVoidReason.Properties.NullValuePromptShowForEmptyValue = true;
            var cTop = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            cTop.Controls.Add(new LabelControl { Text = "Số tem:", Margin = new Padding(4, 10, 2, 0) });
            _labelQty.Width = 110; _labelQty.Margin = new Padding(2, 6, 10, 0);
            cTop.Controls.Add(_labelQty);
            NewCmd(null, cTop, "1. CHUẨN BỊ / CẤP TEM", null, PrepareLabelsClick, Can("CREATE"), null, "Cấp tem duy nhất cho công đoạn.");
            NewCmd(null, cTop, "2. IN TEM CÔNG ĐOẠN", "images/print/print_16x16.png", PrintLabelsClick, Can("PRINT"), null, "In tem QR của công đoạn.");
            _labels.Dock = DockStyle.Fill;
            _labels.MainView = _labelView;
            _labels.ViewCollection.Add(_labelView);
            ConfigureGrid(_labelView);
            var cBottom = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            _labelScan.Width = 300; _labelScan.Margin = new Padding(4, 6, 6, 0);
            cBottom.Controls.Add(_labelScan);
            NewCmd(null, cBottom, "XÁC NHẬN ĐÃ DÁN TEM", null, ApplyLabelClick, Can("CONFIRM"), null, "Xác nhận tem đã được dán lên sản phẩm.");
            _labelVoidReason.Width = 260; _labelVoidReason.Margin = new Padding(14, 6, 6, 0);
            cBottom.Controls.Add(_labelVoidReason);
            NewCmd(null, cBottom, "HỦY TEM CHƯA DÙNG", "images/actions/cancel_16x16.png", VoidLabelClick, Can("CANCEL"), null,
                "Chỉ hủy được tem chưa dán.");
            var cPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            cPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            cPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cPanel.Controls.Add(cTop, 0, 0);
            cPanel.Controls.Add(_labels, 0, 1);
            cPanel.Controls.Add(cBottom, 0, 2);

            root.Controls.Add(Group("Chọn LOT & công đoạn", aPanel), 0, 0);
            root.Controls.Add(Group("Điều khiển công đoạn & sản lượng", bPanel), 1, 0);
            var labelGroup = Group("Tem công đoạn", cPanel);
            root.Controls.Add(labelGroup, 0, 1);
            root.SetColumnSpan(labelGroup, 2);
            page.Controls.Add(root);
            return page;
        }

        // ---------------------------------------------------------------- Tab 3: Tiêu hao linh kiện
        private XtraTabPage BuildConsumeTab()
        {
            var page = new XtraTabPage { Text = "Tiêu hao LOT linh kiện" };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var topBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            NewCmd(null, topBar, "TẢI LINH KIỆN THEO LỆNH", "images/actions/refresh_16x16.png", LoadMaterialsClick, true, null,
                "Tải danh sách linh kiện theo BOM của lệnh đang chọn ở tab Lệnh và LOT.");
            _materials.Dock = DockStyle.Fill;
            _materials.MainView = _materialView;
            _materials.ViewCollection.Add(_materialView);
            ConfigureGrid(_materialView);
            _materialView.FocusedRowChanged += (s, e) =>
            {
                var row = _materialView.GetFocusedDataRow();
                if (row != null) _materialLineId.Text = Convert.ToString(row["ProductionOrderMaterialId"]);
            };
            top.Controls.Add(topBar, 0, 0);
            top.Controls.Add(_materials, 0, 1);

            ReadOnly(_materialLineId, "Chọn dòng linh kiện trong danh sách");
            _consumeProductionLotId.Properties.NullValuePrompt = "ProductionLotId";
            _consumeProductionLotId.Properties.NullValuePromptShowForEmptyValue = true;
            _componentLotId.Properties.NullValuePrompt = "Quét QR LOT linh kiện (FVN1|LOT)";
            _componentLotId.Properties.NullValuePromptShowForEmptyValue = true;
            Slot(_consumeSlot, "Slot chứa LOT linh kiện (PART hoặc WIP). Bắt buộc.");
            Numeric(_consumeQty, "Số lượng tiêu hao thực tế");
            var fields = FormRows(
                Row("ProductionOrderMaterialId", _materialLineId),
                Row("ProductionLotId", _consumeProductionLotId),
                Row("Quét QR LOT linh kiện", _componentLotId),
                Row("FromSlotId (PART hoặc WIP)", _consumeSlot),
                Row("Số lượng tiêu hao", _consumeQty));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            NewCmd(null, bar, "GHI NHẬN TIÊU HAO + GENEALOGY", "images/actions/apply_16x16.png", ConsumeClick, Can("CONFIRM"),
                Color.FromArgb(214, 230, 250), "Ghi nhận tiêu hao LOT linh kiện và liên kết genealogy thực tế.");
            var inner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            inner.Controls.Add(fields, 0, 0);
            inner.Controls.Add(bar, 0, 1);

            root.Controls.Add(Group("Linh kiện theo BOM của lệnh", top), 0, 0);
            root.Controls.Add(Group("Ghi nhận tiêu hao", inner), 0, 1);
            page.Controls.Add(root);
            return page;
        }

        // =====================================================================================================
        //  UI HELPERS
        // =====================================================================================================

        private static GroupControl Group(string caption, Control content)
        {
            var group = new GroupControl { Text = caption, Dock = DockStyle.Fill, Margin = new Padding(4) };
            group.AppearanceCaption.Font = new Font("Segoe UI Semibold", 10.5F);
            content.Dock = DockStyle.Fill;
            group.Controls.Add(content);
            return group;
        }

        private static KeyValuePair<string, Control> Row(string caption, Control editor)
        {
            return new KeyValuePair<string, Control>(caption, editor);
        }

        private static TableLayoutPanel FormRows(params KeyValuePair<string, Control>[] rows)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = rows.Length, Padding = new Padding(6, 4, 6, 4)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows.Length; i++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                var label = new LabelControl
                {
                    Text = rows[i].Key, Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None
                };
                label.Appearance.TextOptions.VAlignment = VertAlignment.Center;
                label.Appearance.TextOptions.WordWrap = WordWrap.Wrap;
                var editor = rows[i].Value;
                editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                editor.Margin = new Padding(0, 3, 0, 3);
                table.Controls.Add(label, 0, i);
                table.Controls.Add(editor, 1, i);
            }
            return table;
        }

        private static void ReadOnly(TextEdit input, string prompt)
        {
            input.Properties.ReadOnly = true;
            input.Properties.NullValuePrompt = prompt;
            input.Properties.NullValuePromptShowForEmptyValue = true;
            input.Properties.Appearance.BackColor = Color.FromArgb(244, 244, 244);
            input.Properties.Appearance.Options.UseBackColor = true;
        }

        private static void Numeric(TextEdit input, string prompt)
        {
            input.Properties.Mask.MaskType = MaskType.Numeric;
            input.Properties.Mask.EditMask = "n2";
            input.Properties.Mask.UseMaskAsDisplayFormat = true;
            input.Properties.Appearance.TextOptions.HAlignment = HorzAlignment.Far;
            input.Properties.NullValuePrompt = prompt;
            input.Properties.NullValuePromptShowForEmptyValue = true;
        }

        private static void Slot(TextEdit input, string tooltip)
        {
            input.Properties.Mask.MaskType = MaskType.Numeric;
            input.Properties.Mask.EditMask = "d";
            input.Properties.Mask.UseMaskAsDisplayFormat = true;
            input.Properties.Appearance.TextOptions.HAlignment = HorzAlignment.Far;
            input.ToolTip = tooltip;
        }

        private static void AddSeparator(FlowLayoutPanel bar)
        {
            bar.Controls.Add(new Panel { Width = 1, Height = 28, BackColor = Color.Silver, Margin = new Padding(8, 3, 8, 3) });
        }

        private static Image Icon(string name)
        {
            try { return DevExpress.Images.ImageResourceCache.Default.GetImage(name); }
            catch { return null; }
        }

        private static Cmd NewCmd(List<Cmd> registry, Control parent, string caption, string icon, EventHandler handler,
            bool permitted, Color? tint, string tooltip)
        {
            var button = new SimpleButton
            {
                Text = caption, AutoSize = true, MinimumSize = new Size(0, 30), Height = 30,
                Margin = new Padding(3, 2, 3, 2), Padding = new Padding(6, 0, 6, 0), Enabled = permitted
            };
            var image = icon == null ? null : Icon(icon);
            if (image != null) button.ImageOptions.Image = image;
            if (tint.HasValue)
            {
                button.Appearance.BackColor = tint.Value;
                button.Appearance.Options.UseBackColor = true;
            }
            var cmd = new Cmd { Button = button, Permitted = permitted, Caption = tooltip ?? caption };
            button.ToolTip = permitted ? cmd.Caption : "Bạn chỉ có quyền theo dõi, không có quyền thực hiện thao tác này.";
            button.Click += handler;
            parent.Controls.Add(button);
            if (registry != null) registry.Add(cmd);
            return cmd;
        }

        /// <summary>Bật nút khi có quyền VÀ đúng trạng thái; tooltip giải thích vì sao đang tắt.</summary>
        private static void SetCmd(Cmd cmd, bool stateAllows, string blockedReason)
        {
            if (cmd == null) return;
            cmd.Button.Enabled = cmd.Permitted && stateAllows;
            cmd.Button.ToolTip = !cmd.Permitted
                ? "Bạn chỉ có quyền theo dõi, không có quyền thực hiện thao tác này."
                : stateAllows ? cmd.Caption : blockedReason;
        }

        private static void ConfigureGrid(GridView view)
        {
            view.OptionsBehavior.Editable = false;
            view.OptionsBehavior.ReadOnly = true;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ShowAutoFilterRow = true;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.EnableAppearanceEvenRow = true;
            view.OptionsView.ShowIndicator = true;
            view.OptionsSelection.MultiSelect = false;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            view.FocusRectStyle = DrawFocusRectStyle.RowFocus;
            view.RowHeight = 26;
        }

        private static bool Can(string action)
        {
            return UserSession.Current != null &&
                UserSession.Current.HasPermission("MES.PRODUCTION_EXECUTION", action);
        }

        private void SetStatus(string message, StatusKind kind)
        {
            string prefix = kind == StatusKind.Success ? "✔  " : kind == StatusKind.Error ? "✖  " : "ℹ  ";
            _status.Text = prefix + message;
            _status.Appearance.ForeColor = kind == StatusKind.Success ? Green : kind == StatusKind.Error ? Red : Color.FromArgb(60, 60, 60);
            _status.Appearance.Options.UseForeColor = true;
        }

        private static void StyleBadge(LabelControl badge, string text, Color color)
        {
            badge.Text = text;
            badge.Appearance.BackColor = color;
            badge.Appearance.ForeColor = Color.White;
            badge.Appearance.Options.UseBackColor = true;
            badge.Appearance.Options.UseForeColor = true;
        }

        // =====================================================================================================
        //  STATE (ORDER / OPERATION)
        // =====================================================================================================

        private void RefreshOrderState()
        {
            _orderStatus = -1;
            long id;
            if (long.TryParse(_orderId.Text.Trim(), out id) && id > 0)
            {
                try { _orderStatus = _workflow.GetCurrentStatus("ORDER", id); }
                catch (Exception ex) { SetStatus("Không đọc được trạng thái lệnh: " + ex.Message, StatusKind.Error); }
            }
            ApplyOrderState();
        }

        private void ApplyOrderState()
        {
            long planLine, orderId;
            bool hasPlan = long.TryParse(_planLineId.Text.Trim(), out planLine) && planLine > 0;
            bool hasOrder = long.TryParse(_orderId.Text.Trim(), out orderId) && orderId > 0;
            int s = hasOrder ? _orderStatus : -1;
            const string needOrder = "Chưa có lệnh. Hãy tạo lệnh từ kế hoạch hoặc chọn một lệnh trong danh sách.";

            SetCmd(_cmdSelectPlan, true, null);
            SetCmd(_cmdCreateOrder, hasPlan && !hasOrder,
                hasOrder ? "Dòng kế hoạch này đã có lệnh." : "Hãy chọn dòng kế hoạch FIRM trước.");
            SetCmd(_cmdRelease, hasOrder && s == 0, hasOrder ? "Chỉ release được lệnh ở trạng thái Mới tạo." : needOrder);
            SetCmd(_cmdCreateLot, hasOrder && (s == 10 || s == 20 || s == 30),
                hasOrder ? "Chỉ tạo LOT khi lệnh đã phát hành / đang chạy / tạm dừng." : needOrder);
            SetCmd(_cmdStart, hasOrder && s == 10, hasOrder ? "Chỉ start được lệnh đã phát hành." : needOrder);
            SetCmd(_cmdPause, hasOrder && s == 20, hasOrder ? "Chỉ pause được lệnh đang sản xuất." : needOrder);
            SetCmd(_cmdResume, hasOrder && s == 30, hasOrder ? "Chỉ resume được lệnh đang tạm dừng." : needOrder);
            SetCmd(_cmdComplete, hasOrder && s == 20, hasOrder ? "Chỉ complete được lệnh đang sản xuất." : needOrder);
            SetCmd(_cmdClose, hasOrder && s == 40, hasOrder ? "Chỉ close được lệnh đã hoàn thành." : needOrder);
            SetCmd(_cmdHistory, hasOrder, needOrder);

            // Badge
            if (!hasOrder) StyleBadge(_badge, hasPlan ? "CHƯA CÓ LỆNH" : "CHƯA CHỌN", Gray);
            else if (s == 900) StyleBadge(_badge, "ĐÃ HỦY", Red);
            else if (s == 20) StyleBadge(_badge, "ĐANG CHẠY", Green);
            else if (s == 30) StyleBadge(_badge, "TẠM DỪNG", Orange);
            else if (s == 40 || s == 10) StyleBadge(_badge, ProductionOrderStatus.Text(s).Substring(3).ToUpperInvariant(), Blue);
            else if (s == 50) StyleBadge(_badge, "ĐÃ ĐÓNG", Gray);
            else StyleBadge(_badge, s == 0 ? "MỚI TẠO" : "KHÔNG RÕ", Gray);

            UpdateStepper(hasOrder ? s : -1);
            _hint.Text = HintFor(hasPlan, hasOrder, s);
            UpdateFlowMap(hasPlan, hasOrder, s);
        }

        private void UpdateStepper(int status)
        {
            int current = status == 0 ? 0 : status == 10 ? 1 : status == 20 || status == 30 ? 2 : status == 40 ? 3 : status == 50 ? 4 : -1;
            _steps[2].Text = status == 30 ? "3. Tạm dừng" : "3. Đang sản xuất";
            for (int i = 0; i < _steps.Length; i++)
            {
                var label = _steps[i];
                label.Appearance.Options.UseBackColor = true;
                label.Appearance.Options.UseForeColor = true;
                label.Appearance.Options.UseFont = true;
                if (status == 900) { label.Appearance.BackColor = Color.FromArgb(248, 225, 222); label.Appearance.ForeColor = Red; label.Appearance.Font = new Font(Font, FontStyle.Regular); }
                else if (current >= 0 && i < current) { label.Appearance.BackColor = Color.FromArgb(222, 241, 230); label.Appearance.ForeColor = Green; label.Appearance.Font = new Font(Font, FontStyle.Regular); }
                else if (i == current) { label.Appearance.BackColor = status == 30 ? Orange : Blue; label.Appearance.ForeColor = Color.White; label.Appearance.Font = new Font(Font, FontStyle.Bold); }
                else { label.Appearance.BackColor = Color.FromArgb(238, 238, 238); label.Appearance.ForeColor = Gray; label.Appearance.Font = new Font(Font, FontStyle.Regular); }
            }
        }

        private static string HintFor(bool hasPlan, bool hasOrder, int s)
        {
            if (!hasOrder && !hasPlan) return "Bước 1: bấm CHỌN KẾ HOẠCH FIRM, hoặc chọn một dòng trong danh sách bên dưới.";
            if (!hasOrder) return "Bước 2: nhập OrderNo rồi bấm TẠO LỆNH TỪ KẾ HOẠCH.";
            switch (s)
            {
                case 0: return "Bước 3: bấm RELEASE LỆNH để phát hành lệnh.";
                case 10: return "Bước 4: nhập số lượng OK/NG và bấm TẠO LOT SẢN XUẤT, sau đó START LỆNH.";
                case 20: return "Đang sản xuất: ghi nhận công đoạn, tem và tiêu hao ở các tab bên cạnh. Khi đủ sản lượng bấm COMPLETE LỆNH.";
                case 30: return "Lệnh đang tạm dừng: bấm RESUME LỆNH để chạy tiếp.";
                case 40: return "Lệnh đã hoàn thành: kiểm tra đối soát vật tư, tem, NG rồi bấm CLOSE LỆNH.";
                case 50: return "Lệnh đã đóng: chỉ còn xem lịch sử và in lại tem.";
                case 900: return "Lệnh đã hủy.";
                default: return "Không xác định được trạng thái lệnh. Bấm TẢI LẠI DANH SÁCH để đồng bộ.";
            }
        }

        private void RefreshOperationState()
        {
            _operationStatus = -1;
            long lotId, stepId;
            if (long.TryParse(_operationLotId.Text.Trim(), out lotId) && lotId > 0 &&
                long.TryParse(_routingStepId.Text.Trim(), out stepId) && stepId > 0)
            {
                try { _operationStatus = _workflow.GetCurrentStatus("OPERATION", lotId, stepId); }
                catch (Exception ex) { SetStatus("Không đọc được trạng thái công đoạn: " + ex.Message, StatusKind.Error); }
            }
            ApplyOperationState();
        }

        private void ApplyOperationState()
        {
            int s = _operationStatus;
            const string pick = "Tải routing và chọn một công đoạn trước.";
            bool known = s >= 0;
            SetCmd(_opPrepare, known && s == 0, known ? "Chỉ PREPARE khi công đoạn chưa bắt đầu." : pick);
            SetCmd(_opReady, known && s == 10, known ? "Chỉ READY sau khi PREPARE." : pick);
            SetCmd(_opStart, known && s == 20, known ? "Chỉ START khi công đoạn đã READY." : pick);
            SetCmd(_opPause, known && s == 30, known ? "Chỉ PAUSE khi công đoạn đang chạy." : pick);
            SetCmd(_opResume, known && s == 40, known ? "Chỉ RESUME khi công đoạn đang tạm dừng." : pick);
            switch (s)
            {
                case 0: StyleBadge(_opBadge, "CHƯA BẮT ĐẦU", Gray); break;
                case 10: StyleBadge(_opBadge, "ĐANG CHUẨN BỊ", Blue); break;
                case 20: StyleBadge(_opBadge, "SẴN SÀNG", Blue); break;
                case 30: StyleBadge(_opBadge, "ĐANG CHẠY", Green); break;
                case 40: StyleBadge(_opBadge, "TẠM DỪNG", Orange); break;
                case 50: StyleBadge(_opBadge, "HOÀN THÀNH", Gray); break;
                case 900: StyleBadge(_opBadge, "ĐÃ HỦY", Red); break;
                default: StyleBadge(_opBadge, "CHƯA CHỌN CĐ", Gray); break;
            }
        }

        // =====================================================================================================
        //  WORKLIST (dòng kế hoạch FIRM / lệnh trong phạm vi của người dùng)
        // =====================================================================================================

        private string ResolveSite()
        {
            var scope = UserSession.Current.DataScopes.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.SiteCode) &&
                (string.IsNullOrWhiteSpace(x.FunctionCode) ||
                 string.Equals(x.FunctionCode, "MES.PRODUCTION_FLOW", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(x.FunctionCode, "MES.PRODUCTION_EXECUTION", StringComparison.OrdinalIgnoreCase)));
            return scope == null ? null : scope.SiteCode;
        }

        private void LoadWorklist()
        {
            if (_loadingWork) return;
            if (!UserSession.Current.HasPermission("MES.PRODUCTION_FLOW", "VIEW"))
            {
                SetStatus("Tài khoản không có quyền xem dòng chảy sản xuất nên không hiển thị danh sách lệnh. Bạn vẫn có thể nhập ID thủ công.", StatusKind.Info);
                return;
            }
            string site = ResolveSite();
            if (string.IsNullOrWhiteSpace(site))
            {
                SetStatus("Không tìm thấy Site trong phạm vi được cấp cho tài khoản.", StatusKind.Error);
                return;
            }
            _loadingWork = true;
            _refreshWork.Enabled = false;
            long keepKey = 0;
            var focused = _workView.GetFocusedDataRow();
            if (focused != null) long.TryParse(Str(focused, "ProductionPlanLineId"), out keepKey);

            Task.Run(() =>
            {
                try
                {
                    if (_flow == null) _flow = MesProductionFlowComposition.Create();
                    return Tuple.Create(_flow.GetDashboard(site, null, null), (Exception)null);
                }
                catch (Exception ex) { return Tuple.Create((DataTable)null, ex); }
            }).ContinueWith(task =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)(() => ApplyWorklist(task.Result.Item1, task.Result.Item2, keepKey)));
            });
        }

        private void ApplyWorklist(DataTable table, Exception error, long keepKey)
        {
            _loadingWork = false;
            _refreshWork.Enabled = true;
            if (error != null)
            {
                SetStatus("Không tải được danh sách lệnh: " + error.GetBaseException().Message, StatusKind.Error);
                return;
            }
            _syncing = true;
            try
            {
                _work.DataSource = table;
                ConfigureWorkColumns();
                if (keepKey > 0 && table.Columns.Contains("ProductionPlanLineId"))
                {
                    int handle = _workView.LocateByValue("ProductionPlanLineId", keepKey);
                    if (handle != GridControl.InvalidRowHandle) _workView.FocusedRowHandle = handle;
                }
            }
            finally { _syncing = false; }
            SetStatus("Đã tải " + _workView.RowCount.ToString("N0") + " dòng kế hoạch FIRM trong phạm vi của bạn.", StatusKind.Info);
        }

        private void ConfigureWorkColumns()
        {
            var cols = _workView.Columns;
            if (cols.Count == 0) return;
            string[] visible = { "PlanNo", "ProductionDate", "ShiftCode", "ProductionLineCode", "ProductPartNo", "ProductDescription",
                "PlannedQty", "OrderNo", "OrderStatus", "ProductionLotCount", "ProducedQty", "CompletedOperationCount", "OperationCount" };
            foreach (DevExpress.XtraGrid.Columns.GridColumn column in cols)
                column.Visible = visible.Contains(column.FieldName);
            Caption("PlanNo", "Kế hoạch"); Caption("ProductionDate", "Ngày SX"); Caption("ShiftCode", "Ca");
            Caption("ProductionLineCode", "Chuyền"); Caption("ProductPartNo", "Mã sản phẩm"); Caption("ProductDescription", "Tên sản phẩm");
            Caption("PlannedQty", "SL kế hoạch"); Caption("OrderNo", "Lệnh SX"); Caption("OrderStatus", "Trạng thái lệnh");
            Caption("ProductionLotCount", "Số LOT"); Caption("ProducedQty", "SL đã SX");
            Caption("CompletedOperationCount", "CĐ hoàn thành"); Caption("OperationCount", "Số CĐ");
            for (int i = 0; i < visible.Length; i++)
                if (cols[visible[i]] != null) cols[visible[i]].VisibleIndex = i;
            foreach (var name in new[] { "PlannedQty", "ProducedQty" })
            {
                if (cols[name] == null) continue;
                cols[name].DisplayFormat.FormatType = FormatType.Numeric;
                cols[name].DisplayFormat.FormatString = "n0";
                cols[name].SummaryItem.SummaryType = DevExpress.Data.SummaryItemType.Sum;
                cols[name].SummaryItem.DisplayFormat = "{0:n0}";
            }
            if (cols["ProductionDate"] != null)
            {
                cols["ProductionDate"].DisplayFormat.FormatType = FormatType.DateTime;
                cols["ProductionDate"].DisplayFormat.FormatString = "dd/MM/yyyy";
            }
            if (cols["PlanStatus"] != null && cols["IsRelatedDepartment"] != null)
                _workView.ActiveFilterString = "[PlanStatus] = 'FIRM' AND [IsRelatedDepartment] = False";
            _workView.BestFitColumns();
        }

        private void Caption(string field, string caption)
        {
            if (_workView.Columns[field] != null) _workView.Columns[field].Caption = caption;
        }

        private void WorkView_CustomColumnDisplayText(object sender, DevExpress.XtraGrid.Views.Base.CustomColumnDisplayTextEventArgs e)
        {
            if (e.Column.FieldName != "OrderStatus") return;
            int code;
            if (ProductionOrderStatus.TryFromName(Convert.ToString(e.Value), out code))
                e.DisplayText = ProductionOrderStatus.Text(code);
        }

        private void WorkRowChanged()
        {
            if (_syncing) return;
            var row = _workView.GetFocusedDataRow();
            if (row == null) return;
            _syncing = true;
            try
            {
                _planLineId.Text = Str(row, "ProductionPlanLineId");
                string orderId = Str(row, "ProductionOrderId");
                _orderId.Text = orderId;
                if (orderId.Length > 0) _orderNo.Text = Str(row, "OrderNo");
                string lot = Str(row, "LatestProductionLotId");
                _lotId.Text = lot;
                if (lot.Length > 0 && lot != "0")
                {
                    _operationLotId.Text = lot;
                    _consumeProductionLotId.Text = lot;
                }
            }
            finally { _syncing = false; }
            RefreshOrderState();
        }

        private static string Str(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToString(row[column]) : string.Empty;
        }

        // =====================================================================================================
        //  PARSING
        // =====================================================================================================

        private static long LongValue(TextEdit input, string name)
        {
            long value;
            if (!long.TryParse(input.Text.Trim(), out value) || value <= 0)
                throw new InvalidOperationException(name + " phải là số nguyên dương.");
            return value;
        }

        private static decimal DecimalValue(TextEdit input, string name, bool allowZero)
        {
            decimal value;
            if (!decimal.TryParse(input.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
                value < 0 || (!allowZero && value == 0))
                throw new InvalidOperationException(name + " không hợp lệ.");
            return value;
        }

        /// <summary>Số lượng NG: để trống được hiểu là 0.</summary>
        private static decimal OptionalDecimal(TextEdit input, string name)
        {
            return string.IsNullOrWhiteSpace(input.Text) ? 0m : DecimalValue(input, name, true);
        }

        private static int? OptionalSlot(TextEdit input)
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return null;
            int value;
            if (!int.TryParse(input.Text.Trim(), out value) || value <= 0)
                throw new InvalidOperationException("SlotId phải là số nguyên dương hoặc để trống.");
            return value;
        }

        private bool Confirm(string message, string title)
        {
            return XtraMessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        // =====================================================================================================
        //  ACTIONS
        // =====================================================================================================

        private void SelectPlanLineClick(object sender, EventArgs e)
        {
            using (var picker = new FormMesProductionControlTower(true))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                _syncing = true;
                try
                {
                    _planLineId.Text = picker.SelectedPlanLineId.ToString(CultureInfo.InvariantCulture);
                    _orderId.Text = string.Empty;
                    _lotId.Text = string.Empty;
                }
                finally { _syncing = false; }
                RefreshOrderState();
                SetStatus("Đã chọn dòng kế hoạch " + _planLineId.Text + ". Nhập OrderNo rồi bấm TẠO LỆNH TỪ KẾ HOẠCH.", StatusKind.Info);
                _orderNo.Focus();
            }
        }

        private void CreateOrderClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                if (string.IsNullOrWhiteSpace(_orderNo.Text))
                    throw new InvalidOperationException("Nhập OrderNo cho lệnh sản xuất.");
                var result = _service.CreateOrder(LongValue(_planLineId, "ProductionPlanLineId"),
                    _orderNo.Text.Trim(), Environment.UserName);
                if (result.Rows.Count > 0) _orderId.Text = Convert.ToString(result.Rows[0]["ProductionOrderId"]);
                RefreshOrderState();
                LoadWorklist();
                return "Đã tạo lệnh MES " + _orderNo.Text.Trim() + ". " + HintFor(true, true, _orderStatus);
            });
        }

        private void CreateLotClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                var result = _service.CreateLot(LongValue(_orderId, "ProductionOrderId"),
                    _productionDate.DateTime.ToUniversalTime(), DecimalValue(_goodQty, "Số lượng OK", false),
                    OptionalDecimal(_ngQty, "Số lượng NG"), _finished.Checked, Environment.UserName);
                string message = "Đã tạo LOT sản xuất.";
                if (result.Rows.Count > 0)
                {
                    _lotId.Text = Convert.ToString(result.Rows[0]["ProductionLotId"]);
                    _operationLotId.Text = _lotId.Text;
                    _consumeProductionLotId.Text = _lotId.Text;
                    _routingSteps.DataSource = _service.GetRoutingSteps(LongValue(_operationLotId, "ProductionLotId"));
                    _routingView.BestFitColumns();
                    message = "Đã tạo LOT " + Convert.ToString(result.Rows[0]["LotNo"]) + " và tải routing. Chuyển sang tab Công đoạn để thao tác.";
                }
                _goodQty.Text = string.Empty;
                _ngQty.Text = string.Empty;
                RefreshOrderState();
                LoadWorklist();
                return message;
            });
        }

        private void CompleteOperationClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                _service.CompleteOperation(LongValue(_operationLotId, "ProductionLotId"), _operationNo.Text,
                    DecimalValue(_inputQty, "InputQty", false), DecimalValue(_opGoodQty, "GoodQty", true),
                    OptionalDecimal(_opNgQty, "NgQty"), OptionalSlot(_inputSlot), OptionalSlot(_outputSlot),
                    Environment.UserName);
                _routingSteps.DataSource = _service.GetRoutingSteps(LongValue(_operationLotId, "ProductionLotId"));
                _routingView.BestFitColumns();
                RefreshOperationState();
                LoadWorklist();
                return "Đã ghi nhận hoàn thành công đoạn và bàn giao theo cấu hình DIRECT/VIA_WIP. Workflow đã được SQL đồng bộ.";
            });
        }

        private void PrepareLabelsClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                int quantity;
                if (!int.TryParse(_labelQty.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out quantity) || quantity <= 0)
                    throw new InvalidOperationException("Số tem phải là số nguyên dương.");
                _labels.DataSource = _service.PrepareLabels(
                    LongValue(_operationLotId, "ProductionLotId"), _operationNo.Text,
                    quantity, Environment.UserName);
                _labelView.BestFitColumns();
                return "Đã cấp " + _labelView.RowCount.ToString("N0") +
                    " tem duy nhất cho công đoạn " + _operationNo.Text + ". Bấm 2. IN TEM CÔNG ĐOẠN để in.";
            });
        }

        private void PrintLabelsClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                using (var document = new PrintDocument())
                using (var dialog = new PrintDialog { Document = document, UseEXDialog = true })
                {
                    dialog.Document.DefaultPageSettings.Landscape = false;
                    dialog.Document.DefaultPageSettings.PaperSize = new PaperSize("MES product label 75x50mm", 295, 197);
                    dialog.Document.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return "Đã hủy lệnh in; chưa gửi dữ liệu in.";
                    _printRows = _service.PrintLabels(
                        LongValue(_operationLotId, "ProductionLotId"), _operationNo.Text,
                        Environment.UserName, _labels.DataSource is DataTable &&
                            ((DataTable)_labels.DataSource).Select("Status = 'RESERVED'").Length == 0);
                    if (_printRows.Rows.Count == 0)
                        throw new InvalidOperationException("Không có tem hợp lệ để in.");
                    _printIndex = 0;
                    document.PrintPage += (s, args) =>
                    {
                        if (_printIndex >= _printRows.Rows.Count) { args.HasMorePages = false; return; }
                        var row = _printRows.Rows[_printIndex++];
                        using (var titleFont = new Font("Arial", 10, FontStyle.Bold))
                        using (var bodyFont = new Font("Arial", 9, FontStyle.Regular))
                        {
                            float y = 8;
                            args.Graphics.DrawString("FVN MES - PRODUCT LABEL", titleFont, Brushes.Black, 25, y);
                            y += 20;
                            args.Graphics.DrawString("LABEL: " + Convert.ToString(row["LabelCode"]), titleFont, Brushes.Black, 25, y);
                            y += 18;
                            args.Graphics.DrawString("LOT: " + Convert.ToString(row["LotNo"]), bodyFont, Brushes.Black, 25, y);
                            y += 17;
                            args.Graphics.DrawString("PROCESS: " + Convert.ToString(row["OperationNo"]) +
                                " - " + Convert.ToString(row["OperationName"]), bodyFont, Brushes.Black, 25, y);
                            y += 18;
                            var qrPayload = Convert.ToString(row["QrPayload"]);
                            using (var qrControl = new BarCodeControl())
                            using (var qrBitmap = new Bitmap(120, 120))
                            {
                                qrControl.Size = new Size(120, 120);
                                qrControl.AutoModule = true;
                                qrControl.Text = qrPayload;
                                var qrGenerator = new QRCodeGenerator
                                {
                                    CompactionMode = QRCodeCompactionMode.Byte,
                                    ErrorCorrectionLevel = QRCodeErrorCorrectionLevel.H,
                                    Version = QRCodeVersion.AutoVersion
                                };
                                qrControl.Symbology = qrGenerator;
                                qrControl.CreateControl();
                                qrControl.DrawToBitmap(qrBitmap, new Rectangle(0, 0, 120, 120));
                                args.Graphics.DrawImage(qrBitmap, 20, y, 95, 95);
                            }
                            args.Graphics.DrawString("SCAN QR", bodyFont, Brushes.Black, 125, y + 10);
                            args.Graphics.DrawString(qrPayload, bodyFont, Brushes.Black,
                                new RectangleF(125, y + 28, 150, 65));
                        }
                        args.HasMorePages = _printIndex < _printRows.Rows.Count;
                    };
                    document.Print();
                    _labels.DataSource = _printRows;
                    _labelView.BestFitColumns();
                    return "Đã gửi " + _printRows.Rows.Count.ToString("N0") +
                        " tem tới máy in. QR Code và dữ liệu tem đã được gửi tới máy in.";
                }
            });
        }

        private void ApplyLabelClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                var result = _service.ApplyLabel(_labelScan.Text, _operationNo.Text, Environment.UserName);
                _labelScan.Text = string.Empty;
                _labelScan.Focus();
                return "Đã xác nhận tem " + Convert.ToString(result.Rows[0]["LabelCode"]) +
                    " đã được dán cho công đoạn " + _operationNo.Text + ".";
            });
        }

        private void VoidLabelClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                if (string.IsNullOrWhiteSpace(_labelVoidReason.Text))
                    throw new InvalidOperationException("Nhập lý do hủy tem.");
                if (!Confirm("Hủy tem chưa sử dụng? Tem đã hủy không dùng lại được.", "Hủy tem")) return "Đã bỏ qua thao tác hủy tem.";
                var result = _service.VoidLabel(_labelScan.Text, Environment.UserName, _labelVoidReason.Text);
                _labelScan.Text = string.Empty;
                _labelVoidReason.Text = string.Empty;
                return "Đã hủy tem " + Convert.ToString(result.Rows[0]["LabelCode"]) +
                    ". Tem đã dán không thể hủy.";
            });
        }

        private void LoadMaterialsClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                _materials.DataSource = _service.GetOrderMaterials(LongValue(_orderId, "ProductionOrderId"));
                _materialView.BestFitColumns();
                return "Đã tải danh sách linh kiện theo BOM của lệnh sản xuất.";
            });
        }

        private void ConsumeClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                _service.ConsumeMaterialByQr(LongValue(_materialLineId, "ProductionOrderMaterialId"),
                    LongValue(_consumeProductionLotId, "ProductionLotId"), _componentLotId.Text,
                    Convert.ToInt32(LongValue(_consumeSlot, "FromSlotId")),
                    DecimalValue(_consumeQty, "Số lượng tiêu hao", false), Environment.UserName);
                _componentLotId.Text = string.Empty;
                _componentLotId.Focus();
                return "Đã ghi nhận tiêu hao LOT và liên kết genealogy thực tế.";
            });
        }

        private void TransitionOrder(int targetStatus, string confirmMessage)
        {
            Run(() =>
            {
                long id = LongValue(_orderId, "ProductionOrderId");
                if (confirmMessage != null && !Confirm(confirmMessage, "Điều hành sản xuất MES"))
                    return "Đã bỏ qua thao tác.";
                int current = _workflow.GetCurrentStatus("ORDER", id);
                _workflow.TransitionOrder(id, current, targetStatus, AuthorizationGuard.ActorCode,
                    "Điều hành từ form MES");
                RefreshOrderState();
                LoadWorklist();
                return "Lệnh " + id + ": " + ProductionOrderStatus.Text(current) + " → " + ProductionOrderStatus.Text(targetStatus) +
                    ". " + HintFor(true, true, _orderStatus);
            });
        }

        private void LoadRoutingClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                _routingSteps.DataSource = _service.GetRoutingSteps(LongValue(_operationLotId, "ProductionLotId"));
                _routingView.BestFitColumns();
                RefreshOperationState();
                return "Đã tải routing theo LOT. Chọn công đoạn trong danh sách để thao tác workflow.";
            });
        }

        private void TransitionOperation(int targetStatus)
        {
            Run(() =>
            {
                long lotId = LongValue(_operationLotId, "ProductionLotId");
                long stepId = LongValue(_routingStepId, "RoutingStepId");
                int current = _workflow.GetCurrentStatus("OPERATION", lotId, stepId);
                _workflow.TransitionOperation(lotId, stepId, current, targetStatus,
                    AuthorizationGuard.ActorCode, "Điều hành công đoạn " + _operationNo.Text);
                RefreshOperationState();
                LoadWorklist();
                return "Công đoạn " + _operationNo.Text + " đã chuyển trạng thái (" + current + " → " + targetStatus + ").";
            });
        }

        private void OpenTimeline(string entityType, TextEdit idInput)
        {
            Run(() =>
            {
                long id = LongValue(idInput, entityType == "ORDER" ? "ProductionOrderId" : "ProductionLotId");
                using (var form = new FormProductionWorkflowTimeline(_workflow))
                {
                    form.SetEntity(entityType, id);
                    form.ShowDialog(this);
                }
                return "Đã xem lịch sử workflow " + entityType + " #" + id + ".";
            });
        }

        private void Run(Func<string> action)
        {
            var previous = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                SetStatus(action(), StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, StatusKind.Error);
                XtraMessageBox.Show(this, ex.Message, "Điều hành sản xuất MES",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Cursor.Current = previous; }
        }
    }
}
