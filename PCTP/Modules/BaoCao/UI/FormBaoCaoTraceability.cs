using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    public sealed class FormBaoCaoTraceability : Form
    {
        private readonly IQrTraceQuery _qrQuery;
        private readonly ILotTraceQuery _lotQuery;
        private readonly ICustomerDeliveryQuery _customerQuery;

        private readonly TextBox _txtQr = new TextBox();
        private readonly TextBox _txtCustomerLabel = new TextBox();
        private readonly TextBox _txtLot = new TextBox();
        private readonly TextBox _txtPart = new TextBox();
        private readonly TextBox _txtCustomer = new TextBox();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly DataGridView _grid = new DataGridView();
        private readonly DataGridView _lotGrid = new DataGridView();
        private readonly Label _status = new Label();

        private List<DeliveryTraceRow> _currentRows = new List<DeliveryTraceRow>();

        public FormBaoCaoTraceability() : this(new DeliveryTraceQueryService()) { }

        public FormBaoCaoTraceability(DeliveryTraceQueryService query)
        {
            if (query == null)
                throw new ArgumentNullException("query");

            _qrQuery = query;
            _lotQuery = query;
            _customerQuery = query;

            Text = "BaoCao - Tra cứu truy xuất giao hàng";
            Width = 1250;
            Height = 720;
            StartPosition = FormStartPosition.CenterParent;

            BuildUi();
        }

        private void BuildUi()
        {
            var search = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 165,
                ColumnCount = 6,
                RowCount = 4,
                Padding = new Padding(8)
            };

            for (int i = 0; i < 6; i++)
                search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6667f));

            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            search.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            AddField(search, "QR / Carton", _txtQr, 0);
            AddField(search, "Customer label", _txtCustomerLabel, 1);
            AddField(search, "LOT", _txtLot, 2);
            AddField(search, "PartNo", _txtPart, 3);
            AddField(search, "Customer", _txtCustomer, 4);

            _from.Format = DateTimePickerFormat.Short;
            _to.Format = DateTimePickerFormat.Short;
            _from.Value = DateTime.Today.AddDays(-30);
            _to.Value = DateTime.Today;

            search.Controls.Add(new Label { Text = "From", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 2);
            search.Controls.Add(_from, 1, 2);
            search.Controls.Add(new Label { Text = "To", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 2, 2);
            search.Controls.Add(_to, 3, 2);

            var searchButton = new Button { Text = "Tra cứu", Dock = DockStyle.Fill };
            searchButton.Click += SearchButton_Click;
            search.Controls.Add(searchButton, 4, 2);

            var clearButton = new Button { Text = "Xóa điều kiện", Dock = DockStyle.Fill };
            clearButton.Click += ClearButton_Click;
            search.Controls.Add(clearButton, 5, 2);

            _status.Text = "Sẵn sàng";
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            search.Controls.Add(_status, 0, 3);
            search.SetColumnSpan(_status, 6);

            Controls.Add(search);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 330
            };

            ConfigureGrid(_grid);
            ConfigureGrid(_lotGrid);
            _grid.CellDoubleClick += DeliveryGrid_CellDoubleClick;

            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_lotGrid);
            Controls.Add(split);
            split.BringToFront();
        }

        private static void AddField(TableLayoutPanel panel, string caption, Control editor, int column)
        {
            panel.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            }, column, 0);

            panel.Controls.Add(editor, column, 1);
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            try
            {
                ToggleSearch(false);
                _status.Text = "Đang tra cứu...";

                DateTime? from = _from.Value.Date;
                DateTime? to = _to.Value.Date;
                string qr = _txtQr.Text.Trim();
                string customerLabel = _txtCustomerLabel.Text.Trim();
                string lot = _txtLot.Text.Trim();
                string part = _txtPart.Text.Trim();
                string customer = _txtCustomer.Text.Trim();

                IReadOnlyList<DeliveryTraceRow> rows;

                if (!string.IsNullOrWhiteSpace(lot))
                {
                    rows = await _lotQuery.SearchAsync(lot, part, customer, from, to, CancellationToken.None);
                }
                else if (!string.IsNullOrWhiteSpace(customer)
                         && string.IsNullOrWhiteSpace(qr)
                         && string.IsNullOrWhiteSpace(customerLabel))
                {
                    rows = await _customerQuery.SearchAsync(customer, part, from, to, CancellationToken.None);
                }
                else
                {
                    rows = await _qrQuery.SearchAsync(qr, customerLabel, part, from, to, CancellationToken.None);
                }

                _currentRows = rows == null ? new List<DeliveryTraceRow>() : rows.ToList();
                _grid.DataSource = new BindingList<DeliveryTraceRow>(_currentRows);
                _lotGrid.DataSource = null;
                _status.Text = "Tìm thấy " + _currentRows.Count + " dòng.";
            }
            catch (Exception ex)
            {
                _status.Text = "Lỗi tra cứu.";
                MessageBox.Show(this, ex.Message, "BaoCao", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleSearch(true);
            }
        }

        private async void DeliveryGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _currentRows.Count)
                return;

            DeliveryTraceRow row = _currentRows[e.RowIndex];

            try
            {
                IReadOnlyList<DeliveryLotTraceRow> lots = await _qrQuery.GetLotsAsync(
                    row.DeliveryKey, row.QRCode, CancellationToken.None);

                _lotGrid.DataSource = new BindingList<DeliveryLotTraceRow>(
                    lots == null ? new List<DeliveryLotTraceRow>() : lots.ToList());

                _status.Text = "DeliveryKey: " + row.DeliveryKey + " | " + _lotGrid.Rows.Count + " LOT.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaoCao", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            _txtQr.Clear();
            _txtCustomerLabel.Clear();
            _txtLot.Clear();
            _txtPart.Clear();
            _txtCustomer.Clear();
            _from.Value = DateTime.Today.AddDays(-30);
            _to.Value = DateTime.Today;
            _grid.DataSource = null;
            _lotGrid.DataSource = null;
            _currentRows.Clear();
            _status.Text = "Sẵn sàng";
        }

        private void ToggleSearch(bool enabled)
        {
            _txtQr.Enabled = enabled;
            _txtCustomerLabel.Enabled = enabled;
            _txtLot.Enabled = enabled;
            _txtPart.Enabled = enabled;
            _txtCustomer.Enabled = enabled;
            _from.Enabled = enabled;
            _to.Enabled = enabled;
        }
    }
}
