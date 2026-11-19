using PCTP.Infrastructure.Security;
using PCTP.Security;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing.Printing;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraTab;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraPrinting.BarCode;

namespace PCTP.Modules.MesPlanning.UI
{
    public sealed class FormDieuHanhSanXuat : XtraForm
    {
        private readonly IProductionExecutionService _service;
        private readonly IProductionWorkflowService _workflow;
        private readonly TextEdit _routingStepId = new TextEdit();
        private readonly TextEdit _planLineId = new TextEdit(), _orderNo = new TextEdit();
        private readonly TextEdit _orderId = new TextEdit(), _goodQty = new TextEdit(), _ngQty = new TextEdit();
        private readonly TextEdit _lotId = new TextEdit(), _operationLotId = new TextEdit(), _consumeProductionLotId = new TextEdit();
        private readonly TextEdit _operationNo = new TextEdit(), _inputQty = new TextEdit();
        private readonly TextEdit _opGoodQty = new TextEdit(), _opNgQty = new TextEdit();
        private readonly TextEdit _inputSlot = new TextEdit(), _outputSlot = new TextEdit();
        private readonly TextEdit _materialLineId = new TextEdit(), _componentLotId = new TextEdit();
        private readonly TextEdit _consumeQty = new TextEdit(), _consumeSlot = new TextEdit();
        private readonly CheckEdit _finished = new CheckEdit();
        private readonly DateEdit _productionDate = new DateEdit();
        private readonly LabelControl _status = new LabelControl();
        private readonly GridControl _materials = new GridControl();
        private readonly GridView _materialView = new GridView();
        private readonly GridControl _routingSteps = new GridControl();
        private readonly GridView _routingView = new GridView();
        private readonly GridControl _labels = new GridControl();
        private readonly GridView _labelView = new GridView();
        private readonly TextEdit _labelQty = new TextEdit(), _labelScan = new TextEdit();
        private readonly TextEdit _labelVoidReason = new TextEdit();
        private DataTable _printRows;
        private int _printIndex;

        public FormDieuHanhSanXuat()
            : this(new ProductionExecutionService(new ProductionExecutionRepository()), MesWorkflowComposition.Create())
        {
        }

        public FormDieuHanhSanXuat(IProductionExecutionService service)
            : this(service, MesWorkflowComposition.Create())
        {
        }

        public FormDieuHanhSanXuat(IProductionExecutionService service, IProductionWorkflowService workflow)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            Text = "Điều hành sản xuất MES";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1120, 760);
            MinimumSize = new Size(900, 620);
            BuildUi();
        }

        private void BuildUi()
        {
            var tabs = new XtraTabControl { Dock = DockStyle.Fill };
            var orderTab = new XtraTabPage { Text = "Lệnh và LOT" };
            var operationTab = new XtraTabPage { Text = "Công đoạn" };
            var consumeTab = new XtraTabPage { Text = "Tiêu hao LOT linh kiện" };

            var orderPanel = Panel();
            AddField(orderPanel, "ProductionPlanLineId", _planLineId);
            AddButton(orderPanel, "CHỌN KẾ HOẠCH FIRM", SelectPlanLineClick, UserSession.Current.HasPermission("MES.PLAN_UPLOAD", "VIEW"));
            AddField(orderPanel, "OrderNo", _orderNo);
            AddButton(orderPanel, "TẠO LỆNH TỪ KẾ HOẠCH", CreateOrderClick, Can("CREATE"));
            AddField(orderPanel, "ProductionOrderId", _orderId);
            AddField(orderPanel, "ProductionLotId (sau khi tạo LOT)", _lotId);
            _productionDate.Properties.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";
            _productionDate.Properties.EditFormat.FormatString = "dd/MM/yyyy HH:mm";
            _productionDate.Properties.Mask.EditMask = "dd/MM/yyyy HH:mm";
            _productionDate.DateTime = DateTime.Now;
            _productionDate.Width = 190;
            orderPanel.Controls.Add(new Label { Text = "Ngày/giờ sản xuất", AutoSize = true, Padding = new Padding(4, 8, 0, 0) });
            orderPanel.Controls.Add(_productionDate);
            AddField(orderPanel, "Số lượng OK", _goodQty);
            AddField(orderPanel, "Số lượng NG", _ngQty);
            _finished.Text = "LOT thành phẩm cuối (FG); bỏ chọn = bán thành phẩm (WIP)";
            _finished.AutoSize = true;
            orderPanel.Controls.Add(_finished);
            AddButton(orderPanel, "RELEASE LỆNH", (s, e) => TransitionOrder(10), Can("APPROVE"));
            AddButton(orderPanel, "TẠO LOT SẢN XUẤT", CreateLotClick, Can("CREATE"));
            AddButton(orderPanel, "START LỆNH", (s, e) => TransitionOrder(20), Can("CONFIRM"));
            AddButton(orderPanel, "PAUSE LỆNH", (s, e) => TransitionOrder(30), Can("CONFIRM"));
            AddButton(orderPanel, "RESUME LỆNH", (s, e) => TransitionOrder(20), Can("CONFIRM"));
            AddButton(orderPanel, "COMPLETE LỆNH", (s, e) => TransitionOrder(40), Can("APPROVE"));
            AddButton(orderPanel, "CLOSE LỆNH", (s, e) => TransitionOrder(50), Can("APPROVE"));
            AddButton(orderPanel, "LỊCH SỬ LỆNH", (s, e) => OpenTimeline("ORDER", _orderId), true);
            orderTab.Controls.Add(orderPanel);

            var operationPanel = Panel();
            AddField(operationPanel, "ProductionLotId", _operationLotId);
            AddField(operationPanel, "OperationNo", _operationNo);
            AddField(operationPanel, "RoutingStepId (workflow)", _routingStepId);
            _routingStepId.Properties.ReadOnly = true;
            _operationNo.Properties.ReadOnly = true;
            AddButton(operationPanel, "PREPARE", (s, e) => TransitionOperation(10), Can("CONFIRM"));
            AddButton(operationPanel, "READY", (s, e) => TransitionOperation(20), Can("CONFIRM"));
            AddButton(operationPanel, "START", (s, e) => TransitionOperation(30), Can("CONFIRM"));
            AddButton(operationPanel, "PAUSE", (s, e) => TransitionOperation(40), Can("CONFIRM"));
            AddButton(operationPanel, "RESUME", (s, e) => TransitionOperation(30), Can("CONFIRM"));
            AddButton(operationPanel, "TẢI ROUTING THEO LOT", LoadRoutingClick, true);
            _routingSteps.Width = 980; _routingSteps.Height = 160;
            _routingSteps.MainView = _routingView; _routingSteps.ViewCollection.Add(_routingView);
            ConfigureGrid(_routingView);
            _routingView.FocusedRowChanged += (s, e) =>
            {
                var row = _routingView.GetFocusedDataRow();
                if (row == null) return;
                _routingStepId.Text = Convert.ToString(row["RoutingStepId"]);
                _operationNo.Text = Convert.ToString(row["OperationNo"]);
            };
            operationPanel.Controls.Add(_routingSteps);
            AddButton(operationPanel, "LỊCH SỬ LOT", (s, e) => OpenTimeline("OPERATION", _operationLotId), true);
            AddField(operationPanel, "InputQty", _inputQty);
            AddField(operationPanel, "GoodQty", _opGoodQty);
            AddField(operationPanel, "NgQty", _opNgQty);
            AddField(operationPanel, "InputSlotId (bắt buộc nếu công đoạn trước VIA_WIP)", _inputSlot);
            AddField(operationPanel, "OutputSlotId (bắt buộc nếu công đoạn này VIA_WIP)", _outputSlot);
            AddButton(operationPanel, "XÁC NHẬN HOÀN THÀNH CÔNG ĐOẠN", CompleteOperationClick, Can("CONFIRM"));
            AddField(operationPanel, "Số sản phẩm cần chuẩn bị tem", _labelQty);
            AddButton(operationPanel, "1. CHUẨN BỊ / CẤP TEM", PrepareLabelsClick, Can("CREATE"));
            AddButton(operationPanel, "2. IN TEM CÔNG ĐOẠN", PrintLabelsClick, Can("PRINT"));
            _labels.Width = 980; _labels.Height = 190;
            _labels.MainView = _labelView; _labels.ViewCollection.Add(_labelView);
            ConfigureGrid(_labelView);
            operationPanel.Controls.Add(_labels);
            AddField(operationPanel, "Quét QR tem MES hoặc LabelCode để xác nhận dán tem", _labelScan);
            AddButton(operationPanel, "XÁC NHẬN ĐÃ DÁN TEM", ApplyLabelClick, Can("CONFIRM"));
            AddField(operationPanel, "Lý do hủy tem chưa sử dụng", _labelVoidReason);
            AddButton(operationPanel, "HỦY TEM CHƯA DÙNG", VoidLabelClick, Can("CANCEL"));
            operationTab.Controls.Add(operationPanel);

            var consumePanel = Panel();
            var loadMaterials = new SimpleButton { Text = "TẢI LINH KIỆN THEO LỆNH", AutoSize = true, Height = 34, Enabled = true };
            loadMaterials.Click += LoadMaterialsClick;
            consumePanel.Controls.Add(loadMaterials);
            _materials.Width = 980; _materials.Height = 190;
            _materials.MainView = _materialView; _materials.ViewCollection.Add(_materialView);
            ConfigureGrid(_materialView);
            _materialView.FocusedRowChanged += (s, e) =>
            {
                var row = _materialView.GetFocusedDataRow();
                if (row != null) _materialLineId.Text = Convert.ToString(row["ProductionOrderMaterialId"]);
            };
            consumePanel.Controls.Add(_materials);
            AddField(consumePanel, "ProductionOrderMaterialId", _materialLineId);
            AddField(consumePanel, "ProductionLotId", _consumeProductionLotId);
            AddField(consumePanel, "Quét QR LOT linh kiện (FVN1|LOT)", _componentLotId);
            AddField(consumePanel, "FromSlotId (PART hoặc WIP)", _consumeSlot);
            AddField(consumePanel, "Số lượng tiêu hao thực tế", _consumeQty);
            AddButton(consumePanel, "GHI NHẬN TIÊU HAO + GENEALOGY", ConsumeClick, Can("CONFIRM"));
            consumeTab.Controls.Add(consumePanel);

            tabs.TabPages.Add(orderTab);
            tabs.TabPages.Add(operationTab);
            tabs.TabPages.Add(consumeTab);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            _status.Dock = DockStyle.Fill;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _status.Text = "Lệnh/LOT/công đoạn/tiêu hao đều ghi vào MES; DIRECT không tạo tồn WIP, VIA_WIP dùng ledger chung.";
            root.Controls.Add(tabs, 0, 0);
            root.Controls.Add(_status, 0, 1);
            Controls.Add(root);
        }

        private static void ConfigureGrid(GridView view)
        {
            view.OptionsBehavior.Editable = false;
            view.OptionsBehavior.ReadOnly = true;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ShowAutoFilterRow = true;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsSelection.MultiSelect = false;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            view.FocusRectStyle = DrawFocusRectStyle.RowFocus;
        }

        private static FlowLayoutPanel Panel()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), WrapContents = true };
        }

        private static void AddField(FlowLayoutPanel panel, string caption, TextEdit input)
        {
            var group = new Panel { Width = 320, Height = 58, Margin = new Padding(5) };
            group.Controls.Add(new Label { Text = caption, Dock = DockStyle.Top, Height = 22 });
            input.Dock = DockStyle.Bottom;
            input.Width = 300;
            group.Controls.Add(input);
            panel.Controls.Add(group);
        }

        private static bool Can(string action)
        {
            return UserSession.Current != null &&
                UserSession.Current.HasPermission("MES.PRODUCTION_EXECUTION", action);
        }

        private static void AddButton(FlowLayoutPanel panel, string caption, EventHandler handler, bool enabled)
        {
            var button = new SimpleButton
            {
                Text = caption, AutoSize = true, Height = 34, Margin = new Padding(8, 18, 8, 8),
                Enabled = enabled,
                ToolTip = enabled ? caption : "Bạn chỉ có quyền theo dõi, không có quyền thực hiện thao tác này."
            };
            button.Click += handler;
            panel.Controls.Add(button);
        }

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

        private static int? OptionalSlot(TextEdit input)
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return null;
            int value;
            if (!int.TryParse(input.Text.Trim(), out value) || value <= 0)
                throw new InvalidOperationException("SlotId phải là số nguyên dương hoặc để trống.");
            return value;
        }

        private void SelectPlanLineClick(object sender, EventArgs e)
        {
            using (var picker = new FormProductionPlanWorkflow())
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                    _planLineId.Text = picker.SelectedPlanLineId.ToString();
            }
        }

        private void CreateOrderClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                var result = _service.CreateOrder(LongValue(_planLineId, "ProductionPlanLineId"),
                    _orderNo.Text, Environment.UserName);
                if (result.Rows.Count > 0) _orderId.Text = Convert.ToString(result.Rows[0]["ProductionOrderId"]);
                return "Đã tạo và release lệnh MES " + _orderNo.Text + ".";
            });
        }

        private void CreateLotClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                var result = _service.CreateLot(LongValue(_orderId, "ProductionOrderId"),
                    _productionDate.DateTime.ToUniversalTime(), DecimalValue(_goodQty, "GoodQty", false),
                    DecimalValue(_ngQty, "NgQty", true), _finished.Checked, Environment.UserName);
                if (result.Rows.Count > 0)
                {
                    _lotId.Text = Convert.ToString(result.Rows[0]["ProductionLotId"]);
                    _operationLotId.Text = _lotId.Text;
                    _consumeProductionLotId.Text = _lotId.Text;
                    _routingSteps.DataSource = _service.GetRoutingSteps(LongValue(_operationLotId, "ProductionLotId"));
                    _routingView.BestFitColumns();
                    return "Đã tạo LOT " + Convert.ToString(result.Rows[0]["LotNo"]) + " và tải routing.";
                }
                return "Đã tạo LOT sản xuất.";
            });
        }

        private void CompleteOperationClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                _service.CompleteOperation(LongValue(_operationLotId, "ProductionLotId"), _operationNo.Text,
                    DecimalValue(_inputQty, "InputQty", false), DecimalValue(_opGoodQty, "GoodQty", true),
                    DecimalValue(_opNgQty, "NgQty", true), OptionalSlot(_inputSlot), OptionalSlot(_outputSlot),
                    Environment.UserName);
                _routingSteps.DataSource = _service.GetRoutingSteps(LongValue(_operationLotId, "ProductionLotId"));
                _routingView.BestFitColumns();
                return "Đã ghi nhận hoàn thành công đoạn và bàn giao theo cấu hình DIRECT/VIA_WIP. Workflow đã được SQL đồng bộ.";
            });
        }

        private void PrepareLabelsClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                int quantity;
                if (!int.TryParse(_labelQty.Text.Trim(), out quantity) || quantity <= 0)
                    throw new InvalidOperationException("Số tem phải là số nguyên dương.");
                _labels.DataSource = _service.PrepareLabels(
                    LongValue(_operationLotId, "ProductionLotId"), _operationNo.Text,
                    quantity, Environment.UserName);
                return "Đã cấp " + _labelView.RowCount.ToString("N0") +
                    " tem duy nhất cho công đoạn " + _operationNo.Text + ".";
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
                _labelScan.Clear();
                return "Đã xác nhận tem " + Convert.ToString(result.Rows[0]["LabelCode"]) +
                    " đã được dán cho công đoạn " + _operationNo.Text + ".";
            });
        }

        private void VoidLabelClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                var result = _service.VoidLabel(_labelScan.Text, Environment.UserName, _labelVoidReason.Text);
                _labelScan.Clear();
                _labelVoidReason.Clear();
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
                    DecimalValue(_consumeQty, "Quantity", false), Environment.UserName);
                _componentLotId.Clear();
                return "Đã ghi nhận tiêu hao LOT và liên kết genealogy thực tế.";
            });
        }

        private void TransitionOrder(int targetStatus)
        {
            Run(() =>
            {
                long id = LongValue(_orderId, "ProductionOrderId");
                int current = _workflow.GetCurrentStatus("ORDER", id);
                _workflow.TransitionOrder(id, current, targetStatus, AuthorizationGuard.ActorCode,
                    "Điều hành từ form MES");
                return "Đã chuyển trạng thái lệnh " + id + " từ " + current + " sang " + targetStatus + ".";
            });
        }

        private void LoadRoutingClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                _routingSteps.DataSource = _service.GetRoutingSteps(LongValue(_operationLotId, "ProductionLotId"));
                _routingView.BestFitColumns();
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
                return "Đã chuyển trạng thái công đoạn " + stepId + " từ " + current + " sang " + targetStatus + ".";
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
            try { _status.Text = action(); }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
                XtraMessageBox.Show(this, ex.Message, "Điều hành sản xuất MES",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
