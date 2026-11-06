using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shared.Lookup
{
    /// <summary>Lookup dùng chung cho mã hàng, LOT và QR FCC (4/6 trường).</summary>
    public sealed class StockLookupControl : UserControl
    {
        private readonly GridLookUpEdit _editor = new GridLookUpEdit();
        private readonly SimpleButton _searchButton = new SimpleButton();
        public event EventHandler SearchRequested;

        public StockLookupControl()
        {
            MinimumSize = new Size(260, 30);
            Height = 34;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), Padding = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _editor.Dock = DockStyle.Fill;
            _editor.Properties.NullValuePrompt = "Nhập mã hàng / LOT hoặc quét QR 4 / 6 trường";
            _editor.Properties.TextEditStyle = TextEditStyles.Standard;
            _editor.Properties.PopupFilterMode = DevExpress.XtraEditors.PopupFilterMode.Contains;
            _editor.Properties.ImmediatePopup = true;
            _editor.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
            _editor.Properties.DisplayMember = "PART";
            _editor.Properties.ValueMember = "PART";
            _editor.Properties.View.OptionsView.ShowAutoFilterRow = true;
            _editor.Properties.View.OptionsView.ShowGroupPanel = false;
            _editor.Properties.View.OptionsBehavior.Editable = false;
            _editor.KeyDown += (sender, args) =>
            {
                if (args.KeyCode != Keys.Enter) return;
                RequestSearch();
                args.Handled = true;
                args.SuppressKeyPress = true;
            };
            _searchButton.Text = "Tra cứu";
            _searchButton.Dock = DockStyle.Fill;
            _searchButton.Click += (sender, args) => RequestSearch();
            layout.Controls.Add(_editor, 0, 0);
            layout.Controls.Add(_searchButton, 1, 0);
            Controls.Add(layout);
        }

        public string InputText
        {
            get
            {
                string value = (_editor.Text ?? string.Empty).Trim();
                if (value.Length == 0 && _editor.EditValue != null)
                    value = Convert.ToString(_editor.EditValue)?.Trim() ?? string.Empty;
                return value;
            }
        }

        public void SetItemSuggestions(DataTable items)
        {
            _editor.Properties.DataSource = items;
            _editor.Properties.PopulateViewColumns();
            if (_editor.Properties.View.Columns["PART"] != null)
            {
                _editor.Properties.View.Columns["PART"].Caption = "Mã hàng";
                _editor.Properties.View.Columns["PART"].Visible = true;
            }
            if (_editor.Properties.View.Columns["NAME"] != null)
            {
                _editor.Properties.View.Columns["NAME"].Caption = "Tên hàng";
                _editor.Properties.View.Columns["NAME"].Visible = true;
            }
            _editor.Properties.View.BestFitColumns();
        }

        public void FocusInput() { _editor.Focus(); }
        private void RequestSearch() { SearchRequested?.Invoke(this, EventArgs.Empty); }
    }
}
