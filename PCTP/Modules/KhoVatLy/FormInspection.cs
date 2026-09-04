using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    public partial class FormInspection : DevExpress.XtraEditors.XtraForm
    {
        private readonly QRCodeInfo _temTong;
        private readonly InspectionConfig _config;
        private readonly IInspectionService _inspSvc;  // ✅ inject
        private readonly string _inspectionCode;

        private int _requiredQty;
        private List<BoxScanResult> _scannedBoxes = new List<BoxScanResult>();

        private TextBox _txtBoxScan;
        private GridControl _grid;
        private GridView _gridView;
        private SpinEdit _spinQty;
        private LabelControl _lblProgress;
        private SimpleButton _btnConfirmOK, _btnFail, _btnCancel;

        public bool InspectionPassed { get; private set; } = false;

        public FormInspection(QRCodeInfo temTong, InspectionConfig config, IInspectionService inspSvc)
        {
            InitializeComponent();
            _temTong = temTong;
            _config = config;
            _inspSvc = inspSvc
            ?? throw new ArgumentNullException(nameof(inspSvc));
            _requiredQty = config.DefaultQty;
            _inspectionCode = $"INS-{DateTime.Now:yyyyMMddHHmmss}";
            BuildUI();
            UpdateProgress();
        }

        private void BuildUI()
        {
            this.Text = $"Kiểm tra hàng — {_temTong.ItemCode}";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // info tem tổng
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // scan + số thùng
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grid
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // buttons

            // ── Row 0: Thông tin tem tổng ──────────────────────────
            var grpTong = new GroupControl
            {
                Text = "📦 Thông tin Phiếu Tổng",
                Dock = DockStyle.Fill
            };

            var tblTong = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(5)
            };
            for (int i = 0; i < 5; i++)
                tblTong.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

            tblTong.Controls.Add(MakeInfoBox("Mã hàng", _temTong.ItemCode), 0, 0);
            tblTong.Controls.Add(MakeInfoBox("LotNo", _temTong.LotNo), 1, 0);
            tblTong.Controls.Add(MakeInfoBox("Ngày SX", _temTong.NgaySX), 2, 0);
            tblTong.Controls.Add(MakeInfoBox("Số lượng", _temTong.Quantity.ToString()), 3, 0);
            tblTong.Controls.Add(MakeInfoBox("Mã phiếu", _temTong.MaPhieu), 4, 0);

            grpTong.Controls.Add(tblTong);
            mainLayout.Controls.Add(grpTong, 0, 0);

            // ── Row 1: Scan + chọn số thùng ───────────────────────
            var scanPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 8, 5, 0)
            };

            scanPanel.Controls.Add(new LabelControl
            {
                Text = "Số thùng cần KT:",
                AutoSize = true,
                Padding = new Padding(0, 6, 5, 0)
            });

            _spinQty = new SpinEdit { Width = 65 };
            _spinQty.Properties.MinValue = 1;
            _spinQty.Properties.MaxValue = 999;
            _spinQty.EditValue = _requiredQty;
            _spinQty.EditValueChanged += (s, ev) =>
                _requiredQty = Convert.ToInt32(_spinQty.EditValue);
            scanPanel.Controls.Add(_spinQty);

            scanPanel.Controls.Add(new LabelControl
            {
                Text = "   Bắn tem thùng:",
                AutoSize = true,
                Padding = new Padding(0, 6, 5, 0)
            });

            _txtBoxScan = new TextBox
            {
                Width = 400,
                Font = new Font("Tahoma", 11)
            };
            _txtBoxScan.KeyDown += TxtBoxScan_KeyDown;
            scanPanel.Controls.Add(_txtBoxScan);

            _lblProgress = new LabelControl
            {
                Text = "",
                AutoSize = true,
                Padding = new Padding(10, 6, 0, 0),
                Appearance =
            {
                Font      = new Font("Tahoma", 10, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            }
            };
            scanPanel.Controls.Add(_lblProgress);
            mainLayout.Controls.Add(scanPanel, 0, 1);

            // ── Row 2: Grid kết quả ────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsView.ShowGroupPanel = false;

            _gridView.Columns.Add(new GridColumn
            { FieldName = "TemCode", Caption = "LotNo Thùng", Width = 220, VisibleIndex = 0 });
            _gridView.Columns.Add(new GridColumn
            { FieldName = "ItemCode", Caption = "Mã hàng", Width = 160, VisibleIndex = 1 });
            _gridView.Columns.Add(new GridColumn
            { FieldName = "NSX", Caption = "Ngày SX", Width = 100, VisibleIndex = 2 });
            _gridView.Columns.Add(new GridColumn
            { FieldName = "IsMatch", Caption = "Khớp", Width = 60, VisibleIndex = 3 });
            _gridView.Columns.Add(new GridColumn
            { FieldName = "MismatchFields", Caption = "Sai trường", Width = 200, VisibleIndex = 4 });

            // Tô màu
            _gridView.RowStyle += (s, ev) =>
            {
                if (_gridView.GetRow(ev.RowHandle) is BoxScanResult r)
                    ev.Appearance.BackColor = r.IsMatch ? Color.LightGreen : Color.LightSalmon;
            };

            _grid.DataSource = _scannedBoxes;
            mainLayout.Controls.Add(_grid, 0, 2);

            // ── Row 3: Buttons ─────────────────────────────────────
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(5)
            };

            _btnConfirmOK = new SimpleButton
            {
                Text = "✅ Xác nhận OK — Nhập kho",
                Width = 210,
                Height = 36,
                Enabled = false
            };
            _btnConfirmOK.Appearance.BackColor = Color.SeaGreen;
            _btnConfirmOK.Appearance.ForeColor = Color.White;
            _btnConfirmOK.Appearance.Font = new Font("Tahoma", 10, FontStyle.Bold);
            _btnConfirmOK.Click += BtnConfirmOK_Click;

            _btnFail = new SimpleButton
            {
                Text = "❌ Từ chối — Không nhập",
                Width = 185,
                Height = 36
            };
            _btnFail.Appearance.BackColor = Color.IndianRed;
            _btnFail.Appearance.ForeColor = Color.White;
            _btnFail.Click += BtnFail_Click;

            _btnCancel = new SimpleButton { Text = "Huỷ", Width = 80, Height = 36 };
            _btnCancel.Click += (s, ev) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            btnPanel.Controls.Add(_btnConfirmOK);
            btnPanel.Controls.Add(_btnFail);
            btnPanel.Controls.Add(_btnCancel);
            mainLayout.Controls.Add(btnPanel, 0, 3);

            this.Controls.Add(mainLayout);
            _txtBoxScan.Focus();
        }

        private void TxtBoxScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            string raw = _txtBoxScan.Text.Trim();
            _txtBoxScan.Clear();
            if (string.IsNullOrEmpty(raw)) return;

            // ✅ Gọi Service thay vì tự parse + so sánh
            var result = _inspSvc.Inspect(
                _temTong, _config,
                new[] { raw });

            if (result.Details.Count == 0) return;

            var box = result.Details[0];

            // Cảnh báo tem tổng
            QRCodeInfo parsed;
            try { parsed = QRCodeParser.ParseQRCode(raw.ToUpper()); }
            catch
            {
                XtraMessageBox.Show("Tem không đúng định dạng!",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Handled = true;
                return;
            }

            if (parsed.IsTongPhieu)
            {
                XtraMessageBox.Show("Đây là tem TỔNG! Vui lòng bắn tem THÙNG.",
                    "Sai loại tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Handled = true;
                return;
            }

            _scannedBoxes.Add(box);
            _grid.RefreshDataSource();
            _gridView.FocusedRowHandle = _gridView.RowCount - 1;

            UpdateProgress();
            e.Handled = true;
        }

        private void UpdateProgress()
        {
            int scanned = _scannedBoxes.Count;
            int failed = _scannedBoxes.Count(r => !r.IsMatch);
            bool done = scanned >= _requiredQty;
            bool allOK = done && failed == 0;

            _lblProgress.Text = $"Đã bắn: {scanned}/{_requiredQty}  |  Lỗi: {failed}";
            _lblProgress.Appearance.ForeColor = failed > 0 ? Color.Red : Color.DarkBlue;

            _btnConfirmOK.Enabled = allOK;

            // Thông báo khi đủ số thùng
            if (done && allOK)
                XtraMessageBox.Show($"✅ Đã kiểm tra đủ {_requiredQty} thùng — Tất cả đạt!",
                    "Đạt chuẩn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (done && failed > 0)
                XtraMessageBox.Show($"❌ Kiểm tra xong — Có {failed} thùng không đạt!",
                    "Không đạt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void BtnConfirmOK_Click(object sender, EventArgs e)
        {
            // ✅ Gọi Service thay vì tự ghi SQL
            _inspSvc.SaveLog(
                _inspectionCode, _temTong, _scannedBoxes, "PASS");
            InspectionPassed = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnFail_Click(object sender, EventArgs e)
        {
            _inspSvc.SaveLog(
                _inspectionCode, _temTong, _scannedBoxes, "FAIL");
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        // Helper tạo ô thông tin
        private GroupControl MakeInfoBox(string caption, string value)
        {
            var grp = new GroupControl
            {
                Text = caption,
                Dock = DockStyle.Fill,
                Height = 70
            };
            grp.Controls.Add(new LabelControl
            {
                Text = value ?? "-",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = { Font = new Font("Tahoma", 10, FontStyle.Bold) }
            });
            return grp;
        }
    }
}