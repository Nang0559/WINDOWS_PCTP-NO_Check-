using DevExpress.CodeParser;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Domain.Events;
using PCTP.Infrastructure;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.NhapKho;
using PCTP.Modules.NhapKho.Services;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
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
        
        private readonly MainStockSV _mainStockForm;

        // ★ CHỈ CÒN 4 SERVICE — không còn field Repository nào trong Form
        private readonly INhapTpReceivingService _nhapTpService;
        private readonly ISlotService _slotService;
        private readonly IWarehouseService _warehouseService;
        private readonly IInspectionService _inspectionService;

        // Thêm fields trong class FormEnterItemSV
        private GridControl _gridPhieu;
        private GridView _gridViewPhieu;
        // ★ Nguồn dữ liệu DUY NHẤT của grid — dùng chung với FormNhapKhoTienTrinh (không tự query lại).
        private readonly ReceivingWorklist _worklist;
        private readonly bool _ownsWorklist;
        // LOT đã được người dùng xác nhận "mở lại" nhưng CHƯA lưu — chỉ áp dụng ngay trước khi lưu.
        private PhieuNhapInfo _pendingReopen;
        private LabelControl lblBulkStatus;
        private SimpleButton btnVeTienTrinh;

        /// <summary>Mở độc lập (từ MainStockSV): tự tạo + nạp worklist riêng.</summary>
        public FormEnterItemSV(MainStockSV mainStockForm) : this(mainStockForm, null) { }

        /// <summary>
        /// Mở form quét nhập ĐỘC LẬP (từ bản đồ kho / nút Nhập hàng) và khi đóng thì tự quay về
        /// màn hình <see cref="FormNhapKhoTienTrinh"/>. Khi form này được mở TỪ FormNhapKhoTienTrinh
        /// (constructor có worklist) thì KHÔNG dùng hàm này — đóng form là tự về lại form cha,
        /// tránh mở lồng 2 form tiến trình.
        /// </summary>
        public static void ShowThenReturnToProgress(MainStockSV mainStock, IWin32Window owner)
        {
            using (var f = new FormEnterItemSV(mainStock))
                f.ShowDialog(owner);

            if (mainStock == null || mainStock.IsDisposed) return;
            global::PCTP.Common.WarehouseProcessNavigator.OpenNhapKhoTienTrinh(owner ?? mainStock, mainStock);
        }

        /// <summary>
        /// Mở từ FormNhapKhoTienTrinh: nhận worklist ĐÃ NẠP của form cha, không nạp lại.
        /// </summary>
        public FormEnterItemSV(MainStockSV mainStockForm, ReceivingWorklist worklist)
        {
            InitializeComponent();
            _ownsWorklist = worklist == null;
            _worklist = worklist;
          
            _mainStockForm = mainStockForm;

            // ★ Toàn bộ wiring nằm ở Factory — Form không biết Repository nào tồn tại
            var module = NhapTpModuleFactory.Build();
            _nhapTpService = module.NhapTpService;
            _slotService = module.SlotService;
            _warehouseService = module.WarehouseService;
            _inspectionService = module.InspectionService;
            if (_worklist == null)
                _worklist = new ReceivingWorklist(() => _nhapTpService.GetPhieuChoNhap());
            InitializeForm();
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
                RowCount = 4,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // nút về màn hình Tiến trình
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panelTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

            chkNhapHangLoat = new CheckEdit
            {
                Name = "receive_bulkImport",
                Text = "Nhập hàng loạt (không cần chọn Slot — vào kho tạm)",
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 10, FontStyle.Bold),
                Checked = true
            };
            chkNhapHangLoat.Properties.Appearance.ForeColor = Color.DarkOrange;
            chkNhapHangLoat.Properties.Appearance.Options.UseForeColor = true;

            txtQRCode = new TextBox
            {
                Name = "receive_qrInput",
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 12)
            };
            txtQRCode.KeyDown += TxtQRCode_KeyDown;

            lblBulkStatus = new LabelControl
            {
                Name = "receive_bulkStatus",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold) }
            };

            btnVeTienTrinh = new SimpleButton
            {
                Name = "receive_btnBackToProgress",
                Text = "◀ Về màn hình Tiến trình nhập kho",
                Width = 260,
                Height = 30,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            btnVeTienTrinh.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            // Đóng form: mở độc lập → ShowThenReturnToProgress tự mở FormNhapKhoTienTrinh;
            // mở từ FormNhapKhoTienTrinh → ShowDialog trả quyền điều khiển về form cha.
            btnVeTienTrinh.Click += (s, e) => Close();

            panelTop.Controls.Add(btnVeTienTrinh, 0, 0);
            panelTop.Controls.Add(chkNhapHangLoat, 0, 1);
            panelTop.Controls.Add(txtQRCode, 0, 2);
            panelTop.Controls.Add(lblBulkStatus, 0, 3);

            // ── Grid phiếu chiếm phần còn lại ────────────────────────────────────
            _gridPhieu = new GridControl { Dock = DockStyle.Fill };
            _gridViewPhieu = new GridView(_gridPhieu);
            _gridPhieu.MainView = _gridViewPhieu;
            // Chỉ riêng cột "Kết Thúc" được tương tác (bỏ tick = yêu cầu Mở lại LOT); các cột khác read-only.
            _gridViewPhieu.OptionsBehavior.Editable = true;
            _gridViewPhieu.ShowingEditor += GridViewPhieu_ShowingEditor;
            _gridViewPhieu.OptionsView.ShowGroupPanel = false;
            _gridViewPhieu.RowStyle += GridViewPhieu_RowStyle;

            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "Find", Caption = "FIND", Width = 180, VisibleIndex = 0 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "LotNo", Caption = "LotNo", Width = 150, VisibleIndex = 1 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "Model", Caption = "Model", Width = 80, VisibleIndex = 2 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "MaSP", Caption = "Mã SP", Width = 130, VisibleIndex = 3 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "TenSP", Caption = "Tên SP", Width = 180, VisibleIndex = 4 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "CaSX", Caption = "Ca SX", Width = 60, VisibleIndex = 5 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "NgaySX", Caption = "Ngày SX", Width = 90, VisibleIndex = 6 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "SlSanXuat", Caption = "SL Sản Xuất", Width = 90, VisibleIndex = 7 });
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "SlDaNhap", Caption = "SL Đã Nhập", Width = 90, VisibleIndex = 8 });
            var chkKetThuc = new DevExpress.XtraEditors.Repository.RepositoryItemCheckEdit();
            chkKetThuc.EditValueChanging += KetThucLot_EditValueChanging;
            _gridViewPhieu.Columns.Add(new GridColumn { FieldName = "KetThucLot", Caption = "Kết Thúc", Width = 70, VisibleIndex = 9, ColumnEdit = chkKetThuc });
            foreach (GridColumn c in _gridViewPhieu.Columns)
                c.OptionsColumn.AllowEdit = c.FieldName == "KetThucLot";

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

            // Sự kiện có thể bắn từ thread khác — grid chỉ được đụng ở UI thread.
            if (InvokeRequired) { BeginInvoke(new Action(() => OnLotStatusReset(e))); return; }

            PhieuNhapInfo target = null;
            if (!string.IsNullOrEmpty(e.Find))
                _worklist.TryGet(e.Find, out target);
            if (target == null)
                target = _worklist.FindByLot(e.Lot);
            if (target == null) return;

            var latest = _nhapTpService.GetPhieuByFind(target.Find);
            if (latest == null) return;

            _worklist.Sync(latest);

            if (_matchedPhieu != null &&
                string.Equals(_matchedPhieu.Find, target.Find, StringComparison.OrdinalIgnoreCase))
            {
                _matchedPhieu.KetThucLot = latest.KetThucLot;
                _matchedPhieu.SlDaNhap = latest.SlDaNhap;
                _matchedPhieu.SlSanXuat = latest.SlSanXuat;
            }
        }


        /// <summary>Chỉ mở editor ở ô "Kết Thúc" của dòng đang bị khoá (đang tick) — các ô khác/dòng khác không sửa được.</summary>
        private void GridViewPhieu_ShowingEditor(object sender, CancelEventArgs e)
        {
            var view = (GridView)sender;
            var row = view.GetFocusedRow() as PhieuNhapInfo;
            e.Cancel = row == null
                       || view.FocusedColumn == null
                       || view.FocusedColumn.FieldName != "KetThucLot"
                       || !row.KetThucLot;
        }

        /// <summary>
        /// Bỏ tick "Kết Thúc" = người dùng yêu cầu MỞ LẠI LOT. Chỉ ghi nhận ý định vào _pendingReopen;
        /// mutation DB vẫn nằm trong transaction của NhapTpVaoSlot (xem NhapTpReceivingService.MoLaiLot).
        /// </summary>
        private void KetThucLot_EditValueChanging(object sender, DevExpress.XtraEditors.Controls.ChangingEventArgs e)
        {
            var row = _gridViewPhieu.GetFocusedRow() as PhieuNhapInfo;
            bool newValue = e.NewValue is bool b && b;

            // Chỉ cho bỏ tick; không cho tự đánh dấu kết thúc từ ô này.
            if (row == null || newValue || !row.KetThucLot) { e.Cancel = true; return; }

            var confirm = XtraMessageBox.Show(
                $"MỞ LẠI LOT [{row.LotNo}] (đã nhập {row.SlDaNhap}/{row.SlSanXuat}) để tiếp tục nhập kho?",
                "Mở lại LOT", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) { e.Cancel = true; return; }

            _pendingReopen = row;
            lblBulkStatus.Text = $"🔓 Đã chọn mở lại LOT {row.LotNo} — quét tem TỔNG của LOT này để nhập tiếp.";
            lblBulkStatus.Appearance.ForeColor = Color.DarkOrange;
            txtQRCode.Focus();
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
            else if (_worklist.IsTouched(row.Find))
            {
                // Vừa bắn trong phiên làm việc này — tô nổi bật để dễ theo dõi
                e.Appearance.BackColor = Color.FromArgb(255, 230, 150); // cam nhạt
                e.Appearance.Font = new Font("Tahoma", 9, FontStyle.Bold);
            }
            // else: giữ màu mặc định — chưa động tới
        }
        /// <summary>
        /// Định vị + focus dòng trong grid theo Find (feedback tức thì khi bắn tem).
        /// KHÔNG đánh dấu "đã nhập" ở đây — việc đó chỉ làm sau khi lưu thành công
        /// (<see cref="ReceivingWorklist.MarkReceived"/>), tránh tô nhầm khi tem bị từ chối.
        /// </summary>
        private void FocusRow(PhieuNhapInfo phieu)
        {
            if (phieu == null || string.IsNullOrEmpty(phieu.Find)) return;

            int rowHandle = _gridViewPhieu.LocateByValue("Find", phieu.Find);
            if (rowHandle >= 0)
            {
                _gridViewPhieu.FocusedRowHandle = rowHandle;
                _gridViewPhieu.MakeRowVisible(rowHandle);
            }
        }

        /// <summary>Sau khi lưu thành công: cộng SL + đánh dấu dòng trong worklist dùng chung.</summary>
        private void UpdateSlDaNhapInGrid(string find, int slVuaNhap)
        {
            _worklist.MarkReceived(find, slVuaNhap);
            FocusRow(new PhieuNhapInfo { Find = find });
        }

        /// <summary>
        /// Grid chỉ BIND vào worklist. Chỉ nạp DB khi worklist chưa có dữ liệu
        /// (mở độc lập); khi được form cha truyền sang thì dùng nguyên dữ liệu đã nạp.
        /// </summary>
        private void LoadDanhSachPhieu()
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                if (_ownsWorklist)
                    _worklist.Reload();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không tải được danh sách phiếu sản xuất!\n{ex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _gridPhieu.DataSource = _worklist.Items;
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
                parsed = QRCodeParser.ParseQRCode(qrCode.ToUpper());
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

            // ★ đổi từ _productionRepo.TimPhieuTheoLotQR sang _nhapTpService
            var phieu = _nhapTpService.TimPhieuTheoLotQR(parsed.RawLotNo, parsed.ItemCode);
            if (phieu == null)
            {
                XtraMessageBox.Show(
                    "Không tìm thấy phiếu sản xuất khớp với LOT này trong vNhapTP!\n" +
                    "Kiểm tra lại tem hoặc liên hệ QLSX.",
                    "Không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // ★ RULE: tem phải thuộc danh sách chờ nhập đang hiển thị trên grid.
            // Không tự thêm dòng ngoài danh sách (hành vi cũ) — chỉ cho nhập dòng có sẵn.
            if (!_worklist.TryGet(phieu.Find, out _))
            {
                XtraMessageBox.Show(
                    $"LOT [{phieu.LotNo}] không nằm trong danh sách phiếu CHỜ NHẬP.\n" +
                    "Có thể LOT đã kết thúc / đã nhập đủ, hoặc danh sách chưa cập nhật.\n" +
                    "Hãy bấm Làm mới ở màn hình tiến trình rồi quét lại.",
                    "Không có trong danh sách chờ nhập",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Đưa số liệu mới nhất từ DB vào dòng grid (tránh cảnh báo vượt SL dựa trên số cũ).
            _worklist.Sync(phieu);
            FocusRow(phieu);

            bool daDuyetMoLaiTuGrid = _pendingReopen != null &&
                string.Equals(_pendingReopen.Find, phieu.Find, StringComparison.OrdinalIgnoreCase);
            _pendingReopen = null;
            if (phieu.KetThucLot && daDuyetMoLaiTuGrid)
            {
                _pendingReopen = phieu;   // đã bỏ tick ở grid → không hỏi lại
            }
            else if (phieu.KetThucLot)
            {
                var confirmMoLai = XtraMessageBox.Show(
                    $"LOT [{phieu.LotNo}] đã được đánh dấu KẾT THÚC (đã nhập {phieu.SlDaNhap}/{phieu.SlSanXuat}).\n" +
                    "Bạn có muốn MỞ LẠI để tiếp tục nhập không?",
                    "LOT đã kết thúc",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (confirmMoLai != DialogResult.Yes) return;

                // CHỈ ghi nhận ý định mở lại; MoLaiLot() thật sự gọi ngay trước khi lưu
                // (nếu gọi ở đây mà các bước sau bị huỷ thì approval bị "treo" trong service).
                _pendingReopen = phieu;
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

            var config = _warehouseService.GetInspectionConfig(parsed.ItemCode);
            if (config != null)
            {
                using (var formInspect = new FormInspection(parsed, config, _inspectionService))
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

            if (chkNhapHangLoat.Checked)
            {
                ApplyPendingReopen();
                SaveScanImmediately(parsed, phieu);
                return;
            }

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
            // ★ đổi từ _stockService.GetOrCreateBulkImportSlotText sang _slotService
            string slotText = _slotService.GetOrCreateVirtualSlotText(
                BulkImportConfig.WarehouseName,
                BulkImportConfig.RackName,
                BulkImportConfig.Capacity);

            int slotId = ResolveSlotId(slotText);
            if (slotId <= 0)
            {
                lblBulkStatus.Text = "❌ Không xác định được Slot ảo gom hàng.";
                lblBulkStatus.Appearance.ForeColor = Color.Red;
                return;
            }

            var result = _nhapTpService.NhapTpVaoSlot(qr, slotId, matchedPhieu); // ★ int, không còn string

            if (!result.IsOK)
            {
                lblBulkStatus.Text = $"❌ {result.Message}";
                lblBulkStatus.Appearance.ForeColor = Color.Red;
                return;
            }

            UpdateSlDaNhapInGrid(matchedPhieu.Find, qr.Quantity);

            lblBulkStatus.Text = $"✅ {result.Message}";
            lblBulkStatus.Appearance.ForeColor = Color.Green;

            _mainStockForm?.OnSlotUpdated();
            txtQRCode.Focus();
        }

        private void ApplyPendingReopen()
        {
            if (_pendingReopen == null) return;
            _nhapTpService.MoLaiLot(_pendingReopen.LotNo, _pendingReopen.Find);
            _pendingReopen = null;
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
                if (listBoxSlots.SelectedItem == null) return;

                try
                {
                    var info = _slotService.GetSlotInfoFromString(listBoxSlots.SelectedItem.ToString());
                    lblwhName.Text = info.WarehouseName;
                    lblrackName.Text = info.RackName;
                    lblSlotNumber.Text = info.SlotNumber.ToString();
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
                var emptySlots = _slotService.GetEmptySlots(
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
                Name = "receive_btnConfirm",
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
                targetSlotText = _slotService.GetOrCreateVirtualSlotText(
               BulkImportConfig.WarehouseName,
               BulkImportConfig.RackName,
               BulkImportConfig.Capacity);
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

            int slotId = ResolveSlotId(targetSlotText);
            if (slotId <= 0)
            {
                XtraMessageBox.Show("Không xác định được Slot đích.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            ApplyPendingReopen();
            var result = _nhapTpService.NhapTpVaoSlot(codeInfo, slotId, _matchedPhieu);

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
            _pendingReopen = null;
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
        // ── Helper resolve string slot -> id, chỗ DUY NHẤT trong Form ────────
        private int ResolveSlotId(string slotText)
        {
            if (string.IsNullOrWhiteSpace(slotText)) return -1;
            return _slotService.GetSlotIdFromString(slotText);
        }
    }
}