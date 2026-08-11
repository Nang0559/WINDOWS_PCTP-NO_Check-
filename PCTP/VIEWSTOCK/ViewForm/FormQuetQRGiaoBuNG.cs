using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Models;
using PCTP.VIEWSTOCK.FunctionForm;
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
    public partial class FormQuetQRGiaoBuNG : XtraForm
    {
        private readonly PhieuGiaoGocInfo _phieuGoc;
    private readonly GiaoBuNGService _service; // ← THÊM: cần để resolve slot ngay khi quét
    private readonly BindingList<TemFccQuetInfo> _danhSachTem = new BindingList<TemFccQuetInfo>();

    private TextEdit _txtQr;
    private GridControl _gridTem;
    private GridView _gridViewTem;
    private LabelControl _lblTongHop;
    private SimpleButton _btnXacNhan, _btnHuy;

    public List<TemFccQuetInfo> DanhSachTemDaQuet => _danhSachTem.ToList();

    public FormQuetQRGiaoBuNG(PhieuGiaoGocInfo phieuGoc, GiaoBuNGService service)
    {
        _phieuGoc = phieuGoc;
        _service = service;
        BuildUI();
    }

    private void BuildUI()
        {
            Text = $"Quét tem FCC giao bù — Mã hàng: {_phieuGoc.MaHang} — LOT gốc: {_phieuGoc.Lot}";
            Size = new System.Drawing.Size(700, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(10) };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            var scanPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            scanPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            scanPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanPanel.Controls.Add(new LabelControl
            {
                Text = "Bắn tem FCC:",
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) }
            }, 0, 0);
            _txtQr = new TextEdit { Dock = DockStyle.Fill, Font = new System.Drawing.Font("Tahoma", 12) };
            _txtQr.KeyDown += TxtQr_KeyDown;
            scanPanel.Controls.Add(_txtQr, 1, 0);
            main.Controls.Add(scanPanel, 0, 0);

            _gridTem = new GridControl { Dock = DockStyle.Fill, DataSource = _danhSachTem };
            _gridViewTem = new GridView(_gridTem);
            _gridTem.MainView = _gridViewTem;
            _gridViewTem.OptionsBehavior.Editable = false;
            _gridViewTem.Columns.Add(new GridColumn { FieldName = "LotFcc", Caption = "Lot FCC", Width = 200, VisibleIndex = 0 });
            _gridViewTem.Columns.Add(new GridColumn { FieldName = "MaHangFcc", Caption = "Mã hàng", Width = 130, VisibleIndex = 1 });
            _gridViewTem.Columns.Add(new GridColumn { FieldName = "SlTemFcc", Caption = "SL", Width = 80, VisibleIndex = 2 });
            _gridViewTem.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && _gridViewTem.FocusedRowHandle >= 0)
                {
                    _danhSachTem.RemoveAt(_gridViewTem.FocusedRowHandle);
                    CapNhatTongHop();
                }
            };
            main.Controls.Add(_gridTem, 0, 1);

            _lblTongHop = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) },
                Text = "Đã quét: 0 tem, tổng SL: 0"
            };
            main.Controls.Add(_lblTongHop, 0, 2);

            var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _btnXacNhan = new SimpleButton { Text = "✅ Xác nhận giao bù", Width = 160, Height = 40, Enabled = false };
            _btnXacNhan.Appearance.BackColor = System.Drawing.Color.SeaGreen;
            _btnXacNhan.Appearance.ForeColor = System.Drawing.Color.White;
            _btnXacNhan.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            _btnHuy = new SimpleButton { Text = "Huỷ", Width = 100, Height = 40 };
            _btnHuy.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            bottomPanel.Controls.Add(_btnXacNhan);
            bottomPanel.Controls.Add(_btnHuy);
            main.Controls.Add(bottomPanel, 0, 3);

            Controls.Add(main);
        }

        private void TxtQr_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            string raw = _txtQr.Text.Trim();
            _txtQr.Clear();
            if (string.IsNullOrEmpty(raw)) return;

            var parts = raw.Split(':');
            if (parts.Length != 4)
            {
                XtraMessageBox.Show("Chỉ được quét tem FCC nội bộ (định dạng 4 phần).\n" +
                    "Không quét tem khách hàng (HVN) trong màn hình giao bù này.",
                    "Sai loại tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string lotFcc = parts[0].Trim();
            string maHangFcc = parts[1].Trim();
            if (!int.TryParse(parts[3].Trim(), out int slTem))
            {
                XtraMessageBox.Show("Số lượng trên tem không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_danhSachTem.Any(t => t.LotFcc == lotFcc))
            {
                XtraMessageBox.Show("Tem này đã được quét.", "Trùng tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var tem = new TemFccQuetInfo { LotFcc = lotFcc, MaHangFcc = maHangFcc, SlTemFcc = slTem, RawQr = raw };

            // ✅ MỚI: resolve ngay khi quét — báo lỗi ngay lập tức nếu LOT chưa nhập kho / không đủ tồn / rải nhiều Slot
            var resolveResult = _service.ResolveTemFcc(_phieuGoc.MaHang, tem);
            if (!resolveResult.IsOK)
            {
                XtraMessageBox.Show(resolveResult.Message, "Không thể dùng tem này",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _danhSachTem.Add(tem);
            CapNhatTongHop();
        }

        private void CapNhatTongHop()
        {
            int tongSl = _danhSachTem.Sum(t => t.SlTemFcc);
            _lblTongHop.Text = $"Đã quét: {_danhSachTem.Count} tem, tổng SL: {tongSl} " +
                $"(SL phiếu gốc cần bù: {_phieuGoc.SoLuong})";
            _btnXacNhan.Enabled = _danhSachTem.Count > 0;

            if (tongSl > _phieuGoc.SoLuong)
            {
                _lblTongHop.Appearance.ForeColor = System.Drawing.Color.Red;
                _lblTongHop.Text += "  ⚠ VƯỢT SL phiếu gốc!";
            }
            else
            {
                _lblTongHop.Appearance.ForeColor = System.Drawing.Color.DarkGreen;
            }
        }
    }
}