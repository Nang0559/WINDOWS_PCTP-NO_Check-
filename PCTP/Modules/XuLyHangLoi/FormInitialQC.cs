using DevExpress.XtraEditors;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.XuLyHangLoi
{
    /// <summary>
    /// Initial QC theo snapshot LOT.
    /// Mỗi dòng phải được phân loại đủ: DaKiemTra = OK + NG và NG = Rework + LoaiBo.
    /// </summary>
    public sealed class FormInitialQC : XtraForm
    {
        private readonly IInitialQCService _initialQCService;
        private readonly IAffectedLotTraceService _traceService;
        private readonly int _phieuXuLyId;
        private readonly DataGridView _grid;
        private readonly TextEdit _txtNoiDung;
        private readonly TextEdit _txtKetLuan;
        private readonly LabelControl _lblSummary;

        public FormInitialQC(
            IInitialQCService initialQCService,
            IAffectedLotTraceService traceService,
            int phieuXuLyId)
        {
            _initialQCService = initialQCService ?? throw new ArgumentNullException(nameof(initialQCService));
            _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
            if (phieuXuLyId <= 0) throw new ArgumentException("PhieuXuLyId không hợp lệ.", nameof(phieuXuLyId));
            _phieuXuLyId = phieuXuLyId;

            Text = "Initial QC - Xử lý hàng lỗi";
            Size = new Size(1250, 720);
            StartPosition = FormStartPosition.CenterParent;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            _lblSummary = new LabelControl { Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 10F, FontStyle.Bold) } };
            root.Controls.Add(_lblSummary, 0, 0);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            AddTextColumn("AffectedLotId", "Id", 70, false);
            AddTextColumn("SourceType", "Nguồn", 100, false);
            AddTextColumn("LotNo", "LOT", 120, true);
            AddTextColumn("MaSanPham", "Mã hàng", 130, true);
            AddTextColumn("SoLuongAnhHuong", "Ảnh hưởng", 90, true);
            AddNumberColumn("SoLuongDaKiemTra", "Đã kiểm tra", 95);
            AddNumberColumn("SoLuongOK", "OK", 80);
            AddNumberColumn("SoLuongNG", "NG", 80);
            AddNumberColumn("SoLuongRework", "Rework", 90);
            AddNumberColumn("SoLuongLoaiBo", "Loại bỏ", 90);
            _grid.CellEndEdit += Grid_CellEndEdit;
            root.Controls.Add(_grid, 0, 1);

            _txtNoiDung = new TextEdit { Dock = DockStyle.Fill };
            _txtNoiDung.Properties.NullValuePrompt = "Nội dung kiểm tra...";
            root.Controls.Add(_txtNoiDung, 0, 2);

            _txtKetLuan = new TextEdit { Dock = DockStyle.Fill };
            _txtKetLuan.Properties.NullValuePrompt = "Kết luận QC...";
            root.Controls.Add(_txtKetLuan, 0, 3);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var btnCancel = new SimpleButton { Text = "Đóng", Width = 100 };
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            var btnConfirm = new SimpleButton { Text = "Xác nhận Initial QC", Width = 170 };
            btnConfirm.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            btnConfirm.Click += Confirm_Click;
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnConfirm);
            root.Controls.Add(buttons, 0, 4);
            Controls.Add(root);

            LoadSnapshot();
        }

        private void AddTextColumn(string name, string caption, int width, bool visible)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = caption,
                Width = width,
                Visible = visible,
                ReadOnly = true
            });
        }

        private void AddNumberColumn(string name, string caption, int width)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = caption,
                Width = width,
                ValueType = typeof(int)
            });
        }

        private void LoadSnapshot()
        {
            var existing = _initialQCService.Get(_phieuXuLyId);
            if (existing != null)
                throw new InvalidOperationException("Phiếu đã có Initial QC; không cho ghi đè kết quả đã xác nhận.");

            var snapshot = _traceService.GetSnapshot(_phieuXuLyId);
            if (snapshot == null || snapshot.Count == 0)
                throw new InvalidOperationException("Chưa có snapshot LOT. Hãy thực hiện Truy vết LOT trước.");

            foreach (var item in snapshot)
            {
                _grid.Rows.Add(
                    item.Id,
                    item.SourceType.ToString(),
                    item.LotNo,
                    item.MaSanPham,
                    item.SoLuongAnhHuong,
                    0, 0, 0, 0, 0);
            }
            UpdateSummary();
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            UpdateSummary();
        }

        private int CellInt(int row, string name)
        {
            var value = _grid.Rows[row].Cells[name].Value;
            int result;
            return int.TryParse(Convert.ToString(value), out result) ? result : 0;
        }

        private void UpdateSummary()
        {
            int affected = 0, inspected = 0, ok = 0, ng = 0, rework = 0, scrap = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                affected += Convert.ToInt32(row.Cells["SoLuongAnhHuong"].Value);
                inspected += CellInt(row.Index, "SoLuongDaKiemTra");
                ok += CellInt(row.Index, "SoLuongOK");
                ng += CellInt(row.Index, "SoLuongNG");
                rework += CellInt(row.Index, "SoLuongRework");
                scrap += CellInt(row.Index, "SoLuongLoaiBo");
            }
            _lblSummary.Text = $"Ảnh hưởng: {affected:n0} | Đã kiểm tra: {inspected:n0} | OK: {ok:n0} | NG: {ng:n0} | Rework: {rework:n0} | Loại bỏ ban đầu: {scrap:n0}";
        }

        private void Confirm_Click(object sender, EventArgs e)
        {
            try
            {
                var list = new List<InitialQCLotResult>();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    list.Add(new InitialQCLotResult
                    {
                        AffectedLotId = Convert.ToInt32(row.Cells["AffectedLotId"].Value),
                        SoLuongDaKiemTra = CellInt(row.Index, "SoLuongDaKiemTra"),
                        SoLuongOK = CellInt(row.Index, "SoLuongOK"),
                        SoLuongNG = CellInt(row.Index, "SoLuongNG"),
                        SoLuongRework = CellInt(row.Index, "SoLuongRework"),
                        SoLuongLoaiBo = CellInt(row.Index, "SoLuongLoaiBo")
                    });
                }

                var result = _initialQCService.Confirm(
                    _phieuXuLyId,
                    list,
                    _txtNoiDung.Text,
                    _txtKetLuan.Text,
                    Environment.UserName);

                XtraMessageBox.Show(
                    this,
                    $"Đã xác nhận Initial QC.\r\nOK: {result.SoLuongOK:n0}\r\nNG: {result.SoLuongNG:n0}\r\nRework: {result.SoLuongRework:n0}\r\nLoại bỏ ban đầu: {result.SoLuongLoaiBoBanDau:n0}",
                    "Initial QC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Không thể xác nhận Initial QC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}