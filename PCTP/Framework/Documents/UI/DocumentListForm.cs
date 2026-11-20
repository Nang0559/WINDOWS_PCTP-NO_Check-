using System;
using System.Data;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Framework.Documents.UI
{
    /// <summary>Metadata-driven document list. Opening a row shows the read-only document editor.</summary>
    public sealed class DocumentListForm : XtraForm
    {
        private const int ListLimit = 500;
        private readonly DocumentRuntimeServices _runtime;
        private readonly string _moduleCode;
        private readonly string _siteCode;
        private readonly IDocumentCommandInputProvider _inputProvider;
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl();
        private ResolvedDocument _document;

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
            var bar = new PanelControl { Dock = DockStyle.Top, Height = 42, Padding = new Padding(6) };
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
                _grid.DataSource = _runtime.Data.ListHeaders(_moduleCode, _siteCode, ListLimit);
                DocumentGridConfigurator.Apply(_view, _document.HeaderFields);
                _status.Text = "Hiển thị tối đa " + ListLimit + " chứng từ mới nhất của Site " + _siteCode + ". Nhấp đúp để mở.";
                _status.Appearance.ForeColor = System.Drawing.Color.FromArgb(70, 85, 100);
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được danh sách: " + ex.GetBaseException().Message;
                _status.Appearance.ForeColor = System.Drawing.Color.Firebrick;
            }
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
