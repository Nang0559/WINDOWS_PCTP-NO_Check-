using DevExpress.CodeParser;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.XtraSplashScreen;
using PCTP.ClassSQL;
using PCTP.Domain.Events;
using PCTP.Infrastructure;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.ViewForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace PCTP.VIEWSTOCK
{

    /// <summary>
    /// Form nhập kho hàng hóa: quét QR tem tổng -> đối chiếu vNhapTP ->
    /// (tuỳ ItemCode) kiểm tra qua FormInspection -> chọn Slot đích (hoặc nhập
    /// hàng loạt vào Slot ảo) -> lưu (STOCKTP + SlotLot + Slot, transactional).
    ///
    /// GHI CHÚ TÍCH HỢP:
    ///   - Toàn bộ ghi DB đi qua NhapTpReceivingService.NhapTpVaoSlot — nơi DUY NHẤT
    ///     cập nhật STOCKTP + SlotLot + Slot trong 1 transaction. KHÔNG dùng
    ///     StockService.ImportLotToSlot nữa vì method đó không đụng STOCKTP.
    ///   - Trước khi cho nhập, LOT quét được PHẢI khớp với 1 dòng trong vNhapTP
    ///     (qua StockTpRepository.TimPhieuTheoLotQR) — tái dùng thuật toán
    ///     LotNoHelper.BuildFindList (logic cũ từ NHAP_TP.cs).
    ///   - Chế độ "Nhập hàng loạt": bỏ qua bước chọn Slot, luôn nhập vào 1
    ///     Warehouse/Rack/Slot ẢO cố định (StockService.GetOrCreateBulkImportSlotText),
    ///     dùng để gom hàng tạm — cần có quy trình dồn hàng ra Slot thật sau đó.
    /// </summary>
    public partial class FormEnterItemSV : DevExpress.XtraEditors.XtraForm
    {
        // ==== Controls (giữ theo đúng những gì bạn đã có; phần còn lại do Designer sinh) ====

        private Panel panelSlotList;
        private ListBoxControl listBoxSlots;
        private SimpleButton btnOK;
        private SimpleButton btnCancel;
        private LabelControl lblTemCode, lblItemCode, lblLotNo, lblQty, lblrackName, lblSlotNumber, lblwhName;
        private GroupControl groupInfo, groupSlotList;
        private TableLayoutPanel contentPanel, mainLayout;

        // ==== THÊM: toggle nhập hàng loạt ====
        private CheckEdit chkNhapHangLoat;

        // ==== State ====
        private QRCodeInfo codeInfo;

        // ==== THÊM: phiếu sản xuất (vNhapTP) đã đối chiếu khớp với LOT quét được ====
        private PhieuNhapInfo _matchedPhieu;

        // ==== Dependencies ====
        private readonly StockService _stockService = new StockService();
        private readonly MainStockSV _mainStockForm;

        // ==== THÊM: dependencies cho luồng nhập STOCKTP (giống NHAP_TP.cs cũ) ====
        private readonly SQLPROVIDER _sql = new SQLPROVIDER();
        private readonly IStockTpRepository _stockTpRepo;
        private readonly NhapTpReceivingService _nhapTpService;
        // Thêm fields trong class FormEnterItemSV
        private GridControl _gridPhieu;
        private GridView _gridViewPhieu;
        private BindingList<PhieuNhapInfo> _dsPhieu;
        private HashSet<string> _sessionTouchedFinds = new HashSet<string>(); // Find đã bắn trong phiên này
                                                                              // Thêm field lblBulkStatus (đã đề cập ở câu trả lời trước)
        private LabelControl lblBulkStatus;
        private Dictionary<string, PhieuNhapInfo> _dsPhieuIndex;
        public FormEnterItemSV(MainStockSV mainStockForm)
        {
            InitializeComponent();
          
            _mainStockForm = mainStockForm;

            // ── Khởi tạo repository/service cho luồng nhập STOCKTP ──────────────
            _stockTpRepo = new StockTpRepository(_sql);
            _nhapTpService = new NhapTpReceivingService(
                _sql,
                _stockTpRepo,
                new PhieuTrackingRepository(_sql));
            InitializeForm();
            // ✅ Đăng ký lắng nghe
            AppEventBus.Instance.Subscribe<LotStatusResetEvent>(OnLotStatusReset);
        }

        private void InitializeForm()
        {
            this.Text = "NHẬP KHO HÀNG HÓA - CANVAS UI";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterParent;

            // ── Panel top: gom chkNhapHangLoat + txtQRCode + lblBulkStatus ──────
            // Dock=Top của panel này tự eat đúng chiều cao cần thiết, không xung
            // đột với _gridPhieu (Dock=Fill) hay mainLayout (được add SAU trong
            // ShowQRCodeInfo, cũng Dock=Fill).
            var panelTop = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                RowCount = 3,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

            chkNhapHangLoat = new CheckEdit
            {
                Text = "Nhập hàng loạt (không cần chọn Slot — vào kho tạm)",
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 10, FontStyle.Bold)
            };
            chkNhapHangLoat.Properties.Appearance.ForeColor = Color.DarkOrange;
            chkNhapHangLoat.Properties.Appearance.Options.UseForeColor = true;

            txtQRCode = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 12)
            };
            txtQRCode.KeyDown += TxtQRCode_KeyDown;

            lblBulkStatus = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold) }
            };

            panelTop.Controls.Add(chkNhapHangLoat, 0, 0);
            panelTop.Controls.Add(txtQRCode, 0, 1);
            panelTop.Controls.Add(lblBulkStatus, 0, 2);

            // ── Grid phiếu chiếm phần còn lại ────────────────────────────────────
            _gridPhieu = new GridControl { Dock = DockStyle.Fill };
            _gridViewPhieu = new GridView(_gridPhieu);
            _gridPhieu.MainView = _gridViewPhieu;
            _gridViewPhieu.OptionsBehavior.Editable = false;
            _gridViewPhieu.OptionsView.ShowGroupPanel = false;
            _gridViewPhieu.RowStyle += GridViewPhieu_RowStyle;

            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "Find", Caption = "FIND", Width = 180, VisibleIndex = 0 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "LotNo", Caption = "LotNo", Width = 150, VisibleIndex = 1 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "MaSP", Caption = "Mã SP", Width = 130, VisibleIndex = 2 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "TenSP", Caption = "Tên SP", Width = 180, VisibleIndex = 3 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "CaSX", Caption = "Ca SX", Width = 60, VisibleIndex = 4 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "NgaySX", Caption = "Ngày SX", Width = 90, VisibleIndex = 5 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "SlSanXuat", Caption = "SL Sản Xuất", Width = 90, VisibleIndex = 6 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "SlDaNhap", Caption = "SL Đã Nhập", Width = 90, VisibleIndex = 7 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "KetThucLot", Caption = "Kết Thúc", Width = 70, VisibleIndex = 8 });

            // ── QUAN TRỌNG: add _gridPhieu TRƯỚC, panelTop SAU ───────────────────
            // Vì trong Controls collection, control add sau sẽ "chiếm chỗ trước"
            // đối với các Dock cạnh biên (Top/Bottom/Left/Right); control Dock=Fill
            // phải add sớm nhất để nhường không gian đúng cho các Dock biên add sau.
            this.Controls.Add(_gridPhieu);
            this.Controls.Add(panelTop);

            LoadDanhSachPhieu();
        }
        private void OnLotStatusReset(LotStatusResetEvent e)
        {
            if (string.IsNullOrEmpty(e.Lot)) return;
            if (_dsPhieuIndex == null) return;   // form chưa load xong danh sách (phòng thủ)

            // ── Ưu tiên tra theo Find (chính xác tuyệt đối) ──────────────────────
            PhieuNhapInfo target = null;
            if (!string.IsNullOrEmpty(e.Find))
                _dsPhieuIndex.TryGetValue(e.Find, out target);

            // ── Fallback: tra theo LotNo nếu Publish không kèm Find ──────────────
            if (target == null)
                target = _dsPhieuIndex.Values
                    .FirstOrDefault(x => string.Equals(x.LotNo, e.Lot, StringComparison.OrdinalIgnoreCase));

            if (target == null) return;   // LOT này không nằm trong danh sách đang hiển thị -> bỏ qua

            var latest = _stockTpRepo.GetPhieuByFind(target.Find);
            if (latest == null) return;

            // ── Mutate TRỰC TIẾP object đang nằm trong _dsPhieuIndex/_dsPhieu ────
            // (đây là object mà _gridViewPhieu đang bind — sửa tại chỗ để grid vẽ lại đúng)
            target.KetThucLot = latest.KetThucLot;
            target.SlDaNhap = latest.SlDaNhap;
            target.SlSanXuat = latest.SlSanXuat;

            // ── Đồng bộ luôn _matchedPhieu nếu đang trỏ đúng Find này ─────────────
            // (không dùng == để so identity vì _matchedPhieu có thể là instance khác
            //  target dù cùng Find — do TimPhieuTheoLotQR luôn tạo object mới)
            if (_matchedPhieu != null &&
                string.Equals(_matchedPhieu.Find, target.Find, StringComparison.OrdinalIgnoreCase))
            {
                _matchedPhieu.KetThucLot = latest.KetThucLot;
                _matchedPhieu.SlDaNhap = latest.SlDaNhap;
                _matchedPhieu.SlSanXuat = latest.SlSanXuat;
            }

            _gridViewPhieu.RefreshData();   // ép GridViewPhieu_RowStyle chạy lại -> đổi màu ngay
        }

        private void GridViewPhieu_RowStyle(object sender, RowStyleEventArgs e)
        {
            var row = _gridViewPhieu.GetRow(e.RowHandle) as PhieuNhapInfo;
            if (row == null) return;

            if (row.KetThucLot)
            {
                // Đã kết thúc LOT — xám, không thể nhập thêm
                e.Appearance.BackColor = Color.LightGray;
                e.Appearance.ForeColor = Color.DimGray;
            }
            else if (row.SlDaNhap >= row.SlSanXuat && row.SlSanXuat > 0)
            {
                // Đã nhập đủ hoặc vượt sản lượng
                e.Appearance.BackColor = Color.FromArgb(200, 255, 200); // xanh nhạt
            }
            else if (_sessionTouchedFinds.Contains(row.Find))
            {
                // Vừa bắn trong phiên làm việc này — tô nổi bật để dễ theo dõi
                e.Appearance.BackColor = Color.FromArgb(255, 230, 150); // cam nhạt
                e.Appearance.Font = new Font("Tahoma", 9, FontStyle.Bold);
            }
            // else: giữ màu mặc định — chưa động tới
        }
        /// <summary>
        /// Định vị dòng trong grid theo Find, focus + đánh dấu đã chạm trong phiên.
        /// Gọi ngay sau khi TimPhieuTheoLotQR trả về kết quả khớp — không phân biệt
        /// bulk hay thường, vì cả 2 đều cần feedback trực quan trên grid.
        /// </summary>
        private void HighlightMatchedRow(PhieuNhapInfo phieu)
        {
            if (phieu == null || string.IsNullOrEmpty(phieu.Find)) return;

            _sessionTouchedFinds.Add(phieu.Find);

            // Nếu phieu không nằm trong danh sách đã tải (VD: nằm ngoài khoảng 30 ngày
            // do lọc GetPhieuDangSanXuat), thêm bổ sung vào _dsPhieu để vẫn thấy được
            // trên grid thay vì bị "mất tích" dù nhập thành công.
            if (_dsPhieuIndex != null && !_dsPhieuIndex.ContainsKey(phieu.Find))
            {
                _dsPhieu.Add(phieu);
                _dsPhieuIndex[phieu.Find] = phieu;
            }

            int rowHandle = _gridViewPhieu.LocateByValue("Find", phieu.Find);
            if (rowHandle >= 0)
            {
                _gridViewPhieu.FocusedRowHandle = rowHandle;
                _gridViewPhieu.MakeRowVisible(rowHandle);
            }

            _gridViewPhieu.RefreshData();
        }

        /// <summary>
        /// Sau khi lưu thành công (bulk hoặc thường), cập nhật SL đã nhập của đúng dòng
        /// trong _dsPhieu để grid phản ánh đúng tiến độ mà không cần query lại toàn bộ.
        /// </summary>
        private void UpdateSlDaNhapInGrid(string find, int slVuaNhap)
        {
            if (_dsPhieuIndex == null || string.IsNullOrEmpty(find)) return;

            if (_dsPhieuIndex.TryGetValue(find, out var item))
            {
                item.SlDaNhap += slVuaNhap;
                // ✅ Đồng bộ KetThucLot ngay trên grid, khớp logic Satus vừa tính trong service
                item.KetThucLot = item.SlSanXuat > 0 && item.SlDaNhap >= item.SlSanXuat;
                _gridViewPhieu.RefreshData();
            }
        }
        private void LoadDanhSachPhieu()
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;

                var list = _stockTpRepo.GetPhieuDangSanXuat(soNgayGanDay: 30);

                _dsPhieu = new BindingList<PhieuNhapInfo>(list);
                _gridPhieu.DataSource = _dsPhieu;

                // ✅ Build index tra cứu O(1) theo Find — dùng trong UpdateSlDaNhapInGrid
                // thay vì FirstOrDefault (O(n)) mỗi lần quét.
                _dsPhieuIndex = list
                    .Where(x => !string.IsNullOrEmpty(x.Find))
                    .GroupBy(x => x.Find)
                    .ToDictionary(g => g.Key, g => g.First());
                // GroupBy + First phòng trường hợp Find bị trùng trong view (không nên xảy ra,
                // nhưng tránh crash ToDictionary nếu dữ liệu thực tế có trùng).
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không tải được danh sách phiếu sản xuất!\n{ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);

                _dsPhieu = new BindingList<PhieuNhapInfo>();
                _dsPhieuIndex = new Dictionary<string, PhieuNhapInfo>();
                _gridPhieu.DataSource = _dsPhieu;
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }
        private void TxtQRCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            string qrCode = txtQRCode.Text.Trim();
            txtQRCode.Clear();
            if (string.IsNullOrEmpty(qrCode)) return;

            QRCodeInfo parsed;
            try
            {
                parsed = _stockService.ParseQr(qrCode.ToUpper());
                parsed.RawQr = qrCode.ToUpper();
            }
            catch (FormatException fex)
            {
                XtraMessageBox.Show($"QR Code không hợp lệ!\n{fex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!parsed.IsTongPhieu)
            {
                XtraMessageBox.Show("Vui lòng bắn tem TỔNG để nhập kho!",
                    "Sai loại tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var precheck = _nhapTpService.KiemTraTruocKhiNhap(parsed);
            if (!precheck.IsOK)
            {
                XtraMessageBox.Show(precheck.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var phieu = _stockTpRepo.TimPhieuTheoLotQR(parsed.RawLotNo, parsed.ItemCode);
            if (phieu == null)
            {
                XtraMessageBox.Show(
                    "Không tìm thấy phiếu sản xuất khớp với LOT này trong vNhapTP!\n" +
                    "Kiểm tra lại tem hoặc liên hệ QLSX.",
                    "Không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // ✅ THÊM: highlight ngay khi tìm thấy — áp dụng cho cả 2 chế độ
            HighlightMatchedRow(phieu);
            if (phieu.KetThucLot)
            {
                var confirmMoLai = XtraMessageBox.Show(
                    $"LOT [{phieu.LotNo}] đã được đánh dấu KẾT THÚC (đã nhập {phieu.SlDaNhap}/{phieu.SlSanXuat}).\n" +
                    "Bạn có muốn MỞ LẠI để tiếp tục nhập không?",
                    "LOT đã kết thúc",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (confirmMoLai != DialogResult.Yes) return;

                _stockTpRepo.MoLaiLot(phieu.LotNo,phieu.Find);
                // Không cần re-query phieu — status sẽ tự tính lại đúng trong NhapTpVaoSlot
                // dựa trên SlSanXuat/SlDaNhap thực tế, KetThucLot ở đây chỉ dùng để hỏi UI.
            }

            if (!string.Equals(phieu.MaSP, parsed.ItemCode, StringComparison.OrdinalIgnoreCase))
            {
                XtraMessageBox.Show(
                    $"Mã hàng không khớp!\nTem quét: {parsed.ItemCode}\nPhiếu SX: {phieu.MaSP}",
                    "Sai mã hàng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (phieu.SlDaNhap + parsed.Quantity > phieu.SlSanXuat)
            {
                var confirmVuot = XtraMessageBox.Show(
                    $"Tổng SL nhập ({phieu.SlDaNhap + parsed.Quantity}) sẽ vượt quá " +
                    $"SL sản xuất ({phieu.SlSanXuat}) của LOT [{phieu.LotNo}].\n" +
                    "Bạn có chắc chắn muốn tiếp tục?",
                    "Cảnh báo vượt sản lượng",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (confirmVuot != DialogResult.Yes) return;
            }

            var config = _stockService.GetInspectionConfig(parsed.ItemCode);
            if (config != null)
            {
                using (var formInspect = new FormInspection(parsed, config))
                {
                    var result = formInspect.ShowDialog(this);
                    if (result != DialogResult.OK)
                    {
                        XtraMessageBox.Show(
                            "Kiểm tra không đạt — Hàng không được nhập kho.",
                            "Từ chối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            // ── PHÂN NHÁNH: chế độ bulk -> lưu NGAY, không chờ OK ────────────────
            // Mỗi tem quét được xử lý độc lập, không ghi đè lên nhau, không cần
            // người dùng thao tác thêm -> đúng bản chất "nhập hàng loạt liên tục".
         
            if (chkNhapHangLoat.Checked)
            {
                SaveScanImmediately(parsed, phieu);
                return;
            }
            // ── Chế độ thường: giữ nguyên luồng cũ — hiển thị UI, chờ chọn Slot + OK ──
            _matchedPhieu = phieu;
            ShowQRCodeInfo(parsed);
        }

        /// <summary>
        /// Lưu 1 tem ngay khi quét xong — dùng riêng cho chế độ "Nhập hàng loạt".
        /// Không đụng đến codeInfo/_matchedPhieu (biến dùng cho chế độ thường có UI chọn Slot),
        /// tránh xung đột trạng thái giữa 2 luồng.
        /// </summary>
        private void SaveScanImmediately(QRCodeInfo qr, PhieuNhapInfo matchedPhieu)
        {
            string targetSlotText = _stockService.GetOrCreateBulkImportSlotText();
            var result = _nhapTpService.NhapTpVaoSlot(qr, targetSlotText, matchedPhieu);

            if (!result.IsOK)
            {
                lblBulkStatus.Text = $"❌ {result.Message}";
                lblBulkStatus.Appearance.ForeColor = Color.Red;
                return;
            }

            // ✅ THÊM: cập nhật SL đã nhập ngay trên grid
            UpdateSlDaNhapInGrid(matchedPhieu.Find, qr.Quantity);

            lblBulkStatus.Text = $"✅ {result.Message}";
            lblBulkStatus.Appearance.ForeColor = Color.Green;

            _mainStockForm?.OnSlotUpdated();
            txtQRCode.Focus();
        }

        private bool ValidateQRCode(string qrCode)
        {
            return !string.IsNullOrEmpty(qrCode);
        }

        private LabelControl CreateLabel()
        {
            return new LabelControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 10),
                Padding = new Padding(3),
                AutoSizeMode = LabelAutoSizeMode.Vertical, // BẮT BUỘC để hiển thị nhiều dòng
                Appearance =
                {
                    TextOptions = { WordWrap = DevExpress.Utils.WordWrap.Wrap }
                }
            };
        }

        private void ShowQRCodeInfo(QRCodeInfo qrInfo)
        {
            // Xóa layout cũ nếu có
            if (mainLayout != null)
            {
                Controls.Remove(mainLayout);
                mainLayout.Dispose();
                mainLayout = null;
            }

            if (qrInfo == null)
            {
                XtraMessageBox.Show("Không có dữ liệu QR.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Lưu lại để dùng khi Import
            codeInfo = qrInfo;

            int top = 10;

            // =========================
            // Layout chính
            // =========================
            contentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };

            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            // =========================
            // Group thông tin
            // =========================
            groupInfo = new GroupControl
            {
                Text = "Thông tin vị trí nhập",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(5)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            lblwhName = CreateLabel();
            lblrackName = CreateLabel();
            lblSlotNumber = CreateLabel();
            lblTemCode = CreateLabel();
            lblItemCode = CreateLabel();
            lblLotNo = CreateLabel();
            lblQty = CreateLabel();

            layout.Controls.Add(new LabelControl { Text = "WHName:" }, 0, 0);
            layout.Controls.Add(lblwhName, 1, 0);

            layout.Controls.Add(new LabelControl { Text = "Rack:" }, 0, 1);
            layout.Controls.Add(lblrackName, 1, 1);

            layout.Controls.Add(new LabelControl { Text = "Slot Number:" }, 0, 2);
            layout.Controls.Add(lblSlotNumber, 1, 2);

            layout.Controls.Add(new LabelControl { Text = "TemCode:" }, 0, 3);
            layout.Controls.Add(lblTemCode, 1, 3);

            layout.Controls.Add(new LabelControl { Text = "ItemCode:" }, 0, 4);
            layout.Controls.Add(lblItemCode, 1, 4);

            layout.Controls.Add(new LabelControl { Text = "LotNo:" }, 0, 5);
            layout.Controls.Add(lblLotNo, 1, 5);

            layout.Controls.Add(new LabelControl { Text = "Tồn kho:" }, 0, 6);
            layout.Controls.Add(lblQty, 1, 6);

            // ── THÊM: hiển thị thông tin phiếu SX đã đối chiếu (nếu có) ──────────
            if (_matchedPhieu != null)
            {
                var lblPhieu = CreateLabel();
                lblPhieu.Text =
                    $"LOT: {_matchedPhieu.LotNo}  |  SP: {_matchedPhieu.TenSP}  |  " +
                    $"Đã nhập: {_matchedPhieu.SlDaNhap}/{_matchedPhieu.SlSanXuat}";
                lblPhieu.Appearance.Font = new Font("Tahoma", 9, FontStyle.Italic);
                lblPhieu.Appearance.ForeColor = Color.DarkSlateGray;

                layout.Controls.Add(new LabelControl { Text = "Phiếu SX:" }, 0, 7);
                layout.Controls.Add(lblPhieu, 1, 7);
            }

            LoadSlotData(codeInfo);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            scrollPanel.Controls.Add(layout);
            groupInfo.Controls.Add(scrollPanel);

            // =========================
            // Danh sách Slot
            // =========================
            groupSlotList = new GroupControl
            {
                Text = "Danh sách vị trí trống phù hợp",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            listBoxSlots = new ListBoxControl
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true,
                SelectionMode = SelectionMode.One
            };

            listBoxSlots.SelectedIndexChanged += (s, e) =>
            {
                if (listBoxSlots.SelectedItem == null)
                    return;

                try
                {
                    StockService.ParseSlotString(
                        listBoxSlots.SelectedItem.ToString(),
                        out string targetWh,
                        out string targetRack,
                        out int targetSlot,
                        out int capacity);

                    lblwhName.Text = targetWh;
                    lblrackName.Text = targetRack;
                    lblSlotNumber.Text = targetSlot.ToString();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            };

            // ── THAY ĐỔI: nếu đang ở chế độ "Nhập hàng loạt" thì bỏ qua hoàn toàn
            // bước build danh sách Slot trống — luôn nhập vào Slot ảo cố định ────
            if (chkNhapHangLoat.Checked)
            {
                groupSlotList.Visible = false;

                lblwhName.Text = BulkImportConfig.WarehouseName;
                lblrackName.Text = BulkImportConfig.RackName;
                lblSlotNumber.Text = "(Slot ảo — gom hàng tạm)";
            }
            else
            {
                // Danh sách slot trống/phù hợp cho ItemCode + số lượng cần nhập
                var emptySlots = _stockService.GetAvailableSlotsForImport(
                    codeInfo.ItemCode,
                    codeInfo.Quantity);

                if (emptySlots.Count == 0)
                {
                    XtraMessageBox.Show(
                        "Không còn Slot trống nào phù hợp trong kho này!",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                foreach (var slot in emptySlots)
                {
                    listBoxSlots.Items.Add(slot);
                }

                groupSlotList.Controls.Add(listBoxSlots);
            }

            // =========================
            // Button
            // =========================
            btnOK = new SimpleButton
            {
                Text = "OK",
                Size = new Size(80, 30),
                Top = top,
                Left = 50
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new SimpleButton
            {
                Text = "Cancel",
                Size = new Size(80, 30),
                Top = top,
                Left = 150
            };
            btnCancel.Click += BtnCancel_Click;

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10)
            };

            bottomPanel.Controls.Add(btnCancel);
            bottomPanel.Controls.Add(btnOK);

            // =========================
            // Main Layout
            // =========================
            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            // ── THAY ĐỔI: nếu đang bulk mode, groupSlotList ẩn -> nới rộng groupInfo ─
            if (chkNhapHangLoat.Checked)
            {
                contentPanel.RowStyles[0] = new RowStyle(SizeType.Percent, 100);
                contentPanel.RowStyles[1] = new RowStyle(SizeType.Percent, 0);
            }

            contentPanel.Controls.Add(groupInfo, 0, 0);
            contentPanel.Controls.Add(groupSlotList, 0, 1);

            mainLayout.Controls.Add(contentPanel, 0, 0);
            mainLayout.Controls.Add(bottomPanel, 0, 1);

            Controls.Add(mainLayout);
            mainLayout.BringToFront();
        }

        private void LoadSlotData(QRCodeInfo qrCodeInfo)
        {
            if (qrCodeInfo != null)
            {
                lblTemCode.Text = qrCodeInfo.WarehouseCode + qrCodeInfo.Unit;
                lblItemCode.Text = qrCodeInfo.ItemCode;
                lblLotNo.Text = qrCodeInfo.LotNo;
                lblQty.Text = qrCodeInfo.Quantity.ToString();
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            string targetSlotText;

            // ── THAY ĐỔI: chọn Slot đích tuỳ theo chế độ ─────────────────────────
            if (chkNhapHangLoat.Checked)
            {
                // Nhập hàng loạt -> luôn dùng Slot ảo cố định, tự tạo nếu chưa có
                targetSlotText = _stockService.GetOrCreateBulkImportSlotText();
            }
            else
            {
                if (listBoxSlots.SelectedItem == null)
                {
                    XtraMessageBox.Show("Vui lòng chọn một Slot!", "Chú ý",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                targetSlotText = listBoxSlots.SelectedItem.ToString();
            }

            // ── THAY ĐỔI: dùng NhapTpReceivingService thay vì StockService.ImportLotToSlot
            // -> ghi đồng thời STOCKTP + SlotLot + Slot trong 1 transaction, có check
            // trùng case (NHAP_TP_HIS), check sức chứa Slot, và dùng đúng LOT/tên SP/
            // SLSX từ phiếu vNhapTP đã đối chiếu (_matchedPhieu) nếu có. ─────────────
            var result = _nhapTpService.NhapTpVaoSlot(codeInfo, targetSlotText, _matchedPhieu);

            if (!result.IsOK)
            {
                XtraMessageBox.Show(result.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // ✅ THÊM: cập nhật grid cho chế độ thường
            if (_matchedPhieu != null)
                UpdateSlDaNhapInGrid(_matchedPhieu.Find, codeInfo.Quantity);
            XtraMessageBox.Show(result.Message, "Thành công",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Báo cho form cha vẽ lại Canvas trung tâm từ CSDL mới cập nhật
            _mainStockForm?.OnSlotUpdated();
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (mainLayout != null)
            {
                Controls.Remove(mainLayout);
                mainLayout.Dispose();
                mainLayout = null;
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // ✅ BẮT BUỘC — tránh memory leak vì bus là static/singleton,
            // nếu không Unsubscribe form sẽ không bao giờ bị GC.
            AppEventBus.Instance.Unsubscribe<LotStatusResetEvent>(OnLotStatusReset);
            base.OnFormClosed(e);
        }
    }
}