using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
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
    public partial class frmGiaoBuNG : XtraForm
    {
        private readonly SQLPROVIDER _sql = new SQLPROVIDER();
        private readonly IGiaoBuNGRepository _repo;
        private readonly ITraHangRepository _rah;
        private readonly GiaoBuNGService _service;

        private RadioGroup _rdoCheDoTim;
        private TextEdit _txtLot;
        private TextEdit _txtMaHang;
        private DateEdit _dateTu, _dateDen;
        private SimpleButton _btnTim;
        private GridControl _gridPhieuGoc;
        private GridView _gridViewPhieuGoc;
        private SimpleButton _btnThucHienGiaoBu;

        private PhieuGiaoGocInfo _phieuGocDangChon;

        public frmGiaoBuNG()
        {
            _repo = new GiaoBuNGRepository(_sql);
            _service = new GiaoBuNGService(_sql, _repo,_rah);
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Giao bù hàng NG";
            Size = new System.Drawing.Size(1000, 650);
            StartPosition = FormStartPosition.CenterParent;

            // ── Panel tìm kiếm ──────────────────────────────────────────
            var panelTop = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 2, Padding = new Padding(2) };
            panelTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            panelTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Dòng 0: Radio
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Dòng 1: LotNo
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Dòng 2: Mã hàng + Date

            _rdoCheDoTim = new RadioGroup { Dock = DockStyle.Fill };
            _rdoCheDoTim.Properties.Items.Add(new DevExpress.XtraEditors.Controls.RadioGroupItem(0, "Theo LotNo"));
            _rdoCheDoTim.Properties.Items.Add(new DevExpress.XtraEditors.Controls.RadioGroupItem(1, "Theo Mã hàng + Ngày giao"));
            _rdoCheDoTim.EditValue = 0;
            _rdoCheDoTim.SelectedIndexChanged += (s, e) => ToggleCheDoTim();
            panelTop.Controls.Add(new LabelControl { Text = "Tìm kiếm theo:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold) } }, 0, 0);
            panelTop.Controls.Add(_rdoCheDoTim, 1, 0);

            _txtLot = new TextEdit { Dock = DockStyle.Fill };
            _txtLot.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) TimTheoLot(); };
            panelTop.Controls.Add(new LabelControl { Text = "LotNo:", Dock = DockStyle.Fill }, 0, 1);
            panelTop.Controls.Add(_txtLot, 1, 1);

            // ✅ Đã sửa chuẩn ColumnStyles và thêm RowStyles cho panelMaHang
            var panelMaHang = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            panelMaHang.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); // Mã hàng
            panelMaHang.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Từ ngày
            panelMaHang.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Đến ngày
            panelMaHang.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60)); // Nút tìm
            panelMaHang.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _txtMaHang = new TextEdit { Dock = DockStyle.Fill };
            _dateTu = new DateEdit { Dock = DockStyle.Fill, DateTime = DateTime.Today.AddDays(-14) };
            _dateDen = new DateEdit { Dock = DockStyle.Fill, DateTime = DateTime.Today };
            _btnTim = new SimpleButton { Text = "🔍 Tìm", Dock = DockStyle.Fill };
            _btnTim.Click += (s, e) => TimTheoMaHangNgay();

            panelMaHang.Controls.Add(_txtMaHang, 0, 0);
            panelMaHang.Controls.Add(_dateTu, 1, 0);
            panelMaHang.Controls.Add(_dateDen, 2, 0);
            panelMaHang.Controls.Add(_btnTim, 3, 0);

            panelTop.Controls.Add(new LabelControl { Text = "Mã hàng / Ngày:", Dock = DockStyle.Fill }, 0, 2);
            panelTop.Controls.Add(panelMaHang, 1, 2);

            // ── Main Layout ─────────────────────────────────────────────
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            main.Controls.Add(panelTop, 0, 0);

            // ── Grid phiếu gốc ────────────────────────────────────────
            _gridPhieuGoc = new GridControl { Dock = DockStyle.Fill };
            _gridViewPhieuGoc = new GridView(_gridPhieuGoc);
            _gridPhieuGoc.MainView = _gridViewPhieuGoc;
            _gridViewPhieuGoc.OptionsBehavior.Editable = false;
            _gridViewPhieuGoc.OptionsView.ShowGroupPanel = false;
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "Lot", Caption = "LOT", Width = 160, VisibleIndex = 0 });
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "MaHang", Caption = "Mã hàng", Width = 110, VisibleIndex = 1 });
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "TenHang", Caption = "Tên hàng", Width = 160, VisibleIndex = 2 });
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "SoLuong", Caption = "SL", Width = 60, VisibleIndex = 3 });
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "NgayGiao", Caption = "Ngày giao", Width = 90, VisibleIndex = 4 });
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "GioGiao", Caption = "Giờ", Width = 60, VisibleIndex = 5 });
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "NhaMay", Caption = "Nhà máy", Width = 150, VisibleIndex = 6 });
            _gridViewPhieuGoc.Columns.Add(new GridColumn { FieldName = "Note", Caption = "Ghi chú", Width = 100, VisibleIndex = 7 });
            _gridViewPhieuGoc.FocusedRowChanged += (s, e) =>
            {
                _phieuGocDangChon = _gridViewPhieuGoc.GetFocusedRow() as PhieuGiaoGocInfo;
                _btnThucHienGiaoBu.Enabled = _phieuGocDangChon != null;
            };
            main.Controls.Add(_gridPhieuGoc, 0, 1);

            // ── Nút hành động ───────────────────────────────────────────
            _btnThucHienGiaoBu = new SimpleButton
            {
                Text = "🚚 Thực hiện giao bù (Quét tem FCC)",
                Dock = DockStyle.Fill,
                Height = 45,
                Enabled = false
            };
            _btnThucHienGiaoBu.Appearance.BackColor = System.Drawing.Color.SeaGreen;
            _btnThucHienGiaoBu.Appearance.ForeColor = System.Drawing.Color.White;
            _btnThucHienGiaoBu.Appearance.Font = new System.Drawing.Font("Tahoma", 10, System.Drawing.FontStyle.Bold);
            _btnThucHienGiaoBu.Click += BtnThucHienGiaoBu_Click;
            main.Controls.Add(_btnThucHienGiaoBu, 0, 2);

            Controls.Add(main);
            ToggleCheDoTim();
        }

        private void ToggleCheDoTim()
        {
            bool theoLot = Convert.ToInt32(_rdoCheDoTim.EditValue) == 0;
            _txtLot.Enabled = theoLot;
            _txtMaHang.Enabled = !theoLot;
            _dateTu.Enabled = !theoLot;
            _dateDen.Enabled = !theoLot;
            _btnTim.Enabled = !theoLot;
        }

        private void TimTheoLot()
        {
            string lot = _txtLot.Text.Trim();
            if (string.IsNullOrEmpty(lot)) return;

            var ds = _service.TimPhieuGocTheoLot(lot);
            _gridPhieuGoc.DataSource = ds;
            if (ds.Count == 0)
                XtraMessageBox.Show("Không tìm thấy phiếu giao nào khớp LOT này.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TimTheoMaHangNgay()
        {
            string maHang = _txtMaHang.Text.Trim();
            if (string.IsNullOrEmpty(maHang))
            {
                XtraMessageBox.Show("Vui lòng nhập mã hàng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var ds = _service.TimPhieuGocTheoMaHangNgay(maHang, _dateTu.DateTime, _dateDen.DateTime);
            _gridPhieuGoc.DataSource = ds;

            if (ds.Count == 0)
                XtraMessageBox.Show("Không tìm thấy phiếu giao nào khớp điều kiện.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnThucHienGiaoBu_Click(object sender, EventArgs e)
        {
            if (_phieuGocDangChon == null) return;

            using (var frmQuet = new FormQuetQRGiaoBuNG(_phieuGocDangChon, _service))
            {
                if (frmQuet.ShowDialog(this) != DialogResult.OK) return;

                var temDaQuet = frmQuet.DanhSachTemDaQuet;
                if (temDaQuet == null || temDaQuet.Count == 0) return;

                var result = _service.XacNhanGiaoBu(_phieuGocDangChon, temDaQuet, Environment.UserName);

                XtraMessageBox.Show(result.Message, result.IsOK ? "Thành công" : "Lỗi",
                    MessageBoxButtons.OK, result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Error);

                if (result.IsOK)
                {
                    _phieuGocDangChon = null;
                    _btnThucHienGiaoBu.Enabled = false;
                    _gridPhieuGoc.DataSource = null;
                }
            }
        }
    }
}