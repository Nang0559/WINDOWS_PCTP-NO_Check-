using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>Read-only workflow/audit timeline for plan, order and production LOT.</summary>
    public sealed class FormProductionWorkflowTimeline : XtraForm
    {
        private readonly IProductionWorkflowService _service;
        private readonly ComboBoxEdit _entityType = new ComboBoxEdit();
        private readonly TextEdit _entityId = new TextEdit();
        private readonly GridControl _events = new GridControl();
        private readonly GridView _view;
        private readonly LabelControl _message = new LabelControl();

        public FormProductionWorkflowTimeline(IProductionWorkflowService service)
        {
            // Entity-specific VIEW permission and Site scope are enforced by the workflow service at query time.
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _view = new GridView(_events);
            Text = "MES - Lịch sử workflow sản xuất";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 700);
            MinimumSize = new Size(900, 520);
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            _entityType.Width = 160;
            _entityType.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _entityType.Properties.Items.AddRange(new object[] { "PLAN", "ORDER", "OPERATION" });
            _entityType.SelectedIndex = 0;
            _entityId.Width = 170;
            _entityId.Properties.NullValuePrompt = "ID chứng từ / LOT";
            var load = new SimpleButton { Text = "TẢI LỊCH SỬ", Width = 130, Height = 32 };
            load.Click += LoadClick;
            header.Controls.Add(new LabelControl { Text = "Đối tượng", AutoSize = true, Padding = new Padding(0, 9, 0, 0) });
            header.Controls.Add(_entityType);
            header.Controls.Add(new LabelControl { Text = "Entity ID (OPERATION = ProductionLotId)", AutoSize = true, Padding = new Padding(10, 9, 0, 0) });
            header.Controls.Add(_entityId);
            header.Controls.Add(load);

            _events.Dock = DockStyle.Fill;
            _events.MainView = _view;
            _events.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsSelection.MultiSelect = false;

            _message.Dock = DockStyle.Fill;
            _message.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _message.Text = "Lịch sử chỉ đọc từ workflow event và operation event.";

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_events, 0, 1);
            root.Controls.Add(_message, 0, 2);
            Controls.Add(root);
        }

        public void SetEntity(string entityType, long entityId)
        {
            _entityType.SelectedIndex = string.Equals(entityType, "PLAN", StringComparison.OrdinalIgnoreCase) ? 0 :
                string.Equals(entityType, "ORDER", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            _entityId.Text = entityId.ToString();
            LoadClick(this, EventArgs.Empty);
        }

        private void LoadClick(object sender, EventArgs e)
        {
            try
            {
                long id;
                if (!long.TryParse(_entityId.Text.Trim(), out id) || id <= 0)
                    throw new InvalidOperationException("ID phải là số nguyên dương.");
                if (_entityType.SelectedIndex < 0)
                    throw new InvalidOperationException("Chọn loại đối tượng.");
                string type = Convert.ToString(_entityType.EditValue);
                _events.DataSource = _service.GetTimeline(type, id);
                _view.BestFitColumns();
                _message.Text = "Đã tải " + _view.RowCount.ToString("N0") + " sự kiện " + type + " #" + id + ".";
            }
            catch (Exception ex)
            {
                _events.DataSource = null;
                _message.Text = ex.Message;
                XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
