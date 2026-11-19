using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Shared.MesControls
{
    /// <summary>Reusable read-only workflow event timeline. It never loads data or performs transitions.</summary>
    public sealed class FvnWorkflowTimelineControl : XtraUserControl
    {
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly LabelControl _message = new LabelControl();

        public FvnWorkflowTimelineControl()
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
            _message.Dock = DockStyle.Bottom;
            _message.Height = 30;
            _message.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            Controls.Add(_grid);
            Controls.Add(_message);
        }

        public void Bind(DataTable events, string entityType, long entityId)
        {
            _grid.DataSource = events ?? new DataTable();
            _view.BestFitColumns();
            _message.Text = "Đã tải " + _view.RowCount.ToString("N0") +
                " sự kiện " + (entityType ?? string.Empty) + " #" + entityId + ".";
        }

        public void Clear(string message)
        {
            _grid.DataSource = null;
            _message.Text = message ?? string.Empty;
        }
    }
}
