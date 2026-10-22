using DevExpress.XtraEditors;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using PCTP.Shell.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    [DesignerCategory("Code")]
    public sealed class FormBaoCaoTraceability : WmsTitleBarForm
    {
        private readonly IQrTraceQuery _qrQuery;
        private readonly ILotTraceQuery _lotQuery;
        private readonly ICustomerDeliveryQuery _customerQuery;
        private readonly TextBox _txtQr = new TextBox();
        private readonly TextBox _txtCustomerLabel = new TextBox();
        private readonly TextBox _txtLot = new TextBox();
        private readonly LookUpEdit _txtPart = new LookUpEdit();
        private readonly LookUpEdit _txtCustomer = new LookUpEdit();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly DataGridView _grid = new DataGridView();
        private readonly DataGridView _lotGrid = new DataGridView();
        private readonly Label _status = new Label();
        private List<DeliveryTraceRow> _currentRows = new List<DeliveryTraceRow>();

        public FormBaoCaoTraceability() : this(new DeliveryTraceReadService()) { }

        public FormBaoCaoTraceability(string quickSearch) : this(new DeliveryTraceReadService())
        {
            ApplyQuickSearch(quickSearch);
        }

        public FormBaoCaoTraceability(DeliveryTraceReadService query)
        {
            if (query == null) throw new ArgumentNullException("query");
            _qrQuery = query;
            _lotQuery = query;
            _customerQuery = query;
            Text = "Báo cáo - Tra cứu truy xuất giao hàng";
            ClientSize = new Size(1250, 720);
            StartPosition = FormStartPosition.CenterParent;
            BuildUi();
            Shown += async (s, e) => await LoadLookupsAsync();
        }

        private void ApplyQuickSearch(string value)
        {
            string keyword = (value ?? string.Empty).Trim();
            if (keyword.Length == 0) return;
            if (keyword.StartsWith("LOT:", StringComparison.OrdinalIgnoreCase))
                _txtLot.Text = keyword.Substring(4).Trim();
            else if (keyword.StartsWith("QR:", StringComparison.OrdinalIgnoreCase))
                _txtQr.Text = keyword.Substring(3).Trim();
            else if (keyword.StartsWith("PART:", StringComparison.OrdinalIgnoreCase))
                _txtPart.Text = keyword.Substring(5).Trim();
            else
                _txtQr.Text = keyword;
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                ToggleSearch(false);
                _status.Text = "Đang tải danh sách mã hàng/khách hàng...";

                Task<IReadOnlyList<string>> itemTask = _customerQuery.GetItemCodesAsync(CancellationToken.None);
                Task<IReadOnlyList<string>> customerTask = _customerQuery.GetCustomersAsync(CancellationToken.None);
                await Task.WhenAll(itemTask, customerTask);

                ConfigureLookup(_txtPart, itemTask.Result, "Mã hàng");
                ConfigureLookup(_txtCustomer, customerTask.Result, "Khách hàng");
                _status.Text = "Sẵn sàng";
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được danh sách mã hàng/khách hàng.";
                MessageBox.Show(this, ex.Message, "Báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleSearch(true);
            }
        }

        private static void ConfigureLookup(LookUpEdit lookup, IReadOnlyList<string> values, string nullText)
        {
            lookup.Properties.NullText = nullText;
            lookup.Properties.DataSource = values == null ? new List<string>() : values.ToList();
            lookup.Properties.DisplayMember = string.Empty;
            lookup.Properties.ValueMember = string.Empty;
            lookup.Properties.ShowHeader = false;
            lookup.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            lookup.Properties.AutoHeight = false;
        }

        private void BuildUi()
        {
            // Use one explicit root layout. WmsTitleBarForm reparents controls into its
            // content host on Load, so sibling Dock=Top/Dock=Fill controls can be
            // reordered and the fill panel can cover the search captions.
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = SystemColors.Control,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var search = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 174,
                ColumnCount = 6,
                RowCount = 4,
                Padding = new Padding(8),
                Margin = Padding.Empty,
                BackColor = SystemColors.Control,
                ForeColor = Color.FromArgb(35, 35, 35),
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };

            for (int i = 0; i < 6; i++)
                search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6667f));

            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            search.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            AddField(search, "QR / Carton", _txtQr, 0);
            AddField(search, "Customer label", _txtCustomerLabel, 1);
            AddField(search, "LOT", _txtLot, 2);
            AddField(search, "Mã hàng", _txtPart, 3);
            AddField(search, "Khách hàng", _txtCustomer, 4);

            _from.Format = DateTimePickerFormat.Short;
            _to.Format = DateTimePickerFormat.Short;
            _from.Value = DateTime.Today.AddDays(-30);
            _to.Value = DateTime.Today;

            search.Controls.Add(new Label
            {
                Text = "From",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Margin = new Padding(3, 0, 3, 0)
            }, 0, 2);
            _from.Dock = DockStyle.Fill;
            _from.Margin = new Padding(3, 2, 3, 2);
            search.Controls.Add(_from, 1, 2);

            search.Controls.Add(new Label
            {
                Text = "To",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Margin = new Padding(3, 0, 3, 0)
            }, 2, 2);
            _to.Dock = DockStyle.Fill;
            _to.Margin = new Padding(3, 2, 3, 2);
            search.Controls.Add(_to, 3, 2);

            var searchButton = new SimpleButton { Text = "Tra cứu", Dock = DockStyle.Fill, Margin = new Padding(3, 2, 3, 2) };
            searchButton.Click += SearchButton_Click;
            search.Controls.Add(searchButton, 4, 2);

            var clearButton = new SimpleButton { Text = "Xóa điều kiện", Dock = DockStyle.Fill, Margin = new Padding(3, 2, 3, 2) };
            clearButton.Click += ClearButton_Click;
            search.Controls.Add(clearButton, 5, 2);

            _status.Text = "Đang chuẩn bị...";
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.FromArgb(35, 35, 35);
            _status.BackColor = Color.Transparent;
            search.Controls.Add(_status, 0, 3);
            search.SetColumnSpan(_status, 6);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 330,
                Margin = Padding.Empty,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            ConfigureGrid(_grid);
            ConfigureGrid(_lotGrid);
            _grid.CellDoubleClick += DeliveryGrid_CellDoubleClick;
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_lotGrid);

            root.Controls.Add(search, 0, 0);
            root.Controls.Add(split, 0, 1);
            Controls.Add(root);
        }

        private static void AddField(TableLayoutPanel panel, string caption, Control editor, int column)
        {
            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                AutoEllipsis = true,
                Margin = new Padding(3, 0, 3, 0)
            };

            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(3, 2, 3, 3);
            panel.Controls.Add(label, column, 0);
            panel.Controls.Add(editor, column, 1);
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.ForeColor = Color.FromArgb(35, 35, 35);
            grid.GridColor = Color.FromArgb(220, 220, 220);
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersVisible = true;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 230, 216);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(35, 35, 35);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(35, 35, 35);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
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
                string part = GetLookupText(_txtPart);
                string customer = GetLookupText(_txtCustomer);

                IReadOnlyList<DeliveryTraceRow> rows;
                if (!string.IsNullOrWhiteSpace(lot))
                    rows = await _lotQuery.SearchAsync(lot, part, customer, from, to, CancellationToken.None);
                else if (!string.IsNullOrWhiteSpace(customer) && string.IsNullOrWhiteSpace(qr) && string.IsNullOrWhiteSpace(customerLabel))
                    rows = await _customerQuery.SearchAsync(customer, part, from, to, CancellationToken.None);
                else
                    rows = await _qrQuery.SearchAsync(qr, customerLabel, part, customer, from, to, CancellationToken.None);

                _currentRows = rows == null ? new List<DeliveryTraceRow>() : rows.ToList();
                _grid.DataSource = new BindingList<DeliveryTraceRow>(_currentRows);
                _lotGrid.DataSource = null;
                _status.Text = "Tìm thấy " + _currentRows.Count + " dòng.";
            }
            catch (Exception ex)
            {
                _status.Text = "Lỗi tra cứu.";
                XtraMessageBox.Show(this, ex.Message, "Báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleSearch(true);
            }
        }

        private static string GetLookupText(LookUpEdit lookup)
        {
            object value = lookup.EditValue;
            if (value != null && value != DBNull.Value)
                return Convert.ToString(value).Trim();
            return (lookup.Text ?? string.Empty).Trim();
        }

        private async void DeliveryGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _currentRows.Count) return;

            DeliveryTraceRow row = _currentRows[e.RowIndex];
            try
            {
                IReadOnlyList<DeliveryLotTraceRow> lots = await _qrQuery.GetLotsAsync(
                    row.DeliveryKey,
                    row.QRCode,
                    CancellationToken.None);

                _lotGrid.DataSource = new BindingList<DeliveryLotTraceRow>(
                    lots == null ? new List<DeliveryLotTraceRow>() : lots.ToList());

                _status.Text = "DeliveryKey: " + row.DeliveryKey +
                                " | " + _lotGrid.Rows.Count + " LOT.";
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            _txtQr.Clear();
            _txtCustomerLabel.Clear();
            _txtLot.Clear();
            _txtPart.EditValue = null;
            _txtPart.Text = string.Empty;
            _txtCustomer.EditValue = null;
            _txtCustomer.Text = string.Empty;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _grid.CellDoubleClick -= DeliveryGrid_CellDoubleClick;
            base.Dispose(disposing);
        }
    }
}
