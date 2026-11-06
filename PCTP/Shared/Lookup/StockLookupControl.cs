using DevExpress.XtraEditors;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shared.Lookup
{
    /// <summary>
    /// Ô nhập tìm kiếm thống nhất cho mã hàng, LOT và QR FCC (4/6 trường).
    ///
    /// Quan trọng: đây là ô nhập văn bản tự do, KHÔNG phải GridLookUpEdit.
    /// Danh sách gợi ý PART cố định sẽ chặn LOT/QR trước khi SearchStock được gọi,
    /// khiến người dùng gõ LOT nhưng popup vẫn chỉ lọc mã hàng.
    /// Việc phân loại mã hàng/LOT/QR được thực hiện tại repository SearchStock.
    /// </summary>
    public sealed class StockLookupControl : UserControl
    {
        private readonly TextEdit _editor = new TextEdit();
        private readonly SimpleButton _searchButton = new SimpleButton();

        public event EventHandler SearchRequested;

        public StockLookupControl()
        {
            MinimumSize = new Size(260, 30);
            Height = 34;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _editor.Dock = DockStyle.Fill;
            _editor.Properties.NullValuePrompt = "Nhập mã hàng / LOT hoặc quét QR 4 / 6 trường";
            _editor.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
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

        /// <summary>
        /// Trả về nguyên văn chuỗi người dùng nhập để repository tự phân loại.
        /// Không đọc EditValue từ lookup mã hàng và không thay đổi chuỗi QR/LOT.
        /// </summary>
        public string InputText
        {
            get { return (_editor.Text ?? string.Empty).Trim(); }
        }

        public void FocusInput()
        {
            _editor.Focus();
            _editor.SelectAll();
        }

        public void ClearInput()
        {
            _editor.EditValue = null;
        }

        private void RequestSearch()
        {
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
