using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Common;
using PCTP.Domain.Interfaces;
using PCTP.Models;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
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
        private readonly CustomerConfig _cfg;
        private readonly IDocQRRepository _docQrRepo;
        private readonly BindingList<TemFccQuetInfo> _danhSachTem = new BindingList<TemFccQuetInfo>();

    private TextEdit _txtQr;
    private GridControl _gridTem;
    private GridView _gridViewTem;
    private LabelControl _lblTongHop;
    private SimpleButton _btnXacNhan, _btnHuy;

    public List<TemFccQuetInfo> DanhSachTemDaQuet => _danhSachTem.ToList();

    public FormQuetQRGiaoBuNG(PhieuGiaoGocInfo phieuGoc, GiaoBuNGService service,CustomerConfig cfg, IDocQRRepository docQrRepo)
    {
        _phieuGoc = phieuGoc;
        _service = service;
            _cfg = cfg;
            _docQrRepo = docQrRepo;
            BuildUI();
            Text += TemFccParser.ExpectsTemTong(_cfg)
           ? "  [Bắn TEM TỔNG - 6 phần]"
           : "  [Bắn TEM THÙNG - 4 phần]";
        }

        private void BuildUI()
        {
            // 1. Tiêu đề ngắn gọn trên thanh Title Bar
            Text = "Quét tem FCC giao bù";
            Size = new System.Drawing.Size(700, 540); // Tăng chiều cao lên chút cho thoáng
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            // Tăng số dòng của TableLayoutPanel lên 5 để chứa thêm dòng thông tin
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(10) };

            // Dòng 0: Dành cho thông tin chi tiết Mã hàng & LOT gốc
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            // Dòng 1: Ô quét QR
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            // Dòng 2: Grid danh sách tem
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            // Dòng 3: Tổng hợp số lượng
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            // Dòng 4: Nút bấm xác nhận/hủy
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            // --- Thêm Label hiển thị thông tin chi tiết ở trong form ---
            var lblInfo = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Regular), ForeColor = System.Drawing.Color.DimGray },
                Text = $"Mã hàng: {_phieuGoc.MaHang}\nLOT gốc: {_phieuGoc.Lot}"
            };
            main.Controls.Add(lblInfo, 0, 0);

            // --- Panel quét QR (chuyển xuống dòng 1) ---
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
            main.Controls.Add(scanPanel, 0, 1);

            // --- Grid Control (dòng 2) ---
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
            main.Controls.Add(_gridTem, 0, 2);

            // --- Label tổng hợp (dòng 3) ---
            _lblTongHop = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) },
                Text = "Đã quét: 0 tem, tổng SL: 0"
            };
            main.Controls.Add(_lblTongHop, 0, 3);

            // --- Bottom Panel (dòng 4) ---
            var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _btnXacNhan = new SimpleButton { Text = "✅ Xác nhận giao bù", Width = 160, Height = 40, Enabled = false };
            _btnXacNhan.Appearance.BackColor = System.Drawing.Color.SeaGreen;
            _btnXacNhan.Appearance.ForeColor = System.Drawing.Color.White;
            _btnXacNhan.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            _btnHuy = new SimpleButton { Text = "Huỷ", Width = 100, Height = 40 };
            _btnHuy.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            bottomPanel.Controls.Add(_btnXacNhan);
            bottomPanel.Controls.Add(_btnHuy);
            main.Controls.Add(bottomPanel, 0, 4);

            Controls.Add(main);
        }

        private void TxtQr_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            string raw = _txtQr.Text.Trim();
            _txtQr.Clear();
            if (string.IsNullOrEmpty(raw)) return;

            var parsed = TemFccParser.Parse(raw, _cfg,
                getIdMaHangPadded: ma => _docQrRepo.GetIdMaHangPadded(ma),
                getGearNameByCode: code => _docQrRepo.GetGearName(code));

            if (!parsed.Success)
            {
                XtraMessageBox.Show(parsed.ErrorMessage, "Sai định dạng tem",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Chống trùng trong phiên hiện tại — phân biệt theo loại tem:
            // tem tổng trùng nếu cùng LotFcc + SoPhieu; tem thùng trùng nếu cùng LotFcc.
            bool daTrung = parsed.IsTongPhieu
                ? _danhSachTem.Any(t => t.LotFcc == parsed.LotFcc && t.SoPhieu == parsed.SoPhieu)
                : _danhSachTem.Any(t => t.LotFcc == parsed.LotFcc);

            if (daTrung)
            {
                XtraMessageBox.Show("Tem này đã được quét.", "Trùng tem",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var tem = new TemFccQuetInfo
            {
                LotFcc = parsed.LotFcc,
                MaHangFcc = parsed.MaHangFcc,
                SlTemFcc = parsed.SlTemFcc,
                Gear = parsed.Gear,
                SoPhieu = parsed.SoPhieu,
                IsTongPhieu = parsed.IsTongPhieu,
                RawQr = raw
            };

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