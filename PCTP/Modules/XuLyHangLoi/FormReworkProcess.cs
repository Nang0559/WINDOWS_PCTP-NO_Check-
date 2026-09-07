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
            int phieuXuLyId)
        {
            _qtChungService = qtChungService
                ?? throw new ArgumentNullException(nameof(qtChungService));
            _reworkStockService = reworkStockService
                ?? throw new ArgumentNullException(nameof(reworkStockService));
            _phieuXuLyId = phieuXuLyId;

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
                Text = "Chỉ các LOT hợp lệ cho hướng xử lý CanRework mới được phép xuất kho.",
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
            _btnDangRework = new SimpleButton { Text = "⚙ Đang Rework", Dock = DockStyle.Fill };

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

                // ----------------------------------------------------
                // REWORK CHỈ ĐƯỢC PHÉP CHO CanRework
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
            }
            catch (Exception ex)
            {
                ShowError("Không thể lấy danh sách LOT có thể Rework.", ex);
            }
        }

        // ============================================================
        // GRID
        // ============================================================
        private void BuildLotColumns()
        {
            _gridView.Columns.Clear();

            AddColumn("MaHang", "Mã hàng", 130);
            AddColumn("LotNo", "LOT", 150);
            AddColumn("SoLuong", "Số lượng", 90);
            AddColumn("SlotId", "Slot", 80);

            // Nếu LotInfo có các field này thì sẽ tự hiển thị.
            AddColumnIfExists("TenHang", "Tên hàng", 180);
            AddColumnIfExists("ViTri", "Vị trí", 120);

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
            int soLuongKho = GetPropertyInt(lot, "SoLuong");

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
                    $"Slot: {slotId}\r\n" +
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
                ScanResult result = _qtChungService.XuatKhoRework(
                    _phieuXuLyId,
                    slotId,
                    lotNo,
                    soLuong,
                    Environment.UserName);

                if (result == null || !result.IsOK)
                {
                    ShowWarning(result?.Message ?? "Xuất kho Rework không thành công.");
                    return;
                }

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
                var lots = _reworkStockService.GetLotsCanReworkByPhieuXuLy(_phieuXuLyId);
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

                string boPhanNhan = XtraInputBox.Show(
                    "Nhập bộ phận nhận:",
                    "Giao hàng Rework",
                    "SX");
                if (boPhanNhan == null) return;
                boPhanNhan = boPhanNhan.Trim();
                if (string.IsNullOrWhiteSpace(boPhanNhan))
                {
                    ShowWarning("Chưa nhập bộ phận nhận.");
                    return;
                }

                if (XtraMessageBox.Show(
                        $"Xác nhận giao Rework cho:\r\n" +
                        $"Bộ phận: {boPhanNhan}\r\n" +
                        $"Người nhận: {nguoiNhan}\r\n" +
                        $"Số LOT: {lots.Count}",
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
                _qtChungService.GiaoHangRework(
                    _phieuXuLyId,
                    lots,
                    ngayGiao,
                    nguoiNhan,
                    boPhanNhan);

                XtraMessageBox.Show(
                    "Đã giao hàng Rework cho sản xuất.",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadPhieu();
                UpdateActionState();
            }
            catch (Exception ex)
            {
                ShowError("Không thể giao hàng Rework cho sản xuất.", ex);
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
                    "Đã ghi nhận quá trình đang Rework.",
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

                // ----------------------------------------------------
                // Xuất kho: chỉ có ý nghĩa trước khi đã giao SX.
                // ----------------------------------------------------
                _btnXuatKho.Enabled =
                    daChonLot && status == QTChungStatus.DaDinhHuong;

                // ----------------------------------------------------
                // Giao SX: Service sẽ tự validate số lượng / trạng thái.
                // ----------------------------------------------------
                _btnGiaoSX.Enabled = status == QTChungStatus.DaXuatKhoRework;

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