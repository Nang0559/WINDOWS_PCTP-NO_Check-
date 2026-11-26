using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// Read-only screen drawn from an approved layout (Bravo style): quick filters on the left, master grid on top,
    /// child tabs below. Right-click offers "Khai báo cơ sở dữ liệu…" / "Khai báo layout…" only to users who hold the
    /// designer permission; SQL re-checks the same permission on every declaration.
    /// </summary>
    public sealed class UiScreenRuntimeForm : XtraForm
    {
        private const string AllFilterText = "(Tất cả)";
        private readonly UiScreenRuntime _runtime;
        private readonly Func<UiLayoutDesignerService> _designerFactory;
        private readonly string _screenCode;
        private readonly string _siteCode;
        private readonly IList<UiScreenAction> _actions;

        private readonly ListBoxControl _filters = new ListBoxControl { Dock = DockStyle.Left, Width = 190 };
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly XtraTabControl _tabs = new XtraTabControl { Dock = DockStyle.Fill };
        private readonly SplitContainerControl _split = new SplitContainerControl { Dock = DockStyle.Fill, Horizontal = false };
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };
        private readonly SimpleButton _prev = new SimpleButton { Text = "◀ Trước", Width = 90, Dock = DockStyle.Right };
        private readonly SimpleButton _next = new SimpleButton { Text = "Sau ▶", Width = 90, Dock = DockStyle.Right };
        private readonly SimpleButton _reload = new SimpleButton { Text = "TẢI LẠI", Width = 100, Dock = DockStyle.Left };
        private readonly Timer _childTimer = new Timer { Interval = 200 };
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private readonly Dictionary<string, GridControl> _childGrids = new Dictionary<string, GridControl>(StringComparer.OrdinalIgnoreCase);

        private UiScreenLoadResult _screen;
        private UiQuickFilter _activeFilter;
        private int _pageIndex;
        private bool _loadingFilters;

        public UiScreenRuntimeForm(UiScreenRuntime runtime, string screenCode, string siteCode,
            Func<UiLayoutDesignerService> designerFactory = null, IEnumerable<UiScreenAction> actions = null)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _runtime = runtime;
            _screenCode = screenCode;
            _siteCode = siteCode.Trim();
            _designerFactory = designerFactory ?? new Func<UiLayoutDesignerService>(UiLayoutModule.CreateDesigner);
            _actions = (actions ?? Enumerable.Empty<UiScreenAction>()).Where(a => a != null).ToList();
            _view = new GridView(_grid);
            Text = "Màn hình " + screenCode;
            StartPosition = FormStartPosition.CenterParent;
            Size = new System.Drawing.Size(1280, 760);
            MinimumSize = new System.Drawing.Size(900, 520);
            BuildUi();
            Load += (s, e) => OpenScreen();
        }

        private void BuildUi()
        {
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsSelection.MultiSelect = false;
            _view.FocusedRowChanged += (s, e) => ScheduleChildLoad();

            _childTimer.Tick += (s, e) => { _childTimer.Stop(); LoadChild(); };
            _tabs.SelectedPageChanged += (s, e) => ScheduleChildLoad();
            _filters.SelectedIndexChanged += (s, e) => OnFilterChanged();
            _reload.Click += (s, e) => LoadPage();
            _prev.Click += (s, e) => { if (_pageIndex > 0) { _pageIndex--; LoadPage(); } };
            _next.Click += (s, e) => { _pageIndex++; LoadPage(); };

            _split.Panel1.Controls.Add(_grid);
            _split.Panel2.Controls.Add(_tabs);
            _split.SplitterPosition = 420;

            var bar = new PanelControl { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
            bar.Controls.Add(_next);
            bar.Controls.Add(_prev);
            // Extra module actions sit to the right of TẢI LẠI. Left-docked controls are laid out last-added-first,
            // so they are added in reverse order before _reload.
            foreach (var action in _actions.Reverse())
            {
                var captured = action;
                var button = new SimpleButton { Text = captured.Caption, Width = captured.Width, Dock = DockStyle.Left, Visible = captured.IsAvailable() };
                button.Click += (s, e) => RunAction(captured);
                bar.Controls.Add(button);
            }
            bar.Controls.Add(_reload);

            Controls.Add(_split);
            Controls.Add(_filters);
            Controls.Add(bar);
            Controls.Add(_status);

            var itemData = new ToolStripMenuItem("Khai báo cơ sở dữ liệu…") { ShortcutKeyDisplayString = "Ctrl+Alt+Shift+D" };
            var itemLayout = new ToolStripMenuItem("Khai báo layout…") { ShortcutKeyDisplayString = "Ctrl+Alt+Shift+L" };
            itemData.Click += (s, e) => OpenDataSourceDeclaration();
            itemLayout.Click += (s, e) => OpenLayoutDeclaration();
            _menu.Items.Add(itemData);
            _menu.Items.Add(itemLayout);
            // Visibility is a usability aid only; SQL re-checks APPROVE / EDIT on every declaration.
            _menu.Opening += (s, e) =>
            {
                try
                {
                    var designer = _designerFactory();
                    itemData.Visible = designer.CanApprove;
                    itemLayout.Visible = designer.CanEdit || designer.CanApprove;
                    e.Cancel = !itemData.Visible && !itemLayout.Visible;
                }
                catch (Exception) { e.Cancel = true; }
            };
            _grid.ContextMenuStrip = _menu;
            _filters.ContextMenuStrip = _menu;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const Keys mask = Keys.Control | Keys.Alt | Keys.Shift;
            if (keyData == (mask | Keys.D)) { OpenDataSourceDeclaration(); return true; }
            if (keyData == (mask | Keys.L)) { OpenLayoutDeclaration(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OpenScreen()
        {
            try
            {
                _screen = _runtime.Open(_screenCode, _siteCode);
                Text = _screen.Layout.Title;

                _tabs.TabPages.Clear();
                _childGrids.Clear();
                foreach (var child in _screen.Layout.Children.OrderBy(c => c.SequenceNo))
                {
                    var page = new XtraTabPage { Text = child.TabCaption, Tag = child };
                    var grid = new GridControl { Dock = DockStyle.Fill };
                    var view = new GridView(grid);
                    grid.MainView = view;
                    grid.ViewCollection.Add(view);
                    view.OptionsBehavior.ReadOnly = true;
                    page.Controls.Add(grid);
                    _tabs.TabPages.Add(page);
                    _childGrids[child.ChildCode] = grid;
                }
                _split.PanelVisibility = _screen.Layout.Children.Count == 0
                    ? SplitPanelVisibility.Panel1 : SplitPanelVisibility.Both;

                FillFilters();
                _pageIndex = 0;
                LoadPage();
            }
            catch (Exception ex)
            {
                SetStatus("Không mở được màn hình: " + ex.GetBaseException().Message, true);
            }
        }

        private void FillFilters()
        {
            _loadingFilters = true;
            _filters.Items.Clear();
            _filters.Items.Add(AllFilterText);
            foreach (var f in _screen.Layout.Filters.OrderBy(x => x.SequenceNo)) _filters.Items.Add(f);
            var def = _runtime.DefaultFilter(_screen.Layout);
            _filters.SelectedIndex = def == null ? 0 : _filters.Items.IndexOf(def);
            _activeFilter = def;
            _loadingFilters = false;
        }

        private void OnFilterChanged()
        {
            if (_loadingFilters || _screen == null) return;
            _activeFilter = _filters.SelectedItem as UiQuickFilter;   // "(Tất cả)" is a string => no filter
            _pageIndex = 0;
            LoadPage();
        }

        private void LoadPage()
        {
            if (_screen == null) return;
            try
            {
                long total;
                var table = _runtime.ReadMainPage(_screen, _siteCode, _activeFilter, _pageIndex, out total);
                int pageSize = _screen.Layout.PageSize;
                int pageCount = (int)((total + pageSize - 1) / pageSize);
                if (table.Rows.Count == 0 && _pageIndex > 0 && pageCount > 0)
                {
                    _pageIndex = pageCount - 1;
                    table = _runtime.ReadMainPage(_screen, _siteCode, _activeFilter, _pageIndex, out total);
                }
                _grid.DataSource = table;
                UiGridConfigurator.Apply(_view, _screen.Layout.ColumnsOf(UiRegion.Main), _screen.Layout.GroupColumn);
                _prev.Enabled = _pageIndex > 0;
                _next.Enabled = _pageIndex + 1 < pageCount;
                SetStatus("Trang " + (pageCount == 0 ? 0 : _pageIndex + 1) + "/" + pageCount + " - " + total.ToString("N0") +
                          " dòng, Site " + _siteCode + (_activeFilter == null ? string.Empty : " - " + _activeFilter.Caption), false);
                ScheduleChildLoad();
            }
            catch (Exception ex)
            {
                SetStatus("Không tải được dữ liệu: " + ex.GetBaseException().Message, true);
            }
        }

        private void ScheduleChildLoad()
        {
            _childTimer.Stop();
            _childTimer.Start();
        }

        private void LoadChild()
        {
            if (_screen == null || _tabs.SelectedTabPage == null) return;
            var child = _tabs.SelectedTabPage.Tag as UiChildLayout;
            if (child == null) return;
            var grid = _childGrids[child.ChildCode];
            DataRow row = _view.GetFocusedDataRow();
            if (row == null || row.IsNull(child.ParentColumn)) { grid.DataSource = null; return; }
            try
            {
                grid.DataSource = _runtime.ReadChild(_screen, child, _siteCode, row[child.ParentColumn]);
                UiGridConfigurator.Apply((GridView)grid.MainView, _screen.Layout.ColumnsOf(child.ChildCode), null);
            }
            catch (Exception ex)
            {
                SetStatus("Không tải được tab " + child.TabCaption + ": " + ex.GetBaseException().Message, true);
            }
        }

        private void OpenDataSourceDeclaration()
        {
            try
            {
                using (var form = new UiDataSourceForm(_designerFactory(), _screenCode)) form.ShowDialog(this);
                OpenScreen();
            }
            catch (Exception ex) { XtraMessageBox.Show(this, ex.GetBaseException().Message, "Khai báo cơ sở dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void OpenLayoutDeclaration()
        {
            try
            {
                var designer = _designerFactory();
                string site = _siteCode;
                using (var form = new UiLayoutDesignerForm(designer, _screenCode,
                    s => { long total; return _runtime.ReadMainPage(s, site, null, 0, out total); }))
                    form.ShowDialog(this);
                OpenScreen();   // always reload so the saved layout (or a conflict-reloaded one) is what the user sees
            }
            catch (Exception ex) { XtraMessageBox.Show(this, ex.GetBaseException().Message, "Khai báo layout", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RunAction(UiScreenAction action)
        {
            try
            {
                string status = action.Execute(this, _siteCode);
                if (status == null) return;   // cancelled by the user
                LoadPage();
                SetStatus(status, false);
            }
            catch (Exception ex)
            {
                string message = ex.GetBaseException().Message;
                SetStatus(action.Caption + " thất bại: " + message, true);
                XtraMessageBox.Show(this, message, action.Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = error ? System.Drawing.Color.Firebrick : System.Drawing.Color.FromArgb(70, 85, 100);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _childTimer.Dispose(); _menu.Dispose(); }
            base.Dispose(disposing);
        }
    }
}