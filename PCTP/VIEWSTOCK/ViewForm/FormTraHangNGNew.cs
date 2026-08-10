using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using PCTP.ClassSQL;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
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
    public partial class FormTraHangNGNew : XtraForm
    {
        private readonly SQLPROVIDER _sql = new SQLPROVIDER();
        private readonly IStockTpRepository _stockTpRepo;
        private readonly ITraHangRepository _traHangRepo;
        private readonly TraHangService _traHangService;
        private readonly StockService _stockService;

        private XtraTabControl _tabs;

        // ── Tab 1: trả hàng từ kho (rework trước khi giao) ────────────
        private TextEdit _txtLot1;
        private LabelControl _lblInfo1;
        private SpinEdit _spinSl1;
        private TextEdit _txtLyDo1;
        private SimpleButton _btnTra1;
        private StockItem _currentStock;
        private int _slotIdHienTai;

        // ── Tab 2: khách trả — quét thùng ──────────────────────────────
        private SpinEdit _spinIdp;
        private TextEdit _txtQrThung;
        private GridControl _gridThung;
        private GridView _gridViewThung;
        private DataTable _donHangDuKien;

        public FormTraHangNGNew()
        {
            _stockTpRepo = new StockTpRepository(_sql);
            _traHangRepo = new TraHangRepository(_sql);
            _stockService = new StockService();
            _traHangService = new TraHangService(_sql, _stockTpRepo, _traHangRepo, _stockService);

            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Trả hàng NG";
            Size = new System.Drawing.Size(1000, 680);
            StartPosition = FormStartPosition.CenterParent;

            _tabs = new XtraTabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(BuildTabTuKho());
            _tabs.TabPages.Add(BuildTabKhachTra());
            Controls.Add(_tabs);
        }

        // ════════════════════════════════════════════════════════════
        // TAB 1 — Trả hàng đang lưu kho về sản xuất (Luồng 1a)
        // ════════════════════════════════════════════════════════════
        private XtraTabPage BuildTabTuKho()
        {
            var page = new XtraTabPage { Text = "Trả hàng từ kho → SX (rework)" };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 2, Padding = new Padding(15) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); // Tăng chiều cao chứa nút để không bị cắt chữ
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(new LabelControl { Text = "Quét/Nhập LOT NO:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) } }, 0, 0);
            _txtLot1 = new TextEdit { Dock = DockStyle.Fill, Font = new System.Drawing.Font("Tahoma", 12) };
            _txtLot1.KeyDown += TxtLot1_KeyDown;
            layout.Controls.Add(_txtLot1, 1, 0);

            _lblInfo1 = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 10) },
                Text = "Chưa có thông tin LOT."
            };
            layout.SetColumnSpan(_lblInfo1, 2);
            layout.Controls.Add(_lblInfo1, 0, 1);

            layout.Controls.Add(new LabelControl { Text = "SL trả rework:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F) } }, 0, 2);
            _spinSl1 = new SpinEdit { Dock = DockStyle.Fill, Properties = { MinValue = 1, IsFloatValue = false } };
            layout.Controls.Add(_spinSl1, 1, 2);

            layout.Controls.Add(new LabelControl { Text = "Lý do NG:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F) } }, 0, 3);
            _txtLyDo1 = new TextEdit { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtLyDo1, 1, 3);

            // Sửa lỗi hiển thị nút: Gộp 2 cột cho panel chứa nút bấm
            _btnTra1 = new SimpleButton { Text = "Trả về sản xuất", Anchor = AnchorStyles.Right | AnchorStyles.Top, Width = 220, Height = 40, Enabled = false };
            _btnTra1.Appearance.BackColor = System.Drawing.Color.IndianRed;
            _btnTra1.Appearance.ForeColor = System.Drawing.Color.White;
            _btnTra1.Appearance.Font = new System.Drawing.Font("Tahoma", 10, System.Drawing.FontStyle.Bold);
            _btnTra1.Click += BtnTra1_Click;

            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnPanel.Controls.Add(_btnTra1);

            layout.SetColumnSpan(btnPanel, 2);
            layout.Controls.Add(btnPanel, 0, 4);

            page.Controls.Add(layout);
            return page;
        }

        private void TxtLot1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            string lot = LotNoHelper.NormalizeLot(_txtLot1.Text.Trim());
            if (string.IsNullOrEmpty(lot)) return;

            _currentStock = _stockTpRepo.GetByLot(lot);
            if (_currentStock == null)
            {
                _lblInfo1.Text = $"Không tìm thấy LOT [{lot}] trong STOCKTP.";
                _btnTra1.Enabled = false;
                return;
            }

            int slXuat = _currentStock.SlXuat ?? 0;
            int slConLai = _currentStock.SlConLai ?? 0;
            int slNhap = _currentStock.SlNhap ?? 0;

            _lblInfo1.Text =
                $"Mã hàng: {_currentStock.Part}  |  Tên: {_currentStock.Name}\n" +
                $"Đã sản xuất/nhập: {slNhap}   |   Đã xuất/giao: {slXuat}   |   Còn trong kho: {slConLai}";

            if (slConLai <= 0)
            {
                _lblInfo1.Text += $"\n⚠ Kho không còn tồn — LOT này đã xuất hết (Đã xuất {slXuat}/{slNhap}). Hãy dùng tab \"Khách trả\".";
                _btnTra1.Enabled = false;
            }
            else
            {
                _spinSl1.Properties.MaxValue = slConLai;
                _spinSl1.Value = slConLai;
                _btnTra1.Enabled = true;

                _slotIdHienTai = TimSlotChuaLot(lot);
            }
        }

        private int TimSlotChuaLot(string lot)
        {
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdbb,
                $"SELECT TOP 1 SlotId FROM SlotLot WHERE LotNo = '{lot.Replace("'", "''")}' ORDER BY Quantity DESC");
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["SlotId"]) : 0;
        }

        private void BtnTra1_Click(object sender, EventArgs e)
        {
            if (_currentStock == null || _slotIdHienTai <= 0)
            {
                XtraMessageBox.Show("Không xác định được Slot chứa LOT này — không thể trả tự động.\n" +
                    "Vui lòng kiểm tra thủ công trong màn hình kho.",
                    "Không thể trả", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int sl = Convert.ToInt32(_spinSl1.Value);
            string lyDo = _txtLyDo1.Text.Trim();
            if (string.IsNullOrEmpty(lyDo))
            {
                XtraMessageBox.Show("Vui lòng nhập lý do NG.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (XtraMessageBox.Show($"Trả {sl} SP của LOT [{_currentStock.Lot}] về sản xuất để rework?",
                "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var result = _traHangService.TraHangSanXuat(_slotIdHienTai, _currentStock.Lot, sl, lyDo);

            XtraMessageBox.Show(result.Message, result.IsOK ? "Thành công" : "Lỗi",
                MessageBoxButtons.OK, result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            if (result.IsOK)
            {
                _txtLot1.Text = "";
                _lblInfo1.Text = "Chưa có thông tin LOT.";
                _btnTra1.Enabled = false;
                _currentStock = null;
            }
        }

        // ════════════════════════════════════════════════════════════
        // TAB 2 — Khách trả hàng: quét từng thùng theo phiếu IDP (Luồng 2)
        // ════════════════════════════════════════════════════════════
        private XtraTabPage BuildTabKhachTra()
        {
            var page = new XtraTabPage { Text = "Khách trả hàng (quét thùng)" };

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(15) };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.Controls.Add(new LabelControl { Text = "Phiếu nhận (IDP):", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) } }, 0, 0);

            _spinIdp = new SpinEdit { Dock = DockStyle.Fill, Properties = { MinValue = 1, IsFloatValue = false } };
            _spinIdp.EditValueChanged += (s, e) => LoadThungTheoIdp();
            top.Controls.Add(_spinIdp, 1, 0);
            main.Controls.Add(top, 0, 0);

            var scanPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            scanPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            scanPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanPanel.Controls.Add(new LabelControl { Text = "Bắn tem thùng:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) } }, 0, 0);

            _txtQrThung = new TextEdit { Dock = DockStyle.Fill, Font = new System.Drawing.Font("Tahoma", 12) };
            _txtQrThung.KeyDown += TxtQrThung_KeyDown;
            scanPanel.Controls.Add(_txtQrThung, 1, 0);
            main.Controls.Add(scanPanel, 0, 1);

            _gridThung = new GridControl { Dock = DockStyle.Fill };
            _gridViewThung = new GridView(_gridThung);
            _gridThung.MainView = _gridViewThung;
            _gridViewThung.OptionsBehavior.Editable = false;
            _gridViewThung.Columns.Add(new GridColumn { FieldName = "LotGoc", Caption = "Lot Gốc", Width = 180, VisibleIndex = 0 });
            _gridViewThung.Columns.Add(new GridColumn { FieldName = "MaHang", Caption = "Mã hàng", Width = 150, VisibleIndex = 1 });
            _gridViewThung.Columns.Add(new GridColumn { FieldName = "TongSl", Caption = "Tổng SL đã quét", Width = 130, VisibleIndex = 2 });
            main.Controls.Add(_gridThung, 0, 2);

            var btnXacNhan = new SimpleButton { Text = "✅ Xác nhận nhận hàng — Nhập kho ảo", Anchor = AnchorStyles.Left | AnchorStyles.Top, Width = 300, Height = 40 };
            btnXacNhan.Appearance.BackColor = System.Drawing.Color.SeaGreen;
            btnXacNhan.Appearance.ForeColor = System.Drawing.Color.White;
            btnXacNhan.Appearance.Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold);
            btnXacNhan.Click += BtnXacNhanNhanHang_Click;

            var bottomPanel = new Panel { Dock = DockStyle.Fill };
            bottomPanel.Controls.Add(btnXacNhan);
            main.Controls.Add(bottomPanel, 0, 3);

            page.Controls.Add(main);
            return page;
        }

        private void LoadThungTheoIdp()
        {
            int idp = Convert.ToInt32(_spinIdp.Value);
            if (idp <= 0) return;

            // FIX: Nạp dữ liệu dự kiến từ cơ sở dữ liệu dựa trên IDP thực tế thay vì để null
            string query = $"SELECT * FROM TMPPHIEUGIAOHANGDBCT WHERE IDP = {idp}";
            _donHangDuKien = _sql.ExecuteQuery(_sql.B7R2_FCCdbb, query);

            var nhom = _traHangRepo.GetNhomLotChuaXuLy(idp);
            _gridThung.DataSource = nhom;
        }

        private void TxtQrThung_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            string qr = _txtQrThung.Text.Trim();
            _txtQrThung.Clear();
            if (string.IsNullOrEmpty(qr)) return;

            int idp = Convert.ToInt32(_spinIdp.Value);
            if (idp <= 0)
            {
                XtraMessageBox.Show("Vui lòng nhập Phiếu nhận (IDP) trước khi quét.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Gọi hàm load lại để đảm bảo _donHangDuKien đã sẵn sàng dữ liệu
            if (_donHangDuKien == null)
            {
                LoadThungTheoIdp();
            }

            var result = _traHangService.LuuThungQuetTra(idp, qr, _donHangDuKien);
            if (!result.IsOK)
                XtraMessageBox.Show(result.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);

            LoadThungTheoIdp();
        }

        private void BtnXacNhanNhanHang_Click(object sender, EventArgs e)
        {
            int idp = Convert.ToInt32(_spinIdp.Value);
            if (idp <= 0) return;

            if (XtraMessageBox.Show("Xác nhận nhận toàn bộ hàng đã quét của phiếu này vào kho?",
                "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var result = _traHangService.XacNhanNhanHangKhachTraVeKho(idp);

            XtraMessageBox.Show(result.Message, result.IsOK ? "Thành công" : "Lỗi",
                MessageBoxButtons.OK, result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            LoadThungTheoIdp();
        }
    }
}