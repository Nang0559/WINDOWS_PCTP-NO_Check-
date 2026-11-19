using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>Read-only timeline viewer for plan, order or production LOT workflow history.</summary>
    public sealed class FormProductionWorkflowTimeline : DevExpress.XtraEditors.XtraForm
    {
        private readonly IProductionWorkflowService _service;
        private readonly ComboBox _entityType = new ComboBox();
        private readonly TextBox _entityId = new TextBox();
        private readonly DataGridView _events = new DataGridView();
        private readonly Label _message = new Label();

        public FormProductionWorkflowTimeline(IProductionWorkflowService service)
        {
            AuthorizationGuard.Demand("MES.PRODUCTION_EXECUTION", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Text = "MES - Lịch sử workflow sản xuất";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 650);
            MinimumSize = new Size(850, 480);
            BuildUi();
        }

        private void BuildUi()
        {
            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 58, Padding = new Padding(10),
                WrapContents = false
            };
            _entityType.DropDownStyle = ComboBoxStyle.DropDownList;
            _entityType.Width = 150;
            _entityType.Items.AddRange(new object[] { "PLAN", "ORDER", "OPERATION" });
            _entityType.SelectedIndex = 0;
            _entityId.Width = 150;
            var load = new Button { Text = "TẢI LỊCH SỬ", Width = 130, Height = 30 };
            load.Click += LoadClick;
            header.Controls.Add(new Label { Text = "Đối tượng", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            header.Controls.Add(_entityType);
            header.Controls.Add(new Label { Text = "ID (Operation = ProductionLotId)", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
            header.Controls.Add(_entityId);
            header.Controls.Add(load);

            _events.Dock = DockStyle.Fill;
            _events.ReadOnly = true;
            _events.AllowUserToAddRows = false;
            _events.AllowUserToDeleteRows = false;
            _events.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _events.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            _message.Dock = DockStyle.Bottom;
            _message.Height = 30;
            _message.TextAlign = ContentAlignment.MiddleLeft;
            _message.Padding = new Padding(10, 0, 0, 0);
            Controls.Add(_events);
            Controls.Add(_message);
            Controls.Add(header);
        }

        public void SetEntity(string entityType, long entityId)
        {
            _entityType.SelectedItem = entityType;
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
                var type = Convert.ToString(_entityType.SelectedItem);
                _events.DataSource = _service.GetTimeline(type, id);
                _message.Text = "Đã tải " + _events.Rows.Count.ToString("N0") + " sự kiện " + type + ".";
            }
            catch (Exception ex)
            {
                _events.DataSource = null;
                _message.Text = ex.Message;
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}