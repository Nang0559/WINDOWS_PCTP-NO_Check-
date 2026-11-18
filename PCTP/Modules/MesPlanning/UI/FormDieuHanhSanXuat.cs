using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing.Printing;

namespace PCTP.Modules.MesPlanning.UI
{
    public sealed class FormDieuHanhSanXuat : Form
    {
        private readonly IProductionExecutionService _service;
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
        private readonly DataGridView _labels = new DataGridView();
        private readonly TextBox _labelQty = new TextBox(), _labelScan = new TextBox();
        private DataTable _printRows;
        private int _printIndex;

        public FormDieuHanhSanXuat()
            : this(new ProductionExecutionService(new ProductionExecutionRepository()))
        {
        }

        public FormDieuHanhSanXuat(IProductionExecutionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
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
            orderTab.Controls.Add(orderPanel);

            var operationPanel = Panel();
            AddField(operationPanel, "ProductionLotId", _operationLotId);
            AddField(operationPanel, "OperationNo", _operationNo);
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
            AddField(operationPanel, "Quét LabelCode để xác nhận dán tem", _labelScan);
            AddButton(operationPanel, "XÁC NHẬN ĐÃ DÁN TEM", ApplyLabelClick);
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
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return "Đã hủy lệnh in; chưa gửi dữ liệu in.";
                    _printRows = _service.PrintLabels(
                        LongValue(_operationLotId, "ProductionLotId"), _operationNo.Text,
                        Environment.UserName, true);
                    if (_printRows.Rows.Count == 0)
                        throw new InvalidOperationException("Không có tem hợp lệ để in.");
                    _printIndex = 0;
                    document.PrintPage += (s, args) =>
                    {
                        if (_printIndex >= _printRows.Rows.Count) { args.HasMorePages = false; return; }
                        var row = _printRows.Rows[_printIndex++];
                        using (var titleFont = new Font("Arial", 13, FontStyle.Bold))
                        using (var bodyFont = new Font("Arial", 9, FontStyle.Regular))
                        {
                            float y = 35;
                            args.Graphics.DrawString("FVN MES - PRODUCT LABEL", titleFont, Brushes.Black, 25, y);
                            y += 35;
                            args.Graphics.DrawString("LABEL: " + Convert.ToString(row["LabelCode"]), titleFont, Brushes.Black, 25, y);
                            y += 35;
                            args.Graphics.DrawString("LOT: " + Convert.ToString(row["LotNo"]), bodyFont, Brushes.Black, 25, y);
                            y += 24;
                            args.Graphics.DrawString("PROCESS: " + Convert.ToString(row["OperationNo"]) +
                                " - " + Convert.ToString(row["OperationName"]), bodyFont, Brushes.Black, 25, y);
                            y += 24;
                            args.Graphics.DrawString("QR PAYLOAD: " + Convert.ToString(row["QrPayload"]),
                                bodyFont, Brushes.Black, new RectangleF(25, y, 360, 90));
                            y += 100;
                            args.Graphics.DrawString("Each label identifies one product. Scan the QR payload with MES.",
                                bodyFont, Brushes.Black, 25, y);
                        }
                        args.HasMorePages = _printIndex < _printRows.Rows.Count;
                    };
                    document.Print();
                    _labels.DataSource = _printRows;
                    return "Đã gửi " + _printRows.Rows.Count.ToString("N0") +
                        " tem tới máy in. QR payload được in kèm dưới dạng dữ liệu văn bản.";
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
