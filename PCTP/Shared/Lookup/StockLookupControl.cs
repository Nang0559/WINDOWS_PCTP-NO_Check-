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

        public StockLookupControl()
        {
            MinimumSize = new Size(360, 74);
            Height = 78;
            _tabs.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(_itemPage);
            _tabs.TabPages.Add(_lotQrPage);
            BuildItemTab();
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
                string value = IsItemSearch
                    ? (Convert.ToString(_itemEditor.EditValue) ?? _itemEditor.Text)
                    : _lotQrEditor.Text;
                return (value ?? string.Empty).Trim();
            }
        }

        public string SelectedItemCode
        {
            get { return (Convert.ToString(_itemEditor.EditValue) ?? string.Empty).Trim(); }
        }

        public void SetItemSuggestions(DataTable items)
        {
            _itemEditor.Properties.DataSource = items;
            _itemEditor.Properties.DisplayMember = "PART";
            _itemEditor.Properties.ValueMember = "PART";
            _itemEditor.Properties.NullText = "Gõ mã hoặc tên hàng để xem đề xuất...";
            _itemEditor.Properties.TextEditStyle = TextEditStyles.Standard;
            _itemEditor.Properties.PopupFilterMode = PopupFilterMode.Contains;
            _itemEditor.Properties.ImmediatePopup = true;
            _itemEditor.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
            _itemEditor.Properties.View.OptionsView.ShowGroupPanel = false;
            _itemEditor.Properties.View.OptionsView.ShowAutoFilterRow = true;
            _itemEditor.Properties.View.OptionsBehavior.Editable = false;
            _itemEditor.Properties.View.Columns.Clear();
            if (items != null)
            {
                _itemEditor.Properties.View.Columns.AddVisible("PART", "Mã hàng");
                if (items.Columns.Contains("NAME"))
                    _itemEditor.Properties.View.Columns.AddVisible("NAME", "Tên hàng");
            }
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

        public void ClearInput() { ClearEditors(); }

        private void BuildItemTab()
        {
            var layout = CreateInputLayout();
            _itemEditor.Dock = DockStyle.Fill;
            _itemEditor.Properties.NullValuePrompt = "Gõ mã hoặc tên hàng...";
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
