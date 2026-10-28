using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
    using DevExpress.XtraGrid;
    using DevExpress.XtraGrid.Views.Grid;
    using PCTP.Modules.KhoVatLy.Kho.Models;
    using PCTP.Modules.XuLyHangLoi.Enums;
    using PCTP.Modules.XuLyHangLoi.Services;
    using PCTP.Modules.XuLyHangLoi.Repository;
    using PCTP.Modules.XuLyHangLoi.Models;
    using PCTP.Shared.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;

    public partial class FormReworkProcess : XtraForm
    {
        // ============================================================
        // SERVICES
        // ============================================================
        private readonly IQTChungService _qtChungService;
        private readonly IReworkStockService _reworkStockService;
        private readonly int _phieuXuLyId;
        private Guid? _pendingReworkExportOperationId;
        private readonly INoiBoLookupRepository _noiBoLookupRepo;
        private readonly ITraHangQTChungRepository _qtChungRepo;

        // ============================================================
        // UI
        // ============================================================
        private LabelControl _lblPhieu;
        private LabelControl _lblTrangThai;
        private LabelControl _lblHuongXuLy;

        private TextEdit _txtMaHang;
        private TextEdit _txtLotNo;
        private SimpleButton _btnLoadLot;
        private SimpleButton _btnXuatKho;
        private SimpleButton _btnGiaoSX;
        private SimpleButton _btnDangRework;
        private SimpleButton _btnRefresh;
        private SimpleButton _btnDong;

        private GridControl _grid;
        private GridView _gridView;

        private MemoEdit _txtGhiChu;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public FormReworkProcess()
        {
            BuildUI();
        }

        public FormReworkProcess(
            IQTChungService qtChungService,
            IReworkStockService reworkStockService,
            int phieuXuLyId,
            INoiBoLookupRepository noiBoLookupRepo = null,
            ITraHangQTChungRepository qtChungRepo = null)
        {
            _qtChungService = qtChungService
                ?? throw new ArgumentNullException(nameof(qtChungService));
            _reworkStockService = reworkStockService
                ?? throw new ArgumentNullException(nameof(reworkStockService));
            _phieuXuLyId = phieuXuLyId;
            _noiBoLookupRepo = noiBoLookupRepo;
            _qtChungRepo = qtChungRepo;

            BuildUI();
            LoadPhieu();
        }

        // ============================================================
        // UI
        // ============================================================
        private void BuildUI()
        {
            Text = "Quản lý Rework";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1200, 720);
            MinimumSize = new Size(1000, 620);

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                Padding = new Padding(8)
            };

            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            // ========================================================
            // HEADER
            // ========================================================
            var pnlHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            _lblPhieu = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 12F, FontStyle.Bold),
                    ForeColor = Color.DarkBlue
                }
            };
            _lblTrangThai = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 10F, FontStyle.Bold),
                    ForeColor = Color.DarkGreen
                }
            };
            _lblHuongXuLy = new LabelControl
            {
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 10F, FontStyle.Bold),
                    ForeColor = Color.DarkOrange
                }
            };

            pnlHeader.Controls.Add(_lblPhieu, 0, 0);
            pnlHeader.Controls.Add(_lblTrangThai, 1, 0);
            pnlHeader.Controls.Add(_lblHuongXuLy, 2, 0);
            main.Controls.Add(pnlHeader, 0, 0);

            // ========================================================
            // SEARCH LOT
            // ========================================================
            var pnlSearch = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2
            };
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            pnlSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            var lblMaHang = new LabelControl
            {
                Text = "Mã hàng:",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Bold) }
            };
            _txtMaHang = new TextEdit { Dock = DockStyle.Fill };

            var lblLot = new LabelControl
            {
                Text = "LOT:",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Bold) }
            };
            _txtLotNo = new TextEdit { Dock = DockStyle.Fill };

            _btnLoadLot = new SimpleButton { Text = "🔍 Tìm LOT", Dock = DockStyle.Fill };
            _btnRefresh = new SimpleButton { Text = "⟳ Làm mới", Dock = DockStyle.Fill };
            _btnLoadLot.Click += BtnLoadLot_Click;
            _btnRefresh.Click += (s, e) => LoadLots();

            pnlSearch.Controls.Add(lblMaHang, 0, 0);
            pnlSearch.Controls.Add(_txtMaHang, 1, 0);
            pnlSearch.Controls.Add(lblLot, 2, 0);
            pnlSearch.Controls.Add(_txtLotNo, 3, 0);
            pnlSearch.Controls.Add(_btnLoadLot, 4, 0);
            pnlSearch.Controls.Add(_btnRefresh, 5, 0);

            var lblHuongDan = new LabelControl
            {
                Text = "Rework / không Rework được QC quyết định tại bước 4. Bước 6 chỉ thao tác với phiếu đã có hướng CanRework; chỉ xuất đúng LOT tồn khả dụng.",
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 8.5F, FontStyle.Italic),
                    ForeColor = Color.DimGray
                }
            };
            pnlSearch.Controls.Add(lblHuongDan, 0, 1);
            pnlSearch.SetColumnSpan(lblHuongDan, 6);

            main.Controls.Add(pnlSearch, 0, 1);

            // ========================================================
            // ACTION BAR
            // ========================================================
            var pnlAction = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4
            };
            pnlAction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            pnlAction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            pnlAction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            pnlAction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            _btnXuatKho = new SimpleButton { Text = "📦 Xuất kho Rework", Dock = DockStyle.Fill };
            _btnGiaoSX = new SimpleButton { Text = "🚚 Giao cho SX", Dock = DockStyle.Fill };
            _btnDangRework = new SimpleButton { Text = "Ghi nhận tiến độ Rework", Dock = DockStyle.Fill };

            var lblAction = new LabelControl
            {
                Text = "Chọn LOT phía dưới trước khi thao tác.",
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 8.5F, FontStyle.Italic),
                    ForeColor = Color.DimGray
                }
            };

            _btnXuatKho.Click += BtnXuatKho_Click;
            _btnGiaoSX.Click += BtnGiaoSX_Click;
            _btnDangRework.Click += BtnDangRework_Click;

            pnlAction.Controls.Add(_btnXuatKho, 0, 0);
            pnlAction.Controls.Add(_btnGiaoSX, 1, 0);
            pnlAction.Controls.Add(_btnDangRework, 2, 0);
            pnlAction.Controls.Add(lblAction, 3, 0);

            main.Controls.Add(pnlAction, 0, 2);

            // ========================================================
            // GRID + NOTE
            // ========================================================
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));

            // --------------------------------------------------------
            // GRID
            // --------------------------------------------------------
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.RowAutoHeight = true;
            _gridView.OptionsSelection.MultiSelect = false;
            _gridView.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;
            _gridView.FocusedRowChanged += (s, e) => UpdateActionState();

            content.Controls.Add(_grid, 0, 0);

            // --------------------------------------------------------
            // NOTE
            // --------------------------------------------------------
            var pnlNote = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            pnlNote.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            pnlNote.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var lblNote = new LabelControl
            {
                Text = "Ghi chú:",
                Dock = DockStyle.Fill,
                Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Bold) }
            };
            _txtGhiChu = new MemoEdit { Dock = DockStyle.Fill };

            pnlNote.Controls.Add(lblNote, 0, 0);
            pnlNote.Controls.Add(_txtGhiChu, 1, 0);

            content.Controls.Add(pnlNote, 0, 1);
            main.Controls.Add(content, 0, 3);

            // ========================================================
            // FOOTER
            // ========================================================
            var pnlFooter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

            var lblFooter = new LabelControl
            {
                Text = "💡 Sau khi giao cho sản xuất, QC sẽ thực hiện xác nhận cuối tại bước QC.",
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 8.5F, FontStyle.Italic),
                    ForeColor = Color.DimGray
                }
            };

            _btnDong = new SimpleButton { Text = "Đóng", Dock = DockStyle.Fill };
            _btnDong.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            pnlFooter.Controls.Add(lblFooter, 0, 0);
            pnlFooter.Controls.Add(_btnDong, 1, 0);

            main.Controls.Add(pnlFooter, 0, 4);

            Controls.Add(main);
        }

        // ============================================================
        // LOAD PHIEU
        // ============================================================
        private void LoadPhieu()
        {
            try
            {
                var phieu = _qtChungService.GetById(_phieuXuLyId);
                if (phieu == null)
                {
                    XtraMessageBox.Show(
                        $"Không tìm thấy phiếu xử lý bất thường ID = {_phieuXuLyId}.",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    Close();
                    return;
                }

                // ── ĐÃ SỬA ──────────────────────────────────────────────
                // phieu đã là PhieuXuLyBatThuong cụ thể (không phải object
                // vô danh) — đọc trực tiếp property thay vì qua reflection
                // GetPropertyString, tránh sai tên field mà compiler không
                // bắt được.
                _lblPhieu.Text = $"Phiếu xử lý: {phieu.SoPhieu}";

                // ── ĐÃ SỬA ──────────────────────────────────────────────
                // IQTChungService.GetTrangThai(int) đã bị bỏ khỏi
                // QTChungService (method "14. GET STATUS" bị comment out
                // trong service thật) — gọi lại sẽ lỗi biên dịch.
                // Trạng thái nay lấy trực tiếp từ PhieuXuLyBatThuong.Status.
                var status = phieu.Status;
                _lblTrangThai.Text = $"Trạng thái: {status}";

                var huong = phieu.HuongXuLy;
                _lblHuongXuLy.Text = $"Hướng: {huong}";

                // Nạp thông tin từ đúng phiếu đang mở. Trước đây hai ô tìm kiếm
                // luôn để trống nên người dùng không biết form đang xử lý mã hàng/
                // LOT nào; danh sách LOT cũng không được lọc theo phiếu.
                _txtMaHang.Text = (phieu.MaSanPham ?? string.Empty).Trim();
                _txtLotNo.Text = (phieu.SoLoLoi ?? phieu.SoLo ?? string.Empty).Trim();

                // ----------------------------------------------------
                // REWORK CHỈ ĐƯỢC PHÉP CHO CanRework.
                // Quyết định "Rework / không Rework" thuộc bước 4 - QC định hướng.
                // Form này chỉ thực hiện xuất kho/giao SX/ghi nhận Rework sau khi
                // QC đã chọn CanRework; không tự đổi hướng xử lý tại bước 6.
                // ----------------------------------------------------
                if (huong != HuongXuLyBatThuong.CanRework)
                {
                    XtraMessageBox.Show(
                        "Phiếu này không có hướng xử lý CanRework.",
                        "Không thể Rework",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _btnXuatKho.Enabled = false;
                    _btnGiaoSX.Enabled = false;
                    _btnDangRework.Enabled = false;
                    return;
                }

                LoadLots();
            }
            catch (Exception ex)
            {
                ShowError("Không thể tải phiếu xử lý.", ex);
            }
        }

        // ============================================================
        // LOAD LOT
        // ============================================================
        private void LoadLots()
        {
            try
            {
                var phieuHienTai = _qtChungService.GetById(_phieuXuLyId);
                if (phieuHienTai != null && phieuHienTai.Status == QTChungStatus.DaGiaoSanXuat)
                {
                    if (_qtChungRepo == null)
                        throw new InvalidOperationException("Chưa cấu hình repository để tải lịch sử giao Rework.");

                    var deliveries = _qtChungRepo.GetGiao(_phieuXuLyId) ?? new List<TraHangQTChungGiao>();
                    _grid.DataSource = deliveries;
                    BuildDeliveryColumns();
                    _lblTrangThai.Text = $"Trạng thái: {phieuHienTai.Status} | Đã giao {deliveries.Sum(x => x.SoLuongGiao):n0} sản phẩm";
                    _lblTrangThai.Appearance.ForeColor = Color.DarkGreen;
                    _lblHuongXuLy.Text = $"Hướng: {phieuHienTai.HuongXuLy}";
                    UpdateActionState();
                    return;
                }

                string maHang = _txtMaHang.Text.Trim();
                string lotNo = _txtLotNo.Text.Trim();

                List<LotInfo> lots;
                if (!string.IsNullOrWhiteSpace(maHang) ||
                    !string.IsNullOrWhiteSpace(lotNo))
                {
                    lots = _reworkStockService.GetLotsCanRework(maHang, lotNo);
                }
                else
                {
                    lots = _reworkStockService.GetLotsCanReworkByPhieuXuLy(_phieuXuLyId);
                }

                _grid.DataSource = lots;
                BuildLotColumns();
                UpdateActionState();

                if (lots == null || lots.Count == 0)
                {
                    string lot = string.IsNullOrWhiteSpace(_txtLotNo.Text)
                        ? "(chưa có LOT)"
                        : _txtLotNo.Text.Trim();

                    // Giữ nguyên hướng xử lý; không tự thay bằng LOT khác
                    // khi LOT nguồn không có tồn khả dụng.
                    var phieu = _qtChungService.GetById(_phieuXuLyId);
                    _lblTrangThai.Text = "Trạng thái: " +
                        (phieu == null ? "Không xác định" : phieu.Status.ToString()) +
                        " | Không có tồn khả dụng";
                    _lblTrangThai.Appearance.ForeColor = Color.DarkOrange;
                    _lblHuongXuLy.Text = "Hướng: CanRework | LOT nguồn: " + lot;
                }
            }
            catch (Exception ex)
            {
                ShowError("Không thể lấy danh sách LOT có thể Rework.", ex);
            }
        }

        private List<LotInfo> GetLotsDaXuatChuaGiao()
        {
            if (_qtChungRepo == null)
                throw new InvalidOperationException("Chưa cấu hình repository lịch sử xuất Rework.");

            var exports = (_qtChungRepo.GetXuat(_phieuXuLyId) ?? new List<TraHangQTChungXuat>())
                .Where(x => x != null &&
                    string.Equals(x.LoaiXuat, "Rework", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(x.LotXuat) && x.SoLuongXuat > 0)
                .GroupBy(x => x.LotXuat.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Lot = g.Key,
                    ItemCode = g.Select(x => x.MaHang).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? _txtMaHang.Text.Trim(),
                    Exported = g.Sum(x => x.SoLuongXuat)
                })
                .ToList();

            var delivered = (_qtChungRepo.GetGiao(_phieuXuLyId) ?? new List<TraHangQTChungGiao>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.LotGiao))
                .GroupBy(x => x.LotGiao.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.SoLuongGiao)), StringComparer.OrdinalIgnoreCase);

            return exports.Select(x =>
            {
                int alreadyDelivered;
                delivered.TryGetValue(x.Lot, out alreadyDelivered);
                return new LotInfo
                {
                    ItemCode = x.ItemCode,
                    LotNo = x.Lot,
                    Quantity = Math.Max(0, x.Exported - alreadyDelivered)
                };
            }).Where(x => x.Quantity > 0).ToList();
        }

        // ============================================================
        // GRID
        // ============================================================
        private void BuildLotColumns()
        {
            _gridView.Columns.Clear();

            // SlotId là SlotLotId phục vụ xuất kho; vị trí vật lý nằm ở các field riêng.
            AddColumn("ItemCode", "Mã hàng", 130);
            AddColumn("LotNo", "LOT", 180);
            AddColumn("Quantity", "SL tại Slot", 90);
            AddColumn("WarehouseName", "Kho", 110);
            AddColumn("RackName", "Rack", 110);
            AddColumn("SlotNumber", "Vị trí Slot", 90);
            AddColumn("SlotId", "SlotLot ID", 85);

            _gridView.BestFitColumns();
        }

        private void AddColumn(string fieldName, string caption, int width)
        {
            var property = typeof(LotInfo).GetProperty(fieldName);
            if (property == null)
                return;

            var col = _gridView.Columns.AddField(fieldName);
            col.Caption = caption;
            col.Width = width;
            col.Visible = true;
        }

        // Khi phiếu đã giao SX, GridView phải đọc lịch sử giao đã lưu,
        // không tiếp tục hiển thị các Slot tồn kho khả dụng.
        private void BuildDeliveryColumns()
        {
            _gridView.Columns.Clear();
            AddDeliveryColumn(nameof(TraHangQTChungGiao.MaHang), "Mã hàng", 130);
            AddDeliveryColumn(nameof(TraHangQTChungGiao.LotGiao), "LOT đã giao", 180);
            AddDeliveryColumn(nameof(TraHangQTChungGiao.SoLuongGiao), "SL đã giao", 90);
            AddDeliveryColumn(nameof(TraHangQTChungGiao.BoPhanNhan), "Bộ phận nhận", 180);
            AddDeliveryColumn(nameof(TraHangQTChungGiao.NguoiNhan), "Người nhận", 140);
            AddDeliveryColumn(nameof(TraHangQTChungGiao.NgayGiao), "Ngày giao", 150);
            AddDeliveryColumn(nameof(TraHangQTChungGiao.ThoiGian), "Thời điểm ghi nhận", 160);
            AddDeliveryColumn(nameof(TraHangQTChungGiao.Note), "Ghi chú", 180);
            _gridView.BestFitColumns();
        }

        private void AddDeliveryColumn(string fieldName, string caption, int width)
        {
            var col = _gridView.Columns.AddField(fieldName);
            col.Caption = caption;
            col.Width = width;
            col.Visible = true;
        }

        private void AddColumnIfExists(string fieldName, string caption, int width)
        {
            AddColumn(fieldName, caption, width);
        }

        // ============================================================
        // SEARCH
        // ============================================================
        private void BtnLoadLot_Click(object sender, EventArgs e)
        {
            LoadLots();
        }

        // ============================================================
        // XUẤT KHO REWORK
        // ============================================================
        private void BtnXuatKho_Click(object sender, EventArgs e)
        {
            var lot = GetFocusedLot();
            if (lot == null)
            {
                ShowWarning("Vui lòng chọn LOT cần xuất kho.");
                return;
            }

            string lotNo = GetPropertyString(lot, "LotNo");
            int slotId = GetPropertyInt(lot, "SlotId");
            int soLuongKho = GetPropertyInt(lot, "Quantity");

            if (string.IsNullOrWhiteSpace(lotNo))
            {
                ShowWarning("LOT không hợp lệ.");
                return;
            }
            if (slotId <= 0)
            {
                ShowWarning("LOT chưa xác định được Slot nguồn.");
                return;
            }
            if (soLuongKho <= 0)
            {
                ShowWarning("Số lượng LOT không hợp lệ.");
                return;
            }

            string input = XtraInputBox.Show(
                $"LOT: {lotNo}\r\n" +
                $"Tồn khả dụng: {soLuongKho}\r\n\r\n" +
                "Nhập số lượng xuất Rework:",
                "Xuất kho Rework",
                soLuongKho.ToString());

            if (input == null)
                return;

            if (!int.TryParse(input, out int soLuong))
            {
                ShowWarning("Số lượng không hợp lệ.");
                return;
            }
            if (soLuong <= 0 || soLuong > soLuongKho)
            {
                ShowWarning($"Số lượng phải từ 1 đến {soLuongKho}.");
                return;
            }

            if (XtraMessageBox.Show(
                    $"Xác nhận xuất {soLuong} cái\r\n" +
                    $"LOT: {lotNo}\r\n" +
                    $"Vị trí: {lot.WarehouseName} / {lot.RackName} / Slot {lot.SlotNumber}\r\n" +
                    $"SlotLot ID: {slotId}\r\n" +
                    $"Phiếu xử lý: {_phieuXuLyId}",
                    "Xác nhận xuất kho Rework",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question)
                != DialogResult.Yes)
            {
                return;
            }

            try
            {
                // ── ĐÃ SỬA ──────────────────────────────────────────────
                // Form gốc gọi thẳng _reworkStockService.XuatKhoRework(...),
                // BỎ QUA _qtChungService.XuatKhoRework(...). Theo code thật
                // của QTChungService.XuatKhoRework:
                //   var result = _reworkStockService.XuatKhoRework(...);
                //   if (!result.IsOK) { SafeRollback(); return result; }
                //   _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaXuatKhoRework, nguoiXuat);
                // Gọi thẳng ReworkStockService làm hàng xuất kho thật nhưng
                // KHÔNG BAO GIỜ cập nhật QTChungStatus sang DaXuatKhoRework
                // → nút "Giao cho SX" (điều kiện status==DaXuatKhoRework)
                // sẽ không bao giờ bật được, quy trình bị kẹt vĩnh viễn.
                // Đồng thời form gốc không kiểm tra kết quả trả về, luôn
                // báo "thành công" kể cả khi thất bại.
                if (!_pendingReworkExportOperationId.HasValue)
                    _pendingReworkExportOperationId = Guid.NewGuid();

                ScanResult result = _qtChungService.XuatKhoRework(
                    _phieuXuLyId,
                    slotId,
                    lotNo,
                    soLuong,
                    Environment.UserName,
                    _pendingReworkExportOperationId.Value);

                if (result == null || !result.IsOK)
                {
                    ShowWarning(result?.Message ?? "Xuất kho Rework không thành công.");
                    return;
                }

                _pendingReworkExportOperationId = null;

                XtraMessageBox.Show(
                    result.Message ?? "Đã xuất kho Rework thành công.",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadLots();
                LoadPhieu();
            }
            catch (Exception ex)
            {
                ShowError("Không thể xuất kho Rework.", ex);
            }
        }

        // ============================================================
        // GIAO CHO SX
        // ============================================================
        private void BtnGiaoSX_Click(object sender, EventArgs e)
        {
            try
            {
                var lots = GetLotsDaXuatChuaGiao();
                if (lots == null || lots.Count == 0)
                {
                    ShowWarning("Không có LOT để giao cho sản xuất.");
                    return;
                }

                string ngayGiao = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string nguoiNhan = XtraInputBox.Show(
                    "Nhập người nhận:",
                    "Giao hàng Rework",
                    Environment.UserName);
                if (nguoiNhan == null) return;
                nguoiNhan = nguoiNhan.Trim();
                if (string.IsNullOrWhiteSpace(nguoiNhan))
                {
                    ShowWarning("Chưa nhập người nhận.");
                    return;
                }

                var boPhan = ChonBoPhanNhan();
                if (boPhan == null) return;
                string boPhanNhan = (boPhan.Code ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(boPhanNhan))
                {
                    ShowWarning("Vui lòng chọn bộ phận nhận.");
                    return;
                }

                if (XtraMessageBox.Show(
                        $"Xác nhận giao Rework cho:\r\n" +
                        $"Bộ phận: {boPhan.Display}\r\n" +
                        $"Người nhận: {nguoiNhan}\r\n" +
                        $"Số LOT: {lots.Count}\r\n" +
                        $"Tổng số lượng giao: {lots.Sum(x => x.Quantity):n0}",
                        "Xác nhận giao sản xuất",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question)
                    != DialogResult.Yes)
                {
                    return;
                }

                // LƯU Ý: QTChungService.GiaoHangRework hiện tại (bản thật
                // trên GitHub) đang là:
                //   throw new NotImplementedException(
                //       "Cần repository giao hàng/rework hiện tại.");
                // → lời gọi dưới đây LUÔN ném exception cho tới khi backend
                // implement xong repository giao hàng rework. Đây KHÔNG
                // phải lỗi ở Form — Form gọi đúng API theo interface.
                var result = _qtChungService.GiaoHangRework(
                    _phieuXuLyId,
                    lots,
                    ngayGiao,
                    nguoiNhan,
                    boPhanNhan);

                if (result == null || !result.IsOK)
                {
                    ShowWarning(result?.Message ?? "Giao hàng Rework không thành công.");
                    LoadPhieu();
                    return;
                }

                XtraMessageBox.Show(
                    result.Message ?? "Đã giao hàng Rework cho sản xuất.",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Nạp lại phiếu và grid từ bảng lịch sử giao; không lấy lại
                // các dòng tồn kho Slot như trước khi giao.
                LoadPhieu();
                LoadLots();
                UpdateActionState();
            }
            catch (Exception ex)
            {
                ShowError("Không thể giao hàng Rework cho sản xuất.", ex);
            }
        }

        // ============================================================
        // CHỌN BỘ PHẬN NHẬN TỪ DANH MỤC B20Dept
        // Dùng chung INoiBoLookupRepository với FormTaoPhieuTraNoiBo.
        // Giá trị lưu xuống nghiệp vụ là Code; người dùng tìm/chọn theo mã hoặc tên.
        // ============================================================
        private DeptLookupItem ChonBoPhanNhan()
        {
            if (_noiBoLookupRepo == null)
            {
                ShowWarning("Chưa khởi tạo danh mục bộ phận. Vui lòng mở form qua màn hình Quản lý tiến trình để nạp INoiBoLookupRepository.");
                return null;
            }

            try
            {
                var depts = _noiBoLookupRepo.GetActiveDepartments() ?? new List<DeptLookupItem>();
                if (depts.Count == 0)
                {
                    ShowWarning("Danh mục B20Dept không có bộ phận đang hoạt động.");
                    return null;
                }

                using (var dialog = new XtraForm())
                {
                    dialog.Text = "Chọn bộ phận nhận";
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.MinimizeBox = false;
                    dialog.MaximizeBox = false;
                    dialog.ShowInTaskbar = false;
                    dialog.ClientSize = new Size(560, 145);

                    var root = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        Padding = new Padding(12),
                        ColumnCount = 1,
                        RowCount = 3
                    };
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                    var label = new LabelControl
                    {
                        Text = "Bộ phận nhận:",
                        Dock = DockStyle.Fill
                    };
                    label.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;

                    var lookup = new SearchLookUpEdit { Dock = DockStyle.Fill };
                    lookup.Properties.DataSource = depts;
                    lookup.Properties.DisplayMember = nameof(DeptLookupItem.Display);
                    lookup.Properties.ValueMember = nameof(DeptLookupItem.Code);
                    lookup.Properties.NullText = "Chọn bộ phận...";
                    lookup.Properties.ShowClearButton = true;
                    lookup.Properties.ImmediatePopup = true;
                    lookup.Properties.PopupFormSize = new Size(520, 320);
                    lookup.Properties.View.OptionsView.ShowAutoFilterRow = false;
                    lookup.Properties.View.OptionsFind.AlwaysVisible = true;
                    lookup.Properties.View.OptionsFind.FindNullPrompt = "Tìm theo mã hoặc tên bộ phận...";
                    lookup.Properties.View.OptionsFind.ShowCloseButton = false;
                    lookup.Properties.PopulateViewColumns();

                    var view = lookup.Properties.View;
                    if (view.Columns[nameof(DeptLookupItem.Display)] != null)
                        view.Columns[nameof(DeptLookupItem.Display)].Visible = false;
                    var codeColumn = view.Columns[nameof(DeptLookupItem.Code)];
                    if (codeColumn != null)
                    {
                        codeColumn.Caption = "Mã bộ phận";
                        codeColumn.Width = 120;
                    }
                    var nameColumn = view.Columns[nameof(DeptLookupItem.Name)];
                    if (nameColumn != null)
                    {
                        nameColumn.Caption = "Tên bộ phận";
                        nameColumn.Width = 340;
                    }

                    var buttons = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        FlowDirection = FlowDirection.RightToLeft,
                        WrapContents = false
                    };
                    var ok = new SimpleButton { Text = "OK", Width = 95 };
                    var cancel = new SimpleButton { Text = "Hủy", Width = 95 };
                    ok.Click += (s, e) =>
                    {
                        if (lookup.EditValue == null || string.IsNullOrWhiteSpace(Convert.ToString(lookup.EditValue)))
                        {
                            XtraMessageBox.Show(dialog, "Vui lòng chọn bộ phận nhận.", "Thông báo",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    };
                    cancel.Click += (s, e) =>
                    {
                        dialog.DialogResult = DialogResult.Cancel;
                        dialog.Close();
                    };

                    buttons.Controls.Add(cancel);
                    buttons.Controls.Add(ok);
                    root.Controls.Add(label, 0, 0);
                    root.Controls.Add(lookup, 0, 1);
                    root.Controls.Add(buttons, 0, 2);
                    dialog.Controls.Add(root);

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return null;

                    string code = Convert.ToString(lookup.EditValue);
                    return depts.FirstOrDefault(d =>
                        string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex)
            {
                ShowError("Không tải được danh sách bộ phận nhận.", ex);
                return null;
            }
        }

        // ============================================================
        // GHI NHẬN ĐANG REWORK
        // ============================================================
        private void BtnDangRework_Click(object sender, EventArgs e)
        {
            string ghiChu = _txtGhiChu.Text.Trim();
            if (string.IsNullOrWhiteSpace(ghiChu))
            {
                ghiChu = XtraInputBox.Show(
                    "Nhập ghi chú quá trình Rework:",
                    "Đang Rework",
                    "");
                if (ghiChu == null) return;
            }

            try
            {
                // LƯU Ý: QTChungService.GhiNhanDangRework hiện tại (bản
                // thật trên GitHub) đang throw NotImplementedException
                // (chưa có repository ghi nhận thông tin rework). Form gọi
                // đúng API — đây là việc backend cần hoàn thiện, không
                // phải lỗi ở Form.
                _qtChungService.GhiNhanDangRework(
                    _phieuXuLyId,
                    ghiChu,
                    Environment.UserName);

                XtraMessageBox.Show(
                    "Đã lưu ghi chú tiến độ Rework (chưa xác nhận hoàn tất sửa hàng). Khi sửa xong: đóng form Rework, chọn \"7. QC Rework / Disposition\" trên màn hình quản lý tiến trình để QC xác nhận OK/NG, sau đó nhập số lượng OK/NG vào kho.",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _txtGhiChu.Text = "";
                LoadPhieu();
            }
            catch (Exception ex)
            {
                ShowError("Không thể ghi nhận trạng thái đang Rework.", ex);
            }
        }

        // ============================================================
        // SELECTED LOT
        // ============================================================
        private LotInfo GetFocusedLot()
        {
            int rowHandle = _gridView.FocusedRowHandle;
            if (rowHandle < 0)
                return null;
            return _gridView.GetRow(rowHandle) as LotInfo;
        }

        // ============================================================
        // ENABLE / DISABLE BUTTON
        // ============================================================
        private void UpdateActionState()
        {
            try
            {
                // ── ĐÃ SỬA ──────────────────────────────────────────────
                // GetTrangThai không còn tồn tại — đọc Status từ GetById.
                QTChungStatus status = _qtChungService.GetById(_phieuXuLyId).Status;

                bool daChonLot = GetFocusedLot() != null;

                // A/X/D chỉ để gợi ý UI; server (policy sau khóa phiếu) mới là nơi quyết định.
                // Không đọc được kế hoạch thì chỉ dựa vào trạng thái, không khóa nhầm nghiệp vụ.
                var plan = TryGetReworkPlan();
                ApplyQuantitySummary(plan);

                // ----------------------------------------------------
                // Xuất kho: được xuất nhiều lần (DaDinhHuong hoặc DaXuatKhoRework)
                // khi còn hạn mức và chưa giao đủ.
                // ----------------------------------------------------
                _btnXuatKho.Enabled =
                    daChonLot &&
                    (status == QTChungStatus.DaDinhHuong ||
                     status == QTChungStatus.DaXuatKhoRework) &&
                    (plan == null || (plan.SoLuongConPhaiXuat > 0 && !plan.DaGiaoDu));

                // ----------------------------------------------------
                // Giao SX: nhiều lần, chỉ phần đã xuất chưa giao.
                // ----------------------------------------------------
                _btnGiaoSX.Enabled =
                    status == QTChungStatus.DaXuatKhoRework &&
                    (plan == null || plan.SoLuongDaXuatRework - plan.SoLuongDaGiaoSanXuat > 0);

                // ----------------------------------------------------
                // Ghi nhận đang rework: nghiệp vụ note, KHÔNG tạo state
                // DangRework.
                // ----------------------------------------------------
                _btnDangRework.Enabled = status == QTChungStatus.DaGiaoSanXuat;
            }
            catch
            {
                _btnXuatKho.Enabled = false;
                _btnGiaoSX.Enabled = false;
                _btnDangRework.Enabled = false;
            }
        }

        private const string QuantitySummaryMarker = " | A/X/D: ";

        private ReworkPhase4Plan TryGetReworkPlan()
        {
            try
            {
                return _reworkStockService.GetReworkPlan(_phieuXuLyId);
            }
            catch
            {
                return null;
            }
        }

        private const string PlanUnavailableWarning =
            " | CẢNH BÁO: không tải được kế hoạch A/X/D — nút theo trạng thái, hệ thống sẽ kiểm tra số lượng khi lưu";

        private void ApplyQuantitySummary(ReworkPhase4Plan plan)
        {
            if (_lblTrangThai == null) return;
            string text = _lblTrangThai.Text ?? string.Empty;
            int marker = text.IndexOf(QuantitySummaryMarker, StringComparison.Ordinal);
            if (marker >= 0) text = text.Substring(0, marker);
            if (plan == null)
            {
                // Không âm thầm chuyển sang fallback: người dùng phải thấy số liệu đang thiếu.
                _lblTrangThai.Text = text + PlanUnavailableWarning;
                return;
            }
            int conGiao = Math.Max(0, plan.SoLuongDaXuatRework - plan.SoLuongDaGiaoSanXuat);
            _lblTrangThai.Text = text + QuantitySummaryMarker +
                $"{plan.SoLuongReworkDuocPhep:n0}/{plan.SoLuongDaXuatRework:n0}/{plan.SoLuongDaGiaoSanXuat:n0}" +
                $" | còn xuất {plan.SoLuongConPhaiXuat:n0} | còn giao {conGiao:n0}";
        }

        // ============================================================
        // REFLECTION HELPERS
        // ============================================================
        //
        // Vẫn dùng cho LotInfo (PCTP.Modules.KhoVatLy.Kho.Models) vì form
        // này không có định nghĩa đầy đủ của LotInfo trong phạm vi sửa —
        // giữ nguyên cách tiếp cận "khoan dung" ban đầu cho các field hiển
        // thị thêm (TenHang, ViTri) có thể không tồn tại.
        //
        // Các property nghiệp vụ chính vẫn đang dùng:
        //   LotNo, SlotId, SoLuong
        //
        // ============================================================
        private static string GetPropertyString(object obj, string propertyName)
        {
            if (obj == null) return "";
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop == null) return "";
            object value = prop.GetValue(obj);
            return value?.ToString() ?? "";
        }

        private static int GetPropertyInt(object obj, string propertyName)
        {
            if (obj == null) return 0;
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop == null) return 0;
            object value = prop.GetValue(obj);
            if (value == null) return 0;
            try { return Convert.ToInt32(value); }
            catch { return 0; }
        }

        // ============================================================
        // MESSAGE
        // ============================================================
        // ── PHẦN NÀY BỊ GITHUB CẮT KHI HIỂN THỊ (blob viewer giới hạn
        // ~1000 dòng, /raw/ bị robots chặn nên không lấy được nguyên văn).
        // Dựng lại theo đúng pattern MessageBox dùng thống nhất trong
        // toàn bộ các file khác của module (title "Thông báo"/"Lỗi",
        // icon Warning/Error, nút OK) — nếu bản gốc có khác (vd thêm log,
        // buttons khác), gửi lại đoạn gốc để tôi khớp chính xác.
        private static void ShowWarning(string message)
        {
            XtraMessageBox.Show(
                message,
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void ShowError(string message, Exception ex)
        {
            XtraMessageBox.Show(
                $"{message}\r\n\r\n{ex.Message}",
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}