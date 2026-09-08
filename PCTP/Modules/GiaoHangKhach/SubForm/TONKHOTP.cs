using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.FuctionMain;
using PCTP.Modules.KhoCore.Services;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


    namespace PCTP.Modules.GiaoHangKhach.SubForm
    {
        /// <summary>
        /// Xem / đối chiếu tồn kho STOCKTP.
        /// - TONKHOTP(services) -> xem toàn bộ tồn kho hiện tại (SLCONLAI > 0).
        /// - TONKHOTP(services, lotFilter) -> lọc theo 1 hoặc nhiều LOT.
        ///
        /// Khi lotFilter chỉ chứa ĐÚNG 1 LOT, form tự động bật thêm panel
        /// "Điều chỉnh tồn kho" để sửa lại SLCONLAI cho khớp thực tế, kèm
        /// bắt buộc nhập lý do và ghi lịch sử (StockHistory) để truy vết.
        ///
        /// Kiến trúc: Form chỉ phụ thuộc Service (IStockTpLookupService,
        /// IStockHistoryService) — KHÔNG tự tạo SQLPROVIDER, KHÔNG phụ thuộc
        /// trực tiếp Repository.
        /// </summary>
        public partial class TONKHOTP : DevExpress.XtraEditors.XtraForm
        {
            private readonly IStockTpLookupService _stockTpService;
            private readonly IStockHistoryService _historyService;
            private readonly List<string> _lotList;
            private readonly bool _isSingleLotEditMode;

            private GridControl _grid;
            private GridView _gridView;

            // ── Panel điều chỉnh — chỉ build khi _isSingleLotEditMode ───────────
            private GroupControl _grpDieuChinh;
            private LabelControl _lblPart, _lblName, _lblNgaySX, _lblSlSX, _lblSlNhap, _lblSlXuat;
            private SpinEdit _spinSlConLaiMoi;
            private TextEdit _txtLyDo;
            private SimpleButton _btnCapNhat;

            // ── Constructor chính — nhận Service từ ngoài ───────────────────────
            public TONKHOTP(
                IStockTpLookupService stockTpService,
                IStockHistoryService historyService,
                string lotFilter = "")
            {
                InitializeComponent();
                _stockTpService = stockTpService ?? throw new ArgumentNullException(nameof(stockTpService));
                _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));

                _lotList = ParseLotFilter(lotFilter);
                _isSingleLotEditMode = _lotList.Count == 1;

                Text = _isSingleLotEditMode
                    ? $"Tồn kho — Điều chỉnh LOT [{_lotList[0]}]"
                    : "Tồn kho hiện tại";
                Size = new Size(1000, 620);
                StartPosition = FormStartPosition.CenterParent;

                BuildUI();
                LoadData();
            }
        // ════════════════════════════════════════════════════════════════
        // ★ Factory tiện dụng — dùng cho nơi gọi đơn giản (ví dụ:
        // GridView_cnk_err_RowClick khi người dùng click 1 dòng lỗi CNK).
        // Đóng gói toàn bộ việc khởi tạo Service/Repository, nơi gọi không
        // cần biết chi tiết DI — vẫn tuân thủ Form → Service → Repository
        // vì bản thân TONKHOTP vẫn chỉ nhận Service qua constructor.
        // ════════════════════════════════════════════════════════════════
        public static void ShowForLot(IWin32Window owner, string lotFilter)
        {
            var provider = new SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);

            var stockTpRepo = new StockTpRepository(sql, uow);
            var stockTpService = new StockTpLookupService(stockTpRepo);

            var historyRepo = new StockHistoryRepository(sql, uow);
            var historyService = new StockHistoryService(historyRepo);

            using (var f = new TONKHOTP(stockTpService, historyService, lotFilter))
                f.ShowDialog(owner);
        }
        // ════════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════════
        private void BuildUI()
            {
                var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
                main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                if (_isSingleLotEditMode)
                {
                    BuildEditPanel();
                    main.Controls.Add(_grpDieuChinh, 0, 0);
                }

                _grid = new GridControl { Dock = DockStyle.Fill };
                _gridView = new GridView(_grid);
                _grid.MainView = _gridView;
                _gridView.OptionsBehavior.Editable = false; // sửa qua panel riêng
                _gridView.OptionsView.ShowGroupPanel = false;

                _gridView.Columns.Add(new GridColumn { FieldName = "LOT", Caption = "LOT", Width = 220, VisibleIndex = 0 });
                _gridView.Columns.Add(new GridColumn { FieldName = "PART", Caption = "Mã hàng", Width = 110, VisibleIndex = 1 });
                _gridView.Columns.Add(new GridColumn { FieldName = "NAME", Caption = "Tên hàng", Width = 180, VisibleIndex = 2 });
                _gridView.Columns.Add(new GridColumn { FieldName = "CASX", Caption = "Ca SX", Width = 60, VisibleIndex = 3 });
                _gridView.Columns.Add(new GridColumn { FieldName = "NGAYSX", Caption = "Ngày SX", Width = 90, VisibleIndex = 4 });
                _gridView.Columns.Add(new GridColumn { FieldName = "SLSX", Caption = "SL Sản Xuất", Width = 90, VisibleIndex = 5 });
                _gridView.Columns.Add(new GridColumn { FieldName = "SLNHAP", Caption = "SL Nhập", Width = 90, VisibleIndex = 6 });
                _gridView.Columns.Add(new GridColumn { FieldName = "SLXUAT", Caption = "SL Xuất", Width = 90, VisibleIndex = 7 });
                _gridView.Columns.Add(new GridColumn { FieldName = "SLCONLAI", Caption = "Còn lại", Width = 90, VisibleIndex = 8 });
                _gridView.Columns.Add(new GridColumn { FieldName = "Satus", Caption = "Trạng thái", Width = 80, VisibleIndex = 9 });

                // Cảnh báo trực quan nếu SLCONLAI âm (dấu hiệu lỗi CNK điển hình)
                _gridView.RowStyle += (s, e) =>
                {
                    var row = _gridView.GetDataRow(e.RowHandle);
                    if (row == null) return;
                    if (row["SLCONLAI"] != DBNull.Value && Convert.ToInt32(row["SLCONLAI"]) < 0)
                        e.Appearance.BackColor = Color.LightSalmon;
                };

                main.Controls.Add(_grid, 0, 1);
                Controls.Add(main);
            }

            private void BuildEditPanel()
            {
                _grpDieuChinh = new GroupControl
                {
                    Text = "Điều chỉnh tồn kho — dùng khi Cập Nhật Kho báo lệch dữ liệu",
                    Dock = DockStyle.Top,
                    Height = 175
                };

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 4,
                    RowCount = 4,
                    Padding = new Padding(8)
                };
                for (int i = 0; i < 4; i++)
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

                _lblPart = new LabelControl();
                _lblName = new LabelControl();
                _lblNgaySX = new LabelControl();
                _lblSlSX = new LabelControl();
                _lblSlNhap = new LabelControl();
                _lblSlXuat = new LabelControl();

                layout.Controls.Add(new LabelControl { Text = "Mã hàng:" }, 0, 0); layout.Controls.Add(_lblPart, 1, 0);
                layout.Controls.Add(new LabelControl { Text = "Tên hàng:" }, 2, 0); layout.Controls.Add(_lblName, 3, 0);
                layout.Controls.Add(new LabelControl { Text = "Ngày SX:" }, 0, 1); layout.Controls.Add(_lblNgaySX, 1, 1);
                layout.Controls.Add(new LabelControl { Text = "SL sản xuất:" }, 2, 1); layout.Controls.Add(_lblSlSX, 3, 1);
                layout.Controls.Add(new LabelControl { Text = "SL đã nhập:" }, 0, 2); layout.Controls.Add(_lblSlNhap, 1, 2);
                layout.Controls.Add(new LabelControl { Text = "SL đã xuất:" }, 2, 2); layout.Controls.Add(_lblSlXuat, 3, 2);

                layout.Controls.Add(new LabelControl { Text = "SL còn lại MỚI:", Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold) } }, 0, 3);
                _spinSlConLaiMoi = new SpinEdit
                {
                    Dock = DockStyle.Fill,
                    Properties = { MinValue = 0, MaxValue = 999999999, IsFloatValue = false }
                };
                layout.Controls.Add(_spinSlConLaiMoi, 1, 3);

                layout.Controls.Add(new LabelControl { Text = "Lý do điều chỉnh (*):" }, 2, 3);
                _txtLyDo = new TextEdit { Dock = DockStyle.Fill };
                layout.Controls.Add(_txtLyDo, 3, 3);

                _btnCapNhat = new SimpleButton
                {
                    Text = "💾 Cập nhật tồn kho",
                    Dock = DockStyle.Bottom,
                    Height = 34
                };
                _btnCapNhat.Appearance.BackColor = Color.SeaGreen;
                _btnCapNhat.Appearance.ForeColor = Color.White;
                _btnCapNhat.Click += BtnCapNhat_Click;

                _grpDieuChinh.Controls.Add(layout);
                _grpDieuChinh.Controls.Add(_btnCapNhat);
            }

            // ════════════════════════════════════════════════════════════════
            // Data — đọc qua Service, không còn ExecuteQuery / Repository trực tiếp
            // ════════════════════════════════════════════════════════════════
            private void LoadData()
            {
                DataTable dt = _lotList.Count == 0
                    ? _stockTpService.GetTonKhoHienTai()
                    : _stockTpService.GetTonKhoTheoLot(_lotList);

                _grid.DataSource = dt;

                // ✅ FIX BestFitColumns: gọi BestFit() từng cột — an toàn với
                // mọi phiên bản DevExpress (BestFitColumns() cấp GridView không
                // tồn tại như 1 method công khai trong bản đang dùng).
                foreach (GridColumn col in _gridView.Columns)
                    col.BestFit();

                if (!_isSingleLotEditMode) return;

                if (dt.Rows.Count == 0)
                {
                    _grpDieuChinh.Enabled = false;
                    XtraMessageBox.Show($"Không tìm thấy LOT [{_lotList[0]}] trong STOCKTP.",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                FillEditPanel(dt.Rows[0]);
            }

            private void FillEditPanel(DataRow row)
            {
                _lblPart.Text = row["PART"]?.ToString();
                _lblName.Text = row["NAME"]?.ToString();
                _lblNgaySX.Text = row["NGAYSX"] == DBNull.Value ? "" : Convert.ToDateTime(row["NGAYSX"]).ToString("dd/MM/yyyy");
                _lblSlSX.Text = row["SLSX"]?.ToString();
                _lblSlNhap.Text = row["SLNHAP"]?.ToString();
                _lblSlXuat.Text = row["SLXUAT"]?.ToString();

                int slConLai = row["SLCONLAI"] == DBNull.Value ? 0 : Convert.ToInt32(row["SLCONLAI"]);
                _spinSlConLaiMoi.Value = Math.Max(slConLai, 0);
            }

            // ════════════════════════════════════════════════════════════════
            // Cập nhật — gọi qua Service, ghi lịch sử qua IStockHistoryService
            // ════════════════════════════════════════════════════════════════
            private void BtnCapNhat_Click(object sender, EventArgs e)
            {
                string lot = _lotList[0];
                int slConLaiMoi = (int)_spinSlConLaiMoi.Value;
                string lyDo = _txtLyDo.Text.Trim();

                if (string.IsNullOrEmpty(lyDo))
                {
                    XtraMessageBox.Show("Vui lòng nhập lý do điều chỉnh tồn kho.",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var stock = _stockTpService.GetByLot(lot);
                if (stock == null)
                {
                    XtraMessageBox.Show($"LOT [{lot}] không còn tồn tại trong STOCKTP — có thể đã bị xoá/đổi.",
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int slConLaiCu = stock.SlConLai ?? 0;
                if (slConLaiMoi == slConLaiCu)
                {
                    XtraMessageBox.Show("Số lượng không thay đổi so với hiện tại.",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string dau = slConLaiMoi > slConLaiCu ? "+" : "";
                if (XtraMessageBox.Show(
                    $"Xác nhận điều chỉnh tồn kho LOT [{lot}]:\n" +
                    $"SLCONLAI: {slConLaiCu} → {slConLaiMoi} ({dau}{slConLaiMoi - slConLaiCu})\n" +
                    $"Lý do: {lyDo}",
                    "Xác nhận điều chỉnh", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                try
                {
                    _stockTpService.DieuChinhSlConLai(lot, slConLaiMoi);

                    _historyService.SaveHistory(
                        actionType: "DIEU_CHINH_TON_CNK_LOI",
                        itemCode: stock.Part,
                        lot: new LotInfo { LotNo = lot, Quantity = slConLaiMoi - slConLaiCu },
                        fromSlotId: null,
                        toSlotId: null,
                        performedBy: $"{Environment.UserName} | {slConLaiCu}->{slConLaiMoi} | Lý do: {lyDo}");

                    XtraMessageBox.Show("Đã cập nhật tồn kho thành công.",
                        "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show("Lỗi khi cập nhật tồn kho:\n" + ex.Message,
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // ════════════════════════════════════════════════════════════════
            // Helper: parse "'LOT1','LOT2'" hoặc "'LOT1'" -> List<string>
            // ════════════════════════════════════════════════════════════════
            private static List<string> ParseLotFilter(string raw)
            {
                var result = new List<string>();
                if (string.IsNullOrWhiteSpace(raw))
                    return result;

                foreach (var part in raw.Split(','))
                {
                    string lot = part.Trim().Trim('\'').Trim();
                    if (!string.IsNullOrEmpty(lot))
                        result.Add(lot);
                }

                return result;
            }
        }
    }

