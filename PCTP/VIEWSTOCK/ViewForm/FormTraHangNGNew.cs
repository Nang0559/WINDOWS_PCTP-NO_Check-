using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using PCTP.ClassSQL;
using PCTP.Models;
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
        private List<ChoGiaoItem> _choGiaoHienTai = new List<ChoGiaoItem>();
        private List<SlotChuaLotInfo> _danhSachSlotChuaLot = new List<SlotChuaLotInfo>();
        private enum CheDoTra { KhongXacDinh, TuKho, ChoGiao }
        private CheDoTra _cheDoTraHienTai = CheDoTra.KhongXacDinh;
        private XtraTabControl _tabs;

        // ── Tab 1: trả hàng từ kho (rework trước khi giao) ────────────
        private TextEdit _txtLot1;
        private LabelControl _lblInfo1;
        private SpinEdit _spinSl1;
        private TextEdit _txtLyDo1;
        private SimpleButton _btnTra1;
        private StockItem _currentStock;
        private int _slotIdHienTai;

        // ── Các trường mới bổ sung cho Tab 1 ──────────────────────────
        private RadioGroup _rdoCheDoTim;
        private TextEdit _txtMaHang;
        private DateEdit _dateTu, _dateDen;
        private SimpleButton _btnTimLot;
        private GridControl _gridLotUngVien;
        private GridView _gridViewLotUngVien;

        private GridControl _gridLichSuGiao;
        private GridView _gridViewLichSuGiao;
        private GridControl _gridLichSuQr;
        private GridView _gridViewLichSuQr;
        private LabelControl _lblTitleQr;

        // ── Tab 2: khách trả — quét thùng ──────────────────────────────
        private SpinEdit _spinIdp;
        private TextEdit _txtQrThung;
        private GridControl _gridThung;
        private GridView _gridViewThung;
        private DataTable _donHangDuKien;

        private GridControl _gridSlot;
        private GridView _gridViewSlot;
        private List<LichSuQrCodeInfo> _lichSuQrFull = new List<LichSuQrCodeInfo>(); // cache toàn bộ, filter theo Stt khi chọn phiếu

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
            Size = new System.Drawing.Size(1200, 750);
            StartPosition = FormStartPosition.CenterParent;

            _tabs = new XtraTabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(BuildTabTuKho());
            _tabs.TabPages.Add(BuildTabKhachTra());
            Controls.Add(_tabs);
        }

        // ════════════════════════════════════════════════════════════
        // TAB 1 — Trả hàng đang lưu kho về sản xuất (Luồng 1a/1b)
        // ════════════════════════════════════════════════════════════
        // ════════════════════════════════════════════════════════════
        // TAB 1 — Trả hàng đang lưu kho về sản xuất (Luồng 1a/1b)
        // ════════════════════════════════════════════════════════════
        private XtraTabPage BuildTabTuKho()
        {
            var page = new XtraTabPage { Text = "Trả hàng từ kho → SX (rework)" };

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 1. Phần Top: Chọn chế độ tìm & Nhập liệu
            var panelTop = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 2, Padding = new Padding(5) };
            panelTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            panelTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            panelTop.Controls.Add(new LabelControl { Text = "Hình thức tìm kiếm:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold) } }, 0, 0);

            _rdoCheDoTim = new RadioGroup { Dock = DockStyle.Fill };
            _rdoCheDoTim.Properties.Items.Add(new DevExpress.XtraEditors.Controls.RadioGroupItem(0, "Theo LotNo trực tiếp"));
            _rdoCheDoTim.Properties.Items.Add(new DevExpress.XtraEditors.Controls.RadioGroupItem(1, "Theo Mã hàng + Ngày giao"));
            _rdoCheDoTim.EditValue = 0;
            _rdoCheDoTim.SelectedIndexChanged += (s, e) => ToggleCheDoTim();
            panelTop.Controls.Add(_rdoCheDoTim, 1, 0);

            var lblLot = new LabelControl { Text = "Quét/Nhập LOT NO:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) } };
            _txtLot1 = new TextEdit { Dock = DockStyle.Fill, Font = new System.Drawing.Font("Tahoma", 11) };
            _txtLot1.KeyDown += TxtLot1_KeyDown;
            panelTop.Controls.Add(lblLot, 0, 1);
            panelTop.Controls.Add(_txtLot1, 1, 1);

            var panelMaHangDate = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
            panelMaHangDate.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            panelMaHangDate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            panelMaHangDate.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
            panelMaHangDate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            panelMaHangDate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            _txtMaHang = new TextEdit { Dock = DockStyle.Fill };
            _dateTu = new DateEdit { Dock = DockStyle.Fill, DateTime = DateTime.Today.AddDays(-7) };
            _dateDen = new DateEdit { Dock = DockStyle.Fill, DateTime = DateTime.Today };
            _btnTimLot = new SimpleButton { Text = "🔍 Tìm", Dock = DockStyle.Fill };
            _btnTimLot.Click += BtnTimLot_Click;

            panelMaHangDate.Controls.Add(new LabelControl { Text = "Mã hàng:" }, 0, 0);
            panelMaHangDate.Controls.Add(_txtMaHang, 1, 0);
            panelMaHangDate.Controls.Add(new LabelControl { Text = " Từ:" }, 2, 0);
            panelMaHangDate.Controls.Add(_dateTu, 3, 0);
            panelMaHangDate.Controls.Add(_dateDen, 4, 0);

            panelTop.Controls.Add(new LabelControl { Text = "Điều kiện lọc:", Dock = DockStyle.Fill }, 0, 2);
            panelTop.Controls.Add(panelMaHangDate, 1, 2);

            panelTop.Controls.Add(new LabelControl { Text = "", Dock = DockStyle.Fill }, 0, 3);
            panelTop.Controls.Add(_btnTimLot, 1, 3);

            mainLayout.Controls.Add(panelTop, 0, 0);

            // 2. Phần Bottom: Chia 2 cột (Trái: thông tin + nút trả + grid ứng viên + grid Slot; Phải: 2 bảng lịch sử)
            var splitContent = new SplitContainerControl { Dock = DockStyle.Fill, SplitterPosition = 420 };

            // ── LEFT PANEL — 8 hàng tối ưu bố cục ──────────────────────────
            var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 1, Padding = new Padding(5) };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));  // 0: info
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // 1: SL trả
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // 2: Lý do NG
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // 3: Nút trả về sản xuất
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));  // 4: Tiêu đề Lot hiện có
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // 5: Grid Lot ứng viên
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));  // 6: Tiêu đề Slot
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // 7: Grid Slot

            _lblInfo1 = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9F) },
                Text = "Chưa có thông tin LOT."
            };
            leftPanel.Controls.Add(_lblInfo1, 0, 0);

            var slPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            slPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            slPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            slPanel.Controls.Add(new LabelControl { Text = "SL trả rework:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) } }, 0, 0);
            _spinSl1 = new SpinEdit { Dock = DockStyle.Fill, Properties = { MinValue = 1, IsFloatValue = false }, Font = new System.Drawing.Font("Tahoma", 11) };
            slPanel.Controls.Add(_spinSl1, 1, 0);
            leftPanel.Controls.Add(slPanel, 0, 1);

            var lyDoPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            lyDoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            lyDoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            lyDoPanel.Controls.Add(new LabelControl { Text = "Lý do NG:", Dock = DockStyle.Fill, Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold) } }, 0, 0);
            _txtLyDo1 = new TextEdit { Dock = DockStyle.Fill, Font = new System.Drawing.Font("Tahoma", 10) };
            lyDoPanel.Controls.Add(_txtLyDo1, 1, 0);
            leftPanel.Controls.Add(lyDoPanel, 0, 2);

            _btnTra1 = new SimpleButton { Text = "Trả về sản xuất", Dock = DockStyle.Fill, Height = 40, Enabled = false };
            _btnTra1.Appearance.BackColor = System.Drawing.Color.IndianRed;
            _btnTra1.Appearance.ForeColor = System.Drawing.Color.White;
            _btnTra1.Appearance.Font = new System.Drawing.Font("Tahoma", 10, System.Drawing.FontStyle.Bold);
            _btnTra1.Click += BtnTra1_Click;
            leftPanel.Controls.Add(_btnTra1, 0, 3);

            // Tiêu đề & Grid Lot ứng viên
            var lblTitleLotUV = new LabelControl
            {
                Text = "📦 Thông tin Lot hiện có",
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.DarkGreen }
            };
            leftPanel.Controls.Add(lblTitleLotUV, 0, 4);

            _gridLotUngVien = new GridControl { Dock = DockStyle.Fill };
            _gridViewLotUngVien = new GridView(_gridLotUngVien);
            _gridLotUngVien.MainView = _gridViewLotUngVien;
            _gridViewLotUngVien.OptionsBehavior.Editable = false;
            _gridViewLotUngVien.DoubleClick += GridViewLotUngVien_DoubleClick;
            leftPanel.Controls.Add(_gridLotUngVien, 0, 5);

            // Tiêu đề & Grid Slot chứa LOT
            var lblTitleSlot = new LabelControl
            {
                Text = "📦 Thông tin Slot đang chứa LOT",
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.DarkGreen }
            };
            leftPanel.Controls.Add(lblTitleSlot, 0, 6);

            _gridSlot = new GridControl { Dock = DockStyle.Fill };
            _gridViewSlot = new GridView(_gridSlot);
            _gridSlot.MainView = _gridViewSlot;
            _gridViewSlot.OptionsBehavior.Editable = false;
            _gridViewSlot.OptionsView.ShowGroupPanel = false;

            // Kiểm tra tránh add trùng cột nếu gọi lại BuildUI nhiều lần
            if (_gridViewSlot.Columns.Count == 0)
            {
                _gridViewSlot.Columns.Add(new GridColumn { FieldName = "WarehouseName", Caption = "Kho", Width = 80, VisibleIndex = 0 });
                _gridViewSlot.Columns.Add(new GridColumn { FieldName = "RackName", Caption = "Rack", Width = 70, VisibleIndex = 1 });
                _gridViewSlot.Columns.Add(new GridColumn { FieldName = "SlotNumber", Caption = "Slot", Width = 45, VisibleIndex = 2 });
                _gridViewSlot.Columns.Add(new GridColumn { FieldName = "Quantity", Caption = "SL", Width = 50, VisibleIndex = 3 });
                _gridViewSlot.Columns.Add(new GridColumn { FieldName = "TemCode", Caption = "TemCode", Width = 90, VisibleIndex = 4 });
            }
            _gridViewSlot.FocusedRowChanged += GridViewSlot_FocusedRowChanged;
            leftPanel.Controls.Add(_gridSlot, 0, 7);

            splitContent.Panel1.Controls.Add(leftPanel);

            // 3. Phần Right: 2 bảng Lịch sử giao & Đọc QR (master-detail theo Stt)
            var rightLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(5) };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var lblTitleGiao = new LabelControl
            {
                Text = "📋 Dữ liệu phiếu giao hàng",
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.Navy }
            };
            rightLayout.Controls.Add(lblTitleGiao, 0, 0);

            _gridLichSuGiao = new GridControl { Dock = DockStyle.Fill };
            _gridViewLichSuGiao = new GridView(_gridLichSuGiao);
            _gridLichSuGiao.MainView = _gridViewLichSuGiao;
            _gridViewLichSuGiao.OptionsBehavior.Editable = false;
            _gridViewLichSuGiao.FocusedRowChanged += GridViewLichSuGiao_FocusedRowChanged;
            rightLayout.Controls.Add(_gridLichSuGiao, 0, 1);

            _lblTitleQr = new LabelControl
            {
                Text = "📋 Dữ liệu đọc QRcode",
                Dock = DockStyle.Fill,
                Appearance = { Font = new System.Drawing.Font("Tahoma", 9.5F, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.Navy }
            };
            rightLayout.Controls.Add(_lblTitleQr, 0, 2);

            _gridLichSuQr = new GridControl { Dock = DockStyle.Fill };
            _gridViewLichSuQr = new GridView(_gridLichSuQr);
            _gridLichSuQr.MainView = _gridViewLichSuQr;
            _gridViewLichSuQr.OptionsBehavior.Editable = false;
            rightLayout.Controls.Add(_gridLichSuQr, 0, 3);

            splitContent.Panel2.Controls.Add(rightLayout);

            mainLayout.Controls.Add(splitContent, 0, 1);
            page.Controls.Add(mainLayout);

            ToggleCheDoTim();
            return page;
        }

        private void GridViewSlot_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            var view = sender as GridView;
            if (view == null) return;

            var row = view.GetRow(e.FocusedRowHandle) as SlotChuaLotInfo;
            if (row == null || _cheDoTraHienTai != CheDoTra.TuKho) return;

            _slotIdHienTai = row.SlotId;
            _spinSl1.Properties.MaxValue = row.Quantity;
            _spinSl1.Value = row.Quantity;
            _btnTra1.Enabled = true;
        }

        private void GridViewLichSuGiao_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            var view = sender as GridView;
            if (view == null) return;

            var row = view.GetRow(e.FocusedRowHandle) as LichSuGiaoHangInfo;
            if (row == null)
            {
                _gridLichSuQr.DataSource = new List<LichSuQrCodeInfo>();
                return;
            }

            var selectedLotUV = _gridViewLotUngVien.GetFocusedRow() as LotUngVienInfo;
            string rawLot = selectedLotUV?.Lot?.Trim() ?? "";

            if (string.IsNullOrEmpty(rawLot))
            {
                _gridLichSuQr.DataSource = new List<LichSuQrCodeInfo>();
                return;
            }

            string fullLot = rawLot;
            string shortLot = rawLot.Length > 7 ? rawLot.Substring(0, rawLot.Length - 7) : rawLot;

            string rowGioGiao = row.GioGiao?.Trim() ?? "";
            DateTime? rowDate = row.NgayGiao?.Date;

            // 1. Lọc danh sách QRcode khớp phiếu
            var qrCuaPhieuNay = _lichSuQrFull.Where(x =>
                (!rowDate.HasValue || !x.NgayXuat.HasValue || x.NgayXuat.Value.Date == rowDate.Value)
                &&
                (
                    string.IsNullOrEmpty(rowGioGiao) ||
                    (!string.IsNullOrEmpty(x.GioXuat) && x.GioXuat.Trim().Equals(rowGioGiao, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(x.GioGiao) && x.GioGiao.Trim().Equals(rowGioGiao, StringComparison.OrdinalIgnoreCase))
                )
                &&
                (
                    (!string.IsNullOrEmpty(x.LotFcc) && (x.LotFcc.Contains(shortLot) || x.LotFcc.Contains(fullLot))) ||
                    (!string.IsNullOrEmpty(x.LotHvn) && (x.LotHvn.Contains(shortLot) || x.LotHvn.Contains(fullLot)))
                )
            ).ToList();

            _gridLichSuQr.DataSource = qrCuaPhieuNay;

            // 2. TÍNH SUM SỐ LƯỢNG ĐỂ ĐỐI CHÍNH XÁC:
            // - Số lượng từ chuỗi lot ghép của phiếu giao hiện tại:
            int slTheoPhieuGiao = GetSlFromLotString(row.Lot, rawLot);

            // - Tổng số lượng từ các dòng đọc QR (ví dụ cộng cột SlTemFcc):
            int slTheoDocQr = qrCuaPhieuNay.Sum(x => x.SlTemFcc);

            // ✅ Hiển thị kết quả lên tiêu đề Group/Grid hoặc Label để kiểm tra khớp nhau
            // Ví dụ gán vào tiêu đề nhóm của grid QR hoặc Status:
            _lblTitleQr.Text = $"📋 Dữ liệu đọc QRcode  |  (SL Phiếu giao: {slTheoPhieuGiao}  -  SL Quét QR: {slTheoDocQr})";
        }

        private void ToggleCheDoTim()
        {
            bool theoLot = Convert.ToInt32(_rdoCheDoTim.EditValue) == 0;
            _txtLot1.Enabled = theoLot;
            _txtMaHang.Enabled = !theoLot;
            _dateTu.Enabled = !theoLot;
            _dateDen.Enabled = !theoLot;
            _btnTimLot.Enabled = !theoLot;
            _gridLotUngVien.Visible = !theoLot;
        }

        private void TxtLot1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            string lot = LotNoHelper.GetStockTpKey(_txtLot1.Text.Trim());
            if (string.IsNullOrEmpty(lot)) return;
            TraCuuLot(lot);
        }

        private void TraCuuLot(string lot)
        {
            _currentStock = _stockTpRepo.GetByLot(lot);

            var lichSuGiao = _traHangRepo.GetLichSuGiaoHangTheoLot(lot);
            _lichSuQrFull = _traHangRepo.GetLichSuQrCodeTheoLot(lot);
            _gridLichSuGiao.DataSource = lichSuGiao;
            _gridLichSuQr.DataSource = new List<LichSuQrCodeInfo>(); // để trống, chờ chọn phiếu

            _danhSachSlotChuaLot = _traHangRepo.GetSlotsChuaLot(lot);
            _gridSlot.DataSource = _danhSachSlotChuaLot;

            _choGiaoHienTai = _traHangRepo.GetChoGiaoTheoLot(lot);

            if (_currentStock == null)
            {
                _lblInfo1.Text = $"Không tìm thấy LOT [{lot}] trong STOCKTP " +
                    $"(có {lichSuGiao.Count} lần giao trong lịch sử — xem bảng dưới).";
                _btnTra1.Enabled = false;
                _cheDoTraHienTai = CheDoTra.KhongXacDinh;
                return;
            }

            int slXuat = _currentStock.SlXuat ?? 0;
            int slConLai = _currentStock.SlConLai ?? 0;
            int slNhap = _currentStock.SlNhap ?? 0;
            int tongSlTrongSlot = _danhSachSlotChuaLot.Sum(x => x.Quantity);

            _lblInfo1.Text =
                $"Mã hàng: {_currentStock.Part}  |  Tên: {_currentStock.Name}\n" +
                $"Nhập: {slNhap} | Xuất: {slXuat} | Tồn kho: {slConLai} | Đã giao: {lichSuGiao.Count} phiếu\n" +
                $"Đang nằm trong {_danhSachSlotChuaLot.Count} Slot (tổng {tongSlTrongSlot})";

            if (_danhSachSlotChuaLot.Count > 0)
            {
                _cheDoTraHienTai = CheDoTra.TuKho;
                _spinSl1.Enabled = true;
                _slotIdHienTai = 0; // chờ user chọn 1 dòng trong _gridSlot
                _btnTra1.Enabled = false; // bật lại khi chọn Slot cụ thể — xem GridViewSlot_FocusedRowChanged

                if (_danhSachSlotChuaLot.Count == 1)
                    _gridViewSlot.FocusedRowHandle = 0; // chỉ 1 Slot -> tự chọn luôn
            }
            else if (_choGiaoHienTai.Count > 0)
            {
                int tongChoGiao = _choGiaoHienTai.Sum(x => x.SoLuong);
                _lblInfo1.Text += $"\n🚚 Nguồn: đang CHỜ GIAO ({_choGiaoHienTai.Count} thùng, tổng SL {tongChoGiao}) — sẽ huỷ toàn bộ để rework";
                _cheDoTraHienTai = CheDoTra.ChoGiao;
                _spinSl1.Properties.MaxValue = tongChoGiao;
                _spinSl1.Value = tongChoGiao;
                _spinSl1.Enabled = false;
                _btnTra1.Enabled = true;
            }
            else
            {
                _lblInfo1.Text += "\n⚠ Không tìm thấy LOT trong Slot hoặc trong danh sách chờ giao — không thể trả tự động.";
                _cheDoTraHienTai = CheDoTra.KhongXacDinh;
                _btnTra1.Enabled = false;
            }
        }

        

        private void BtnTra1_Click(object sender, EventArgs e)
        {
            if (_currentStock == null || _cheDoTraHienTai == CheDoTra.KhongXacDinh)
            {
                XtraMessageBox.Show("Không xác định được nguồn của LOT này — không thể trả tự động.",
                    "Không thể trả", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string lyDo = _txtLyDo1.Text.Trim();
            if (string.IsNullOrEmpty(lyDo))
            {
                XtraMessageBox.Show("Vui lòng nhập lý do NG.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ScanResult result;

            if (_cheDoTraHienTai == CheDoTra.TuKho)
            {
                if (_slotIdHienTai <= 0)
                {
                    XtraMessageBox.Show("Vui lòng chọn 1 Slot trong danh sách trước khi trả.",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int sl = Convert.ToInt32(_spinSl1.Value);

                if (XtraMessageBox.Show($"Trả {sl} SP của LOT [{_currentStock.Lot}] về sản xuất để rework?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                result = _traHangService.TraHangSanXuat(_slotIdHienTai, _currentStock.Lot, sl, lyDo);
            }
            else // ChoGiao — huỷ nguyên cả LOT
            {
                int tongSl = _choGiaoHienTai.Sum(x => x.SoLuong);
                if (XtraMessageBox.Show(
                    $"Huỷ {_choGiaoHienTai.Count} thùng chờ giao ({tongSl} SP) của LOT [{_currentStock.Lot}], trả về sản xuất?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                result = _traHangService.HuyChoGiaoVeSanXuat(
                    _choGiaoHienTai.Select(x => x.Id).ToList(), lyDo);
            }

            XtraMessageBox.Show(result.Message, result.IsOK ? "Thành công" : "Lỗi",
                MessageBoxButtons.OK, result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            if (result.IsOK)
            {
                _txtLot1.Text = "";
                _lblInfo1.Text = "Chưa có thông tin LOT.";
                _btnTra1.Enabled = false;
                _spinSl1.Enabled = true;
                _currentStock = null;
                _choGiaoHienTai.Clear();
                _danhSachSlotChuaLot.Clear();
                _gridSlot.DataSource = null;
                _gridLichSuQr.DataSource = null;
                _slotIdHienTai = 0;
                _cheDoTraHienTai = CheDoTra.KhongXacDinh;
            }
        }

        private void BtnTimLot_Click(object sender, EventArgs e)
        {
            string maHang = _txtMaHang.Text.Trim();
            if (string.IsNullOrEmpty(maHang))
            {
                XtraMessageBox.Show("Vui lòng nhập mã hàng cần tìm.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var ungVien = _traHangRepo.TimLotTheoMaHangNgay(
                maHang, _dateTu.DateTime, _dateDen.DateTime);

            _gridLotUngVien.DataSource = ungVien;

            if (ungVien.Count == 0)
                XtraMessageBox.Show("Không tìm thấy LOT nào đã giao khớp mã hàng/khoảng ngày.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void GridViewLotUngVien_DoubleClick(object sender, EventArgs e)
        {
            var row = _gridViewLotUngVien.GetFocusedRow() as LotUngVienInfo;
            if (row == null || string.IsNullOrEmpty(row.Lot)) return;

            _txtLot1.Text = row.Lot;
            TraCuuLot(row.Lot);
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
        private int GetSlFromLotString(string lotString, string targetLot)
        {
            if (string.IsNullOrEmpty(lotString) || string.IsNullOrEmpty(targetLot)) return 0;

            targetLot = targetLot.Trim();
            string shortTarget = targetLot.Length > 7 ? targetLot.Substring(0, targetLot.Length - 7) : targetLot;

            int totalSl = 0;
            // Tách các cụm cách nhau bằng dấu phẩy
            var parts = lotString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                string item = p.Trim();
                // Tìm xem cụm này có chứa mã lot đang tìm không (so sánh cả bản full hoặc short)
                if (item.Contains(targetLot) || item.Contains(shortTarget))
                {
                    var subParts = item.Split('-');
                    if (subParts.Length >= 2)
                    {
                        // Lấy phần tử cuối hoặc phần tử ngay sau dấu '-' sát với mã lot
                        if (int.TryParse(subParts[subParts.Length - 1].Trim(), out int sl))
                        {
                            totalSl += sl;
                        }
                    }
                }
            }
            return totalSl;
        }
    }
}