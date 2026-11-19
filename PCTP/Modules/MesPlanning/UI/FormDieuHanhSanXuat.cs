using PCTP.Infrastructure.Security;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing.Printing;
using DevExpress.XtraEditors;
using DevExpress.XtraPrinting.BarCode;

namespace PCTP.Modules.MesPlanning.UI
{
    public sealed class FormDieuHanhSanXuat : XtraForm
    {
        private readonly IProductionExecutionService _service;
        private readonly IProductionWorkflowService _workflow;
        private readonly TextBox _routingStepId = new TextBox();
        private readonly TextBox _planLineId = new TextBox(), _orderNo = new TextBox();
        private readonly TextBox _orderId = new TextBox(), _goodQty = new TextBox(), _ngQty = new TextBox();
        private readonly TextBox _lotId = new TextBox(), _operationLotId = new TextBox(), _consumeProductionLotId = new TextBox();
        private readonly TextBox _operationNo = new TextBox(), _inputQty = new TextBox();
        private readonly TextBox _opGoodQty = new TextBox(), _opNgQty = new TextBox();
        private readonly TextBox _inputSlot = new TextBox(), _outputSlot = new TextBox();
        private readonly TextBox _materialLineId = new TextBox(), _componentLotId = new TextBox();
        private readonly TextBox _consumeQty = new TextBox(), _consumeSlot = new TextBox();
        private readonly CheckBox _finished = new CheckBox();
        private readonly DateTimePicker _productionDate = new DateTimePicker();
        private readonly Label _status = new Label();
        private readonly DataGridView _materials = new DataGridView();
        private readonly DataGridView _routingSteps = new DataGridView();
        private readonly DataGridView _labels = new DataGridView();
        private readonly TextBox _labelQty = new TextBox(), _labelScan = new TextBox();
        private readonly TextBox _labelVoidReason = new TextBox();
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
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var orderTab = new TabPage("Lệnh và LOT");
            var operationTab = new TabPage("Công đoạn");
            var consumeTab = new TabPage("Tiêu hao LOT linh kiện");

            var orderPanel = Panel();
            AddField(orderPanel, "ProductionPlanLineId", _planLineId);
            AddField(orderPanel, "OrderNo", _orderNo);
            AddButton(orderPanel, "TẠO LỆNH TỪ KẾ HOẠCH", CreateOrderClick);
            AddField(orderPanel, "ProductionOrderId", _orderId);
            AddField(orderPanel, "ProductionLotId (sau khi tạo LOT)", _lotId);
            _productionDate.Format = DateTimePickerFormat.Custom;
            _productionDate.CustomFormat = "dd/MM/yyyy HH:mm";
            _productionDate.Width = 190;
            orderPanel.Controls.Add(new Label { Text = "Ngày/giờ sản xuất", AutoSize = true, Padding = new Padding(4, 8, 0, 0) });
            orderPanel.Controls.Add(_productionDate);
            AddField(orderPanel, "Số lượng OK", _goodQty);
            AddField(orderPanel, "Số lượng NG", _ngQty);
            _finished.Text = "LOT thành phẩm cuối (FG); bỏ chọn = bán thành phẩm (WIP)";
            _finished.AutoSize = true;
            orderPanel.Controls.Add(_finished);
            AddButton(orderPanel, "TẠO LOT SẢN XUẤT", CreateLotClick);
            AddButton(orderPanel, "RELEASE LỆNH", (s, e) => TransitionOrder(10));
            AddButton(orderPanel, "START LỆNH", (s, e) => TransitionOrder(20));
            AddButton(orderPanel, "PAUSE LỆNH", (s, e) => TransitionOrder(30));
            AddButton(orderPanel, "RESUME LỆNH", (s, e) => TransitionOrder(20));
            AddButton(orderPanel, "COMPLETE LỆNH", (s, e) => TransitionOrder(40));
            AddButton(orderPanel, "CLOSE LỆNH", (s, e) => TransitionOrder(50));
            AddButton(orderPanel, "LỊCH SỬ LỆNH", (s, e) => OpenTimeline("ORDER", _orderId));
            orderTab.Controls.Add(orderPanel);

            var operationPanel = Panel();
            AddField(operationPanel, "ProductionLotId", _operationLotId);
            AddField(operationPanel, "OperationNo", _operationNo);
            AddField(operationPanel, "RoutingStepId (workflow)", _routingStepId);
            AddButton(operationPanel, "PREPARE", (s, e) => TransitionOperation(10));
            AddButton(operationPanel, "READY", (s, e) => TransitionOperation(20));
            AddButton(operationPanel, "START", (s, e) => TransitionOperation(30));
            AddButton(operationPanel, "PAUSE", (s, e) => TransitionOperation(40));
            AddButton(operationPanel, "RESUME", (s, e) => TransitionOperation(30));
            AddButton(operationPanel, "COMPLETE", (s, e) => TransitionOperation(50));
            AddButton(operationPanel, "TẢI ROUTING THEO LOT", LoadRoutingClick);
            _routingSteps.Width = 980; _routingSteps.Height = 160; _routingSteps.ReadOnly = true;
            _routingSteps.AllowUserToAddRows = false; _routingSteps.AllowUserToDeleteRows = false;
            _routingSteps.MultiSelect = false; _routingSteps.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _routingSteps.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _routingSteps.SelectionChanged += (s, e) =>
            {
                if (_routingSteps.SelectedRows.Count == 0) return;
                var row = _routingSteps.SelectedRows[0];
                _routingStepId.Text = Convert.ToString(row.Cells["RoutingStepId"].Value);
                _operationNo.Text = Convert.ToString(row.Cells["OperationNo"].Value);
            };
            operationPanel.Controls.Add(_routingSteps);
            AddButton(operationPanel, "LỊCH SỬ LOT", (s, e) => OpenTimeline("OPERATION", _operationLotId));
            AddField(operationPanel, "InputQty", _inputQty);
            AddField(operationPanel, "GoodQty", _opGoodQty);
            AddField(operationPanel, "NgQty", _opNgQty);
            AddField(operationPanel, "InputSlotId (bắt buộc nếu công đoạn trước VIA_WIP)", _inputSlot);
            AddField(operationPanel, "OutputSlotId (bắt buộc nếu công đoạn này VIA_WIP)", _outputSlot);
            AddButton(operationPanel, "XÁC NHẬN HOÀN THÀNH CÔNG ĐOẠN", CompleteOperationClick);
            AddField(operationPanel, "Số sản phẩm cần chuẩn bị tem", _labelQty);
            AddButton(operationPanel, "1. CHUẨN BỊ / CẤP TEM", PrepareLabelsClick);
            AddButton(operationPanel, "2. IN TEM CÔNG ĐOẠN", PrintLabelsClick);
            _labels.Width = 980;
            _labels.Height = 190;
            _labels.ReadOnly = true;
            _labels.AllowUserToAddRows = false;
            _labels.AllowUserToDeleteRows = false;
            _labels.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            operationPanel.Controls.Add(_labels);
            AddField(operationPanel, "Quét QR tem MES hoặc LabelCode để xác nhận dán tem", _labelScan);
            AddButton(operationPanel, "XÁC NHẬN ĐÃ DÁN TEM", ApplyLabelClick);
            AddField(operationPanel, "Lý do hủy tem chưa sử dụng", _labelVoidReason);
            AddButton(operationPanel, "HỦY TEM CHƯA DÙNG", VoidLabelClick);
            operationTab.Controls.Add(operationPanel);

            var consumePanel = Panel();
            var loadMaterials = new Button { Text = "TẢI LINH KIỆN THEO LỆNH", AutoSize = true, Height = 34 };
            loadMaterials.Click += LoadMaterialsClick;
            consumePanel.Controls.Add(loadMaterials);
            _materials.Width = 980;
            _materials.Height = 190;
            _materials.ReadOnly = true;
            _materials.AllowUserToAddRows = false;
            _materials.MultiSelect = false;
            _materials.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _materials.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _materials.SelectionChanged += (s, e) =>
            {
                if (_materials.SelectedRows.Count > 0)
                    _materialLineId.Text = Convert.ToString(_materials.SelectedRows[0].Cells["ProductionOrderMaterialId"].Value);
            };
            consumePanel.Controls.Add(_materials);
            AddField(consumePanel, "ProductionOrderMaterialId", _materialLineId);
            AddField(consumePanel, "ProductionLotId", _consumeProductionLotId);
            AddField(consumePanel, "MaterialLotId (LOT linh kiện)", _componentLotId);
            AddField(consumePanel, "FromSlotId (PART hoặc WIP)", _consumeSlot);
            AddField(consumePanel, "Số lượng tiêu hao thực tế", _consumeQty);
            AddButton(consumePanel, "GHI NHẬN TIÊU HAO + GENEALOGY", ConsumeClick);
            consumeTab.Controls.Add(consumePanel);

            tabs.TabPages.Add(orderTab);
            tabs.TabPages.Add(operationTab);
            tabs.TabPages.Add(consumeTab);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Text = "Lệnh/LOT/công đoạn/tiêu hao đều ghi vào MES; DIRECT không tạo tồn WIP, VIA_WIP dùng ledger chung.";
            root.Controls.Add(tabs, 0, 0);
            root.Controls.Add(_status, 0, 1);
            Controls.Add(root);
        }

        private static FlowLayoutPanel Panel()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), WrapContents = true };
        }

        private static void AddField(FlowLayoutPanel panel, string caption, TextBox input)
        {
            var group = new Panel { Width = 320, Height = 58, Margin = new Padding(5) };
            group.Controls.Add(new Label { Text = caption, Dock = DockStyle.Top, Height = 22 });
            input.Dock = DockStyle.Bottom;
            input.Width = 300;
            group.Controls.Add(input);
            panel.Controls.Add(group);
        }

        private static void AddButton(FlowLayoutPanel panel, string caption, EventHandler handler)
        {
            var button = new Button { Text = caption, AutoSize = true, Height = 34, Margin = new Padding(8, 18, 8, 8) };
            button.Click += handler;
            panel.Controls.Add(button);
        }

        private static long LongValue(TextBox input, string name)
        {
            long value;
            if (!long.TryParse(input.Text.Trim(), out value) || value <= 0)
                throw new InvalidOperationException(name + " phải là số nguyên dương.");
            return value;
        }

        private static decimal DecimalValue(TextBox input, string name, bool allowZero)
        {
            decimal value;
            if (!decimal.TryParse(input.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
                value < 0 || (!allowZero && value == 0))
                throw new InvalidOperationException(name + " không hợp lệ.");
            return value;
        }

        private static int? OptionalSlot(TextBox input)
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return null;
            int value;
            if (!int.TryParse(input.Text.Trim(), out value) || value <= 0)
                throw new InvalidOperationException("SlotId phải là số nguyên dương hoặc để trống.");
            return value;
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
                    _productionDate.Value.ToUniversalTime(), DecimalValue(_goodQty, "GoodQty", false),
                    DecimalValue(_ngQty, "NgQty", true), _finished.Checked, Environment.UserName);
                if (result.Rows.Count > 0)
                {
                    _lotId.Text = Convert.ToString(result.Rows[0]["ProductionLotId"]);
                    _operationLotId.Text = _lotId.Text;
                    _consumeProductionLotId.Text = _lotId.Text;
                    return "Đã tạo LOT " + Convert.ToString(result.Rows[0]["LotNo"]) + ".";
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
                return "Đã ghi nhận hoàn thành công đoạn và bàn giao theo cấu hình DIRECT/VIA_WIP.";
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
                return "Đã cấp " + _labels.Rows.Count.ToString("N0") +
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
                return "Đã tải danh sách linh kiện theo BOM của lệnh sản xuất.";
            });
        }

        private void ConsumeClick(object sender, EventArgs e)
        {
            Run(() =>
            {
                _service.ConsumeMaterial(LongValue(_materialLineId, "ProductionOrderMaterialId"),
                    LongValue(_consumeProductionLotId, "ProductionLotId"), LongValue(_componentLotId, "MaterialLotId"),
                    Convert.ToInt32(LongValue(_consumeSlot, "FromSlotId")),
                    DecimalValue(_consumeQty, "Quantity", false), Environment.UserName);
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

        private void OpenTimeline(string entityType, TextBox idInput)
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
                MessageBox.Show(this, ex.Message, "Điều hành sản xuất MES",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
