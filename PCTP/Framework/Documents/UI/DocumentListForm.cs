using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Framework.Documents.UI
{
    /// <summary>Metadata-driven document list. Opening a row shows the read-only document editor.</summary>
    public sealed class DocumentListForm : XtraForm
    {
        private const int PageSize = 100;
        private readonly DocumentRuntimeServices _runtime;
        private readonly string _moduleCode;
        private readonly string _siteCode;
        private readonly IDocumentCommandInputProvider _inputProvider;
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl();
        private readonly ComboBoxEdit _filterField = new ComboBoxEdit();
        private readonly TextEdit _filterText = new TextEdit();
        private readonly SimpleButton _prev = new SimpleButton { Text = "◀ Trước", Width = 90, Height = 30, Dock = DockStyle.Right };
        private readonly SimpleButton _next = new SimpleButton { Text = "Sau ▶", Width = 90, Height = 30, Dock = DockStyle.Right };
        private ResolvedDocument _document;
        private int _pageIndex;
        private DocumentFilter _activeFilter;

        public DocumentListForm(DocumentRuntimeServices runtime, string moduleCode, string siteCode,
            IDocumentCommandInputProvider inputProvider = null)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _runtime = runtime;
            _moduleCode = moduleCode;
            _siteCode = siteCode.Trim();
            _inputProvider = inputProvider;
            _view = new GridView(_grid);
            Text = "Chứng từ " + moduleCode;
            StartPosition = FormStartPosition.CenterParent;
            Size = new System.Drawing.Size(1180, 700);
            MinimumSize = new System.Drawing.Size(900, 520);
            BuildUi();
            Load += (s, e) => LoadList();
        }

        private void BuildUi()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsSelection.MultiSelect = false;
            _view.DoubleClick += (s, e) => OpenSelected();

            var refresh = new SimpleButton { Text = "TẢI LẠI", Width = 110, Height = 30, Dock = DockStyle.Left };
            refresh.Click += (s, e) => LoadList();
            var search = new SimpleButton { Text = "LỌC", Width = 70, Height = 30, Dock = DockStyle.Left };
            search.Click += (s, e) => { _pageIndex = 0; _activeFilter = BuildFilter(); LoadList(); };
            _filterField.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _filterField.Width = 170;
            _filterField.Dock = DockStyle.Left;
            _filterText.Width = 200;
            _filterText.Dock = DockStyle.Left;
            _filterText.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.Handled = true;
                _pageIndex = 0;
                _activeFilter = BuildFilter();
                LoadList();
            };
            _prev.Click += (s, e) => { if (_pageIndex > 0) { _pageIndex--; LoadList(); } };
            _next.Click += (s, e) => { _pageIndex++; LoadList(); };
            var bar = new PanelControl { Dock = DockStyle.Top, Height = 42, Padding = new Padding(6) };
            // WinForms docks the last-added control first, so add right-docked, then left-docked in reverse order.
            bar.Controls.Add(_next);
            bar.Controls.Add(_prev);
            bar.Controls.Add(search);
            bar.Controls.Add(_filterText);
            bar.Controls.Add(_filterField);
            bar.Controls.Add(refresh);
            _status.Dock = DockStyle.Bottom;
            _status.Height = 26;
            Controls.Add(_grid);
            Controls.Add(_status);
            Controls.Add(bar);
        }

        private void LoadList()
        {
            try
            {
                _document = _runtime.Resolve(_moduleCode);
                Text = _document.DisplayName;
                FillFilterFields();

                var query = new DocumentListQuery { PageIndex = _pageIndex, PageSize = PageSize };
                if (_activeFilter != null) query.Filters.Add(_activeFilter);
                var page = _runtime.Data.ListHeaders(_moduleCode, _siteCode, query);

                // The requested page may have disappeared (rows removed or filter narrowed): step back once.
                if (page.Rows.Rows.Count == 0 && _pageIndex > 0 && page.PageCount > 0)
                {
                    _pageIndex = page.PageCount - 1;
                    query.PageIndex = _pageIndex;
                    page = _runtime.Data.ListHeaders(_moduleCode, _siteCode, query);
                }

                _grid.DataSource = page.Rows;
                DocumentGridConfigurator.Apply(_view, _document.HeaderFields);
                _prev.Enabled = _pageIndex > 0;
                _next.Enabled = _pageIndex + 1 < page.PageCount;
                _status.Text = "Trang " + (page.PageCount == 0 ? 0 : _pageIndex + 1) + "/" + page.PageCount +
                    " - " + page.TotalCount.ToString("N0") + " chứng từ của Site " + _siteCode +
                    (_activeFilter == null ? string.Empty : " (đang lọc)") + ". Nhấp đúp để mở.";
                _status.Appearance.ForeColor = System.Drawing.Color.FromArgb(70, 85, 100);
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được danh sách: " + ex.GetBaseException().Message;
                _status.Appearance.ForeColor = System.Drawing.Color.Firebrick;
            }
        }

        private void FillFilterFields()
        {
            if (_filterField.Properties.Items.Count > 0) return;
            foreach (var field in _document.HeaderFields.Where(f => f.IsVisible && f.Definition.Type != DocumentFieldType.Memo))
                _filterField.Properties.Items.Add(new FilterChoice(field));
            if (_filterField.Properties.Items.Count > 0) _filterField.SelectedIndex = 0;
        }

        private DocumentFilter BuildFilter()
        {
            var choice = _filterField.SelectedItem as FilterChoice;
            string text = (_filterText.Text ?? string.Empty).Trim();
            if (choice == null || text.Length == 0) return null;
            var type = choice.Field.Definition.Type;
            bool isText = type != DocumentFieldType.Number && type != DocumentFieldType.Date &&
                          type != DocumentFieldType.DateTime && type != DocumentFieldType.Check;
            return new DocumentFilter(choice.Field.FieldCode, isText ? DocumentFilterOp.Contains : DocumentFilterOp.Equals, text);
        }

        private sealed class FilterChoice
        {
            public ResolvedField Field { get; private set; }
            public FilterChoice(ResolvedField field) { Field = field; }
            public override string ToString() { return Field.Caption; }
        }

        private void OpenSelected()
        {
            var hit = _view.CalcHitInfo(_grid.PointToClient(Cursor.Position));
            if (!hit.InRow || _document == null) return;
            DataRow row = _view.GetFocusedDataRow();
            if (row == null) return;
            object key = row[_document.Definition.KeyField];
            using (var form = new DocumentEditorForm(_runtime, _moduleCode, _siteCode, key, _inputProvider))
                form.ShowDialog(this);
            LoadList();
        }
    }
}
