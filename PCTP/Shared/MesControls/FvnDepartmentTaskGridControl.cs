using System.Windows.Forms;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Shared.MesControls
{
    /// <summary>
    /// Reusable read-only department task list. Business scope, task labels and commands
    /// remain supplied by the calling application service/form; this control cannot post changes.
    /// </summary>
    public sealed class FvnDepartmentTaskGridControl : DevExpress.XtraEditors.XtraUserControl
    {
        public GridControl Grid { get; private set; }
        public GridView View { get; private set; }

        public FvnDepartmentTaskGridControl()
        {
            Dock = DockStyle.Fill;
            Grid = new GridControl { Dock = DockStyle.Fill };
            View = new GridView(Grid);
            Grid.MainView = View;
            Grid.ViewCollection.Add(View);
            View.OptionsBehavior.Editable = false;
            View.OptionsBehavior.ReadOnly = true;
            View.OptionsView.ShowGroupPanel = false;
            View.OptionsView.ShowAutoFilterRow = true;
            View.OptionsView.ShowIndicator = true;
            View.OptionsView.ColumnAutoWidth = false;
            View.OptionsSelection.MultiSelect = false;
            View.OptionsSelection.EnableAppearanceFocusedCell = false;
            View.FocusRectStyle = DrawFocusRectStyle.RowFocus;
            Controls.Add(Grid);
        }

        public void Bind(object dataSource)
        {
            Grid.DataSource = dataSource;
        }
    }
}
