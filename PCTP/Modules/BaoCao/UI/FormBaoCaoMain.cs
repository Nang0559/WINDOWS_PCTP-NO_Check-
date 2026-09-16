using DevExpress.XtraEditors;
using PCTP.Shell.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    /// <summary>
    /// Entry point for read-only reports and traceability queries.
    /// Business transactions remain in their owning modules.
    /// </summary>
    public sealed class FormBaoCaoMain : WmsTitleBarForm
    {
        public FormBaoCaoMain()
        {
            Text = "Báo cáo / Tra cứu";
            Size = new Size(520, 330);
            StartPosition = FormStartPosition.CenterParent;
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(20)
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(new LabelControl
            {
                Text = "BÁO CÁO / TRA CỨU",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 13, FontStyle.Bold) }
            }, 0, 0);

            var trace = new SimpleButton
            {
                Text = "Tra cứu truy xuất giao hàng (QR / LOT / khách hàng)",
                Dock = DockStyle.Fill
            };
            trace.Click += (s, e) => new FormBaoCaoTraceability().ShowDialog(this);
            root.Controls.Add(trace, 0, 1);

            var stock = new SimpleButton
            {
                Text = "Lịch sử kho / Tồn hiện tại",
                Dock = DockStyle.Fill
            };
            stock.Click += (s, e) => new FormBaoCaoStockHistory().ShowDialog(this);
            root.Controls.Add(stock, 0, 2);

            var quality = new SimpleButton
            {
                Text = "Lịch sử QC / Inspection",
                Dock = DockStyle.Fill
            };
            quality.Click += (s, e) => new FormBaoCaoQualityHistory().ShowDialog(this);
            root.Controls.Add(quality, 0, 3);

            Controls.Add(root);
        }
    }
}
