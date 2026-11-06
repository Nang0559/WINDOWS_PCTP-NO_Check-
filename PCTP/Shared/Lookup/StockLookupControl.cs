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
            Controls.Add(_tabs);
            _tabs.SelectedIndexChanged += (s, e) =>
            {
                ClearEditors();
                SelectionCleared?.Invoke(this, EventArgs.Empty);
            };
        }

        public bool IsItemSearch { get { return _tabs.SelectedTab == _itemPage; } }

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
