using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// "Khai báo layout" (Bravo): available columns of the approved view on the left, live preview grid in the middle,
    /// property editor on the right. The designer only edits presentation, tab con (parent/child key pair) and quick
    /// filters of an already approved data source. It never edits SQL.
    /// </summary>
    public sealed class UiLayoutDesignerForm : XtraForm
    {
        private readonly UiLayoutDesignerService _service;
        private readonly string _screenCode;
        private readonly Func<UiScreenLoadResult, DataTable> _previewLoader;

        private readonly ComboBoxEdit _region = new ComboBoxEdit { Width = 260, Dock = DockStyle.Left };
        private readonly SimpleButton _save = new SimpleButton { Text = "LƯU", Width = 90, Dock = DockStyle.Left };
        private readonly SimpleButton _reload = new SimpleButton { Text = "TẢI LẠI", Width = 90, Dock = DockStyle.Left };
        private readonly SimpleButton _history = new SimpleButton { Text = "LỊCH SỬ", Width = 90, Dock = DockStyle.Left };
        private readonly CheckedListBoxControl _cols = new CheckedListBoxControl { Dock = DockStyle.Left, Width = 250 };
        private readonly GridControl _preview = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _previewView;
        private readonly XtraTabControl _tabs = new XtraTabControl { Dock = DockStyle.Right, Width = 440 };
        private readonly PropertyGrid _colProps = new PropertyGrid { Dock = DockStyle.Fill, HelpVisible = false, ToolbarVisible = false, PropertySort = PropertySort.Categorized };
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 40, AutoSizeMode = LabelAutoSizeMode.None };

        // screen settings
        private readonly TextEdit _title = new TextEdit();
        private readonly ComboBoxEdit _sortColumn = new ComboBoxEdit();
        private readonly CheckEdit _sortDesc = new CheckEdit { Text = "Sắp xếp giảm dần" };
        private readonly ComboBoxEdit _groupColumn = new ComboBoxEdit();
        private readonly SpinEdit _pageSize = new SpinEdit();

        // children / filters
        private readonly GridControl _childGrid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridControl _filterGrid = new GridControl { Dock = DockStyle.Fill };
        private BindingList<UiChildLayout> _children = new BindingList<UiChildLayout>();
        private BindingList<UiQuickFilter> _filters = new BindingList<UiQuickFilter>();

        private UiScreenLoadResult _s;
        private DataTable _previewTable;
        private string _currentRegion = UiRegion.Main;
        private bool _loading, _dirty;

        public UiLayoutDesignerForm(UiLayoutDesignerService service, string screenCode,
            Func<UiScreenLoadResult, DataTable> previewLoader = null)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
            _screenCode = screenCode;
            _previewLoader = previewLoader;
            _previewView = new GridView(_preview);
            Text = "Khai báo layout - " + screenCode;
            StartPosition = FormStartPosition.CenterParent;
            Size = new System.Drawing.Size(1360, 800);
            MinimumSize = new System.Drawing.Size(1100, 600);
            BuildUi();
            Load += (s, e) => OpenFromServer();
            FormClosing += (s, e) =>
            {
                if (_dirty && XtraMessageBox.Show(this, "Layout có thay đổi chưa lưu. Thoát và bỏ thay đổi?", "Khai báo layout",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
            };
        }

        private void BuildUi()
        {
            _preview.MainView = _previewView;
            _preview.ViewCollection.Add(_previewView);
            _previewView.OptionsBehavior.Editable = false;

            _region.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            _region.SelectedIndexChanged += (s, e) => { if (!_loading) SwitchRegion(); };
            _save.Click += (s, e) => SaveLayout();
            _reload.Click += (s, e) => { if (!_dirty || XtraMessageBox.Show(this, "Bỏ thay đổi và tải lại?", "Khai báo layout", MessageBoxButtons.YesNo) == DialogResult.Yes) OpenFromServer(); };
            _cols.ItemCheck += (s, e) => BeginInvoke((Action)ApplyChecks);   // read the state after the control has updated it
            _cols.SelectedIndexChanged += (s, e) => ShowSelectedColumn();
            _colProps.PropertyValueChanged += (s, e) => { MarkDirty(); RefreshPreview(_colProps.SelectedObject as UiColumnLayout); };

            var top = new PanelControl { Dock = DockStyle.Top, Height = 42, Padding = new Padding(5) };
            _history.Click += (s, e) => ShowHistory();
            top.Controls.Add(_history);
            top.Controls.Add(_reload);
            top.Controls.Add(_save);
            top.Controls.Add(_region);

            var colPage = new XtraTabPage { Text = "Cột" };
            colPage.Controls.Add(_colProps);
            var screenPage = new XtraTabPage { Text = "Màn hình" };
            BuildScreenPage(screenPage);
            var childPage = new XtraTabPage { Text = "Tab con" };
            BuildChildPage(childPage);
            var filterPage = new XtraTabPage { Text = "Bộ lọc nhanh" };
            BuildFilterPage(filterPage);
            _tabs.TabPages.AddRange(new[] { colPage, screenPage, childPage, filterPage });

            Controls.Add(_preview);
            Controls.Add(_tabs);
            Controls.Add(_cols);
            Controls.Add(top);
            Controls.Add(_status);
        }

        private void BuildScreenPage(XtraTabPage page)
        {
            int y = 12;
            Action<string, Control> row = (caption, editor) =>
            {
                page.Controls.Add(new LabelControl { Text = caption, Left = 10, Top = y + 4, AutoSizeMode = LabelAutoSizeMode.None, Width = 130 });
                editor.Left = 145; editor.Top = y; editor.Width = 270;
                page.Controls.Add(editor);
                y += 34;
            };
            row("Tiêu đề màn hình", _title);
            row("Sắp xếp mặc định theo", _sortColumn);
            _sortDesc.Left = 145; _sortDesc.Top = y; _sortDesc.Width = 270; page.Controls.Add(_sortDesc); y += 34;
            row("Nhóm theo cột (subtotal)", _groupColumn);
            row("Số dòng mỗi trang", _pageSize);
            _pageSize.Properties.MinValue = 20; _pageSize.Properties.MaxValue = 500; _pageSize.Properties.IsFloatValue = false;
            _sortColumn.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            _groupColumn.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            foreach (BaseEdit edit in new BaseEdit[] { _title, _sortColumn, _sortDesc, _groupColumn, _pageSize })
                edit.EditValueChanged += (s, e) => { if (!_loading) MarkDirty(); };
        }

        private void BuildChildPage(XtraTabPage page)
        {
            var add = new SimpleButton { Text = "Thêm tab con…", Dock = DockStyle.Top, Height = 30 };
            var remove = new SimpleButton { Text = "Xóa tab con đang chọn", Dock = DockStyle.Top, Height = 30 };
            add.Click += (s, e) => AddChild();
            remove.Click += (s, e) => RemoveChild();
            var view = new GridView(_childGrid);
            _childGrid.MainView = view;
            _childGrid.ViewCollection.Add(view);
            view.OptionsView.ShowGroupPanel = false;
            view.CellValueChanged += (s, e) => { if (!_loading) MarkDirty(); };
            page.Controls.Add(_childGrid);
            page.Controls.Add(remove);
            page.Controls.Add(add);
        }

        private void BuildFilterPage(XtraTabPage page)
        {
            var add = new SimpleButton { Text = "Thêm bộ lọc", Dock = DockStyle.Top, Height = 30 };
            var remove = new SimpleButton { Text = "Xóa bộ lọc đang chọn", Dock = DockStyle.Top, Height = 30 };
            add.Click += (s, e) => AddFilter();
            remove.Click += (s, e) => { var v = (GridView)_filterGrid.MainView; var f = v.GetFocusedRow() as UiQuickFilter; if (f != null) { _filters.Remove(f); MarkDirty(); } };
            var view = new GridView(_filterGrid);
            _filterGrid.MainView = view;
            _filterGrid.ViewCollection.Add(view);
            view.OptionsView.ShowGroupPanel = false;
            view.CellValueChanged += (s, e) => { if (!_loading) MarkDirty(); };
            page.Controls.Add(_filterGrid);
            page.Controls.Add(remove);
            page.Controls.Add(add);
        }

        // ------------------------------------------------------------------ load / save
        private void OpenFromServer()
        {
            try
            {
                _loading = true;
                _s = _service.Open(_screenCode);
                foreach (var region in RegionCodes()) EnsureEntries(region, false);
                _currentRegion = UiRegion.Main;
                BindScreenSettings();
                BindChildrenAndFilters();
                FillRegionCombo();
                _dirty = false;
                _loading = false;
                LoadPreviewTable();
                RefreshRegion();
                _save.Enabled = _service.CanEdit;
                SetStatus("Phiên bản cấu hình " + _s.Layout.ConfigVersion + (_service.CanEdit ? string.Empty : " - chỉ xem (cần quyền EDIT để lưu)."), false);
            }
            catch (Exception ex)
            {
                _loading = false;
                SetStatus(ex.GetBaseException().Message, true);
            }
        }

        /// <summary>Change history (who saved which version, when). Read-only; restoring a version is a manual re-save.</summary>
        private void ShowHistory()
        {
            try
            {
                var table = _service.ListAudit(_screenCode, 200);
                using (var form = new XtraForm { Text = "Lịch sử thay đổi - " + _screenCode, StartPosition = FormStartPosition.CenterParent, Size = new System.Drawing.Size(980, 520) })
                {
                    var grid = new GridControl { Dock = DockStyle.Fill, DataSource = table };
                    var view = new GridView(grid);
                    grid.MainView = view;
                    grid.ViewCollection.Add(view);
                    view.OptionsBehavior.Editable = false;
                    view.OptionsView.ShowGroupPanel = false;
                    view.OptionsView.ColumnAutoWidth = false;
                    view.PopulateColumns();
                    view.Columns["AuditId"].Caption = "Id";
                    view.Columns["AtUtc"].Caption = "Thời điểm (UTC)";
                    view.Columns["ActionCode"].Caption = "Thao tác";
                    view.Columns["ActorCode"].Caption = "Người thực hiện";
                    view.Columns["VersionBefore"].Caption = "Phiên bản trước";
                    view.Columns["VersionAfter"].Caption = "Phiên bản sau";
                    view.Columns["DetailJson"].Caption = "Chi tiết";
                    view.BestFitColumns();
                    form.Controls.Add(grid);
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private void SaveLayout()
        {
            try
            {
                SyncFromUi();
                int version = _service.Save(_s);
                OpenFromServer();
                SetStatus("Đã lưu. Phiên bản cấu hình mới: " + version + ".", false);
            }
            catch (UiConfigConflictException ex)
            {
                SetStatus(ex.Message, true);
                if (XtraMessageBox.Show(this, ex.Message + Environment.NewLine + "Tải lại layout mới nhất (bỏ thay đổi của bạn)?",
                        "Xung đột phiên bản", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) OpenFromServer();
            }
            catch (Exception ex)
            {
                SetStatus(ex.GetBaseException().Message, true);
            }
        }

        /// <summary>Copies everything the user can change in the UI back into the layout object.</summary>
        private void SyncFromUi()
        {
            ((GridView)_childGrid.MainView).PostEditor();
            ((GridView)_filterGrid.MainView).PostEditor();
            UiGridConfigurator.ReadBack(_previewView, _s.Layout.Columns.Where(c => Same(c.RegionCode, _currentRegion)).ToList());
            var l = _s.Layout;
            l.Title = (_title.Text ?? string.Empty).Trim();
            l.DefaultSortColumn = Blank(_sortColumn.Text);
            l.DefaultSortDesc = _sortDesc.Checked;
            l.GroupColumn = Blank(_groupColumn.Text);
            l.PageSize = Convert.ToInt32(_pageSize.Value);
            int n = 1;
            foreach (var child in _children) child.SequenceNo = n++;
            l.Children.Clear(); l.Children.AddRange(_children);
            n = 1;
            foreach (var f in _filters)
            {
                f.SequenceNo = n++;
                if (!string.IsNullOrEmpty(f.TokenCode)) f.LiteralValue = null;
                else { f.TokenCode = null; if (f.LiteralValue == null) f.LiteralValue = string.Empty; }
            }
            l.Filters.Clear(); l.Filters.AddRange(_filters);
            if (l.Filters.Count(f => f.IsDefault) > 1) throw new InvalidOperationException("Chỉ được chọn một bộ lọc mặc định.");
        }

        // ------------------------------------------------------------------ regions and columns
        private IEnumerable<string> RegionCodes()
        {
            yield return UiRegion.Main;
            foreach (var child in _s.Layout.Children.OrderBy(c => c.SequenceNo)) yield return child.ChildCode;
        }

        private void FillRegionCombo()
        {
            _region.Properties.Items.Clear();
            _region.Properties.Items.Add(new RegionItem(UiRegion.Main, "Danh sách chính"));
            foreach (var child in _children) _region.Properties.Items.Add(new RegionItem(child.ChildCode, "Tab: " + child.TabCaption));
            _region.SelectedIndex = 0;
        }

        private void SwitchRegion()
        {
            var item = _region.SelectedItem as RegionItem;
            if (item == null) return;
            UiGridConfigurator.ReadBack(_previewView, _s.Layout.Columns.Where(c => Same(c.RegionCode, _currentRegion)).ToList());
            _currentRegion = item.Code;
            LoadPreviewTable();
            RefreshRegion();
        }

        private void EnsureEntries(string region, bool visibleByDefault)
        {
            List<UiSourceColumn> catalog;
            if (!_s.Catalog.TryGetValue(region, out catalog)) return;
            int next = _s.Layout.ColumnsOf(region).Select(c => c.SequenceNo).DefaultIfEmpty(0).Max() + 1;
            foreach (var col in catalog)
            {
                if (_s.Layout.ColumnsOf(region).Any(c => Same(c.ColumnName, col.ColumnName))) continue;
                _s.Layout.Columns.Add(new UiColumnLayout
                {
                    RegionCode = region,
                    ColumnName = col.ColumnName,
                    Caption = col.ColumnName,
                    SequenceNo = next++,
                    Align = col.TypeGroup == "NUM" ? "RIGHT" : "LEFT",
                    IsVisible = visibleByDefault
                });
            }
        }

        private void RefreshRegion()
        {
            _loading = true;
            _cols.Items.Clear();
            foreach (var item in _s.Layout.ColumnsOf(_currentRegion).OrderBy(c => c.SequenceNo))
                _cols.Items.Add(new CheckedListBoxItem
                {
                    Value = item,
                    Description = item.ColumnName,
                    CheckState = item.IsVisible ? CheckState.Checked : CheckState.Unchecked
                });
            _loading = false;
            if (_cols.Items.Count > 0) _cols.SelectedIndex = 0;
            ApplyPreview();
        }

        private void ApplyChecks()
        {
            if (_loading) return;
            for (int i = 0; i < _cols.Items.Count; i++)
            {
                var layoutItem = (UiColumnLayout)_cols.Items[i].Value;
                layoutItem.IsVisible = _cols.Items[i].CheckState == CheckState.Checked;
            }
            MarkDirty();
            RefreshPreview(null);
        }

        private void ShowSelectedColumn()
        {
            if (_loading || _cols.SelectedIndex < 0) return;
            _colProps.SelectedObject = _cols.Items[_cols.SelectedIndex].Value;
        }

        private void RefreshPreview(UiColumnLayout edited)
        {
            UiGridConfigurator.ReadBack(_previewView, _s.Layout.Columns.Where(c => Same(c.RegionCode, _currentRegion)).ToList(),
                edited == null ? null : edited.ColumnName);
            ApplyPreview();
        }

        private void LoadPreviewTable()
        {
            DataTable table = null;
            if (_currentRegion == UiRegion.Main && _previewLoader != null)
            {
                try { table = _previewLoader(_s); }
                catch (Exception ex) { SetStatus("Không đọc được dữ liệu xem trước (vẫn sửa được layout): " + ex.GetBaseException().Message, true); }
            }
            if (table == null) table = EmptyTable(_currentRegion);
            _previewTable = table;
        }

        private DataTable EmptyTable(string region)
        {
            var table = new DataTable();
            List<UiSourceColumn> catalog;
            if (_s.Catalog.TryGetValue(region, out catalog))
                foreach (var col in catalog)
                    table.Columns.Add(col.ColumnName, col.TypeGroup == "NUM" ? typeof(decimal) : col.TypeGroup == "DATE" ? typeof(DateTime)
                        : col.TypeGroup == "BIT" ? typeof(bool) : typeof(string));
            return table;
        }

        private void ApplyPreview()
        {
            if (_s == null || _previewTable == null) return;
            _preview.DataSource = _previewTable;
            // Preview never groups, so the column order read back from the grid is exactly what the user arranged.
            UiGridConfigurator.Apply(_previewView, _s.Layout.ColumnsOf(_currentRegion), null);
        }

        // ------------------------------------------------------------------ screen settings, children, filters
        private void BindScreenSettings()
        {
            var l = _s.Layout;
            var names = new[] { string.Empty }.Concat(_s.Catalog[UiRegion.Main].Select(c => c.ColumnName)).ToArray();
            _title.Text = l.Title;
            _sortColumn.Properties.Items.Clear(); _sortColumn.Properties.Items.AddRange(names); _sortColumn.Text = l.DefaultSortColumn ?? string.Empty;
            _groupColumn.Properties.Items.Clear(); _groupColumn.Properties.Items.AddRange(names); _groupColumn.Text = l.GroupColumn ?? string.Empty;
            _sortDesc.Checked = l.DefaultSortDesc;
            _pageSize.Value = l.PageSize;
        }

        private void BindChildrenAndFilters()
        {
            _children = new BindingList<UiChildLayout>(_s.Layout.Children.OrderBy(c => c.SequenceNo).ToList());
            _filters = new BindingList<UiQuickFilter>(_s.Layout.Filters.OrderBy(f => f.SequenceNo).ToList());
            _childGrid.DataSource = _children;
            _filterGrid.DataSource = _filters;
            ConfigureChildGrid();
            ConfigureFilterGrid();
        }

        private void ConfigureChildGrid()
        {
            var view = (GridView)_childGrid.MainView;
            view.PopulateColumns();
            foreach (var hidden in new[] { "ViewName", "ViewFunctionCode", "SiteColumn", "SequenceNo" })
                view.Columns[hidden].Visible = false;
            view.Columns["ChildCode"].OptionsColumn.ReadOnly = true;
            view.Columns["SourceCode"].OptionsColumn.ReadOnly = true;
            view.Columns["ChildCode"].Caption = "Mã tab";
            view.Columns["TabCaption"].Caption = "Tiêu đề tab";
            view.Columns["SourceCode"].Caption = "Nguồn dữ liệu";
            view.Columns["ParentColumn"].Caption = "Khóa cha (view chính)";
            view.Columns["ChildColumn"].Caption = "Khóa con (view tab)";
            view.Columns["SortColumn"].Caption = "Sắp xếp theo";
            view.Columns["SortDesc"].Caption = "Giảm dần";
            view.Columns["ParentColumn"].ColumnEdit = ComboOf(_s.Catalog[UiRegion.Main].Select(c => c.ColumnName));
            var childNames = _s.Catalog.Where(kv => !Same(kv.Key, UiRegion.Main)).SelectMany(kv => kv.Value).Select(c => c.ColumnName).Distinct().ToList();
            childNames.Insert(0, string.Empty);
            view.Columns["ChildColumn"].ColumnEdit = ComboOf(childNames);
            view.Columns["SortColumn"].ColumnEdit = ComboOf(childNames);
            view.OptionsView.ColumnAutoWidth = false;
            view.BestFitColumns();
        }

        private void ConfigureFilterGrid()
        {
            var view = (GridView)_filterGrid.MainView;
            view.PopulateColumns();
            view.Columns["SequenceNo"].Visible = false;
            view.Columns["FilterCode"].Caption = "Mã";
            view.Columns["Caption"].Caption = "Tiêu đề (hiện bên trái)";
            view.Columns["ColumnName"].Caption = "Cột";
            view.Columns["OperatorCode"].Caption = "Toán tử";
            view.Columns["TokenCode"].Caption = "Giá trị theo token";
            view.Columns["LiteralValue"].Caption = "Hoặc giá trị cố định";
            view.Columns["IsDefault"].Caption = "Mặc định";
            view.Columns["ColumnName"].ColumnEdit = ComboOf(_s.Catalog[UiRegion.Main].Select(c => c.ColumnName));
            view.Columns["OperatorCode"].ColumnEdit = ComboOf(UiFilterOperator.All);
            view.Columns["TokenCode"].ColumnEdit = ComboOf(new[] { string.Empty }.Concat(UiFilterToken.All));
            view.OptionsView.ColumnAutoWidth = false;
            view.BestFitColumns();
        }

        private static RepositoryItemComboBox ComboOf(IEnumerable<string> items)
        {
            var combo = new RepositoryItemComboBox { TextEditStyle = TextEditStyles.DisableTextEditor };
            combo.Items.AddRange(items.ToArray());
            return combo;
        }

        private void AddFilter()
        {
            string column = _s.Catalog[UiRegion.Main].Select(c => c.ColumnName).FirstOrDefault();
            if (column == null) return;
            int n = _filters.Count + 1;
            while (_filters.Any(f => f.FilterCode == "F" + n)) n++;
            _filters.Add(new UiQuickFilter { FilterCode = "F" + n, Caption = "Bộ lọc mới", ColumnName = column, OperatorCode = UiFilterOperator.Equal, LiteralValue = string.Empty });
            MarkDirty();
        }

        private void AddChild()
        {
            try
            {
                SyncFromUi();
                var sources = _service.ListSources().Where(s => s.IsActive).ToList();
                using (var dialog = new AddChildDialog(sources, _children.Select(c => c.ChildCode).ToList()))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    var source = dialog.SelectedSource;
                    var catalog = _service.GetSourceColumns(source.SourceCode).ToList();
                    var main = _s.Catalog[UiRegion.Main];
                    var parentKey = main.FirstOrDefault(m => catalog.Any(c => Same(c.ColumnName, m.ColumnName))) ?? main.First();
                    var childKey = catalog.FirstOrDefault(c => Same(c.ColumnName, parentKey.ColumnName)) ?? catalog.First();
                    _s.Catalog[dialog.ChildCode] = catalog;
                    var child = new UiChildLayout
                    {
                        ChildCode = dialog.ChildCode,
                        TabCaption = dialog.TabCaption,
                        SourceCode = source.SourceCode,
                        ViewName = source.ViewName,
                        ViewFunctionCode = source.ViewFunctionCode,
                        SiteColumn = source.SiteColumn,
                        ParentColumn = parentKey.ColumnName,
                        ChildColumn = childKey.ColumnName,
                        SequenceNo = _children.Count + 1
                    };
                    _s.Layout.Children.Add(child);
                    EnsureEntries(child.ChildCode, true);
                    BindChildrenAndFilters();
                    FillRegionCombo();
                    MarkDirty();
                    SetStatus("Đã thêm tab. Kiểm tra cặp khóa cha-con ở tab \"Tab con\" rồi chọn vùng để sắp xếp cột.", false);
                }
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private void RemoveChild()
        {
            var child = ((GridView)_childGrid.MainView).GetFocusedRow() as UiChildLayout;
            if (child == null) return;
            if (XtraMessageBox.Show(this, "Xóa tab \"" + child.TabCaption + "\" cùng cấu hình cột của nó?", "Tab con", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            SyncFromUi();
            _s.Layout.Children.RemoveAll(c => Same(c.ChildCode, child.ChildCode));
            _s.Layout.Columns.RemoveAll(c => Same(c.RegionCode, child.ChildCode));
            _s.Catalog.Remove(child.ChildCode);
            _currentRegion = UiRegion.Main;
            BindChildrenAndFilters();
            FillRegionCombo();
            LoadPreviewTable();
            RefreshRegion();
            MarkDirty();
        }

        // ------------------------------------------------------------------ helpers
        private void MarkDirty() { if (!_loading) _dirty = true; }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        private static string Blank(string text) { return string.IsNullOrWhiteSpace(text) ? null : text.Trim(); }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = error ? System.Drawing.Color.Firebrick : System.Drawing.Color.FromArgb(70, 85, 100);
        }

        private sealed class RegionItem
        {
            public string Code { get; private set; }
            private readonly string _text;
            public RegionItem(string code, string text) { Code = code; _text = text; }
            public override string ToString() { return _text; }
        }

        private sealed class AddChildDialog : XtraForm
        {
            private readonly ComboBoxEdit _source = new ComboBoxEdit { Left = 130, Top = 14, Width = 300 };
            private readonly TextEdit _code = new TextEdit { Left = 130, Top = 48, Width = 300 };
            private readonly TextEdit _caption = new TextEdit { Left = 130, Top = 82, Width = 300 };
            private readonly IList<string> _used;
            public UiDataSourceInfo SelectedSource { get { return _source.SelectedItem as UiDataSourceInfo; } }
            public string ChildCode { get { return (_code.Text ?? string.Empty).Trim().ToUpperInvariant(); } }
            public string TabCaption { get { return (_caption.Text ?? string.Empty).Trim(); } }

            public AddChildDialog(IList<UiDataSourceInfo> sources, IList<string> usedCodes)
            {
                _used = usedCodes;
                Text = "Thêm tab con";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new System.Drawing.Size(450, 170);
                _source.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
                foreach (var s in sources) _source.Properties.Items.Add(s);
                if (_source.Properties.Items.Count > 0) _source.SelectedIndex = 0;
                Controls.Add(new LabelControl { Text = "Nguồn dữ liệu đã duyệt", Left = 12, Top = 18 });
                Controls.Add(new LabelControl { Text = "Mã tab (A-Z, 0-9, _)", Left = 12, Top = 52 });
                Controls.Add(new LabelControl { Text = "Tiêu đề tab", Left = 12, Top = 86 });
                Controls.Add(_source); Controls.Add(_code); Controls.Add(_caption);
                var ok = new SimpleButton { Text = "Thêm", Left = 270, Top = 125, Width = 76 };
                var cancel = new SimpleButton { Text = "Hủy", Left = 354, Top = 125, Width = 76, DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) =>
                {
                    string code = ChildCode;
                    bool valid = SelectedSource != null && code.Length > 0 && code.Length <= 40 && code != UiRegion.Main &&
                                 code.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_') &&
                                 !_used.Contains(code, StringComparer.OrdinalIgnoreCase) && TabCaption.Length > 0;
                    if (!valid) { XtraMessageBox.Show(this, "Chọn nguồn, nhập mã tab hợp lệ (không trùng) và tiêu đề.", "Thêm tab con"); return; }
                    DialogResult = DialogResult.OK;
                };
                Controls.Add(ok); Controls.Add(cancel);
                AcceptButton = ok; CancelButton = cancel;
            }
        }
    }
}