using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Domain.Entities;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    /// <summary>
    /// Nhập liệu chứng từ hàng lỗi khách trả — Mốc 1 của tiến trình.
    /// Hỗ trợ 2 nguồn:
    ///   - HVN : "Phiếu đổi phụ tùng lỗi" (ảnh 1, đơn dòng) hoặc
    ///           "Phiếu xác nhận phụ tùng lỗi trả về" (ảnh 3, multi-dòng theo Model/Mã hàng)
    ///   - YMVN: "Return Slip (A)" (ảnh 4) — thường 1 dòng/phiếu nhưng vẫn cho phép
    ///           nhập nhiều dòng nếu 1 Slip No gộp nhiều Item.
    /// Lưu xuống PhieuLoiKhachTra (header) + PhieuLoiKhachTraCT (chi tiết) qua
    /// IPhieuLoiRepository.InsertPhieuLoiKhachTra — đúng transaction hiện có, KHÔNG
    /// đổi chữ ký repo.
    /// </summary>
    public partial class FormPhieuLoiKhachTra : XtraForm
    {
        private readonly IPhieuLoiRepository _repo;

        // ── Header controls ──────────────────────────────────────────────
        private RadioGroup _rdoNguon;
        private TextEdit _txtSoPhieuKhach;   // HVN: "Slip no" | YMVN: "Slip No"
        private DateEdit _dateNgayPhatHanh;  // HVN: "Ngày phát hành" | YMVN: "Issued date"
        private TextEdit _txtSlipNo;         // Mã phiếu nội bộ / số lưu (optional)
        private TextEdit _txtCa;             // Chỉ HVN — "Ca"
        private LabelControl _lblCa;
        private TextEdit _txtNguoiTao;

        // ── Detail grid ──────────────────────────────────────────────────
        private GridControl _grid;
        private GridView _gridView;
        private BindingList<PhieuLoiKhachTraCT> _chiTiet;

        private SimpleButton _btnAddRow, _btnDelRow, _btnSave, _btnCancel;

        public FormPhieuLoiKhachTra(IPhieuLoiRepository repo)
        {
            _repo = repo;
            _chiTiet = new BindingList<PhieuLoiKhachTraCT>();
            BuildUI();
            ToggleNguon(); // set label mặc định theo Nguồn đang chọn
        }

        // ════════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "Nhập chứng từ hàng lỗi khách trả";
            Size = new Size(1050, 680);
            StartPosition = FormStartPosition.CenterParent;

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 170)); // header
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // grid
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // buttons

            // ── Header ──────────────────────────────────────────────────
            var grpHeader = new GroupControl { Text = "Thông tin chứng từ", Dock = DockStyle.Fill };
            var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(10) };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            for (int i = 0; i < 3; i++) headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            // Row 0: Nguồn + Số phiếu khách
            headerLayout.Controls.Add(new LabelControl { Text = "Nguồn:", Dock = DockStyle.Fill, Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold) } }, 0, 0);
            _rdoNguon = new RadioGroup { Dock = DockStyle.Fill };
            _rdoNguon.Properties.Items.Add(new RadioGroupItem((int)NguonKhachTra.HVN, "HVN — Phiếu đổi phụ tùng lỗi"));
            _rdoNguon.Properties.Items.Add(new RadioGroupItem((int)NguonKhachTra.YMVN, "YMVN — Return Slip"));
            _rdoNguon.EditValue = (int)NguonKhachTra.HVN;
            _rdoNguon.SelectedIndexChanged += (s, e) => ToggleNguon();
            headerLayout.Controls.Add(_rdoNguon, 1, 0);

            headerLayout.Controls.Add(new LabelControl { Text = "Slip No:", Dock = DockStyle.Fill }, 2, 0);
            _txtSoPhieuKhach = new TextEdit { Dock = DockStyle.Fill, Font = new Font("Tahoma", 10, FontStyle.Bold) };
            headerLayout.Controls.Add(_txtSoPhieuKhach, 3, 0);

            // Row 1: Ngày phát hành + Ca (HVN)
            headerLayout.Controls.Add(new LabelControl { Text = "Ngày phát hành:", Dock = DockStyle.Fill }, 0, 1);
            _dateNgayPhatHanh = new DateEdit { Dock = DockStyle.Fill, DateTime = DateTime.Today };
            _dateNgayPhatHanh.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            _dateNgayPhatHanh.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            headerLayout.Controls.Add(_dateNgayPhatHanh, 1, 1);

            _lblCa = new LabelControl { Text = "Ca:", Dock = DockStyle.Fill };
            headerLayout.Controls.Add(_lblCa, 2, 1);
            _txtCa = new TextEdit { Dock = DockStyle.Fill };
            headerLayout.Controls.Add(_txtCa, 3, 1);

            // Row 2: Mã phiếu nội bộ + Người tạo
            headerLayout.Controls.Add(new LabelControl { Text = "Mã phiếu lưu:", Dock = DockStyle.Fill }, 0, 2);
            _txtSlipNo = new TextEdit { Dock = DockStyle.Fill };
            headerLayout.Controls.Add(_txtSlipNo, 1, 2);

            headerLayout.Controls.Add(new LabelControl { Text = "Người nhập:", Dock = DockStyle.Fill }, 2, 2);
            _txtNguoiTao = new TextEdit { Dock = DockStyle.Fill, Text = Environment.UserName, Properties = { ReadOnly = true } };
            headerLayout.Controls.Add(_txtNguoiTao, 3, 2);

            grpHeader.Controls.Add(headerLayout);
            main.Controls.Add(grpHeader, 0, 0);

            // ── Grid chi tiết ───────────────────────────────────────────
            var grpDetail = new GroupControl { Text = "Chi tiết hàng lỗi (theo Model / Mã hàng)", Dock = DockStyle.Fill };
            var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var toolPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(3) };
            _btnAddRow = new SimpleButton { Text = "➕ Thêm dòng", Width = 110, Height = 28 };
            _btnAddRow.Click += (s, e) => _chiTiet.Add(new PhieuLoiKhachTraCT { SoLuong = 0 });
            _btnDelRow = new SimpleButton { Text = "🗑 Xóa dòng", Width = 100, Height = 28 };
            _btnDelRow.Click += BtnDelRow_Click;
            toolPanel.Controls.Add(_btnAddRow);
            toolPanel.Controls.Add(_btnDelRow);
            detailLayout.Controls.Add(toolPanel, 0, 0);

            _grid = new GridControl { Dock = DockStyle.Fill, DataSource = _chiTiet };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsBehavior.Editable = true;
            BuildGridColumns();
            detailLayout.Controls.Add(_grid, 0, 1);

            grpDetail.Controls.Add(detailLayout);
            main.Controls.Add(grpDetail, 0, 1);

            // ── Buttons ─────────────────────────────────────────────────
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };
            _btnSave = new SimpleButton { Text = "💾 Lưu chứng từ", Width = 150, Height = 36 };
            _btnSave.Appearance.BackColor = Color.SeaGreen;
            _btnSave.Appearance.ForeColor = Color.White;
            _btnSave.Appearance.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new SimpleButton { Text = "Hủy", Width = 90, Height = 36 };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnPanel.Controls.Add(_btnSave);
            btnPanel.Controls.Add(_btnCancel);
            main.Controls.Add(btnPanel, 0, 2);

            Controls.Add(main);

            // Mặc định thêm sẵn 1 dòng trống để nhập ngay
            _chiTiet.Add(new PhieuLoiKhachTraCT { SoLuong = 0 });
        }

        private void BuildGridColumns()
        {
            _gridView.Columns.Add(new GridColumn { FieldName = "Model", Caption = "Model", Width = 90, VisibleIndex = 0 });
            _gridView.Columns.Add(new GridColumn { FieldName = "MaHang", Caption = "Mã hàng (*)", Width = 150, VisibleIndex = 1 });
            _gridView.Columns.Add(new GridColumn { FieldName = "TenHang", Caption = "Tên hàng", Width = 220, VisibleIndex = 2 });
            _gridView.Columns.Add(new GridColumn { FieldName = "SoLo", Caption = "Số lô", Width = 110, VisibleIndex = 3 });

            var colSl = new GridColumn { FieldName = "SoLuong", Caption = "Số lượng (*)", Width = 90, VisibleIndex = 4 };
            colSl.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            colSl.DisplayFormat.FormatString = "n0";
            _gridView.Columns.Add(colSl);

            _gridView.Columns.Add(new GridColumn { FieldName = "NoiDungLoi", Caption = "Nội dung lỗi / Hiện tượng", Width = 260, VisibleIndex = 5 });

            var colCoPhieu = new GridColumn
            {
                FieldName = "CoPhieuLoi",
                Caption = "Có phiếu lỗi",
                Width = 90,
                VisibleIndex = 6,
                ColumnEdit = new DevExpress.XtraEditors.Repository.RepositoryItemCheckEdit()
            };
            _gridView.Columns.Add(colCoPhieu);

            _gridView.Columns.Add(new GridColumn { FieldName = "GhiChu", Caption = "Ghi chú", Width = 160, VisibleIndex = 7 });
        }

        // ════════════════════════════════════════════════════════════════
        // Toggle theo Nguồn — đổi nhãn cho đúng thuật ngữ chứng từ gốc
        // ════════════════════════════════════════════════════════════════
        private void ToggleNguon()
        {
            var nguon = (NguonKhachTra)Convert.ToInt32(_rdoNguon.EditValue);
            bool isHvn = nguon == NguonKhachTra.HVN;

            // Ca chỉ có ở phiếu HVN (ảnh 1: "Ca:") — YMVN không có trường này
            _lblCa.Visible = isHvn;
            _txtCa.Visible = isHvn;

            Text = isHvn
                ? "Nhập chứng từ — Phiếu đổi phụ tùng lỗi (HVN)"
                : "Nhập chứng từ — Return Slip (YMVN)";
        }

        private void BtnDelRow_Click(object sender, EventArgs e)
        {
            var row = _gridView.GetFocusedRow() as PhieuLoiKhachTraCT;
            if (row == null) return;
            if (_chiTiet.Count <= 1)
            {
                XtraMessageBox.Show("Chứng từ phải có ít nhất 1 dòng chi tiết.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _chiTiet.Remove(row);
        }

        // ════════════════════════════════════════════════════════════════
        // Lưu
        // ════════════════════════════════════════════════════════════════
        private void BtnSave_Click(object sender, EventArgs e)
        {
            _gridView.PostEditor();
            _gridView.UpdateCurrentRow();

            if (string.IsNullOrWhiteSpace(_txtSoPhieuKhach.Text))
            {
                XtraMessageBox.Show("Vui lòng nhập Slip No.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtSoPhieuKhach.Focus();
                return;
            }

            var hopLe = _chiTiet.Where(ct =>
                !string.IsNullOrWhiteSpace(ct.MaHang) && ct.SoLuong > 0).ToList();

            if (hopLe.Count == 0)
            {
                XtraMessageBox.Show("Cần ít nhất 1 dòng có Mã hàng và Số lượng > 0.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (hopLe.Count < _chiTiet.Count)
            {
                var confirm = XtraMessageBox.Show(
                    $"Có {_chiTiet.Count - hopLe.Count} dòng thiếu Mã hàng/Số lượng sẽ bị bỏ qua. Tiếp tục lưu?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            var header = new PhieuLoiKhachTra
            {
                Nguon = (NguonKhachTra)Convert.ToInt32(_rdoNguon.EditValue),
                SoPhieuKhach = _txtSoPhieuKhach.Text.Trim(),
                NgayPhatHanh = _dateNgayPhatHanh.DateTime,
                SlipNo = _txtSlipNo.Text.Trim(),
                Ca = _txtCa.Visible ? _txtCa.Text.Trim() : "",
                NguoiTao = _txtNguoiTao.Text.Trim(),
                ChiTiet = hopLe
            };

            try
            {
                int headerId = _repo.InsertPhieuLoiKhachTra(header);
                XtraMessageBox.Show(
                    $"Đã lưu chứng từ #{headerId} — {hopLe.Count} dòng chi tiết.",
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi lưu chứng từ:\n{ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}