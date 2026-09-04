using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

    namespace PCTP.VIEWSTOCK.CanVas
    {
        /// <summary>
        /// Popup nhỏ không viền hiển thị bảng tổng hợp mã hàng của 1 Rack,
        /// dùng GridControl (DevExpress) để copy dạng bảng chuẩn TSV.
        /// </summary>
        public class RackSummaryPopup : Form
        {
            private GridControl _grid;
            private GridView _gridView;
            private LabelControl _lblTitle;
            private SimpleButton _btnCopy;
            private DataTable _dt;
        // ← THÊM: không cho form này activate/lấy focus -> tránh giật khi Show
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOWNOACTIVATE = 4;
        public RackSummaryPopup()
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                TopMost = true;
                BackColor = Color.LightYellow;
                Padding = new Padding(1);

                BuildUI();
            }

            private void BuildUI()
            {
                var outer = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.LightYellow,
                    Padding = new Padding(6)
                };

                _lblTitle = new LabelControl
                {
                    Dock = DockStyle.Top,
                    Height = 22,
                    Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold) }
                };

                _btnCopy = new SimpleButton
                {
                    Text = "📋 Copy bảng",
                    Dock = DockStyle.Bottom,
                    Height = 28
                };
                _btnCopy.Click += (s, e) => CopyTableToClipboard();

                _grid = new GridControl { Dock = DockStyle.Fill };
                _gridView = new GridView(_grid);
                _grid.MainView = _gridView;
                _gridView.OptionsView.ShowGroupPanel = false;
                _gridView.OptionsBehavior.Editable = false;
                _gridView.OptionsSelection.MultiSelect = true;

                _gridView.Columns.Add(new DevExpress.XtraGrid.Columns.GridColumn
                { FieldName = "MaHang", Caption = "Mã hàng", VisibleIndex = 0, Width = 150 });
                _gridView.Columns.Add(new DevExpress.XtraGrid.Columns.GridColumn
                { FieldName = "ViTri", Caption = "Vị trí", VisibleIndex = 1, Width = 60 });
                _gridView.Columns.Add(new DevExpress.XtraGrid.Columns.GridColumn
                { FieldName = "SL", Caption = "SL", VisibleIndex = 2, Width = 70 });

                _gridView.RowStyle += (s, e) =>
                {
                    var row = _gridView.GetDataRow(e.RowHandle);
                    if (row != null && row["MaHang"]?.ToString() == "Tổng cộng")
                    {
                        e.Appearance.Font = new Font("Tahoma", 8.5f, FontStyle.Bold);
                        e.Appearance.BackColor = Color.Beige;
                    }
                };

                outer.Controls.Add(_grid);
                outer.Controls.Add(_btnCopy);
                outer.Controls.Add(_lblTitle);

                Controls.Add(outer);
            }

            /// <summary>Nạp dữ liệu tóm tắt của 1 Rack và hiện popup tại vị trí chỉ định (toạ độ màn hình).</summary>
            public void ShowSummary(string rackTitle,
    IEnumerable<KeyValuePair<string, (int Count, int TotalQty)>> items,
    Point screenLocation)
            {
                _lblTitle.Text = rackTitle;

                _dt = new DataTable();
                _dt.Columns.Add("MaHang");
                _dt.Columns.Add("ViTri", typeof(int));
                _dt.Columns.Add("SL", typeof(int));

                int tongViTri = 0, tongSl = 0;
                foreach (var kvp in items)
                {
                    _dt.Rows.Add(kvp.Key, kvp.Value.Count, kvp.Value.TotalQty);
                    tongViTri += kvp.Value.Count;
                    tongSl += kvp.Value.TotalQty;
                }
                _dt.Rows.Add("Tổng cộng", tongViTri, tongSl);

                _grid.DataSource = _dt;

                // ← SỬA: tự ước lượng chiều cao theo số dòng, không đọc ViewInfo (protected)
                const int rowHeight = 22;
                const int headerHeight = 24;
                int gridContentHeight = headerHeight + rowHeight * _dt.Rows.Count;
                int gridHeight = Math.Min(300, gridContentHeight);

                Size = new Size(340, 22 + gridHeight + 28 + 16);
                Location = screenLocation;

            if (!Visible)
            {
                if (!IsHandleCreated) CreateHandle(); // đảm bảo có handle trước khi ShowWindow
                ShowWindow(Handle, SW_SHOWNOACTIVATE);
            }
            else
            {
                Invalidate(); // chỉ vẽ lại vị trí/nội dung mới, không toggle Show/Hide gây giật
            }
        }

            private void CopyTableToClipboard()
            {
                for (int i = 0; i < _gridView.RowCount; i++)
                    _gridView.SelectRow(i);

                _gridView.CopyToClipboard(); // ← copy chuẩn TSV, dán Excel/Word đúng cột

                _btnCopy.Text = "✅ Đã copy!";
                var t = new Timer { Interval = 1200 };
                t.Tick += (s, e) =>
                {
                    _btnCopy.Text = "📋 Copy bảng";
                    t.Stop();
                    t.Dispose();
                };
                t.Start();
            }

            //protected override void OnDeactivate(EventArgs e)
            //{
            //    base.OnDeactivate(e);
            //    Hide();
            //}
        }
    }

