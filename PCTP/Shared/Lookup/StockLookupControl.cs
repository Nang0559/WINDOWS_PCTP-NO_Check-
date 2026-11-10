using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shared.Lookup
{
    /// <summary>
    /// Reusable stock lookup with two isolated modes:
    /// item code suggestions (PART/NAME) and free-text LOT/QR input.
    /// Repository queries and result presentation remain the responsibility of the host form.
    ///
    /// Optional date-range block (<see cref="ShowDateRange"/>, off by default so existing hosts are unchanged):
    /// a checkbox plus three radio options — production date / delivery date / defect-document date —
    /// and a from/to range. The control only COLLECTS the choice; <see cref="GetCriteria"/> snapshots it
    /// and the host applies it (see StockLookupFilter) so every form interprets the dates identically.
    /// </summary>
    public sealed class StockLookupControl : UserControl
    {
        private readonly TabControl _tabs = new TabControl();
        private readonly TabPage _itemPage = new TabPage("Tìm theo mã");
        private readonly TabPage _lotQrPage = new TabPage("Tìm theo LOT / QR");
        private readonly GridLookUpEdit _itemEditor = new GridLookUpEdit();
        private readonly TextEdit _lotQrEditor = new TextEdit();
        private readonly SimpleButton _itemSearchButton = new SimpleButton();
        private readonly SimpleButton _lotQrSearchButton = new SimpleButton();

        // Khối lọc theo khoảng ngày (tuỳ chọn).
        private const int DateBlockHeight = 58;
        private readonly Panel _datePanel = new Panel();
        private readonly CheckBox _chkDate = new CheckBox();
        private readonly RadioButton _radProduction = new RadioButton();
        private readonly RadioButton _radDelivery = new RadioButton();
        private readonly RadioButton _radDefectDoc = new RadioButton();
        private readonly DateEdit _dateFrom = new DateEdit();
        private readonly DateEdit _dateTo = new DateEdit();
        private bool _showDateRange;

        public event EventHandler SearchRequested;
        public event EventHandler SelectionCleared;

        public StockLookupControl() : this(true) { }

        /// <summary>
        /// Set enableLotQrTab=false for lookup screens that support item-code filtering only.
        /// </summary>
        public StockLookupControl(bool enableLotQrTab)
        {
            MinimumSize = new Size(360, 74);
            Height = 78;
            _tabs.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(_itemPage);
            if (enableLotQrTab)
                _tabs.TabPages.Add(_lotQrPage);
            BuildItemTab();
            if (enableLotQrTab)
                BuildLotQrTab();
            BuildDatePanel();
            Controls.Add(_tabs);
            Controls.Add(_datePanel);
            _tabs.SelectedIndexChanged += (s, e) =>
            {
                ClearEditors();
                SelectionCleared?.Invoke(this, EventArgs.Empty);
            };
        }

        public bool IsItemSearch { get { return _tabs.SelectedTab == _itemPage; } }

        /// <summary>
        /// Hiện khối lọc theo khoảng ngày (mặc định ẩn). Bật thì control cao hơn
        /// (~136px): form chứa phải chừa đủ chiều cao.
        /// </summary>
        public bool ShowDateRange
        {
            get { return _showDateRange; }
            set
            {
                _showDateRange = value;
                _datePanel.Visible = value;
                int tabsHeight = 78;
                MinimumSize = new Size(360, value ? tabsHeight + DateBlockHeight - 4 : 74);
                Height = value ? tabsHeight + DateBlockHeight : tabsHeight;
            }
        }

        /// <summary>Đang bật lọc ngày (ô "Lọc theo ngày" được tick).</summary>
        public bool DateRangeEnabled
        {
            get { return _showDateRange && _chkDate.Checked; }
        }

        public StockLookupDateMode DateMode
        {
            get
            {
                if (_radDelivery.Checked) return StockLookupDateMode.NgayXuatHang;
                if (_radDefectDoc.Checked) return StockLookupDateMode.NgayLapPhieuXuLy;
                return StockLookupDateMode.NgaySanXuat;
            }
        }

        public DateTime DateFrom
        {
            get { return ((_dateFrom.EditValue as DateTime?) ?? DateTime.Today.AddDays(-30)).Date; }
        }

        public DateTime DateTo
        {
            get { return ((_dateTo.EditValue as DateTime?) ?? DateTime.Today).Date; }
        }

        public void SetDateRange(StockLookupDateMode mode, DateTime from, DateTime to, bool enabled)
        {
            _radProduction.Checked = mode == StockLookupDateMode.NgaySanXuat;
            _radDelivery.Checked = mode == StockLookupDateMode.NgayXuatHang;
            _radDefectDoc.Checked = mode == StockLookupDateMode.NgayLapPhieuXuLy;
            _dateFrom.EditValue = from.Date;
            _dateTo.EditValue = to.Date;
            _chkDate.Checked = enabled;
            SyncDateEnabledState();
        }

        /// <summary>
        /// Ảnh chụp toàn bộ tiêu chí hiện tại (tab, chữ nhập, mã đã chọn, khoảng ngày + kiểu ngày).
        /// Khoảng ngày bị nhập ngược (Từ &gt; Đến) được tự đảo lại.
        /// </summary>
        public StockLookupCriteria GetCriteria()
        {
            DateTime from = DateFrom;
            DateTime to = DateTo;
            if (from > to)
            {
                DateTime t = from; from = to; to = t;
            }

            return new StockLookupCriteria
            {
                IsItemSearch = IsItemSearch,
                Text = InputText,
                ItemCode = IsItemSearch ? SelectedItemCode : string.Empty,
                DateEnabled = DateRangeEnabled,
                DateMode = DateMode,
                From = from,
                To = to
            };
        }

        public string InputText
        {
            get
            {
                // DisplayMember is the item-code field, so Text is always the actual
                // search key (typed text or selected code). Do not prefer EditValue:
                // GridLookUpEdit can retain an old EditValue while the user edits text.
                string value = IsItemSearch ? _itemEditor.Text : _lotQrEditor.Text;
                return (value ?? string.Empty).Trim();
            }
        }

        public string SelectedItemCode
        {
            get { return (Convert.ToString(_itemEditor.EditValue) ?? string.Empty).Trim(); }
        }

        public void SetItemSuggestions(DataTable items)
        {
            SetItemSuggestions(items, "PART", "NAME");
        }

        /// <summary>
        /// Binds item suggestions from any lookup schema. The selected value is always
        /// the item-code field; the popup searches a combined code/name display value.
        /// Additional source columns (for example stock quantity) remain visible.
        /// </summary>
        public void SetItemSuggestions(DataTable items, string codeField, string nameField)
        {
            if (items == null)
            {
                _itemEditor.Properties.DataSource = null;
                _itemEditor.EditValue = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(codeField) || !items.Columns.Contains(codeField))
                throw new ArgumentException("Lookup data must contain the item-code column.", nameof(codeField));

            if (!string.IsNullOrWhiteSpace(nameField) && !items.Columns.Contains(nameField))
                nameField = null;

            DataTable lookup = items.Copy();
            if (!lookup.Columns.Contains("DisplayText"))
                lookup.Columns.Add("DisplayText", typeof(string));

            foreach (DataRow row in lookup.Rows)
            {
                string code = Convert.ToString(row[codeField])?.Trim() ?? string.Empty;
                string name = string.IsNullOrWhiteSpace(nameField)
                    ? string.Empty
                    : Convert.ToString(row[nameField])?.Trim() ?? string.Empty;
                row["DisplayText"] = string.IsNullOrEmpty(name) ? code : code + " — " + name;
            }

            _itemEditor.Properties.DataSource = lookup;
            // Keep the editor text equal to the code. The popup still shows code/name
            // as separate columns, while the host form receives a clean search key.
            _itemEditor.Properties.DisplayMember = codeField;
            _itemEditor.Properties.ValueMember = codeField;
            _itemEditor.Properties.NullText = "Gõ mã hoặc tên hàng để xem đề xuất...";
            _itemEditor.Properties.TextEditStyle = TextEditStyles.Standard;
            _itemEditor.Properties.PopupFilterMode = PopupFilterMode.Contains;
            _itemEditor.Properties.ImmediatePopup = true;
            _itemEditor.Properties.SearchMode = DevExpress.XtraEditors.Repository.GridLookUpSearchMode.AutoSearch;
            _itemEditor.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
            _itemEditor.Properties.View.OptionsView.ShowGroupPanel = false;
            _itemEditor.Properties.View.OptionsView.ShowAutoFilterRow = true;
            _itemEditor.Properties.View.OptionsBehavior.Editable = false;
            _itemEditor.Properties.View.Columns.Clear();
            _itemEditor.Properties.View.Columns.AddVisible(codeField, "Mã hàng");

            if (!string.IsNullOrWhiteSpace(nameField))
                _itemEditor.Properties.View.Columns.AddVisible(nameField, "Tên hàng");

            foreach (DataColumn column in lookup.Columns)
            {
                if (column.ColumnName == codeField ||
                    column.ColumnName == nameField ||
                    column.ColumnName == "DisplayText")
                    continue;

                string caption = column.ColumnName == "StockQuantity"
                    ? "Tồn khả dụng"
                    : column.ColumnName;
                _itemEditor.Properties.View.Columns.AddVisible(column.ColumnName, caption);
            }
        }

        public void SetLotQrPlaceholder(string text)
        {
            _lotQrEditor.Properties.NullValuePrompt = string.IsNullOrWhiteSpace(text)
                ? "Nhập LOT 13/20 ký tự hoặc quét QR 4/6 trường..."
                : text;
        }

        public void SelectItemTab()
        {
            _tabs.SelectedTab = _itemPage;
            FocusInput();
        }

        public void SelectLotQrTab()
        {
            _tabs.SelectedTab = _lotQrPage;
            FocusInput();
        }

        public void FocusInput()
        {
            if (IsItemSearch)
            {
                _itemEditor.Focus();
                _itemEditor.SelectAll();
            }
            else
            {
                _lotQrEditor.Focus();
                _lotQrEditor.SelectAll();
            }
        }

        public void SetInputText(string value)
        {
            string text = value ?? string.Empty;
            if (IsItemSearch)
            {
                _itemEditor.EditValue = null;
                _itemEditor.Text = text;
            }
            else
            {
                _lotQrEditor.Text = text;
            }
        }

        public void ClearInput() { ClearEditors(); }

        private void BuildItemTab()
        {
            var layout = CreateInputLayout();
            _itemEditor.Dock = DockStyle.Fill;
            _itemEditor.Properties.NullValuePrompt = "Gõ mã hàng...";
            _itemEditor.KeyDown += (s, e) => HandleEnter(e);
            _itemSearchButton.Text = "Tra cứu mã";
            _itemSearchButton.Dock = DockStyle.Fill;
            _itemSearchButton.Click += (s, e) => RequestSearch();
            layout.Controls.Add(_itemEditor, 0, 0);
            layout.Controls.Add(_itemSearchButton, 1, 0);
            _itemPage.Controls.Add(layout);
        }

        private void BuildLotQrTab()
        {
            var layout = CreateInputLayout();
            _lotQrEditor.Dock = DockStyle.Fill;
            _lotQrEditor.Properties.NullValuePrompt = "Nhập LOT 13/20 ký tự hoặc quét QR 4/6 trường...";
            _lotQrEditor.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
            _lotQrEditor.KeyDown += (s, e) => HandleEnter(e);
            _lotQrSearchButton.Text = "Tra cứu LOT / QR";
            _lotQrSearchButton.Dock = DockStyle.Fill;
            _lotQrSearchButton.Click += (s, e) => RequestSearch();
            layout.Controls.Add(_lotQrEditor, 0, 0);
            layout.Controls.Add(_lotQrSearchButton, 1, 0);
            _lotQrPage.Controls.Add(layout);
        }

        private void BuildDatePanel()
        {
            _datePanel.Dock = DockStyle.Bottom;
            _datePanel.Height = DateBlockHeight;
            _datePanel.Visible = false;

            var row1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 28,
                WrapContents = false,
                Padding = new Padding(3, 2, 0, 0)
            };

            _chkDate.Text = "Lọc theo ngày";
            _chkDate.AutoSize = true;
            _chkDate.Margin = new Padding(3, 4, 12, 0);
            _chkDate.CheckedChanged += (s, e) => SyncDateEnabledState();

            ConfigureRadio(_radProduction, "Ngày sản xuất", true);
            ConfigureRadio(_radDelivery, "Ngày xuất hàng", false);
            ConfigureRadio(_radDefectDoc, "Ngày lập phiếu xử lý hàng lỗi", false);

            row1.Controls.Add(_chkDate);
            row1.Controls.Add(_radProduction);
            row1.Controls.Add(_radDelivery);
            row1.Controls.Add(_radDefectDoc);

            var row2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(3, 0, 0, 0)
            };

            ConfigureDateEdit(_dateFrom, DateTime.Today.AddDays(-30));
            ConfigureDateEdit(_dateTo, DateTime.Today);

            row2.Controls.Add(new Label { Text = "Từ ngày", AutoSize = true, Margin = new Padding(3, 6, 4, 0) });
            row2.Controls.Add(_dateFrom);
            row2.Controls.Add(new Label { Text = "đến ngày", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
            row2.Controls.Add(_dateTo);

            _datePanel.Controls.Add(row2);
            _datePanel.Controls.Add(row1);

            SyncDateEnabledState();
        }

        private void ConfigureRadio(RadioButton radio, string text, bool isChecked)
        {
            radio.Text = text;
            radio.AutoSize = true;
            radio.Checked = isChecked;
            radio.Margin = new Padding(3, 4, 10, 0);
        }

        private void ConfigureDateEdit(DateEdit edit, DateTime value)
        {
            edit.Width = 120;
            edit.Margin = new Padding(3, 2, 3, 0);
            edit.EditValue = value;
            edit.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            edit.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            edit.Properties.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            edit.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            edit.Properties.Mask.EditMask = "dd/MM/yyyy";
            edit.Properties.Mask.UseMaskAsDisplayFormat = true;
            edit.KeyDown += (s, e) => HandleEnter(e);
        }

        // Chưa tick "Lọc theo ngày" thì các lựa chọn bên dưới bị khóa (nhìn là biết đang không lọc).
        private void SyncDateEnabledState()
        {
            bool on = _chkDate.Checked;
            _radProduction.Enabled = on;
            _radDelivery.Enabled = on;
            _radDefectDoc.Enabled = on;
            _dateFrom.Enabled = on;
            _dateTo.Enabled = on;
        }

        private static TableLayoutPanel CreateInputLayout()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(5, 8, 5, 5),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return layout;
        }

        private void HandleEnter(KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            RequestSearch();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void RequestSearch() { SearchRequested?.Invoke(this, EventArgs.Empty); }

        private void ClearEditors()
        {
            _itemEditor.EditValue = null;
            _itemEditor.Text = string.Empty;
            _lotQrEditor.EditValue = null;
        }
    }
}
