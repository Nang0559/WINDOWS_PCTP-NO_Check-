using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;

namespace PCTP.Framework.Documents.UI
{
    /// <summary>
    /// Designer for layer-2 (approved presentation metadata). It edits caption, order, width, format and visibility of
    /// documents that the code registry already declares. It cannot add a view, procedure, command or editor, and it
    /// cannot make a field editable: <see cref="DocumentDesignerService"/> rejects that and the SQL procedure
    /// (migration 051) rejects it again, together with RBAC, concurrency and audit.
    /// </summary>
    public sealed class DocumentDesignerForm : XtraForm
    {
        private readonly DocumentDesignerService _service;
        private readonly ComboBoxEdit _modules = new ComboBoxEdit();
        private readonly LabelControl _status = new LabelControl();
        private readonly SimpleButton _reload = new SimpleButton { Text = "TẢI LẠI", Width = 100, Height = 30, Dock = DockStyle.Left };
        private readonly SimpleButton _provision = new SimpleButton { Text = "KHAI BÁO / BỔ SUNG", Width = 170, Height = 30, Dock = DockStyle.Left };
        private readonly SimpleButton _save = new SimpleButton { Text = "LƯU", Width = 100, Height = 30, Dock = DockStyle.Right };
        private readonly SimpleButton _moveUp = new SimpleButton { Text = "▲ Lên", Width = 80, Height = 30, Dock = DockStyle.Left };
        private readonly SimpleButton _moveDown = new SimpleButton { Text = "▼ Xuống", Width = 80, Height = 30, Dock = DockStyle.Left };
        private readonly XtraTabControl _tabs = new XtraTabControl { Dock = DockStyle.Fill };
        private readonly GridControl _headerGrid = new GridControl();
        private readonly GridControl _detailGrid = new GridControl();
        private readonly GridControl _commandGrid = new GridControl();
        private readonly GridControl _layoutGrid = new GridControl();
        private readonly GridControl _auditGrid = new GridControl();
        private readonly GridControl _previewHeader = new GridControl();
        private readonly GridControl _previewDetail = new GridControl();
        private readonly LabelControl _previewHeaderCaption = new LabelControl { Dock = DockStyle.Top, Padding = new Padding(4) };
        private readonly LabelControl _previewDetailCaption = new LabelControl { Dock = DockStyle.Top, Padding = new Padding(4) };
        private readonly XtraTabPage _previewPage = new XtraTabPage { Text = "Xem trước" };
        private readonly XtraTabPage _auditPage = new XtraTabPage { Text = "Nhật ký thay đổi" };

        private DocumentDesignerSession _session;
        private DocumentMetadata _draft;
        private List<DocumentFieldMetadata> _headerRows = new List<DocumentFieldMetadata>();
        private List<DocumentFieldMetadata> _detailRows = new List<DocumentFieldMetadata>();
        private bool _dirty;
        private bool _loading;

        public DocumentDesignerForm(DocumentDesignerService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
            Text = "Thiết kế chứng từ (Document Designer)";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 720);
            MinimumSize = new Size(960, 560);
            BuildUi();
            Load += (s, e) => LoadModules();
        }

        private void BuildUi()
        {
            _modules.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _modules.Width = 320;
            _modules.Dock = DockStyle.Left;
            _modules.SelectedIndexChanged += (s, e) => { if (!_loading) OpenSelected(); };
            _reload.Click += (s, e) => OpenSelected();
            _provision.Click += (s, e) => Provision();
            _save.Click += (s, e) => SaveChanges();
            _moveUp.Click += (s, e) => MoveFocused(-1);
            _moveDown.Click += (s, e) => MoveFocused(1);

            var top = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            top.Controls.Add(_save);
            top.Controls.Add(_moveDown);
            top.Controls.Add(_moveUp);
            top.Controls.Add(_provision);
            top.Controls.Add(_reload);
            top.Controls.Add(_modules);

            _status.Dock = DockStyle.Bottom;
            _status.Padding = new Padding(8, 4, 8, 4);
            _status.AutoSizeMode = LabelAutoSizeMode.None;
            _status.Height = 44;

            AddGridPage("Cột Header", _headerGrid);
            AddGridPage("Cột Detail", _detailGrid);
            AddGridPage("Lệnh", _commandGrid);
            AddGridPage("Vùng", _layoutGrid);

            var previewBody = new Panel { Dock = DockStyle.Fill };
            _previewHeader.Dock = DockStyle.Fill;
            _previewDetail.Dock = DockStyle.Fill;
            var headerPanel = new Panel { Dock = DockStyle.Fill };
            headerPanel.Controls.Add(_previewHeader);
            headerPanel.Controls.Add(_previewHeaderCaption);
            var detailPanel = new Panel { Dock = DockStyle.Bottom, Height = 270 };
            detailPanel.Controls.Add(_previewDetail);
            detailPanel.Controls.Add(_previewDetailCaption);
            previewBody.Controls.Add(headerPanel);
            previewBody.Controls.Add(detailPanel);
            _previewPage.Controls.Add(previewBody);
            _tabs.TabPages.Add(_previewPage);

            _auditGrid.Dock = DockStyle.Fill;
            _auditPage.Controls.Add(_auditGrid);
            _tabs.TabPages.Add(_auditPage);
            _tabs.SelectedPageChanged += (s, e) => OnTabChanged();

            Controls.Add(_tabs);
            Controls.Add(top);
            Controls.Add(_status);
        }

        private void AddGridPage(string title, GridControl grid)
        {
            var page = new XtraTabPage { Text = title };
            grid.Dock = DockStyle.Fill;
            var view = new GridView(grid);
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsBehavior.Editable = true;
            view.CellValueChanged += (s, e) => { if (!_loading) MarkDirty(); };
            page.Controls.Add(grid);
            _tabs.TabPages.Add(page);
        }

        private void LoadModules()
        {
            try
            {
                _loading = true;
                var items = _service.ListModules();
                _modules.Properties.Items.Clear();
                foreach (var item in items) _modules.Properties.Items.Add(item.ModuleCode);
                if (_modules.Properties.Items.Count > 0) _modules.SelectedIndex = 0;
                _loading = false;
                var broken = items.Where(i => i.Problem != null).ToList();
                if (_modules.Properties.Items.Count > 0) OpenSelected();
                if (broken.Count > 0)
                    SetStatus("Chứng từ cấu hình không hợp lệ: " + string.Join("; ", broken.Select(b => b.ModuleCode + " — " + b.Problem)), true);
            }
            catch (Exception ex)
            {
                _loading = false;
                ShowError(ex);
            }
        }

        private void OpenSelected()
        {
            string moduleCode = _modules.SelectedItem as string;
            if (string.IsNullOrEmpty(moduleCode)) return;
            if (!ConfirmDiscard()) return;
            try
            {
                _loading = true;
                _session = _service.Open(moduleCode);
                _draft = _session.HasMetadata ? _session.CreateEditableCopy() : null;
                BindDraft();
                _dirty = false;
                UpdateButtons();
                string missing = _session.MissingFields.Count + _session.MissingCommands.Count > 0
                    ? " Còn " + _session.MissingFields.Count + " cột và " + _session.MissingCommands.Count +
                      " lệnh đã đăng ký trong code nhưng chưa có metadata — dùng 'Khai báo / bổ sung'."
                    : string.Empty;
                SetStatus(_session.HasMetadata
                    ? "ConfigVersion " + _session.LoadedConfigVersion + ". Chỉ sửa được tiêu đề, thứ tự, độ rộng, định dạng, hiển thị." + missing
                    : "Chứng từ đã đăng ký trong code nhưng chưa khai báo trong SQL." + missing, !_session.HasMetadata);
                LoadAuditIfVisible();
            }
            catch (Exception ex)
            {
                _session = null;
                _draft = null;
                BindDraft();
                UpdateButtons();
                ShowError(ex);
            }
            finally
            {
                _loading = false;
            }
        }

        private void BindDraft()
        {
            _headerRows = _draft == null ? new List<DocumentFieldMetadata>()
                : _draft.Fields.Where(f => IsRegion(f.RegionCode, "HEADER")).OrderBy(f => f.SequenceNo).ToList();
            _detailRows = _draft == null ? new List<DocumentFieldMetadata>()
                : _draft.Fields.Where(f => IsRegion(f.RegionCode, "DETAIL")).OrderBy(f => f.SequenceNo).ToList();
            SetupFieldGrid(_headerGrid, _headerRows);
            SetupFieldGrid(_detailGrid, _detailRows);
            SetupCommandGrid(_draft == null ? new List<DocumentCommandMetadata>() : _draft.Commands.OrderBy(c => c.SequenceNo).ToList());
            SetupLayoutGrid(_draft == null ? new List<DocumentLayoutMetadata>() : _draft.Layouts.OrderBy(l => l.SequenceNo).ToList());
            if (_tabs.SelectedTabPage == _previewPage) RefreshPreview();
        }

        private static bool IsRegion(string code, string region)
        {
            return string.Equals((code ?? string.Empty).Trim(), region, StringComparison.OrdinalIgnoreCase);
        }

        private static GridView ViewOf(GridControl grid)
        {
            return (GridView)grid.MainView;
        }

        private void SetupFieldGrid(GridControl grid, List<DocumentFieldMetadata> rows)
        {
            grid.DataSource = rows;
            var view = ViewOf(grid);
            view.PopulateColumns();
            foreach (var hidden in new[] { "RegionCode", "IsReadOnly", "IsRequired", "LookupCode" })
                if (view.Columns[hidden] != null) view.Columns[hidden].Visible = false;
            Configure(view, "FieldCode", "Mã cột (code)", 0, false, 170);
            Configure(view, "Caption", "Tiêu đề", 1, true, 220);
            Configure(view, "SequenceNo", "Thứ tự", 2, true, 70);
            Configure(view, "Width", "Độ rộng", 3, true, 80);
            Configure(view, "DisplayFormat", "Định dạng", 4, true, 140);
            Configure(view, "IsVisible", "Hiện", 5, true, 60);
            Configure(view, "EditorCode", "Kiểu (khóa)", 6, false, 100);
            SortBySequence(view);
        }

        private void SetupCommandGrid(List<DocumentCommandMetadata> rows)
        {
            _commandGrid.DataSource = rows;
            var view = ViewOf(_commandGrid);
            view.PopulateColumns();
            if (view.Columns["RequiredStatusCode"] != null) view.Columns["RequiredStatusCode"].Visible = false;
            Configure(view, "CommandCode", "Mã lệnh (code)", 0, false, 200);
            Configure(view, "Caption", "Tiêu đề nút", 1, true, 240);
            Configure(view, "SequenceNo", "Thứ tự", 2, true, 70);
            Configure(view, "IsVisible", "Hiện nút", 3, true, 80);
            SortBySequence(view);
        }

        private void SetupLayoutGrid(List<DocumentLayoutMetadata> rows)
        {
            _layoutGrid.DataSource = rows;
            var view = ViewOf(_layoutGrid);
            view.PopulateColumns();
            if (view.Columns["RegionCode"] != null) view.Columns["RegionCode"].Visible = false;
            Configure(view, "LayoutCode", "Vùng", 0, false, 120);
            Configure(view, "Caption", "Tiêu đề vùng", 1, true, 280);
            Configure(view, "SequenceNo", "Thứ tự", 2, true, 70);
            Configure(view, "IsVisible", "Hiện", 3, true, 70);
            SortBySequence(view);
        }

        private static void Configure(GridView view, string name, string caption, int index, bool editable, int width)
        {
            GridColumn column = view.Columns[name];
            if (column == null) return;
            column.Caption = caption;
            column.Visible = true;
            column.VisibleIndex = index;
            column.Width = width;
            column.OptionsColumn.AllowEdit = editable;
            column.OptionsColumn.ReadOnly = !editable;
        }

        private static void SortBySequence(GridView view)
        {
            if (view.Columns["SequenceNo"] != null)
                view.Columns["SequenceNo"].SortOrder = DevExpress.Data.ColumnSortOrder.Ascending;
        }

        private void MoveFocused(int delta)
        {
            if (_session == null || _draft == null) return;
            var page = _tabs.SelectedTabPage;
            if (page == null) return;
            if (page.Controls.Contains(_headerGrid)) Move(_headerGrid, _headerRows, delta, f => f.SequenceNo, (f, v) => f.SequenceNo = v);
            else if (page.Controls.Contains(_detailGrid)) Move(_detailGrid, _detailRows, delta, f => f.SequenceNo, (f, v) => f.SequenceNo = v);
            else if (page.Controls.Contains(_commandGrid))
                Move(_commandGrid, (List<DocumentCommandMetadata>)_commandGrid.DataSource, delta, c => c.SequenceNo, (c, v) => c.SequenceNo = v);
            else if (page.Controls.Contains(_layoutGrid))
                Move(_layoutGrid, (List<DocumentLayoutMetadata>)_layoutGrid.DataSource, delta, l => l.SequenceNo, (l, v) => l.SequenceNo = v);
        }

        private void Move<T>(GridControl grid, List<T> rows, int delta, Func<T, int> getSequence, Action<T, int> setSequence) where T : class
        {
            var view = ViewOf(grid);
            var focused = view.GetFocusedRow() as T;
            if (focused == null) return;
            var ordered = rows.OrderBy(getSequence).ToList();
            int index = ordered.IndexOf(focused);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= ordered.Count) return;
            ordered.RemoveAt(index);
            ordered.Insert(target, focused);
            for (int i = 0; i < ordered.Count; i++) setSequence(ordered[i], (i + 1) * 10);
            view.RefreshData();
            int handle = view.FindRow(focused);
            if (handle != GridControl.InvalidRowHandle) view.FocusedRowHandle = handle;
            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool has = _session != null && _session.HasMetadata;
            _save.Enabled = has && _dirty && _service.CanEdit;
            _moveUp.Enabled = _moveDown.Enabled = has && _service.CanEdit;
            bool incomplete = _session != null &&
                (!_session.HasMetadata || _session.MissingFields.Count + _session.MissingCommands.Count > 0);
            _provision.Enabled = incomplete && _service.CanApprove;
            _provision.Visible = _service.CanApprove;
            _save.Visible = _service.CanEdit;
            Text = "Thiết kế chứng từ (Document Designer)" + (_dirty ? " *" : string.Empty);
        }

        private void SaveChanges()
        {
            if (_session == null || _draft == null) return;
            try
            {
                CommitEditors();
                var result = _service.Save(_session, _draft);
                _dirty = false;
                SetStatus(result.ChangedCount == 0
                    ? "Không có thay đổi nào để lưu."
                    : "Đã lưu " + result.ChangedCount + " thay đổi, ConfigVersion " + result.ConfigVersion +
                      ". Chứng từ đang mở sẽ dùng cấu hình mới sau khi mở lại; máy khác nhận ở lần đăng nhập kế tiếp.", false);
                OpenSelected();
            }
            catch (DocumentConfigConflictException ex)
            {
                XtraMessageBox.Show(this, ex.Message + Environment.NewLine + "Màn hình sẽ tải lại cấu hình mới nhất; thay đổi chưa lưu của bạn sẽ mất.",
                    "Document Designer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dirty = false;
                OpenSelected();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void Provision()
        {
            string moduleCode = _modules.SelectedItem as string;
            if (string.IsNullOrEmpty(moduleCode)) return;
            if (!ConfirmDiscard()) return;
            if (XtraMessageBox.Show(this,
                "Khai báo trong SQL chứng từ " + moduleCode + " từ danh sách đã đăng ký trong code (view, cột, lệnh)?" + Environment.NewLine +
                "Chỉ tạo các dòng còn thiếu, không ghi đè cấu hình đã có. Mọi cột được tạo ở trạng thái chỉ-đọc.",
                "Document Designer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                var result = _service.Provision(moduleCode);
                SetStatus("Đã khai báo/bổ sung " + result.ChangedCount + " dòng, ConfigVersion " + result.ConfigVersion + ".", false);
                OpenSelected();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void CommitEditors()
        {
            foreach (var grid in new[] { _headerGrid, _detailGrid, _commandGrid, _layoutGrid })
            {
                var view = ViewOf(grid);
                view.CloseEditor();
                view.UpdateCurrentRow();
            }
        }

        private void OnTabChanged()
        {
            if (_tabs.SelectedTabPage == _previewPage) RefreshPreview();
            else if (_tabs.SelectedTabPage == _auditPage) LoadAuditIfVisible();
        }

        private void RefreshPreview()
        {
            if (_session == null || _draft == null)
            {
                _previewHeader.DataSource = null;
                _previewDetail.DataSource = null;
                return;
            }
            try
            {
                CommitEditors();
                var resolved = _service.Preview(_session, _draft);
                _previewHeaderCaption.Text = resolved.HeaderVisible ? resolved.HeaderCaption : "(vùng Header bị ẩn)";
                _previewDetailCaption.Text = resolved.DetailVisible ? resolved.DetailCaption : "(vùng Detail bị ẩn)";
                BindPreview(_previewHeader, resolved.HeaderFields);
                BindPreview(_previewDetail, resolved.DetailFields);
            }
            catch (Exception ex)
            {
                _previewHeaderCaption.Text = "Cấu hình chưa hợp lệ: " + ex.GetBaseException().Message;
                _previewDetailCaption.Text = string.Empty;
            }
        }

        private static void BindPreview(GridControl grid, IReadOnlyList<ResolvedField> fields)
        {
            if (!(grid.MainView is GridView))
            {
                var created = new GridView(grid);
                grid.MainView = created;
                grid.ViewCollection.Add(created);
            }
            var view = (GridView)grid.MainView;
            view.OptionsBehavior.Editable = false;
            view.OptionsBehavior.ReadOnly = true;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            var table = new DataTable();
            foreach (var field in fields)
                table.Columns.Add(field.FieldCode, ColumnType(field.Editor));
            grid.DataSource = table;
            DocumentGridConfigurator.Apply(view, fields);
        }

        private static Type ColumnType(DocumentFieldType type)
        {
            switch (type)
            {
                case DocumentFieldType.Number: return typeof(decimal);
                case DocumentFieldType.Date:
                case DocumentFieldType.DateTime: return typeof(DateTime);
                case DocumentFieldType.Check: return typeof(bool);
                default: return typeof(string);
            }
        }

        private void LoadAuditIfVisible()
        {
            if (_tabs.SelectedTabPage != _auditPage) return;
            string moduleCode = _modules.SelectedItem as string;
            if (string.IsNullOrEmpty(moduleCode)) return;
            try
            {
                _auditGrid.DataSource = _service.ListAudit(moduleCode);
                if (_auditGrid.MainView is GridView)
                {
                    var view = (GridView)_auditGrid.MainView;
                    view.OptionsBehavior.Editable = false;
                    view.OptionsView.ColumnAutoWidth = false;
                    view.BestFitColumns();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private bool ConfirmDiscard()
        {
            if (!_dirty) return true;
            return XtraMessageBox.Show(this, "Có thay đổi chưa lưu. Bỏ thay đổi?", "Document Designer",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ConfirmDiscard()) e.Cancel = true;
            base.OnFormClosing(e);
        }

        private void SetStatus(string text, bool warning)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = warning ? Color.DarkOrange : Color.DimGray;
        }

        private void ShowError(Exception ex)
        {
            XtraMessageBox.Show(this, ex.GetBaseException().Message, "Document Designer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
