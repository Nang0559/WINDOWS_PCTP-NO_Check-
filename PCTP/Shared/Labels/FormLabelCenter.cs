using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Shared.Labels
{
    /// <summary>
    /// Trung tâm layout tem: liệt kê mọi loại tem đã đăng ký trong <see cref="LabelCatalog"/>; mở Report Designer,
    /// xem trước / in thử bằng dữ liệu mẫu, khôi phục layout gốc. Xem cần VIEW, thiết kế và khôi phục cần EDIT trên ADMIN.DOCUMENT_DESIGNER.
    /// </summary>
    public sealed class FormLabelCenter : XtraForm
    {
        private sealed class Row
        {
            public string Code { get; set; }
            public string Title { get; set; }
            public string Paper { get; set; }
            public string Layout { get; set; }
            [Browsable(false)] public LabelDefinition Definition { get; set; }
        }

        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly LabelControl _status = new LabelControl();
        private readonly SimpleButton _design = new SimpleButton { Text = "Thiết kế layout" };
        private readonly SimpleButton _preview = new SimpleButton { Text = "Xem trước / In thử" };
        private readonly SimpleButton _reset = new SimpleButton { Text = "Khôi phục layout gốc" };
        private readonly SimpleButton _folder = new SimpleButton { Text = "Mở thư mục layout" };

        public FormLabelCenter()
        {
            Text = "Thiết kế layout tem";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new System.Drawing.Size(960, 520);

            var bar = new PanelControl { Dock = DockStyle.Top, Height = 48 };
            int x = 8;
            foreach (SimpleButton b in new[] { _design, _preview, _reset, _folder })
            {
                b.SetBounds(x, 8, 170, 32);
                bar.Controls.Add(b);
                x += 178;
            }
            _status.Dock = DockStyle.Bottom;
            _status.Padding = new Padding(8, 4, 8, 4);

            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.GridControl = _grid;
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;

            Controls.Add(_grid);
            Controls.Add(bar);
            Controls.Add(_status);

            _design.Click += (s, e) => Run(() => { LabelReportService.Design(this, Selected().Definition); Reload(); });
            _preview.Click += (s, e) => Run(() => LabelReportService.PreviewSample(this, Selected().Definition));
            _reset.Click += (s, e) => Run(ResetSelected);
            _folder.Click += (s, e) => Run(OpenFolder);
            _view.DoubleClick += (s, e) => _preview.PerformClick();

            bool canEdit = LabelReportService.CanEdit();
            _design.Enabled = canEdit;
            _reset.Enabled = canEdit;
            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            var rows = LabelCatalog.All().Select(d =>
            {
                DateTime? modified = LabelLayoutStore.LastModified(d.Code);
                return new Row
                {
                    Code = d.Code,
                    Title = d.Title,
                    Paper = d.Paper,
                    Layout = modified.HasValue ? "Đã chỉnh " + modified.Value.ToString("dd/MM/yyyy HH:mm") : "Bố cục gốc",
                    Definition = d
                };
            }).ToList();
            _grid.DataSource = new BindingList<Row>(rows);
            _view.PopulateColumns();
            Caption("Code", "Mã"); Caption("Title", "Loại tem"); Caption("Paper", "Khổ giấy / số tem"); Caption("Layout", "Layout đang dùng");
            _view.BestFitColumns();
            _status.Text = "Thư mục lưu layout: " + LabelLayoutStore.Folder +
                (_design.Enabled ? string.Empty : "   (chỉ xem: cần quyền EDIT trên ADMIN.DOCUMENT_DESIGNER để thiết kế)");
        }

        private void Caption(string field, string caption)
        {
            var col = _view.Columns[field];
            if (col != null) col.Caption = caption;
        }

        private Row Selected()
        {
            var row = _view.GetFocusedRow() as Row;
            if (row == null) throw new InvalidOperationException("Chưa chọn loại tem nào.");
            return row;
        }

        private void ResetSelected()
        {
            Row row = Selected();
            if (LabelLayoutStore.FindCustom(row.Code) == null)
            {
                XtraMessageBox.Show(this, "Tem này đang dùng bố cục gốc.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (XtraMessageBox.Show(this, "Bỏ layout đã chỉnh của \"" + row.Title + "\" và quay về bố cục gốc? Bản cũ vẫn được giữ trong thư mục history.",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            LabelReportService.ResetLayout(row.Definition);
            Reload();
        }

        private void OpenFolder()
        {
            string folder = LabelLayoutStore.Folder;
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", "\"" + folder + "\"");
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
