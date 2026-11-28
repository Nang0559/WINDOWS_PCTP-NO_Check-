using System;
using DevExpress.XtraEditors;


using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using PCTP.Infrastructure.Security;
using PCTP.Modules.MesPlanning;
using PCTP.Modules.MesPlanning.UI;
using PCTP.Modules.NhapKho.ComponentReceiving;
using PCTP.Security;
using PCTP.Shared.Helpers;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.KhoLinhKien.UI
{
    /// <summary>
    /// Màn hình kho linh kiện PART (DevExpress) theo luồng:
    ///   0 Yêu cầu mua hàng (upload PO + workflow) → 1 Nhận linh kiện QR → 2 Tồn kho (PartNo/LOT/Site/Slot)
    ///   → 3 Điều chuyển LOT → 4 Xuất theo nhu cầu sản xuất → 5 Truy xuất LOT tới thành phẩm.
    /// Vào màn hình cần WAREHOUSE.COMPONENT_STOCK/VIEW; mỗi tab chỉ hiện khi tài khoản có đúng quyền của
    /// bước đó, nút thao tác kiểm tra thêm action (CREATE/CONFIRM/APPROVE/CANCEL) và dịch vụ kiểm tra lại
    /// quyền + phạm vi Site ở tầng service.
    /// </summary>
    public sealed class FormKhoLinhKien : XtraForm
    {
        private const string FnStock = "WAREHOUSE.COMPONENT_STOCK";
        private const string FnPo = ComponentPoStatus.Function;
        private const string FnReceipt = "WAREHOUSE.COMPONENT_RECEIPT";
        private const string FnTransfer = ComponentStockService.TransferFunction;
        private const string FnDemand = "MES.COMPONENT_DEMAND";
        private const string FnIssue = "MES.COMPONENT_ISSUE";
        private const string FnTrace = "REPORT.COMPONENT_TRACEABILITY";

        private readonly IComponentPurchaseOrderService _po;
        private readonly IComponentStockService _stock;
        private readonly IProductionTraceService _trace;

        private readonly XtraTabControl _tabs = new XtraTabControl();
        private readonly LabelControl _status = new LabelControl();

        // Tab 0 - PO
        private readonly ComboBoxEdit _poFilter = new ComboBoxEdit();
        private readonly GridControl _poGrid = new GridControl();
        private readonly GridView _poView = new GridView();
        private readonly GridControl _poLinesGrid = new GridControl();
        private readonly GridView _poLinesView = new GridView();
        private readonly GridControl _poEventsGrid = new GridControl();
        private readonly GridView _poEventsView = new GridView();
        private readonly SimpleButton _btnUpload = new SimpleButton();
        private readonly SimpleButton _btnTemplate = new SimpleButton();
        private readonly SimpleButton _btnValidate = new SimpleButton();
        private readonly SimpleButton _btnRelease = new SimpleButton();
        private readonly SimpleButton _btnClose = new SimpleButton();
        private readonly SimpleButton _btnCancel = new SimpleButton();
        private DataTable _poTable;
        private long _poDetailFor = -1;

        // Tab 2 - Stock
        private readonly ComboBoxEdit _site = new ComboBoxEdit();
        private readonly TextEdit _partNo = new TextEdit();
        private readonly TextEdit _lotNo = new TextEdit();
        private readonly TextEdit _slotText = new TextEdit();
        private readonly CheckEdit _includeZero = new CheckEdit();
        private readonly GridControl _stockGrid = new GridControl();
        private readonly GridView _stockView = new GridView();
        private readonly SimpleButton _btnToTransfer = new SimpleButton();
        private readonly SimpleButton _btnToTrace = new SimpleButton();

        // Tab 3 - Transfer
        private readonly LabelControl _xferSource = new LabelControl();
        private readonly LookUpEdit _toSlot = new LookUpEdit();
        private readonly SpinEdit _xferQty = new SpinEdit();
        private readonly TextEdit _xferReason = new TextEdit();
        private readonly TextEdit _xferNo = new TextEdit();
        private readonly SimpleButton _btnTransfer = new SimpleButton();
        private readonly GridControl _xferGrid = new GridControl();
        private readonly GridView _xferView = new GridView();
        private DataRow _xferRow;
        private string _xferKey = Guid.NewGuid().ToString("N");
        private string _xferFingerprint = string.Empty;

        // Tab 5 - Trace
        private readonly TextEdit _traceLot = new TextEdit();
        private readonly ComboBoxEdit _traceSite = new ComboBoxEdit();
        private readonly ComboBoxEdit _traceDirection = new ComboBoxEdit();
        private readonly GridControl _traceGrid = new GridControl();
        private readonly GridView _traceView = new GridView();

        private XtraTabPage _tabPo, _tabReceipt, _tabStock, _tabTransfer, _tabIssue, _tabTrace, _tabFlow;

        // Tab "Sơ đồ quy trình"
        private readonly MesComponentFlowMapControl _flowMap = new MesComponentFlowMapControl();
        private readonly LabelControl _mapHint = new LabelControl();

        public FormKhoLinhKien()
            : this(new ComponentPurchaseOrderService(new ComponentPurchaseOrderRepository(),
                       new WorkflowTransitionService(new ComponentPoWorkflowRepository())),
                   new ComponentStockService(new ComponentStockRepository(), new ComponentSlotLookupRepository()),
                   new ProductionTraceService(new ProductionTraceRepository()))
        {
        }

        public FormKhoLinhKien(IComponentPurchaseOrderService po, IComponentStockService stock, IProductionTraceService trace)
        {
            AuthorizationGuard.Demand(FnStock, "VIEW");
            _po = po ?? throw new ArgumentNullException(nameof(po));
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));

            Text = "QUẢN LÝ LINH KIỆN MES - Interactive Component Console";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1380, 820);
            MinimumSize = new Size(1100, 650);
            BuildUi();
            Load += (s, e) => InitialLoad();
        }

        // ───────────────────────── Quyền ─────────────────────────

        private static bool Can(string function, string action)
        {
            var session = UserSession.Current;
            return session != null && session.HasPermission(function, action);
        }

        // ───────────────────────── Dựng giao diện ─────────────────────────

        private void BuildUi()
        {
            _tabs.Dock = DockStyle.Fill;

            _tabFlow = new XtraTabPage { Text = "QUẢN LÝ LINH KIỆN (Component Flow Console)" }; BuildFlowTab(_tabFlow); _tabs.TabPages.Add(_tabFlow);
            if (Can(FnPo, "VIEW")) { _tabPo = new XtraTabPage { Text = "0. Yêu cầu mua hàng (PO)" }; BuildPoTab(_tabPo); _tabs.TabPages.Add(_tabPo); }
            if (Can(FnReceipt, "VIEW")) { _tabReceipt = new XtraTabPage { Text = "1. Nhận linh kiện (QR)" }; BuildReceiptTab(_tabReceipt); _tabs.TabPages.Add(_tabReceipt); }
            _tabStock = new XtraTabPage { Text = "2. Tồn kho PART" }; BuildStockTab(_tabStock); _tabs.TabPages.Add(_tabStock);
            if (Can(FnTransfer, "VIEW")) { _tabTransfer = new XtraTabPage { Text = "3. Điều chuyển LOT" }; BuildTransferTab(_tabTransfer); _tabs.TabPages.Add(_tabTransfer); }
            if (Can(FnDemand, "VIEW") && Can(FnIssue, "VIEW")) { _tabIssue = new XtraTabPage { Text = "4. Xuất theo nhu cầu SX" }; BuildIssueTab(_tabIssue); _tabs.TabPages.Add(_tabIssue); }
            if (Can(FnTrace, "VIEW")) { _tabTrace = new XtraTabPage { Text = "5. Truy xuất LOT → thành phẩm" }; BuildTraceTab(_tabTrace); _tabs.TabPages.Add(_tabTrace); }

            _tabs.SelectedTabPage = _tabFlow;
            _tabs.SelectedPageChanged += (s, e) => { if (_tabs.SelectedTabPage == _tabFlow) UpdateFlowMap(); };
            UpdateFlowMap();

            _status.Dock = DockStyle.Bottom;
            _status.AutoSizeMode = LabelAutoSizeMode.None;
            _status.Height = 26;
            _status.Padding = new Padding(8, 4, 0, 0);

            Controls.Add(_tabs);
            Controls.Add(_status);
        }

        private static void ConfigureView(GridControl grid, GridView view)
        {
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.EnableAppearanceEvenRow = true;
            view.OptionsFind.AlwaysVisible = false;
            view.Appearance.HeaderPanel.Options.UseTextOptions = true;
            view.Appearance.HeaderPanel.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            view.ColumnPanelRowHeight = 36;
        }

        private static void Cfg(GridView v, string field, string caption = null, int width = 100,
            string format = null, int order = -1, bool visible = true)
        {
            GridColumn c = v.Columns[field];
            if (c == null) return;
            c.Visible = visible;
            if (!visible) return;
            if (caption != null) c.Caption = caption;
            c.Width = width;
            if (format != null)
            {
                c.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Custom;
                c.DisplayFormat.FormatString = format;
            }
            if (order >= 0) c.VisibleIndex = order;
        }

        private static void HideAll(GridView v)
        {
            foreach (GridColumn c in v.Columns) c.Visible = false;
        }

        private static LabelControl Cap(string text)
        {
            return new LabelControl { Text = text, Padding = new Padding(8, 6, 4, 0) };
        }

        private static SimpleButton MakeButton(string text, int width, EventHandler click, bool enabled = true)
        {
            var b = new SimpleButton { Text = text, Width = width, Height = 30, Enabled = enabled };
            b.Click += click;
            return b;
        }

        private static void SetupButton(SimpleButton b, string text, int width, EventHandler click, bool enabled)
        {
            b.Text = text; b.Width = width; b.Height = 30; b.Enabled = enabled;
            b.Click += click;
        }

        private static LabelControl InfoLabel(string text, int width = 900)
        {
            var label = new LabelControl { Text = text, AutoSizeMode = LabelAutoSizeMode.Vertical, Width = width };
            label.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            label.Padding = new Padding(0, 0, 0, 8);
            return label;
        }

        private static GroupControl Group(string caption, Control content)
        {
            var group = new GroupControl { Text = caption, Dock = DockStyle.Fill };
            content.Dock = DockStyle.Fill;
            group.Controls.Add(content);
            return group;
        }

        private static void ConfigureCombo(ComboBoxEdit combo, int width)
        {
            combo.Width = width;
            combo.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        }

        private static void ConfigureQty(SpinEdit spin)
        {
            // RepositoryItemSpinEdit không có DecimalPlaces; độ chính xác cấu hình qua numeric mask.
            spin.Properties.IsFloatValue = true;
            spin.Properties.Increment = 1;
            spin.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
            spin.Properties.Mask.EditMask = "n3";
            spin.Properties.Mask.UseMaskAsDisplayFormat = true;
            spin.Properties.MinValue = 0;
            spin.Properties.MaxValue = 1000000000;
        }

        // ── Tab "Sơ đồ quy trình" (MesComponentFlowMapControl) ──

        private void BuildFlowTab(XtraTabPage page)
        {
            _mapHint.Dock = DockStyle.Top;
            _mapHint.AutoSizeMode = LabelAutoSizeMode.None;
            _mapHint.Height = 32;
            _mapHint.Padding = new Padding(10, 0, 0, 0);
            _mapHint.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _mapHint.Appearance.BackColor = Color.FromArgb(232, 241, 252);
            _mapHint.Appearance.ForeColor = Color.FromArgb(20, 70, 130);
            _mapHint.Appearance.Options.UseBackColor = true;
            _mapHint.Appearance.Options.UseForeColor = true;

            _flowMap.Dock = DockStyle.Fill;
            _flowMap.NodeClicked += FlowNodeClicked;

            page.Controls.Add(_flowMap);
            page.Controls.Add(_mapHint);
        }

        private int SelectedPoStatus()
        {
            if (_tabPo == null || _poView.FocusedRowHandle < 0) return -1;
            DataRow row = _poView.GetFocusedDataRow();
            if (row == null || row["StatusCode"] == DBNull.Value) return -1;
            return Convert.ToInt32(row["StatusCode"]);
        }

        private void UpdateFlowMap()
        {
            int status = SelectedPoStatus();
            bool live = status >= 0 && status < ComponentPoStatus.Cancelled;
            _flowMap.ApplyPoStatus(status);

            // Ô thuộc quy trình PO: luôn bấm được nếu có tab PO (bấm khi chưa đủ điều kiện sẽ chuyển sang tab PO để chọn PO).
            bool hasPo = _tabPo != null;
            _flowMap.SetNode("upload", _btnUpload.Enabled && hasPo ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled, hasPo,
                hasPo ? "Upload file Excel PO (Bravo) để tạo PO nháp." : "Bạn không có quyền xem tab Yêu cầu mua hàng.");
            _flowMap.SetNode("validate", live && status >= ComponentPoStatus.Validated ? FlowNodeVisual.Done
                : _btnValidate.Enabled ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled, hasPo,
                "Kiểm tra PO ở trạng thái Nháp. Chọn PO ở tab 0 trước.");
            _flowMap.SetNode("release", live && status >= ComponentPoStatus.Released ? FlowNodeVisual.Done
                : _btnRelease.Enabled ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled, hasPo,
                "Phát hành PO đã kiểm tra và sinh LOT/QR. Chọn PO ở tab 0 trước.");

            bool hasReceipt = _tabReceipt != null;
            _flowMap.SetNode("receive", !hasReceipt ? FlowNodeVisual.Disabled
                : live && status >= ComponentPoStatus.Received ? FlowNodeVisual.Done
                : status == ComponentPoStatus.PartiallyReceived ? FlowNodeVisual.Current
                : FlowNodeVisual.Ready, hasReceipt,
                hasReceipt ? "Mở tab Nhận linh kiện (quét QR)." : "Bạn không có quyền xem tab Nhận linh kiện.");
            _flowMap.SetNode("stock", FlowNodeVisual.Ready, true, "Mở tab Tồn kho PART.");
            _flowMap.SetNode("transfer", _tabTransfer != null ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled, _tabTransfer != null,
                "Mở tab Điều chuyển LOT.");
            _flowMap.SetNode("issue", _tabIssue != null ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled, _tabIssue != null,
                "Mở tab Xuất theo nhu cầu sản xuất (PART → WIP).");
            _flowMap.SetNode("trace", _tabTrace != null ? FlowNodeVisual.Ready : FlowNodeVisual.Disabled, _tabTrace != null,
                "Mở tab Truy xuất LOT → thành phẩm.");

            _mapHint.Text = status < 0
                ? "Bấm một ô trên sơ đồ để mở bước tương ứng. Chọn PO ở tab 0 để sơ đồ hiển thị trạng thái PO."
                : "PO đang chọn: " + ComponentPoStatus.Text(status) + ".";
        }

        private void FlowNodeClicked(object sender, FlowNodeEventArgs e)
        {
            switch (e.Key)
            {
                case "upload":
                    if (_tabPo == null) return;
                    _tabs.SelectedTabPage = _tabPo;
                    if (_btnUpload.Enabled) _btnUpload.PerformClick();
                    break;
                case "validate": ClickPoAction(_btnValidate); break;
                case "release": ClickPoAction(_btnRelease); break;
                case "receive": GoTab(_tabReceipt); break;
                case "stock": GoTab(_tabStock); break;
                case "transfer": GoTab(_tabTransfer); break;
                case "issue": GoTab(_tabIssue); break;
                case "trace": GoTab(_tabTrace); break;
            }
        }

        private void ClickPoAction(SimpleButton button)
        {
            if (_tabPo == null) return;
            if (button.Enabled) { button.PerformClick(); return; }
            _tabs.SelectedTabPage = _tabPo;
            SetStatus("Chọn một PO ở trạng thái phù hợp trong danh sách để thực hiện bước này.");
        }

        private void GoTab(XtraTabPage page)
        {
            if (page != null) _tabs.SelectedTabPage = page;
        }

        // ── Tab 0: Yêu cầu mua hàng ──

        private void BuildPoTab(XtraTabPage page)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, Padding = new Padding(4, 6, 0, 0) };
            bar.Controls.Add(Cap("Trạng thái"));
            ConfigureCombo(_poFilter, 200);
            _poFilter.Properties.Items.Add("(Tất cả)");
            foreach (int code in new[] { 0, 10, 20, 30, 40, 50, 900 }) _poFilter.Properties.Items.Add(ComponentPoStatus.Text(code));
            _poFilter.SelectedIndex = 0;
            _poFilter.SelectedIndexChanged += (s, e) => ApplyPoFilter();
            bar.Controls.Add(_poFilter);
            bar.Controls.Add(MakeButton("Tải lại", 80, (s, e) => LoadPoOrders()));
            SetupButton(_btnUpload, "Upload Excel PO…", 130, UploadClick, Can(FnPo, "CREATE"));
            bar.Controls.Add(_btnUpload);
            SetupButton(_btnTemplate, "Tải template PO…", 130, TemplateClick, Can(FnPo, "CREATE"));
            bar.Controls.Add(_btnTemplate);
            ConfigureAction(_btnValidate, "Kiểm tra PO", 110, ComponentPoStatus.Validated);
            ConfigureAction(_btnRelease, "Phát hành + sinh LOT/QR", 170, ComponentPoStatus.Released);
            ConfigureAction(_btnClose, "Đóng PO", 90, ComponentPoStatus.Closed);
            ConfigureAction(_btnCancel, "Hủy PO", 90, ComponentPoStatus.Cancelled);
            bar.Controls.AddRange(new Control[] { _btnValidate, _btnRelease, _btnClose, _btnCancel });

            ConfigureView(_poGrid, _poView);
            _poView.OptionsView.ShowAutoFilterRow = true;
            _poView.FocusedRowChanged += (s, e) => OnPoSelected();
            _poView.FocusedRowChanged += (s, e) => UpdateFlowMap();
            _poView.RowStyle += PoRowStyle;

            ConfigureView(_poLinesGrid, _poLinesView);
            ConfigureView(_poEventsGrid, _poEventsView);

            var detail = new SplitContainerControl { Dock = DockStyle.Fill, Horizontal = true, SplitterPosition = 640 };
            detail.Panel1.Controls.Add(Group("Dòng hàng của PO", _poLinesGrid));
            detail.Panel2.Controls.Add(Group("Lịch sử trạng thái (workflow)", _poEventsGrid));

            var split = new SplitContainerControl { Dock = DockStyle.Fill, Horizontal = false, SplitterPosition = 300 };
            split.Panel1.Controls.Add(_poGrid);
            split.Panel2.Controls.Add(detail);

            page.Controls.Add(split);
            page.Controls.Add(bar);
        }

        private void ConfigureAction(SimpleButton button, string text, int width, int toStatus)
        {
            SetupButton(button, text, width, TransitionClick, false);
            button.Tag = toStatus;
        }

        private void PoRowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
        {
            if (e.RowHandle < 0) return;
            object code = _poView.GetRowCellValue(e.RowHandle, "StatusCode");
            if (code == null || code == DBNull.Value) return;
            int status = Convert.ToInt32(code);
            if (status == ComponentPoStatus.Cancelled) e.Appearance.ForeColor = Color.Gray;
            else if (status == ComponentPoStatus.PartiallyReceived) e.Appearance.ForeColor = Color.DarkOrange;
            else if (status == ComponentPoStatus.Received || status == ComponentPoStatus.Closed) e.Appearance.ForeColor = Color.DarkGreen;
        }

        // ── Tab 1: Nhận linh kiện ──

        private void BuildReceiptTab(XtraTabPage page)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16), WrapContents = false };
            panel.Controls.Add(InfoLabel(
                "Nhận linh kiện từ nhà cung cấp: quét tem QR (FVN1|LOT) của PO đã phát hành, chọn slot PART đích. " +
                "Hệ thống tự cập nhật trạng thái PO sang Đang nhận / Đã nhận đủ và ghi tồn vào sổ MES."));
            panel.Controls.Add(InfoLabel(Can(FnReceipt, "CONFIRM")
                ? "Quyền xác nhận nhận hàng: có."
                : "Quyền xác nhận nhận hàng: KHÔNG (chỉ xem danh sách)."));
            panel.Controls.Add(MakeButton("Mở Kiểm kê hàng về (QR)…", 240, (s, e) =>
                OpenGuarded(() => FormWindowManager.ShowSingle(this, () => new FormKiemKeHangVeList()), "Kiểm kê hàng về")));
            page.Controls.Add(panel);
        }

        // ── Tab 2: Tồn kho ──

        private void BuildStockTab(XtraTabPage page)
        {
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(4, 2, 0, 0) };
            filters.Controls.Add(Cap("Site"));
            ConfigureCombo(_site, 90); filters.Controls.Add(_site);
            filters.Controls.Add(Cap("PartNo"));
            _partNo.Width = 150; filters.Controls.Add(_partNo);
            filters.Controls.Add(Cap("LOT"));
            _lotNo.Width = 200; filters.Controls.Add(_lotNo);
            filters.Controls.Add(Cap("Slot / giá đỡ / vị trí"));
            _slotText.Width = 160; filters.Controls.Add(_slotText);
            _includeZero.Text = "Gồm tồn = 0"; _includeZero.Width = 110; _includeZero.Margin = new Padding(8, 4, 0, 0);
            filters.Controls.Add(_includeZero);
            filters.Controls.Add(MakeButton("Tìm", 80, (s, e) => LoadStock()));
            foreach (var box in new[] { _partNo, _lotNo, _slotText })
                box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadStock(); } };
            var filterGroup = new GroupControl { Text = "Tra cứu tồn kho theo PartNo / LOT / Site / Slot", Dock = DockStyle.Top, Height = 74 };
            filters.Dock = DockStyle.Fill;
            filterGroup.Controls.Add(filters);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, WrapContents = false, Padding = new Padding(4, 6, 0, 0) };
            SetupButton(_btnToTransfer, "Điều chuyển LOT đã chọn →", 200, (s, e) => StartTransferFromSelection(), false);
            SetupButton(_btnToTrace, "Truy xuất LOT đã chọn →", 200, (s, e) => StartTraceFromSelection(), false);
            actions.Controls.Add(_btnToTransfer);
            actions.Controls.Add(_btnToTrace);

            ConfigureView(_stockGrid, _stockView);
            _stockView.OptionsView.ShowAutoFilterRow = true;
            _stockView.OptionsView.ShowFooter = true;
            _stockView.FocusedRowChanged += (s, e) => UpdateStockButtons();
            _stockView.RowCellStyle += StockRowCellStyle;
            _stockView.DoubleClick += (s, e) => { if (_btnToTransfer.Enabled) StartTransferFromSelection(); };

            page.Controls.Add(_stockGrid);
            page.Controls.Add(actions);
            page.Controls.Add(filterGroup);
        }

        private void StockRowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (e.RowHandle < 0) return;
            object avail = _stockView.GetRowCellValue(e.RowHandle, "AvailableQty");
            object state = _stockView.GetRowCellValue(e.RowHandle, "StockState");
            if (state != null && state != DBNull.Value &&
                !string.Equals(Convert.ToString(state), "AVAILABLE", StringComparison.OrdinalIgnoreCase))
                e.Appearance.ForeColor = Color.DarkOrange;
            else if (avail != null && avail != DBNull.Value && Convert.ToDecimal(avail) <= 0)
                e.Appearance.ForeColor = Color.Gray;
        }

        // ── Tab 3: Điều chuyển LOT ──

        private void BuildTransferTab(XtraTabPage page)
        {
            _xferSource.Dock = DockStyle.Top;
            _xferSource.AutoSizeMode = LabelAutoSizeMode.None;
            _xferSource.Height = 54;
            _xferSource.Padding = new Padding(8);
            _xferSource.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            _xferSource.Text = "Chưa chọn LOT nguồn. Vào tab \"2. Tồn kho PART\", chọn một dòng tồn rồi bấm \"Điều chuyển LOT đã chọn\".";

            var form = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false, Padding = new Padding(4, 8, 0, 0) };
            form.Controls.Add(Cap("Slot đích"));
            _toSlot.Width = 340;
            _toSlot.Properties.NullText = "Chọn slot PART đích";
            _toSlot.Properties.Columns.Add(new LookUpColumnInfo("SlotDisplay", "Slot"));
            _toSlot.Properties.DisplayMember = "SlotDisplay";
            _toSlot.Properties.ValueMember = "SlotId";
            _toSlot.Properties.ShowHeader = false;
            _toSlot.Properties.SearchMode = SearchMode.AutoComplete;
            form.Controls.Add(_toSlot);
            form.Controls.Add(Cap("Số lượng"));
            _xferQty.Width = 110; ConfigureQty(_xferQty); form.Controls.Add(_xferQty);
            form.Controls.Add(Cap("Lý do"));
            _xferReason.Width = 260; _xferReason.Properties.MaxLength = 300; form.Controls.Add(_xferReason);
            form.Controls.Add(Cap("Số phiếu"));
            _xferNo.Width = 190; _xferNo.Properties.ReadOnly = true; form.Controls.Add(_xferNo);
            SetupButton(_btnTransfer, "ĐIỀU CHUYỂN", 140, TransferClick, false);
            _btnTransfer.Height = 32;
            form.Controls.Add(_btnTransfer);

            ConfigureView(_xferGrid, _xferView);

            page.Controls.Add(Group("Phiếu điều chuyển gần đây của Site", _xferGrid));
            page.Controls.Add(form);
            page.Controls.Add(_xferSource);
        }

        // ── Tab 4: Xuất theo nhu cầu ──

        private void BuildIssueTab(XtraTabPage page)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16), WrapContents = false };
            panel.Controls.Add(InfoLabel(
                "Xuất linh kiện theo nhu cầu sản xuất: chọn kế hoạch theo ca, tạo yêu cầu linh kiện theo BOM, " +
                "rồi xuất PART → WIP từ LOT/slot nguồn. Màn hình này cần đồng thời quyền xem nhu cầu linh kiện " +
                "(MES.COMPONENT_DEMAND) và quyền xuất PART → WIP (MES.COMPONENT_ISSUE)."));
            panel.Controls.Add(InfoLabel("Tạo yêu cầu: " + (Can(FnDemand, "CREATE") ? "có" : "không") +
                "   |   Xác nhận xuất: " + (Can(FnIssue, "CONFIRM") ? "có" : "không")));
            panel.Controls.Add(MakeButton("Mở Yêu cầu / Xuất linh kiện PART → WIP…", 320, (s, e) =>
                OpenGuarded(() => FormWindowManager.ShowSingle(this, () => new FormYeuCauVaXuatLinhKien()), "Yêu cầu / Xuất linh kiện")));
            page.Controls.Add(panel);
        }

        // ── Tab 5: Truy xuất ──

        private void BuildTraceTab(XtraTabPage page)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, Padding = new Padding(4, 6, 0, 0) };
            bar.Controls.Add(Cap("LOT"));
            _traceLot.Width = 260; bar.Controls.Add(_traceLot);
            bar.Controls.Add(Cap("Site"));
            ConfigureCombo(_traceSite, 90); bar.Controls.Add(_traceSite);
            bar.Controls.Add(Cap("Chiều"));
            ConfigureCombo(_traceDirection, 260);
            _traceDirection.Properties.Items.Add("Linh kiện → thành phẩm (DOWNSTREAM)");
            _traceDirection.Properties.Items.Add("Thành phẩm → linh kiện (UPSTREAM)");
            _traceDirection.SelectedIndex = 0;
            bar.Controls.Add(_traceDirection);
            bar.Controls.Add(MakeButton("Truy xuất", 100, (s, e) => RunTrace()));
            _traceLot.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunTrace(); } };

            ConfigureView(_traceGrid, _traceView);
            page.Controls.Add(_traceGrid);
            page.Controls.Add(bar);
        }

        // ───────────────────────── Nạp dữ liệu ─────────────────────────

        private void InitialLoad()
        {
            try
            {
                DataTable sites = _stock.ListSites();
                FillSiteCombo(_site, sites, true);
                FillSiteCombo(_traceSite, sites, false);
                if (_tabPo != null) LoadPoOrders();
                LoadStock();
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private static void FillSiteCombo(ComboBoxEdit combo, DataTable sites, bool includeAll)
        {
            combo.Properties.Items.Clear();
            if (includeAll) combo.Properties.Items.Add(string.Empty);
            foreach (DataRow row in sites.Rows) combo.Properties.Items.Add(Convert.ToString(row["SiteCode"]));
            if (combo.Properties.Items.Count > 0)
                combo.SelectedIndex = includeAll && combo.Properties.Items.Count > 1 ? 1 : 0;
        }

        private static string ComboText(ComboBoxEdit combo)
        {
            string text = Convert.ToString(combo.EditValue);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private void SetStatus(string text) { _status.Text = text; }

        private void OpenGuarded(Action open, string title)
        {
            try { open(); }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── PO ──

        private void LoadPoOrders()
        {
            try
            {
                _poTable = _po.ListOrders();
                if (!_poTable.Columns.Contains("StatusText"))
                    _poTable.Columns.Add("StatusText", typeof(string));
                foreach (DataRow row in _poTable.Rows)
                    row["StatusText"] = ComponentPoStatus.Text(Convert.ToInt32(row["StatusCode"]));
                ApplyPoFilter();
                SetStatus("Đã tải " + _poTable.Rows.Count + " yêu cầu mua hàng.");
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void ApplyPoFilter()
        {
            if (_poTable == null) return;
            long keep = SelectedPoId();
            var view = new DataView(_poTable);
            if (_poFilter.SelectedIndex > 0)
                view.RowFilter = "StatusText = '" + Convert.ToString(_poFilter.EditValue).Replace("'", "''") + "'";
            _poDetailFor = -1;
            _poGrid.DataSource = view;
            FormatPoColumns();
            if (keep > 0) FocusPo(keep);
            OnPoSelected();
        }

        private void FormatPoColumns()
        {
            HideAll(_poView);
            Cfg(_poView, "StatusText", "Trạng thái", 170, null, 0);
            Cfg(_poView, "SiteCode", "Site", 70, null, 1);
            Cfg(_poView, "PurchaseOrderNo", "Số PO", 120, null, 2);
            Cfg(_poView, "OrderDate", "Ngày đặt", 90, "dd/MM/yyyy", 3);
            Cfg(_poView, "SupplierCode", "Mã NCC", 90, null, 4);
            Cfg(_poView, "SupplierName", "Nhà cung cấp", 260, null, 5);
            Cfg(_poView, "LineCount", "Số dòng", 70, "#,##0", 6);
            Cfg(_poView, "OrderQty", "SL đặt", 90, "#,##0.###", 7);
            Cfg(_poView, "ReceivedQty", "SL đã nhận", 90, "#,##0.###", 8);
            Cfg(_poView, "CreatedBy", "Người tạo", 120, null, 9);
            Cfg(_poView, "CreatedAtUtc", "Tạo lúc (UTC)", 130, "dd/MM/yyyy HH:mm", 10);
            Cfg(_poView, "LastUpdateUtc", "Cập nhật (UTC)", 130, "dd/MM/yyyy HH:mm", 11);
            Cfg(_poView, "Remarks", "Ghi chú", 260, null, 12);
        }

        private long SelectedPoId()
        {
            DataRow row = _poView.FocusedRowHandle < 0 ? null : _poView.GetFocusedDataRow();
            return row == null || row["PurchaseOrderId"] == DBNull.Value ? 0 : Convert.ToInt64(row["PurchaseOrderId"]);
        }

        private void FocusPo(long id)
        {
            for (int i = 0; i < _poView.DataRowCount; i++)
            {
                object v = _poView.GetRowCellValue(i, "PurchaseOrderId");
                if (v != null && v != DBNull.Value && Convert.ToInt64(v) == id)
                {
                    _poView.FocusedRowHandle = i;
                    return;
                }
            }
        }

        private void OnPoSelected()
        {
            DataRow row = _poView.FocusedRowHandle < 0 ? null : _poView.GetFocusedDataRow();
            int status = row == null ? -1 : Convert.ToInt32(row["StatusCode"]);
            IReadOnlyList<WorkflowTransition> actions = new List<WorkflowTransition>();
            try
            {
                if (row != null) actions = _po.GetAvailableActions(status);
            }
            catch (Exception ex) { SetStatus(ex.Message); }

            foreach (var button in new[] { _btnValidate, _btnRelease, _btnClose, _btnCancel })
            {
                int target = (int)button.Tag;
                string need = ComponentPoStatus.RequiredAction(target);
                button.Enabled = row != null && actions.Any(a => a.ToStatus == target) && Can(FnPo, need);
            }
            // Đóng PO khi chưa nhận đủ là "đóng sớm" → đổi nhãn cho rõ.
            _btnClose.Text = status == ComponentPoStatus.PartiallyReceived ? "Đóng sớm PO" : "Đóng PO";

            if (row == null)
            {
                _poDetailFor = -1;
                _poLinesGrid.DataSource = null;
                _poEventsGrid.DataSource = null;
                return;
            }
            long id = Convert.ToInt64(row["PurchaseOrderId"]);
            // Chỉ nạp lại chi tiết khi đổi PO; LoadPoOrders/ApplyPoFilter đặt _poDetailFor = -1 nên sau mỗi
            // lần chuyển trạng thái hoặc tải lại, dòng hàng và lịch sử workflow luôn được nạp mới.
            if (id == _poDetailFor) return;
            _poDetailFor = id;
            try
            {
                _poLinesGrid.DataSource = _po.ListLines(id);
                HideAll(_poLinesView);
                Cfg(_poLinesView, "LineNo", "Dòng", 60, null, 0);
                Cfg(_poLinesView, "PartNo", "PartNo", 150, null, 1);
                Cfg(_poLinesView, "PartDescription", "Mô tả", 220, null, 2);
                Cfg(_poLinesView, "UnitCode", "ĐVT", 55, null, 3);
                Cfg(_poLinesView, "NeedByDate", "Cần trước ngày", 100, "dd/MM/yyyy", 4);
                Cfg(_poLinesView, "OrderQty", "SL đặt", 90, "#,##0.###", 5);
                Cfg(_poLinesView, "ReceivedQty", "SL đã nhận", 90, "#,##0.###", 6);
                Cfg(_poLinesView, "RemainingQty", "SL còn lại", 90, "#,##0.###", 7);
                Cfg(_poLinesView, "LotCount", "Số LOT/tem", 80, "#,##0", 8);

                DataTable events = _po.ListEvents(id);
                events.Columns.Add("FromText", typeof(string));
                events.Columns.Add("ToText", typeof(string));
                foreach (DataRow e in events.Rows)
                {
                    e["FromText"] = e["FromStatus"] == DBNull.Value ? string.Empty : ComponentPoStatus.Text(Convert.ToInt32(e["FromStatus"]));
                    e["ToText"] = ComponentPoStatus.Text(Convert.ToInt32(e["ToStatus"]));
                }
                _poEventsGrid.DataSource = events;
                HideAll(_poEventsView);
                Cfg(_poEventsView, "OccurredAtUtc", "Lúc (UTC)", 130, "dd/MM/yyyy HH:mm", 0);
                Cfg(_poEventsView, "ActionName", "Hành động", 120, null, 1);
                Cfg(_poEventsView, "FromText", "Từ", 150, null, 2);
                Cfg(_poEventsView, "ToText", "Đến", 150, null, 3);
                Cfg(_poEventsView, "ActorCode", "Người thao tác", 120, null, 4);
                Cfg(_poEventsView, "Reason", "Lý do", 240, null, 5);
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void UploadClick(object sender, EventArgs e)
        {
            try
            {
                // Shared with the "Phiếu đặt hàng linh kiện" screen so both show the same confirmation.
                string status = ComponentPoUploadFlow.Run(this, _po);
                if (status == null) return;   // cancelled
                SetStatus(status);
                LoadPoOrders();
            }
            catch (Exception ex)
            {
                SetStatus("Upload thất bại: " + ex.Message);
                XtraMessageBox.Show(this, ex.Message, ComponentPoUploadFlow.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TemplateClick(object sender, EventArgs e)
        {
            try
            {
                string status = PCTP.Shared.Helpers.ExcelTemplateFlow.Run(this, ComponentPoTemplateDefinition.Create());
                if (status != null) SetStatus(status);
            }
            catch (Exception ex)
            {
                SetStatus("Không tạo được template: " + ex.Message);
                XtraMessageBox.Show(this, ex.Message, "Tải template PO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TransitionClick(object sender, EventArgs e)
        {
            var button = (SimpleButton)sender;
            int toStatus = (int)button.Tag;
            DataRow row = _poView.FocusedRowHandle < 0 ? null : _poView.GetFocusedDataRow();
            if (row == null) return;
            long id = Convert.ToInt64(row["PurchaseOrderId"]);
            int from = Convert.ToInt32(row["StatusCode"]);
            string poNo = Convert.ToString(row["PurchaseOrderNo"]);
            try
            {
                string reason = null;
                bool needReason = toStatus == ComponentPoStatus.Cancelled ||
                    (toStatus == ComponentPoStatus.Closed && from == ComponentPoStatus.PartiallyReceived);
                if (needReason)
                {
                    reason = XtraInputBox.Show("Nhập lý do (bắt buộc):", button.Text + " " + poNo, string.Empty);
                    if (string.IsNullOrWhiteSpace(reason)) return;
                    reason = reason.Trim();
                }
                else
                {
                    string ask = toStatus == ComponentPoStatus.Released
                        ? "Phát hành PO " + poNo + " và sinh LOT/QR cho từng dòng hàng?"
                        : button.Text + " cho PO " + poNo + "?";
                    if (XtraMessageBox.Show(this, ask, "Workflow PO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }

                DataTable result = _po.Transition(id, from, toStatus, reason);
                string text = "PO " + poNo + " → " + ComponentPoStatus.Text(toStatus) + ".";
                if (toStatus == ComponentPoStatus.Released && result.Rows.Count > 0 && result.Columns.Contains("LotsIssued"))
                    text += " Đã có " + result.Rows[0]["LotsIssued"] + " LOT/tem QR.";
                LoadPoOrders();
                FocusPo(id);
                SetStatus(text);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                XtraMessageBox.Show(this, ex.Message, "Workflow PO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadPoOrders();
                FocusPo(id);
            }
        }

        // ── Tồn kho ──

        private void LoadStock()
        {
            try
            {
                var filter = new ComponentStockFilter
                {
                    SiteCode = ComboText(_site),
                    PartNo = _partNo.Text,
                    LotNo = _lotNo.Text,
                    SlotText = _slotText.Text,
                    IncludeZero = _includeZero.Checked
                };
                DataTable table = _stock.Search(filter);
                _stockGrid.DataSource = table;
                FormatStockColumns();
                decimal onHand = table.AsEnumerable().Sum(r => Convert.ToDecimal(r["OnHandQty"]));
                SetStatus(table.Rows.Count + " dòng tồn, tổng tồn " + onHand.ToString("N3", CultureInfo.CurrentCulture) +
                    (table.Rows.Count >= 5000 ? " (đã giới hạn 5000 dòng, hãy lọc thêm)" : string.Empty));
                UpdateStockButtons();
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void FormatStockColumns()
        {
            HideAll(_stockView);
            Cfg(_stockView, "SiteCode", "Site", 60, null, 0);
            Cfg(_stockView, "PartNo", "PartNo", 150, null, 1);
            Cfg(_stockView, "LotNo", "LOT", 220, null, 2);
            Cfg(_stockView, "LotStatus", "Trạng thái LOT", 100, null, 3);
            Cfg(_stockView, "SupplierCode", "Mã NCC", 80, null, 4);
            Cfg(_stockView, "PurchaseOrderNo", "Số PO", 100, null, 5);
            Cfg(_stockView, "WarehouseName", "Kho", 120, null, 6);
            Cfg(_stockView, "RackName", "Giá đỡ", 90, null, 7);
            Cfg(_stockView, "SlotNumber", "Slot", 60, null, 8);
            Cfg(_stockView, "LocationNo", "Vị trí MES", 110, null, 9);
            Cfg(_stockView, "StockState", "Trạng thái tồn", 100, null, 10);
            Cfg(_stockView, "OnHandQty", "Tồn", 90, "#,##0.###", 11);
            Cfg(_stockView, "ReservedQty", "Giữ chỗ", 90, "#,##0.###", 12);
            Cfg(_stockView, "AvailableQty", "Khả dụng", 90, "#,##0.###", 13);
            Cfg(_stockView, "UpdatedAtUtc", "Cập nhật (UTC)", 130, "dd/MM/yyyy HH:mm", 14);

            foreach (string name in new[] { "OnHandQty", "ReservedQty", "AvailableQty" })
            {
                GridColumn col = _stockView.Columns[name];
                if (col == null) continue;
                col.Summary.Clear();
                col.Summary.Add(DevExpress.Data.SummaryItemType.Sum, name, "{0:#,##0.###}");
            }
        }

        private void UpdateStockButtons()
        {
            DataRow row = _stockView.FocusedRowHandle < 0 ? null : _stockView.GetFocusedDataRow();
            bool available = row != null &&
                string.Equals(Convert.ToString(row["StockState"]), "AVAILABLE", StringComparison.OrdinalIgnoreCase) &&
                Convert.ToDecimal(row["AvailableQty"]) > 0;
            _btnToTransfer.Enabled = available && _tabTransfer != null && Can(FnTransfer, "CONFIRM");
            _btnToTrace.Enabled = row != null && _tabTrace != null;
        }

        // ── Điều chuyển ──

        private void StartTransferFromSelection()
        {
            DataRow row = _stockView.FocusedRowHandle < 0 ? null : _stockView.GetFocusedDataRow();
            if (row == null || _tabTransfer == null) return;
            try
            {
                string site = Convert.ToString(row["SiteCode"]);
                decimal available = Convert.ToDecimal(row["AvailableQty"]);

                DataTable slots = _stock.ListSlots(site);
                int fromSlot = Convert.ToInt32(row["SlotId"]);
                var view = new DataView(slots) { RowFilter = "SlotId <> " + fromSlot.ToString(CultureInfo.InvariantCulture) };
                _toSlot.Properties.DataSource = view;
                _toSlot.EditValue = null;

                _xferRow = row;
                _xferSource.Text = "Nguồn: Site " + site + "  |  PartNo " + row["PartNo"] + "  |  LOT " + row["LotNo"] + "\r\n" +
                    "Vị trí: " + row["WarehouseName"] + " / " + row["RackName"] + " / Slot " + row["SlotNumber"] +
                    "  |  Khả dụng: " + available.ToString("N3", CultureInfo.CurrentCulture);
                // Tồn khả dụng có thể nhỏ hơn 0.001 (decimal 19,6) nên MinValue không được lớn hơn MaxValue.
                _xferQty.Properties.MinValue = Math.Min(0.001M, available);
                _xferQty.Properties.MaxValue = available;
                _xferQty.Value = available;
                _xferReason.Text = string.Empty;
                _xferKey = Guid.NewGuid().ToString("N");
                _xferFingerprint = string.Empty;
                _xferNo.Text = "CLT-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) +
                    "-" + _xferKey.Substring(0, 4).ToUpperInvariant();
                _btnTransfer.Enabled = Can(FnTransfer, "CONFIRM");
                LoadTransferHistory(site);
                _tabs.SelectedTabPage = _tabTransfer;
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void LoadTransferHistory(string site)
        {
            _xferGrid.DataSource = _stock.ListTransfers(site);
            HideAll(_xferView);
            Cfg(_xferView, "TransferredAtUtc", "Lúc (UTC)", 130, "dd/MM/yyyy HH:mm", 0);
            Cfg(_xferView, "TransferNo", "Số phiếu", 170, null, 1);
            Cfg(_xferView, "SiteCode", "Site", 60, null, 2);
            Cfg(_xferView, "PartNo", "PartNo", 140, null, 3);
            Cfg(_xferView, "LotNo", "LOT", 200, null, 4);
            Cfg(_xferView, "Quantity", "SL", 90, "#,##0.###", 5);
            Cfg(_xferView, "FromSlot", "Từ slot", 200, null, 6);
            Cfg(_xferView, "ToSlot", "Đến slot", 200, null, 7);
            Cfg(_xferView, "Reason", "Lý do", 220, null, 8);
            Cfg(_xferView, "TransferredBy", "Người thực hiện", 120, null, 9);
        }

        private void TransferClick(object sender, EventArgs e)
        {
            if (_xferRow == null) return;
            try
            {
                if (_toSlot.EditValue == null || _toSlot.EditValue == DBNull.Value)
                    throw new InvalidOperationException("Chọn slot đích.");
                string site = Convert.ToString(_xferRow["SiteCode"]);
                string lot = Convert.ToString(_xferRow["LotNo"]);
                long lotId = Convert.ToInt64(_xferRow["MaterialLotId"]);
                int fromSlot = Convert.ToInt32(_xferRow["SlotId"]);
                int toSlot = Convert.ToInt32(_toSlot.EditValue);
                decimal qty = _xferQty.Value;

                string ask = "Điều chuyển " + qty.ToString("N3", CultureInfo.CurrentCulture) + " của LOT " + lot +
                    "\r\nđến: " + _toSlot.Text + "?";
                if (XtraMessageBox.Show(this, ask, "Điều chuyển LOT", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                // Bấm lại cùng một phiếu (mất kết nối...) giữ khóa chống trùng; đổi LOT/slot/số lượng thì cấp khóa mới
                // để không trả nhầm chứng từ cũ.
                string fingerprint = string.Join("|", lotId, fromSlot, toSlot,
                    qty.ToString(CultureInfo.InvariantCulture));
                if (fingerprint != _xferFingerprint)
                {
                    if (_xferFingerprint.Length > 0) _xferKey = Guid.NewGuid().ToString("N");
                    _xferFingerprint = fingerprint;
                }

                DataTable result = _stock.Transfer(site, _xferNo.Text, lotId, fromSlot, toSlot, qty, _xferReason.Text, _xferKey);

                SetStatus("Đã điều chuyển phiếu " + _xferNo.Text + " (giao dịch MES #" + result.Rows[0]["InventoryTransactionId"] + ").");
                _xferRow = null;
                _btnTransfer.Enabled = false;
                _xferSource.Text = "Đã điều chuyển xong. Chọn dòng tồn khác ở tab \"2. Tồn kho PART\" để tiếp tục.";
                _xferKey = Guid.NewGuid().ToString("N");
                _xferFingerprint = string.Empty;
                LoadTransferHistory(site);
                LoadStock();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                XtraMessageBox.Show(this, ex.Message, "Điều chuyển LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Truy xuất ──

        private void StartTraceFromSelection()
        {
            DataRow row = _stockView.FocusedRowHandle < 0 ? null : _stockView.GetFocusedDataRow();
            if (row == null || _tabTrace == null) return;
            _traceLot.Text = Convert.ToString(row["LotNo"]);
            string site = Convert.ToString(row["SiteCode"]);
            int index = _traceSite.Properties.Items.IndexOf(site);
            if (index >= 0) _traceSite.SelectedIndex = index;
            _traceDirection.SelectedIndex = 0;
            _tabs.SelectedTabPage = _tabTrace;
            RunTrace();
        }

        private void RunTrace()
        {
            try
            {
                string direction = _traceDirection.SelectedIndex == 1 ? "UPSTREAM" : "DOWNSTREAM";
                DataTable table = _trace.Trace(_traceLot.Text, direction, ComboText(_traceSite));
                _traceGrid.DataSource = table;
                _traceView.BestFitColumns();
                SetStatus("Truy xuất " + (direction == "DOWNSTREAM" ? "linh kiện → thành phẩm" : "thành phẩm → linh kiện") +
                    ": " + table.Rows.Count + " dòng.");
            }
            catch (Exception ex)
            {
                _traceGrid.DataSource = null;
                SetStatus(ex.Message);
            }
        }
    }
}
