using System.Data;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Framework.Documents.UI
{
    /// <summary>
    /// Reusable read-only detail grid rendered from a resolved document. It receives data; it never
    /// queries SQL and exposes no edit or command path.
    /// </summary>
    public sealed class DocumentDetailControl : XtraUserControl
    {
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;

        public GridView View { get { return _view; } }

        public DocumentDetailControl()
        {
            _view = new GridView(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsSelection.MultiSelect = false;
            _view.FocusRectStyle = DrawFocusRectStyle.RowFocus;
            Dock = DockStyle.Fill;
            Controls.Add(_grid);
        }

        public DataRow FocusedRow
        {
            get { return _view.GetFocusedDataRow(); }
        }

        public void Bind(ResolvedDocument document, DataTable detail)
        {
            if (document == null) throw new System.ArgumentNullException("document");
            _grid.DataSource = detail ?? new DataTable();
            DocumentGridConfigurator.Apply(_view, document.DetailFields);
        }
    }
}
