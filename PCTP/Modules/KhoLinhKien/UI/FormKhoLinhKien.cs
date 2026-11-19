using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PCTP.Infrastructure.Security;
using PCTP.Modules.MesPlanning;
using PCTP.Modules.MesPlanning.UI;
using PCTP.Modules.NhapKho.ComponentReceiving;
using PCTP.Modules.NhapKho.UI;
using PCTP.Security;
using PCTP.Shared.Helpers;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.KhoLinhKien.UI
{
    /// <summary>
    /// Màn hình kho linh kiện PART theo luồng:
    ///   0 Yêu cầu mua hàng (upload PO + workflow) → 1 Nhận linh kiện QR → 2 Tồn kho (PartNo/LOT/Site/Slot)
    ///   → 3 Điều chuyển LOT → 4 Xuất theo nhu cầu sản xuất → 5 Truy xuất LOT tới thành phẩm.
    /// Vào màn hình cần WAREHOUSE.COMPONENT_STOCK/VIEW; mỗi tab chỉ hiện khi tài khoản có đúng quyền của
    /// bước đó, nút thao tác kiểm tra thêm action (CREATE/CONFIRM/APPROVE/CANCEL) và dịch vụ kiểm tra lại
    /// quyền + phạm vi Site ở tầng service.
    /// </summary>
    public sealed class FormKhoLinhKien : Form
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

        private readonly TabControl _tabs = new TabControl();
        private readonly Label _status = new Label();

        // Tab 0 - PO
        private readonly ComboBox _poFilter = new ComboBox();
        private readonly DataGridView _poGrid = NewGrid();
        private readonly DataGridView _poLines = NewGrid();
        private readonly DataGridView _poEvents = NewGrid();
        private readonly Button _btnUpload = new Button();
        private readonly Button _btnValidate = new Button();
        private readonly Button _btnRelease = new Button();
        private readonly Button _btnClose = new Button();
        private readonly Button _btnCancel = new Button();
        private DataTable _poTable;

        // Tab 2 - Stock
        private readonly ComboBox _site = new ComboBox();
        private readonly TextBox _partNo = new TextBox();
        private readonly TextBox _lotNo = new TextBox();
        private readonly TextBox _slotText = new TextBox();
        private readonly CheckBox _includeZero = new CheckBox();
        private readonly DataGridView _stockGrid = NewGrid();
        private readonly Button _btnToTransfer = new Button();
        private readonly Button _btnToTrace = new Button();

        // Tab 3 - Transfer
        private readonly Label _xferSource = new Label();
        private readonly ComboBox _toSlot = new ComboBox();
        private readonly NumericUpDown _xferQty = new NumericUpDown();
        private readonly TextBox _xferReason = new TextBox();
        private readonly TextBox _xferNo = new TextBox();
        private readonly Button _btnTransfer = new Button();
        private readonly DataGridView _xferHistory = NewGrid();
        private DataRow _xferRow;
        private string _xferKey = Guid.NewGuid().ToString("N");

        // Tab 5 - Trace
        private readonly TextBox _traceLot = new TextBox();
        private readonly ComboBox _traceSite = new ComboBox();
        private readonly ComboBox _traceDirection = new ComboBox();
        private readonly DataGridView _traceGrid = NewGrid();

        private TabPage _tabPo, _tabReceipt, _tabStock, _tabTransfer, _tabIssue, _tabTrace;

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

            Text = "Kho linh kiện PART";
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
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            _tabs.Dock = DockStyle.Fill;

            if (Can(FnPo, "VIEW")) { _tabPo = new TabPage("0. Yêu cầu mua hàng (PO)"); BuildPoTab(_tabPo); _tabs.TabPages.Add(_tabPo); }
            if (Can(FnReceipt, "VIEW")) { _tabReceipt = new TabPage("1. Nhận linh kiện (QR)"); BuildReceiptTab(_tabReceipt); _tabs.TabPages.Add(_tabReceipt); }
            _tabStock = new TabPage("2. Tồn kho PART"); BuildStockTab(_tabStock); _tabs.TabPages.Add(_tabStock);
            if (Can(FnTransfer, "VIEW")) { _tabTransfer = new TabPage("3. Điều chuyển LOT"); BuildTransferTab(_tabTransfer); _tabs.TabPages.Add(_tabTransfer); }
            if (Can(FnDemand, "VIEW") && Can(FnIssue, "VIEW")) { _tabIssue = new TabPage("4. Xuất theo nhu cầu SX"); BuildIssueTab(_tabIssue); _tabs.TabPages.Add(_tabIssue); }
            if (Can(FnTrace, "VIEW")) { _tabTrace = new TabPage("5. Truy xuất LOT → thành phẩm"); BuildTraceTab(_tabTrace); _tabs.TabPages.Add(_tabTrace); }

            _tabs.SelectedTab = _tabStock;
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(_tabs, 0, 0);
            root.Controls.Add(_status, 0, 1);
            Controls.Add(root);
        }

        private static DataGridView NewGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                RowHeadersVisible = false
            };
        }

        private static Label Caption(string text, int topPad = 8)
        {
            return new Label { Text = text, AutoSize = true, Padding = new Padding(8, topPad, 0, 0) };
        }

        private static Button MakeButton(string text, int width, EventHandler click, bool enabled = true)
        {
            var b = new Button { Text = text, Width = width, Height = 30, Enabled = enabled };
            b.Click += click;
            return b;
        }

        /// <summary>Đặt tiêu đề cột (cặp tên cột, tiêu đề); tiêu đề rỗng = ẩn cột; định dạng số cho cột "N3".</summary>
        private static void ApplyHeaders(DataGridView grid, params string[] pairs)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                var col = grid.Columns[pairs[i]];
                if (col == null) continue;
                if (string.IsNullOrEmpty(pairs[i + 1])) col.Visible = false;
                else col.HeaderText = pairs[i + 1];
            }
        }

        private static void NumberFormat(DataGridView grid, params string[] columns)
        {
            foreach (var name in columns)
            {
                var col = grid.Columns[name];
                if (col == null) continue;
                col.DefaultCellStyle.Format = "N3";
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        private static DataRow SelectedRow(DataGridView grid)
        {
            if (grid.CurrentRow == null) return null;
            var view = grid.CurrentRow.DataBoundItem as DataRowView;
            return view == null ? null : view.Row;
        }

        // ── Tab 0: Yêu cầu mua hàng ──

        private void BuildPoTab(TabPage page)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(Caption("Trạng thái"));
            _poFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _poFilter.Width = 200;
            _poFilter.Items.Add("(Tất cả)");
            foreach (int code in new[] { 0, 10, 20, 30, 40, 50, 900 }) _poFilter.Items.Add(ComponentPoStatus.Text(code));
            _poFilter.SelectedIndex = 0;
            _poFilter.SelectedIndexChanged += (s, e) => ApplyPoFilter();
            bar.Controls.Add(_poFilter);
            bar.Controls.Add(MakeButton("Tải lại", 80, (s, e) => LoadPoOrders()));
            _btnUpload.Text = "Upload Excel PO…"; _btnUpload.Width = 130; _btnUpload.Height = 30;
            _btnUpload.Enabled = Can(FnPo, "CREATE"); _btnUpload.Click += UploadClick;
            bar.Controls.Add(_btnUpload);
            ConfigureAction(_btnValidate, "Kiểm tra PO", 110, ComponentPoStatus.Validated);
            ConfigureAction(_btnRelease, "Phát hành + sinh LOT/QR", 170, ComponentPoStatus.Released);
            ConfigureAction(_btnClose, "Đóng PO", 90, ComponentPoStatus.Closed);
            ConfigureAction(_btnCancel, "Hủy PO", 90, ComponentPoStatus.Cancelled);
            bar.Controls.AddRange(new Control[] { _btnValidate, _btnRelease, _btnClose, _btnCancel });

            var detail = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            detail.Panel1.Controls.Add(WithCaption(_poLines, "Dòng hàng của PO"));
            detail.Panel2.Controls.Add(WithCaption(_poEvents, "Lịch sử trạng thái (workflow)"));

            _poGrid.SelectionChanged += (s, e) => OnPoSelected();
            root.Controls.Add(bar, 0, 0);
            root.Controls.Add(_poGrid, 0, 1);
            root.Controls.Add(detail, 0, 2);
            page.Controls.Add(root);
        }

        private void ConfigureAction(Button button, string text, int width, int toStatus)
        {
            button.Text = text; button.Width = width; button.Height = 30; button.Enabled = false;
            button.Tag = toStatus;
            button.Click += TransitionClick;
        }

        private static Control WithCaption(Control grid, string caption)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var label = new Label { Text = caption, Dock = DockStyle.Top, Height = 22, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
            panel.Controls.Add(grid);
            panel.Controls.Add(label);
            return panel;
        }

        // ── Tab 1: Nhận linh kiện ──

        private void BuildReceiptTab(TabPage page)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16), WrapContents = false };
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Text = "Nhận linh kiện từ nhà cung cấp: quét tem QR (FVN1|LOT) của PO đã phát hành, chọn slot PART đích. " +
                       "Hệ thống tự cập nhật trạng thái PO sang Đang nhận / Đã nhận đủ và ghi tồn vào sổ MES."
            });
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 8),
                Text = Can(FnReceipt, "CONFIRM")
                    ? "Quyền xác nhận nhận hàng: có."
                    : "Quyền xác nhận nhận hàng: KHÔNG (chỉ xem danh sách)."
            });
            panel.Controls.Add(MakeButton("Mở Kiểm kê hàng về (QR)…", 240, (s, e) =>
                OpenGuarded(() => FormWindowManager.ShowSingle(this, () => new FormKiemKeHangVeList()), "Kiểm kê hàng về")));
            page.Controls.Add(panel);
        }

        // ── Tab 2: Tồn kho ──

        private void BuildStockTab(TabPage page)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            filters.Controls.Add(Caption("Site"));
            _site.DropDownStyle = ComboBoxStyle.DropDownList; _site.Width = 90; filters.Controls.Add(_site);
            filters.Controls.Add(Caption("PartNo"));
            _partNo.Width = 150; filters.Controls.Add(_partNo);
            filters.Controls.Add(Caption("LOT"));
            _lotNo.Width = 200; filters.Controls.Add(_lotNo);
            filters.Controls.Add(Caption("Slot / giá đỡ / vị trí"));
            _slotText.Width = 160; filters.Controls.Add(_slotText);
            _includeZero.Text = "Gồm tồn = 0"; _includeZero.AutoSize = true; _includeZero.Padding = new Padding(8, 6, 0, 0);
            filters.Controls.Add(_includeZero);
            filters.Controls.Add(MakeButton("Tìm", 80, (s, e) => LoadStock()));
            foreach (var box in new[] { _partNo, _lotNo, _slotText })
                box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadStock(); } };

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            _btnToTransfer.Text = "Điều chuyển LOT đã chọn →"; _btnToTransfer.Width = 200; _btnToTransfer.Height = 30;
            _btnToTransfer.Enabled = false; _btnToTransfer.Click += (s, e) => StartTransferFromSelection();
            _btnToTrace.Text = "Truy xuất LOT đã chọn →"; _btnToTrace.Width = 200; _btnToTrace.Height = 30;
            _btnToTrace.Enabled = false; _btnToTrace.Click += (s, e) => StartTraceFromSelection();
            actions.Controls.Add(_btnToTransfer);
            actions.Controls.Add(_btnToTrace);

            _stockGrid.SelectionChanged += (s, e) => UpdateStockButtons();
            root.Controls.Add(filters, 0, 0);
            root.Controls.Add(_stockGrid, 0, 1);
            root.Controls.Add(actions, 0, 2);
            page.Controls.Add(root);
        }

        // ── Tab 3: Điều chuyển LOT ──

        private void BuildTransferTab(TabPage page)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _xferSource.Dock = DockStyle.Fill;
            _xferSource.Padding = new Padding(8);
            _xferSource.Text = "Chưa chọn LOT nguồn. Vào tab \"2. Tồn kho PART\", chọn một dòng tồn rồi bấm \"Điều chuyển LOT đã chọn\".";

            var form = new FlowLayoutPanel { Dock = DockStyle.Fill };
            form.Controls.Add(Caption("Slot đích"));
            _toSlot.DropDownStyle = ComboBoxStyle.DropDownList; _toSlot.Width = 340; form.Controls.Add(_toSlot);
            form.Controls.Add(Caption("Số lượng"));
            _xferQty.Width = 110; _xferQty.DecimalPlaces = 3; _xferQty.Minimum = 0; _xferQty.Maximum = 1000000000; form.Controls.Add(_xferQty);
            form.Controls.Add(Caption("Lý do"));
            _xferReason.Width = 260; _xferReason.MaxLength = 300; form.Controls.Add(_xferReason);
            form.Controls.Add(Caption("Số phiếu"));
            _xferNo.Width = 170; _xferNo.ReadOnly = true; form.Controls.Add(_xferNo);
            _btnTransfer.Text = "ĐIỀU CHUYỂN"; _btnTransfer.Width = 140; _btnTransfer.Height = 32; _btnTransfer.Enabled = false;
            _btnTransfer.Click += TransferClick;
            form.Controls.Add(_btnTransfer);

            root.Controls.Add(_xferSource, 0, 0);
            root.Controls.Add(form, 0, 1);
            root.Controls.Add(WithCaption(_xferHistory, "Phiếu điều chuyển gần đây của Site"), 0, 2);
            page.Controls.Add(root);
        }

        // ── Tab 4: Xuất theo nhu cầu ──

        private void BuildIssueTab(TabPage page)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16), WrapContents = false };
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Text = "Xuất linh kiện theo nhu cầu sản xuất: chọn kế hoạch theo ca, tạo yêu cầu linh kiện theo BOM, " +
                       "rồi xuất PART → WIP từ LOT/slot nguồn. Màn hình này cần đồng thời quyền xem nhu cầu linh kiện " +
                       "(MES.COMPONENT_DEMAND) và quyền xuất PART → WIP (MES.COMPONENT_ISSUE)."
            });
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 8),
                Text = "Tạo yêu cầu: " + (Can(FnDemand, "CREATE") ? "có" : "không") +
                       "   |   Xác nhận xuất: " + (Can(FnIssue, "CONFIRM") ? "có" : "không")
            });
            panel.Controls.Add(MakeButton("Mở Yêu cầu / Xuất linh kiện PART → WIP…", 300, (s, e) =>
                OpenGuarded(() => FormWindowManager.ShowSingle(this, () => new FormYeuCauVaXuatLinhKien()), "Yêu cầu / Xuất linh kiện")));
            page.Controls.Add(panel);
        }

        // ── Tab 5: Truy xuất ──

        private void BuildTraceTab(TabPage page)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(Caption("LOT"));
            _traceLot.Width = 260; bar.Controls.Add(_traceLot);
            bar.Controls.Add(Caption("Site"));
            _traceSite.DropDownStyle = ComboBoxStyle.DropDownList; _traceSite.Width = 90; bar.Controls.Add(_traceSite);
            bar.Controls.Add(Caption("Chiều"));
            _traceDirection.DropDownStyle = ComboBoxStyle.DropDownList; _traceDirection.Width = 230;
            _traceDirection.Items.Add("Linh kiện → thành phẩm (DOWNSTREAM)");
            _traceDirection.Items.Add("Thành phẩm → linh kiện (UPSTREAM)");
            _traceDirection.SelectedIndex = 0;
            bar.Controls.Add(_traceDirection);
            bar.Controls.Add(MakeButton("Truy xuất", 100, (s, e) => RunTrace()));
            _traceLot.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunTrace(); } };

            root.Controls.Add(bar, 0, 0);
            root.Controls.Add(_traceGrid, 0, 1);
            page.Controls.Add(root);
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

        private static void FillSiteCombo(ComboBox combo, DataTable sites, bool includeAll)
        {
            combo.Items.Clear();
            if (includeAll) combo.Items.Add(string.Empty);
            foreach (DataRow row in sites.Rows) combo.Items.Add(Convert.ToString(row["SiteCode"]));
            if (combo.Items.Count > 0) combo.SelectedIndex = includeAll && combo.Items.Count > 1 ? 1 : 0;
        }

        private void SetStatus(string text) { _status.Text = text; }

        private void OpenGuarded(Action open, string title)
        {
            try { open(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var view = new DataView(_poTable);
            if (_poFilter.SelectedIndex > 0)
                view.RowFilter = "StatusText = '" + _poFilter.SelectedItem.ToString().Replace("'", "''") + "'";
            _poGrid.DataSource = view;
            ApplyHeaders(_poGrid,
                "PurchaseOrderId", "", "Status", "", "StatusCode", "", "Remarks", "Ghi chú",
                "SiteCode", "Site", "PurchaseOrderNo", "Số PO", "OrderDate", "Ngày đặt",
                "SupplierCode", "Mã NCC", "SupplierName", "Nhà cung cấp", "StatusText", "Trạng thái",
                "CreatedBy", "Người tạo", "CreatedAtUtc", "Tạo lúc (UTC)", "LastUpdateUtc", "Cập nhật (UTC)",
                "LineCount", "Số dòng", "OrderQty", "SL đặt", "ReceivedQty", "SL đã nhận");
            NumberFormat(_poGrid, "OrderQty", "ReceivedQty");
            OnPoSelected();
        }

        private void OnPoSelected()
        {
            var row = SelectedRow(_poGrid);
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
                _poLines.DataSource = null;
                _poEvents.DataSource = null;
                return;
            }
            try
            {
                long id = Convert.ToInt64(row["PurchaseOrderId"]);
                _poLines.DataSource = _po.ListLines(id);
                ApplyHeaders(_poLines, "PurchaseOrderLineId", "", "LineNo", "Dòng", "PartNo", "PartNo",
                    "PartDescription", "Mô tả", "UnitCode", "ĐVT", "NeedByDate", "Cần trước ngày",
                    "OrderQty", "SL đặt", "ReceivedQty", "SL đã nhận", "RemainingQty", "SL còn lại", "LotCount", "Số LOT/tem");
                NumberFormat(_poLines, "OrderQty", "ReceivedQty", "RemainingQty");

                DataTable events = _po.ListEvents(id);
                events.Columns.Add("FromText", typeof(string));
                events.Columns.Add("ToText", typeof(string));
                foreach (DataRow e in events.Rows)
                {
                    e["FromText"] = e["FromStatus"] == DBNull.Value ? string.Empty : ComponentPoStatus.Text(Convert.ToInt32(e["FromStatus"]));
                    e["ToText"] = ComponentPoStatus.Text(Convert.ToInt32(e["ToStatus"]));
                }
                _poEvents.DataSource = events;
                ApplyHeaders(_poEvents, "FromStatus", "", "ToStatus", "", "OccurredAtUtc", "Lúc (UTC)", "ActionName", "Hành động",
                    "FromText", "Từ", "ToText", "Đến", "ActorCode", "Người thao tác", "Reason", "Lý do");
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void UploadClick(object sender, EventArgs e)
        {
            string path;
            using (var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", Title = "Chọn file yêu cầu mua hàng linh kiện" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }
            try
            {
                IList<string> sheets = ComponentPoExcelReader.GetSheetNames(path);
                string sheet = sheets.FirstOrDefault(n => string.Equals(n, "PO", StringComparison.OrdinalIgnoreCase)) ?? sheets.FirstOrDefault();
                if (sheet == null) throw new InvalidOperationException("File Excel không có sheet nào.");

                List<ComponentPoImportRow> rows = ComponentPoExcelReader.Read(path, sheet);
                int orders = rows.GroupBy(r => new { r.SiteCode, r.PurchaseOrderNo }).Count();
                string message = "Sheet \"" + sheet + "\": " + orders + " PO, " + rows.Count + " dòng hàng.\r\n\r\n" +
                    "Upload tạo PO ở trạng thái Nháp (hoặc thay thế PO Nháp cùng số). PO đã Kiểm tra/Phát hành không bị ghi đè. " +
                    "Toàn bộ file chạy trong một transaction.\r\n\r\nTiếp tục upload?";
                if (MessageBox.Show(this, message, "Upload yêu cầu mua hàng", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                int count = _po.Import(rows, Path.GetFileName(path));
                SetStatus("Đã upload " + count + " PO (" + rows.Count + " dòng) ở trạng thái Nháp.");
                LoadPoOrders();
            }
            catch (Exception ex)
            {
                SetStatus("Upload thất bại: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Upload yêu cầu mua hàng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TransitionClick(object sender, EventArgs e)
        {
            var button = (Button)sender;
            int toStatus = (int)button.Tag;
            var row = SelectedRow(_poGrid);
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
                    reason = PromptReason(button.Text + " " + poNo, "Nhập lý do (bắt buộc):");
                    if (string.IsNullOrWhiteSpace(reason)) return;
                }
                else
                {
                    string ask = toStatus == ComponentPoStatus.Released
                        ? "Phát hành PO " + poNo + " và sinh LOT/QR cho từng dòng hàng?"
                        : button.Text + " cho PO " + poNo + "?";
                    if (MessageBox.Show(this, ask, "Workflow PO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }

                DataTable result = _po.Transition(id, from, toStatus, reason);
                string text = "PO " + poNo + " → " + ComponentPoStatus.Text(toStatus) + ".";
                if (toStatus == ComponentPoStatus.Released && result.Rows.Count > 0 && result.Columns.Contains("LotsIssued"))
                    text += " Đã có " + result.Rows[0]["LotsIssued"] + " LOT/tem QR.";
                SetStatus(text);
                LoadPoOrders();
                SelectPo(id);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                MessageBox.Show(this, ex.Message, "Workflow PO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadPoOrders();
            }
        }

        private void SelectPo(long id)
        {
            foreach (DataGridViewRow gridRow in _poGrid.Rows)
            {
                var view = gridRow.DataBoundItem as DataRowView;
                if (view != null && Convert.ToInt64(view.Row["PurchaseOrderId"]) == id)
                {
                    gridRow.Selected = true;
                    _poGrid.CurrentCell = gridRow.Cells[_poGrid.Columns.GetFirstColumn(DataGridViewElementStates.Visible).Index];
                    return;
                }
            }
        }

        private string PromptReason(string title, string label)
        {
            using (var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(460, 150)
            })
            {
                var lbl = new Label { Text = label, Left = 12, Top = 12, Width = 430, Height = 30 };
                var txt = new TextBox { Left = 12, Top = 48, Width = 430, MaxLength = 400 };
                var ok = new Button { Text = "Đồng ý", Left = 268, Top = 100, Width = 84, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Bỏ qua", Left = 358, Top = 100, Width = 84, DialogResult = DialogResult.Cancel };
                dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        // ── Tồn kho ──

        private void LoadStock()
        {
            try
            {
                var filter = new ComponentStockFilter
                {
                    SiteCode = _site.SelectedItem == null ? null : Convert.ToString(_site.SelectedItem),
                    PartNo = _partNo.Text,
                    LotNo = _lotNo.Text,
                    SlotText = _slotText.Text,
                    IncludeZero = _includeZero.Checked
                };
                DataTable table = _stock.Search(filter);
                _stockGrid.DataSource = table;
                ApplyHeaders(_stockGrid, "MaterialLotId", "", "SlotId", "",
                    "SiteCode", "Site", "PartNo", "PartNo", "LotNo", "LOT", "LotStatus", "Trạng thái LOT",
                    "SupplierCode", "Mã NCC", "PurchaseOrderNo", "Số PO", "WarehouseName", "Kho", "RackName", "Giá đỡ",
                    "SlotNumber", "Slot", "LocationNo", "Vị trí MES", "StockState", "Trạng thái tồn",
                    "OnHandQty", "Tồn", "ReservedQty", "Giữ chỗ", "AvailableQty", "Khả dụng", "UpdatedAtUtc", "Cập nhật (UTC)");
                NumberFormat(_stockGrid, "OnHandQty", "ReservedQty", "AvailableQty");
                decimal onHand = table.AsEnumerable().Sum(r => Convert.ToDecimal(r["OnHandQty"]));
                SetStatus(table.Rows.Count + " dòng tồn, tổng tồn " + onHand.ToString("N3", CultureInfo.CurrentCulture) +
                    (table.Rows.Count >= 5000 ? " (đã giới hạn 5000 dòng, hãy lọc thêm)" : string.Empty));
                UpdateStockButtons();
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void UpdateStockButtons()
        {
            var row = SelectedRow(_stockGrid);
            bool available = row != null &&
                string.Equals(Convert.ToString(row["StockState"]), "AVAILABLE", StringComparison.OrdinalIgnoreCase) &&
                Convert.ToDecimal(row["AvailableQty"]) > 0;
            _btnToTransfer.Enabled = available && _tabTransfer != null && Can(FnTransfer, "CONFIRM");
            _btnToTrace.Enabled = row != null && _tabTrace != null;
        }

        // ── Điều chuyển ──

        private void StartTransferFromSelection()
        {
            var row = SelectedRow(_stockGrid);
            if (row == null || _tabTransfer == null) return;
            try
            {
                _xferRow = row;
                string site = Convert.ToString(row["SiteCode"]);
                decimal available = Convert.ToDecimal(row["AvailableQty"]);

                DataTable slots = _stock.ListSlots(site);
                int fromSlot = Convert.ToInt32(row["SlotId"]);
                var view = new DataView(slots) { RowFilter = "SlotId <> " + fromSlot.ToString(CultureInfo.InvariantCulture) };
                _toSlot.DataSource = view;
                _toSlot.DisplayMember = "SlotDisplay";
                _toSlot.ValueMember = "SlotId";

                _xferSource.Text = "Nguồn: Site " + site + "  |  PartNo " + row["PartNo"] + "  |  LOT " + row["LotNo"] + "\r\n" +
                    "Vị trí: " + row["WarehouseName"] + " / " + row["RackName"] + " / Slot " + row["SlotNumber"] +
                    "  |  Khả dụng: " + available.ToString("N3", CultureInfo.CurrentCulture);
                _xferQty.Maximum = available;
                _xferQty.Minimum = 0.001M;
                _xferQty.Value = available;
                _xferReason.Text = string.Empty;
                _xferNo.Text = "CLT-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                _xferKey = Guid.NewGuid().ToString("N");
                _btnTransfer.Enabled = Can(FnTransfer, "CONFIRM");
                LoadTransferHistory(site);
                _tabs.SelectedTab = _tabTransfer;
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        }

        private void LoadTransferHistory(string site)
        {
            _xferHistory.DataSource = _stock.ListTransfers(site);
            ApplyHeaders(_xferHistory, "InventoryTransactionId", "", "TransferredAtUtc", "Lúc (UTC)", "TransferNo", "Số phiếu",
                "SiteCode", "Site", "PartNo", "PartNo", "LotNo", "LOT", "Quantity", "SL", "FromSlot", "Từ slot",
                "ToSlot", "Đến slot", "Reason", "Lý do", "TransferredBy", "Người thực hiện");
            NumberFormat(_xferHistory, "Quantity");
        }

        private void TransferClick(object sender, EventArgs e)
        {
            if (_xferRow == null) return;
            try
            {
                if (_toSlot.SelectedValue == null) throw new InvalidOperationException("Chọn slot đích.");
                string site = Convert.ToString(_xferRow["SiteCode"]);
                string lot = Convert.ToString(_xferRow["LotNo"]);
                string ask = "Điều chuyển " + _xferQty.Value.ToString("N3", CultureInfo.CurrentCulture) + " của LOT " + lot +
                    "\r\nđến: " + _toSlot.Text + "?";
                if (MessageBox.Show(this, ask, "Điều chuyển LOT", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                // Giữ nguyên khóa chống trùng cho mọi lần bấm lại của cùng một phiếu; đổi khóa sau khi thành công.
                DataTable result = _stock.Transfer(site, _xferNo.Text, Convert.ToInt64(_xferRow["MaterialLotId"]),
                    Convert.ToInt32(_xferRow["SlotId"]), Convert.ToInt32(_toSlot.SelectedValue), _xferQty.Value,
                    _xferReason.Text, _xferKey);

                SetStatus("Đã điều chuyển phiếu " + _xferNo.Text + " (giao dịch MES #" + result.Rows[0]["InventoryTransactionId"] + ").");
                _xferRow = null;
                _btnTransfer.Enabled = false;
                _xferSource.Text = "Đã điều chuyển xong. Chọn dòng tồn khác ở tab \"2. Tồn kho PART\" để tiếp tục.";
                _xferKey = Guid.NewGuid().ToString("N");
                LoadTransferHistory(site);
                LoadStock();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
                MessageBox.Show(this, ex.Message, "Điều chuyển LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Truy xuất ──

        private void StartTraceFromSelection()
        {
            var row = SelectedRow(_stockGrid);
            if (row == null || _tabTrace == null) return;
            _traceLot.Text = Convert.ToString(row["LotNo"]);
            string site = Convert.ToString(row["SiteCode"]);
            int index = _traceSite.Items.IndexOf(site);
            if (index >= 0) _traceSite.SelectedIndex = index;
            _traceDirection.SelectedIndex = 0;
            _tabs.SelectedTab = _tabTrace;
            RunTrace();
        }

        private void RunTrace()
        {
            try
            {
                string direction = _traceDirection.SelectedIndex == 1 ? "UPSTREAM" : "DOWNSTREAM";
                string site = _traceSite.SelectedItem == null ? null : Convert.ToString(_traceSite.SelectedItem);
                DataTable table = _trace.Trace(_traceLot.Text, direction, site);
                _traceGrid.DataSource = table;
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