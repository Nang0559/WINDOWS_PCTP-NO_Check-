using System.Data;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Shared.MesControls
{
    /// <summary>Read-only list of independently addressable blockers/HOLDs for the selected flow.</summary>
    public sealed class FvnHoldListControl : XtraUserControl
    {
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;

        public FvnHoldListControl()
        {
            Dock = System.Windows.Forms.DockStyle.Fill;
            _view = new GridView(_grid);
            _grid.Dock = System.Windows.Forms.DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowColumnHeaders = true;
            _view.OptionsView.RowAutoHeight = true;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            Controls.Add(_grid);
        }

        public void Bind(DataTable holds)
        {
            _grid.DataSource = holds ?? new DataTable();
            if (_view.Columns["HoldType"] != null) _view.Columns["HoldType"].Caption = "Loại chặn";
            if (_view.Columns["OwnerDepartment"] != null) _view.Columns["OwnerDepartment"].Caption = "Bộ phận xử lý";
            if (_view.Columns["BlockedDepartment"] != null) _view.Columns["BlockedDepartment"].Caption = "Bộ phận bị chặn";
            if (_view.Columns["Reason"] != null) _view.Columns["Reason"].Caption = "Nguyên nhân";
            if (_view.Columns["HoldStatus"] != null) _view.Columns["HoldStatus"].Caption = "Trạng thái";
            if (_view.Columns["IsRelated"] != null) _view.Columns["IsRelated"].Caption = "Phạm vi";
            _view.BestFitColumns();
        }
    }
}