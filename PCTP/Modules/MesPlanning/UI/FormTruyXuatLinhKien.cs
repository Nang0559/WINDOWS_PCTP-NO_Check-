using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning.UI
{
    public sealed class FormTruyXuatLinhKien : Form
    {
        private readonly IProductionTraceService _service;
        private readonly TextBox _lotNo = new TextBox();
        private readonly TextBox _siteCode = new TextBox();
        private readonly ComboBox _direction = new ComboBox();
        private readonly DataGridView _results = new DataGridView();
        private readonly Label _status = new Label();

        public FormTruyXuatLinhKien()
            : this(new ProductionTraceService(new ProductionTraceRepository()))
        {
        }

        public FormTruyXuatLinhKien(IProductionTraceService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            AuthorizationGuard.Demand("REPORT.COMPONENT_TRACEABILITY", "VIEW");
            Text = "Truy xuất linh kiện / LOT sản xuất";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1380, 820);
            MinimumSize = new Size(1050, 620);
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            filters.Controls.Add(new Label
            {
                Text = "LOT cần truy xuất",
                AutoSize = true,
                Padding = new Padding(0, 9, 0, 0)
            });
            _lotNo.Width = 260;
            filters.Controls.Add(_lotNo);
            filters.Controls.Add(new Label { Text = "Site (nếu cần)", AutoSize = true, Padding = new Padding(12, 9, 0, 0) });
            _siteCode.Width = 90;
            filters.Controls.Add(_siteCode);

            filters.Controls.Add(new Label
            {
                Text = "Chiều truy xuất",
                AutoSize = true,
                Padding = new Padding(12, 9, 0, 0)
            });
            _direction.DropDownStyle = ComboBoxStyle.DropDownList;
            _direction.Width = 240;
            _direction.Items.Add(new TraceDirection("UPSTREAM", "Ngược: Thành phẩm → linh kiện"));
            _direction.Items.Add(new TraceDirection("DOWNSTREAM", "Xuôi: Linh kiện → thành phẩm"));
            _direction.SelectedIndex = 0;
            filters.Controls.Add(_direction);

            var search = new Button { Text = "TRUY XUẤT", Width = 130, Height = 30 };
            search.Click += SearchClick;
            filters.Controls.Add(search);

            _results.Dock = DockStyle.Fill;
            _results.ReadOnly = true;
            _results.AllowUserToAddRows = false;
            _results.AllowUserToDeleteRows = false;
            _results.MultiSelect = false;
            _results.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _results.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _results.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Text = "Nhập LOT để truy xuất theo dữ liệu tiêu hao thực tế.";

            root.Controls.Add(filters, 0, 0);
            root.Controls.Add(_results, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);
        }

        private void SearchClick(object sender, EventArgs e)
        {
            try
            {
                var selected = _direction.SelectedItem as TraceDirection;
                if (selected == null)
                    throw new InvalidOperationException("Chọn chiều truy xuất.");

                var data = _service.Trace(_lotNo.Text, selected.Code, _siteCode.Text);
                _results.DataSource = data;
                _status.Text = data.Rows.Count == 0
                    ? "Không tìm thấy quan hệ genealogy cho LOT này."
                    : "Đã tìm thấy " + data.Rows.Count.ToString("N0") +
                      " dòng truy xuất. Dữ liệu dựa trên tiêu hao LOT thực tế.";
            }
            catch (Exception ex)
            {
                _results.DataSource = null;
                _status.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Truy xuất linh kiện",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class TraceDirection
        {
            public string Code { get; }
            private string Caption { get; }

            public TraceDirection(string code, string caption)
            {
                Code = code;
                Caption = caption;
            }

            public override string ToString() => Caption;
        }
    }
}
