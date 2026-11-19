using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>Read-only workflow/audit timeline for plan, order and production LOT.</summary>
    public sealed class FormProductionWorkflowTimeline : XtraForm
    {
        private readonly IProductionWorkflowService _service;
        private readonly ComboBoxEdit _entityType = new ComboBoxEdit();
        private readonly TextEdit _entityId = new TextEdit();
        private readonly PCTP.Shared.MesControls.FvnWorkflowTimelineControl _timeline =
            new PCTP.Shared.MesControls.FvnWorkflowTimelineControl();

        public FormProductionWorkflowTimeline(IProductionWorkflowService service)
        {
            // Entity-specific VIEW permission and Site scope are enforced by the workflow service at query time.
            _service = service ?? throw new ArgumentNullException(nameof(service));
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

            _timeline.Dock = DockStyle.Fill;

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_timeline, 0, 1);
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
                _timeline.Bind(_service.GetTimeline(type, id), type, id);
            }
            catch (Exception ex)
            {
                _timeline.Clear(ex.Message);
                XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
