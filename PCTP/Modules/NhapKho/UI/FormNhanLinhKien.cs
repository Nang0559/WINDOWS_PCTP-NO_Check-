using PCTP.Infrastructure.Security;
using PCTP.Modules.NhapKho.ComponentReceiving;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Modules.NhapKho.UI
{
    /// <summary>Receive supplier/component QR labels into a MES-mapped warehouse Slot.</summary>
    public sealed class FormNhanLinhKien : Form
    {
        private readonly IComponentReceivingService _service;
        private readonly TextBox _site = new TextBox();
        private readonly TextBox _receiptNo = new TextBox();
        private readonly TextBox _qr = new TextBox();
        private readonly NumericUpDown _quantity = new NumericUpDown();
        private readonly ComboBox _slot = new ComboBox();
        private readonly IComponentSlotLookupService _slotLookup;
        private readonly TextBox _receivedBy = new TextBox();
        private readonly Button _receive = new Button();
        private readonly Label _status = new Label();
        private string _idempotencyKey = Guid.NewGuid().ToString("N");

        public FormNhanLinhKien()
            : this(new ComponentReceivingService(new ComponentReceivingRepository()), new ComponentSlotLookupService(new ComponentSlotLookupRepository()))
        {
        }

        public FormNhanLinhKien(IComponentReceivingService service, IComponentSlotLookupService slotLookup = null)
        {
            AuthorizationGuard.Demand("WAREHOUSE.RECEIPT", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _slotLookup = slotLookup ?? new ComponentSlotLookupService(new ComponentSlotLookupRepository());
            Text = "Nhận linh kiện về kho";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 500);
            Size = new Size(820, 560);
            Font = new Font("Segoe UI", 10F);
            BuildLayout();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2,
                RowCount = 8, AutoSize = false
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            AddField(root, 0, "Site", _site);
            AddField(root, 1, "Số phiếu nhận", _receiptNo);
            AddField(root, 2, "QR tem linh kiện", _qr);
            AddField(root, 3, "Số lượng nhận", _quantity);
            AddField(root, 4, "Slot nhập kho PART", _slot);
            AddField(root, 5, "Người nhận", _receivedBy);

            _quantity.DecimalPlaces = 3;
            _quantity.Maximum = 1000000000;
            _quantity.Minimum = 0.001M;
            _quantity.Value = 1;
            _slot.DropDownStyle = ComboBoxStyle.DropDownList;
            _site.Leave += (s, e) => LoadSlots();
            _receivedBy.Text = Environment.UserName;

            _receive.Text = "XÁC NHẬN NHẬN LINH KIỆN";
            _receive.Dock = DockStyle.Fill;
            _receive.Height = 42;
            _receive.Click += ReceiveClick;
            root.Controls.Add(_receive, 1, 6);

            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.DarkSlateGray;
            _status.AutoSize = false;
            root.Controls.Add(_status, 0, 7);
            root.SetColumnSpan(_status, 2);
            AcceptButton = _receive;
        }

        private static void AddField(TableLayoutPanel panel, int row, string caption, Control control)
        {
            var label = new Label
            {
                Text = caption, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            control.Dock = DockStyle.Fill;
            if (control is TextBox) ((TextBox)control).Margin = new Padding(3, 8, 3, 8);
            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        private void LoadSlots()
        {
            try
            {
                var slots = _slotLookup.GetActiveSlots(_site.Text);
                _slot.DataSource = slots;
                _slot.DisplayMember = "SlotDisplay";
                _slot.ValueMember = "SlotId";
                if (slots.Rows.Count == 0)
                    _status.Text = "Site chưa có Slot hoạt động được map sang MES Location.";
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được Slot: " + ex.Message;
            }
        }

        private void ReceiveClick(object sender, EventArgs e)
        {
            _receive.Enabled = false;
            try
            {
                var result = _service.Receive(new ComponentReceiptRequest
                {
                    SiteCode = _site.Text,
                    ReceiptNo = _receiptNo.Text,
                    QrPayload = _qr.Text,
                    Quantity = _quantity.Value,
                    TargetSlotId = Convert.ToInt32(_slot.SelectedValue),
                    ReceivedBy = _receivedBy.Text,
                    IdempotencyKey = _idempotencyKey
                });

                _status.ForeColor = Color.DarkGreen;
                _status.Text = string.Format(
                    "Đã nhận {0:N3}. Phiếu #{1}, dòng #{2}, LOT #{3}, giao dịch kho #{4}.",
                    result.ReceivedQty, result.ComponentReceiptId,
                    result.ComponentReceiptLineId, result.ComponentLotId,
                    result.InventoryTransactionId);

                _qr.Clear();
                _idempotencyKey = Guid.NewGuid().ToString("N");
                _qr.Focus();
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.DarkRed;
                _status.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Không thể nhận linh kiện",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _receive.Enabled = true;
            }
        }
    }
}