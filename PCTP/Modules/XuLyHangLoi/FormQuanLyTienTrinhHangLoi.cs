using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.UserControls;
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
    public partial class FormQuanLyTienTrinhHangLoi : XtraForm
    {
        private readonly SQLPROVIDER _sql = new SQLPROVIDER();
        private readonly IPhieuLoiRepository _repo;
        private readonly TraHangService _traHangService;

        // ── Timeline ─────────────────────────────────────────────────────
        private TableLayoutPanel _pnlTimeline;
        private TimelineStepButton[] _steps;
        private int _activeStep = 1;

        // ── Grid ─────────────────────────────────────────────────────────
        private TextEdit _txtSearch;
        private GridControl _grid;
        private GridView _gridView;
        private SimpleButton _btnExportExcel;
        private SimpleButton _btnActionPrimary; // Nút hành động chính
        private SimpleButton _btnActionSecondary; // Nút phụ (ví dụ: tạo phiếu nội bộ ở bước 2)
        private LabelControl _lblHint;

        public FormQuanLyTienTrinhHangLoi()
        {
            _repo = new PhieuLoiRepository(_sql);
            var stockTpRepo = new StockTpRepository(_sql);
            var traHangRepo = new TraHangRepository(_sql);
            var stockService = new StockService();
            _traHangService = new TraHangService(_sql, stockTpRepo, traHangRepo, stockService, _repo);

            BuildUI();
            RefreshBadges();
            SetActiveStep(1);
        }

        // ════════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "Quản lý tiến trình tiếp nhận & xử lý hàng lỗi (HVN / YMVN / Nội bộ)";
            Size = new Size(1400, 780);
            StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115)); // Timeline
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));  // Search & Buttons bar
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Grid
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // Hint

            // ── Row 0: Timeline 6 mốc ──────────────────────────────────────
            _pnlTimeline = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 11, // 6 Bước + 5 Mũi tên xen kẽ
                BackColor = Color.FromArgb(240, 243, 246)
            };
            for (int i = 0; i < 11; i++)
            {
                if (i % 2 == 0) _pnlTimeline.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 / 6f));
                else _pnlTimeline.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }
            _steps = new[]
            {
                new TimelineStepButton(1, "1. Nhập chứng từ khách", "Chờ tiếp nhận", Color.FromArgb(220, 53, 69)),
                new TimelineStepButton(2, "2. Ban hành phiếu XN", "Chờ ban hành", Color.FromArgb(253, 126, 20)),
                new TimelineStepButton(3, "3. QC định hướng", "Chờ định hướng", Color.FromArgb(111, 66, 193)),
                new TimelineStepButton(4, "4. SX đang xử lý", "Chờ SX báo xong", Color.FromArgb(23, 162, 184)),
                new TimelineStepButton(5, "5. QC xác nhận cuối", "Chờ QC chốt", Color.FromArgb(255, 193, 7)),
                new TimelineStepButton(6, "6. Trả hàng NG về SX", "Sẵn sàng trả", Color.FromArgb(40, 167, 69)),
            };

            for (int i = 0; i < _steps.Length; i++)
            {
                var step = _steps[i];
                step.Dock = DockStyle.Fill; // Quan trọng: Để nút tự giãn theo cột
                step.StepClicked += (s, e) => SetActiveStep(step.StepIndex);
                _pnlTimeline.Controls.Add(step, i * 2, 0);

                if (i < _steps.Length - 1)
                {
                    var arrow = new LabelControl
                    {
                        Text = "▶",
                        Appearance = { ForeColor = Color.FromArgb(150, 160, 170), Font = new Font("Tahoma", 12) },
                        AutoSizeMode = LabelAutoSizeMode.Horizontal,
                        Dock = DockStyle.Fill
                    };
                    _pnlTimeline.Controls.Add(arrow, i * 2 + 1, 0);
                }
            }
            mainLayout.Controls.Add(_pnlTimeline, 0, 0);

            // ── Row 1: Search bar + Action Buttons + Export ───────────────
            var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(3) };
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); // Nút phụ (Tạo phiếu nội bộ)
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); // Nút chính theo bước
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Xuất Excel

            _txtSearch = new TextEdit { Dock = DockStyle.Fill, Properties = { NullValuePrompt = "🔍 Tìm nhanh theo Mã hàng, Số phiếu, Số lô..." } };
            _txtSearch.Properties.Appearance.Font = new Font("Tahoma", 9.5F);
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyFilter(); };
            searchPanel.Controls.Add(_txtSearch, 0, 0);

            _btnActionSecondary = new SimpleButton { Text = "➕ Tạo phiếu nội bộ", Dock = DockStyle.Fill };
            _btnActionSecondary.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            _btnActionSecondary.Appearance.ForeColor = Color.DarkBlue;
            _btnActionSecondary.Click += (s, e) => MoFormTaoPhieuNoiBo();
            searchPanel.Controls.Add(_btnActionSecondary, 1, 0);

            _btnActionPrimary = new SimpleButton { Text = "", Dock = DockStyle.Fill };
            _btnActionPrimary.Appearance.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _btnActionPrimary.Click += BtnActionPrimary_Click;
            searchPanel.Controls.Add(_btnActionPrimary, 2, 0);

            _btnExportExcel = new SimpleButton { Text = "📥 Xuất Excel", Dock = DockStyle.Fill };
            _btnExportExcel.Appearance.Font = new Font("Tahoma", 9.5F);
            _btnExportExcel.Click += (s, e) => _gridView.ExportToXlsx(
                $"HangLoi_Buoc{_activeStep}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            searchPanel.Controls.Add(_btnExportExcel, 3, 0);

            mainLayout.Controls.Add(searchPanel, 0, 1);

            // ── Row 2: Grid ──────────────────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.RowAutoHeight = true;
            _gridView.OptionsSelection.MultiSelect = true;
            _gridView.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;
            _gridView.DoubleClick += GridView_DoubleClick;

            _gridView.RowStyle += (s, e) =>
            {
                var row = _gridView.GetDataRow(e.RowHandle);
                if (row == null) return;
                if (!row.Table.Columns.Contains("TrangThaiHienThi")) return;
                string tt = row["TrangThaiHienThi"]?.ToString();
                switch (tt)
                {
                    case "CHUA_NHAP": e.Appearance.BackColor = Color.FromArgb(255, 240, 240); break;
                    case "CHO_QC": e.Appearance.BackColor = Color.FromArgb(255, 250, 230); break;
                    case "QC_DA_DUYET": e.Appearance.BackColor = Color.FromArgb(235, 255, 235); break;
                }
            };

            mainLayout.Controls.Add(_grid, 0, 2);

            // ── Row 3: Hint ──────────────────────────────────────────────
            _lblHint = new LabelControl
            {
                Dock = DockStyle.Fill,
                Text = "💡 Hướng dẫn: Click chọn mốc tiến trình phía trên để xem dữ liệu, thao tác trực tiếp hoặc Double-click để xử lý.",
                Padding = new Padding(10, 6, 0, 0),
                Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Italic), ForeColor = Color.DimGray }
            };
            mainLayout.Controls.Add(_lblHint, 0, 3);

            Controls.Add(mainLayout);
        }

        private void SetActiveStep(int stepIndex)
        {
            _activeStep = stepIndex;
            foreach (var s in _steps) s.SetActive(s.StepIndex == stepIndex);

            _gridView.Columns.Clear();

            // Nút "Tạo phiếu nội bộ" chỉ thực sự hữu ích và hiện ở bước 1 hoặc bước 2
            _btnActionSecondary.Visible = (stepIndex == 1 || stepIndex == 2);

            switch (stepIndex)
            {
                case 1:
                    LoadBuoc1();
                    _btnActionPrimary.Text = "➕ Nhập chứng từ mới";
                    _lblHint.Text = "💡 Bước 1 (Chỉ nhánh khách): Nhập chứng từ Phiếu đổi phụ tùng lỗi (HVN) hoặc Return Slip (YMVN). Đối với hàng nội bộ, hệ thống tự động bỏ qua bước này.";
                    break;
                case 2:
                    LoadBuoc2();
                    _btnActionPrimary.Text = "📄 Sinh phiếu bất thường";
                    _lblHint.Text = "💡 Bước 2: Chọn dòng chứng từ khách và bấm 'Sinh phiếu bất thường', hoặc bấm nút 'Tạo phiếu nội bộ' nếu là lỗi phát sinh nội bộ từ Slot.";
                    break;
                case 3:
                    LoadBuoc3();
                    _btnActionMultiDinhHuong();
                    break;
                case 4:
                    LoadBuoc4();
                    _btnActionPrimary.Text = "⚙️ SX báo đã xử lý xong";
                    _lblHint.Text = "💡 Bước 4: Danh sách đang chờ sản xuất xử lý. Double-click dòng để báo sản xuất hoàn tất (chuyển sang bước QC xác nhận cuối).";
                    break;
                case 5:
                    LoadBuoc5();
                    _btnActionPrimary.Text = "🔍 Mở QC Xác Nhận Cuối";
                    _lblHint.Text = "💡 Bước 5: Sản xuất đã báo xong, mở màn hình FormXuLyBatThuong (mode Final) để QC chốt OK/NG kết quả cuối.";
                    break;
                case 6:
                    LoadBuoc6();
                    _btnActionPrimary.Text = "↩ Trả hàng NG về sản xuất";
                    _lblHint.Text = "💡 Bước 6: Các phiếu đã được QC chốt OK/NG sẵn sàng trả về kho/sản xuất (dùng FormTraHangNGNew).";
                    break;
            }
        }

        private void _btnActionMultiDinhHuong()
        {
            _btnActionPrimary.Text = "✍️ QC Định hướng";
            _lblHint.Text = "💡 Bước 3: Double-click dòng hoặc bấm nút để mở FormXuLyBatThuong (mode DinhHuong), cập nhật định hướng loại lỗi.";
        }

        private void RefreshBadges()
        {
            _steps[0].Count = _repo.DemChuaNhapLieu();
            _steps[1].Count = _repo.DemChoBanHanhPhieuBatThuong();
            _steps[2].Count = _repo.DemChoQCDinhHuong();
            _steps[3].Count = _repo.DemDangSanXuat();
            _steps[4].Count = _repo.DemChoXacNhanCuoi();
            _steps[5].Count = _repo.DemSanSangTra();
        }

        private void ApplyFilter()
        {
            string kw = _txtSearch.Text.Trim();
            _gridView.ActiveFilterString = string.IsNullOrEmpty(kw)
                ? ""
                : $"Contains([MaHang], '{kw}') Or Contains([SoPhieuKhach], '{kw}') Or " +
                  $"Contains([SoPhieu], '{kw}') Or Contains([MaSanPham], '{kw}')";
        }

        // ════════════════════════════════════════════════════════════════
        // Load Grid theo 6 bước
        // ════════════════════════════════════════════════════════════════
        private void LoadBuoc1()
        {
            var dt = _repo.GetGridBuoc1_ChungTuMoi();
            _grid.DataSource = dt;
            _gridView.PopulateColumns();
            HideHelperColumns();
            if (_gridView.Columns["HeaderId"] != null) _gridView.Columns["HeaderId"].Visible = false;
            if (_gridView.Columns["CTId"] != null) _gridView.Columns["CTId"].Visible = false;
            _gridView.BestFitColumns();
        }

        private void LoadBuoc2()
        {
            var dt = _repo.GetGridBuoc2_ChoSinhPhieuBatThuong();
            _grid.DataSource = dt;
            _gridView.PopulateColumns();
            HideHelperColumns();
            if (_gridView.Columns["HeaderId"] != null) _gridView.Columns["HeaderId"].Visible = false;
            _gridView.BestFitColumns();
        }

        //private void LoadBuoc3()
        //{
        //    var dt = _repo.GetGridDinhHuong();
        //    _grid.DataSource = dt;
        //    _gridView.PopulateColumns();
        //    HideHelperColumns();
        //    _gridView.BestFitColumns();
        //}
        private void LoadBuoc3()
        {
            var dt = _repo.GetGridDinhHuong();
            _grid.DataSource = dt;

            // Xóa populate tự động nếu có
            _gridView.Columns.Clear();

            // Chỉ định rõ các cột quan trọng thực sự cần thiết hiển thị cho người dùng
            _gridView.Columns.Add(new GridColumn { FieldName = "Id", Caption = "ID", Visible = false });
            _gridView.Columns.Add(new GridColumn { FieldName = "SoPhieu", Caption = "Số phiếu", Width = 130, VisibleIndex = 0 });
            _gridView.Columns.Add(new GridColumn { FieldName = "Model", Caption = "Model", Width = 90, VisibleIndex = 1 });
            _gridView.Columns.Add(new GridColumn { FieldName = "MaSanPham", Caption = "Mã sản phẩm", Width = 140, VisibleIndex = 2 });
            _gridView.Columns.Add(new GridColumn { FieldName = "SoLo", Caption = "Số lô", Width = 100, VisibleIndex = 3 });

            var colSl = new GridColumn { FieldName = "SoLuongLoi", Caption = "SL lỗi", Width = 70, VisibleIndex = 4 };
            colSl.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            colSl.DisplayFormat.FormatString = "n0";
            _gridView.Columns.Add(colSl);

            _gridView.Columns.Add(new GridColumn { FieldName = "PhanLoaiXuLy", Caption = "Phân loại", Width = 120, VisibleIndex = 5 });

            var colDate = new GridColumn { FieldName = "NgayTao", Caption = "Ngày tạo", Width = 110, VisibleIndex = 6 };
            colDate.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            colDate.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm";
            _gridView.Columns.Add(colDate);

            _gridView.Columns.Add(new GridColumn { FieldName = "BoPhanPhatHanh", Caption = "Bộ phận tạo", Width = 110, VisibleIndex = 7 });

            _gridView.BestFitColumns();
        }
        private void LoadBuoc4()
        {
            var dt = _repo.GetGridDangSanXuat();
            _grid.DataSource = dt;
            _gridView.PopulateColumns();
            HideHelperColumns();
            _gridView.BestFitColumns();
        }

        private void LoadBuoc5()
        {
            var dt = _repo.GetGridXacNhanCuoi();
            _grid.DataSource = dt;
            _gridView.PopulateColumns();
            HideHelperColumns();
            _gridView.BestFitColumns();
        }

        private void LoadBuoc6()
        {
            var dt = _repo.GetGridBuoc4_SanSangTra();
            _grid.DataSource = dt;
            _gridView.PopulateColumns();
            HideHelperColumns();
            _gridView.BestFitColumns();
        }

        private void HideHelperColumns()
        {
            if (_gridView.Columns["TrangThaiHienThi"] != null)
                _gridView.Columns["TrangThaiHienThi"].Visible = false;
        }

        // ════════════════════════════════════════════════════════════════
        // Điều phối hành động chính & Double-click
        // ════════════════════════════════════════════════════════════════
        private void BtnActionPrimary_Click(object sender, EventArgs e)
        {
            ExecuteActionByStep(_activeStep);
        }

        private void GridView_DoubleClick(object sender, EventArgs e)
        {
            ExecuteActionByStep(_activeStep);
        }

        private void ExecuteActionByStep(int step)
        {
            switch (step)
            {
                case 1: MoFormNhapChungTu(); break;
                case 2: SinhPhieuBatThuongTuDongDaChon(); break;
                case 3: MoFormXuLyBatThuongDinhHuong(); break;
                case 4: XuLySXBaoxong(); break;
                case 5: MoFormXuLyBatThuongFinal(); break;
                case 6: MoFormTraHang(); break;
            }
        }

        private void MoFormNhapChungTu()
        {
            using (var f = new FormPhieuLoiKhachTra(_repo))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshBadges();
                    SetActiveStep(_activeStep);
                }
            }
        }

        // Nút phụ: Tạo phiếu nội bộ trực tiếp từ Slot (Bỏ qua bước 1 và 2 chứng từ khách)
        private void MoFormTaoPhieuNoiBo()
        {
            // Mở form hoặc hộp thoại chọn Slot/Lot nội bộ để tạo phiếu bất thường nguồn Nội bộ (Nguon = 2)
            // Tùy biến theo form chọn Slot sẵn có trong hệ thống của bạn, ví dụ:
            using (var f = new FormChonSlotNoiBo(_repo))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    XtraMessageBox.Show("Đã tạo phiếu xử lý bất thường nội bộ thành công từ Slot!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshBadges();
                    SetActiveStep(3); // Chuyển thẳng sang mốc 3 (QC định hướng)
                }
            }
        }

        private void SinhPhieuBatThuongTuDongDaChon()
        {
            var selectedRows = _gridView.GetSelectedRows()
                .Select(h => _gridView.GetDataRow(h))
                .Where(r => r != null)
                .ToList();

            if (selectedRows.Count == 0)
            {
                XtraMessageBox.Show("Vui lòng tick chọn ít nhất 1 dòng để sinh phiếu bất thường.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var groups = selectedRows
                .GroupBy(r => new {
                    Model = r["Model"]?.ToString(),
                    MaHang = r["MaHang"]?.ToString(),
                    SoLo = r["SoLo"]?.ToString()
                });

            int soPhieuTao = 0;
            foreach (var g in groups)
            {
                int ctIdDaiDien = Convert.ToInt32(g.First()["CTId"]);
                int tongSl = g.Sum(r => Convert.ToInt32(r["SoLuong"]));
                string noiDungGop = string.Join(" | ", g.Select(r => r["NoiDungLoi"]?.ToString()).Distinct());

                var pht = new PhieuXuLyBatThuong
                {
                    Nguon = NguonPhieuBatThuong.KhachTra,
                    PhieuLoiKhachTraCTId = ctIdDaiDien,
                    Model = g.Key.Model,
                    MaSanPham = g.Key.MaHang,
                    SoLo = g.Key.SoLo,
                    SoLoLoi = g.Key.SoLo,
                    SoLuongLoi = tongSl,
                    NoiDungBatThuong = noiDungGop,
                    PhanLoaiXuLy = "Hàng lỗi khách trả",
                    BoPhanPhatHanh = Environment.UserName
                };

                _repo.InsertPhieuXuLyBatThuong(pht);
                soPhieuTao++;
            }

            var headerIds = selectedRows.Select(r => Convert.ToInt32(r["HeaderId"])).Distinct();
            foreach (var hid in headerIds)
            {
                var header = _repo.GetPhieuLoiKhachTra(hid);
                if (header != null)
                    new DevExpress.XtraReports.UI.ReportPrintTool(
                        new RpIn.RpPhieuXacNhanPhuTungLoi(header)).ShowPreviewDialog();
            }

            XtraMessageBox.Show($"Đã sinh {soPhieuTao} phiếu xử lý bất thường, chuyển sang QC định hướng.",
                "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

            RefreshBadges();
            SetActiveStep(_activeStep);
        }

        private void MoFormXuLyBatThuongDinhHuong()
        {
            var focused = _gridView.GetFocusedDataRow();
            if (focused == null)
            {
                XtraMessageBox.Show("Vui lòng chọn 1 phiếu để định hướng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = Convert.ToInt32(focused["Id"]);
            using (var f = new FormXuLyBatThuong(_repo, XuLyBatThuongMode.DinhHuong, id))
            {
                f.ShowDialog(this);
            }
            RefreshBadges();
            SetActiveStep(_activeStep);
        }

        // Bước 4: SX báo xong (không cần form riêng, chỉ hỏi xác nhận + nhập nhanh ghi chú)
        private void XuLySXBaoxong()
        {
            var focused = _gridView.GetFocusedDataRow();
            if (focused == null)
            {
                XtraMessageBox.Show("Vui lòng chọn 1 dòng phiếu đang xử lý.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = Convert.ToInt32(focused["Id"]);
            string soPhieu = focused["SoPhieu"]?.ToString();

            // Hiển thị hộp thoại nhập ghi chú nhanh cho sản xuất
            string ghiChu = XtraInputBox.Show($"Nhập ghi chú hoàn tất xử lý cho phiếu [{soPhieu}]:", "Sản xuất báo xong", "");
            if (ghiChu == null) return; // Người dùng bấm Cancel

            try
            {
                _repo.DanhDauSanXuatBaoXong(id, ghiChu, Environment.UserName);
                XtraMessageBox.Show("Đã cập nhật trạng thái sang 'Chờ QC xác nhận cuối'.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshBadges();
                SetActiveStep(_activeStep);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi cập nhật: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MoFormXuLyBatThuongFinal()
        {
            var focused = _gridView.GetFocusedDataRow();
            if (focused == null)
            {
                XtraMessageBox.Show("Vui lòng chọn 1 phiếu để QC xác nhận cuối.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = Convert.ToInt32(focused["Id"]);
            using (var f = new FormXuLyBatThuong(_repo, XuLyBatThuongMode.XacNhanCuoi, id))
            {
                f.ShowDialog(this);
            }
            RefreshBadges();
            SetActiveStep(_activeStep);
        }

        private void MoFormTraHang()
        {
            var focused = _gridView.GetFocusedDataRow();
            int? preselectId = focused != null ? Convert.ToInt32(focused["Id"]) : (int?)null;

            using (var f = new FormTraHangNGNew(preselectId))
            {
                f.ShowDialog(this);
            }
            RefreshBadges();
            SetActiveStep(_activeStep);
        }
    }
}