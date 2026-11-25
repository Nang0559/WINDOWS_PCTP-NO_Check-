using System;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// "Khai báo cơ sở dữ liệu" (Bravo): lists the approved data sources (read-only views) and, for a user holding APPROVE,
    /// approves another MES.vw_FVN_* view or creates a screen on an approved source. The permission that guards reading a
    /// source is chosen here by the approver, never by the layout author, and the Site column is mandatory.
    /// </summary>
    public sealed class UiDataSourceForm : XtraForm
    {
        private readonly UiLayoutDesignerService _service;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly ComboBoxEdit _candidate = new ComboBoxEdit();
        private readonly TextEdit _sourceCode = new TextEdit();
        private readonly TextEdit _displayName = new TextEdit();
        private readonly TextEdit _function = new TextEdit();
        private readonly TextEdit _siteColumn = new TextEdit { Text = "SiteCode" };
        private readonly TextEdit _screenCode = new TextEdit();
        private readonly TextEdit _screenTitle = new TextEdit();
        private readonly SimpleButton _approve = new SimpleButton { Text = "Duyệt nguồn dữ liệu" };
        private readonly SimpleButton _createScreen = new SimpleButton { Text = "Tạo màn hình từ nguồn đang chọn" };
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 44, AutoSizeMode = LabelAutoSizeMode.None };

        public UiDataSourceForm(UiLayoutDesignerService service, string screenCode = null)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
            _view = new GridView(_grid);
            Text = "Khai báo cơ sở dữ liệu";
            StartPosition = FormStartPosition.CenterParent;
            Size = new System.Drawing.Size(1000, 640);
            MinimumSize = new System.Drawing.Size(900, 520);
            BuildUi();
            Load += (s, e) => Reload();
        }

        private void BuildUi()
        {
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.FocusedRowChanged += (s, e) => FillFromSelected();

            _candidate.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            _candidate.SelectedIndexChanged += (s, e) =>
            {
                string view = _candidate.Text ?? string.Empty;
                if (view.Length == 0) return;
                _sourceCode.Text = view.Substring(view.IndexOf('.') + 1).Replace("vw_FVN_", string.Empty).ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(_displayName.Text)) _displayName.Text = _sourceCode.Text;
            };
            _approve.Click += (s, e) => Approve();
            _createScreen.Click += (s, e) => CreateScreen();

            var panel = new PanelControl { Dock = DockStyle.Bottom, Height = 250 };
            int y = 10;
            Action<string, Control> row = (caption, editor) =>
            {
                panel.Controls.Add(new LabelControl { Text = caption, Left = 10, Top = y + 4 });
                editor.Left = 190; editor.Top = y; editor.Width = 330;
                panel.Controls.Add(editor);
                y += 32;
            };
            row("View chưa duyệt (MES.vw_FVN_*)", _candidate);
            row("Mã nguồn", _sourceCode);
            row("Tên hiển thị", _displayName);
            row("Mã quyền xem (SEC.FunctionCatalog)", _function);
            row("Cột Site (bắt buộc)", _siteColumn);
            _approve.Left = 190; _approve.Top = y; _approve.Width = 200; panel.Controls.Add(_approve);

            int x = 560, y2 = 10;
            panel.Controls.Add(new LabelControl { Text = "Mã màn hình mới", Left = x, Top = y2 + 4 });
            _screenCode.Left = x + 120; _screenCode.Top = y2; _screenCode.Width = 290; panel.Controls.Add(_screenCode);
            panel.Controls.Add(new LabelControl { Text = "Tiêu đề", Left = x, Top = y2 + 36 });
            _screenTitle.Left = x + 120; _screenTitle.Top = y2 + 32; _screenTitle.Width = 290; panel.Controls.Add(_screenTitle);
            _createScreen.Left = x + 120; _createScreen.Top = y2 + 68; _createScreen.Width = 290; panel.Controls.Add(_createScreen);

            Controls.Add(_grid);
            Controls.Add(panel);
            Controls.Add(_status);
        }

        private void Reload()
        {
            try
            {
                _grid.DataSource = _service.ListSources().ToList();
                _view.PopulateColumns();
                _view.BestFitColumns();
                bool canApprove = _service.CanApprove;
                _approve.Enabled = canApprove;
                _createScreen.Enabled = canApprove;
                _candidate.Properties.Items.Clear();
                if (canApprove) _candidate.Properties.Items.AddRange(_service.ListCandidateViews().ToArray());
                SetStatus(canApprove ? "Chọn view chưa duyệt, đặt mã quyền xem rồi bấm Duyệt." :
                    "Chỉ xem. Cần quyền APPROVE trên ADMIN.DOCUMENT_DESIGNER để duyệt nguồn dữ liệu hoặc tạo màn hình.", false);
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private void FillFromSelected()
        {
            var source = _view.GetFocusedRow() as UiDataSourceInfo;
            if (source == null) return;
            _sourceCode.Text = source.SourceCode;
            _displayName.Text = source.DisplayName;
            _function.Text = source.ViewFunctionCode;
            _siteColumn.Text = source.SiteColumn;
        }

        private void Approve()
        {
            try
            {
                string view = _candidate.Text;
                if (string.IsNullOrWhiteSpace(view))
                {
                    var existing = _view.GetFocusedRow() as UiDataSourceInfo;
                    view = existing == null ? null : existing.ViewName;     // re-approve an existing source (change name / permission)
                }
                _service.ApproveSource(new UiDataSourceInfo
                {
                    SourceCode = (_sourceCode.Text ?? string.Empty).Trim().ToUpperInvariant(),
                    ViewName = view,
                    DisplayName = (_displayName.Text ?? string.Empty).Trim(),
                    ViewFunctionCode = (_function.Text ?? string.Empty).Trim().ToUpperInvariant(),
                    SiteColumn = (_siteColumn.Text ?? string.Empty).Trim(),
                    IsActive = true
                });
                Reload();
                SetStatus("Đã duyệt nguồn dữ liệu " + _sourceCode.Text + ".", false);
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private void CreateScreen()
        {
            try
            {
                var source = _view.GetFocusedRow() as UiDataSourceInfo;
                if (source == null) throw new InvalidOperationException("Chọn một nguồn dữ liệu trong danh sách.");
                _service.CreateScreen((_screenCode.Text ?? string.Empty).Trim().ToUpperInvariant(),
                    (_screenTitle.Text ?? string.Empty).Trim(), source.SourceCode);
                SetStatus("Đã tạo màn hình " + _screenCode.Text + ". Mở \"Khai báo layout\" để thiết kế; thêm vào menu cần một mục menu đã đăng ký trong C#.", false);
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = error ? System.Drawing.Color.Firebrick : System.Drawing.Color.FromArgb(70, 85, 100);
        }
    }
}