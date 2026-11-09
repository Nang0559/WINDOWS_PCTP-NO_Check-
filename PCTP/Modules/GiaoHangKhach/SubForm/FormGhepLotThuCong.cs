using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraReports.UI;
using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.NhapKho.Repository;
using PCTP.QRCODE_HVN.Report;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.SubForm
{
    /// <summary>
    /// Ghép LOT thủ công theo từng TEM:
    ///  - Một ô quét QR duy nhất (QR tem thùng 4 phần). Mỗi lần quét xong, ô được xóa trống để chờ QR kế tiếp.
    ///  - Mỗi lần quét thêm MỘT LOT vào TEM ĐANG GHÉP (một dòng ở lưới tem). Số LOT của tem không bị cố định:
    ///    quét 2, 3, 4... LOT rồi bấm KẾT THÚC TEM (F2) để chốt tem.
    ///  - Tem khác (kể cả cùng mã hàng) là một DÒNG MỚI ở lưới tem, không thêm cột; các LOT của tem nằm ở lưới chi tiết,
    ///    cột "SL lấy" sửa trực tiếp được.
    ///  - Chỉ những tem được tích "In tem" mới được in, qua TMPLOTGHEP -> Usp_gheplotPrint -> GHEPLOT (YMVN: GHEPLOT_YMVN + Gear)
    ///    như luồng tự động (LOT1-SL1,LOT2-SL2,...).
    ///
    /// Form chỉ kiểm tra tồn và chuẩn bị dữ liệu in; không cập nhật STOCKTP.
    /// </summary>
    public sealed class FormGhepLotThuCong : XtraForm
    {
        private const int MinLots = 2;
        private const int MaxLots = 10;

        // Giới hạn của Usp_gheplotPrint: @_LOT NVARCHAR(25) cho mỗi token "LOT-SL",
        // @_LOTTMP NVARCHAR(600) cho cả chuỗi, @_QRCODE NVARCHAR(256) cho "chuỗi:mã hàng:ngày:tổng SL",
        // @_ReceiptItemCode NVARCHAR(24). Vượt giới hạn thì SQL cắt cụt âm thầm => in sai tổng SL / QR.
        private const int SpMaxTokenLength = 25;
        private const int SpMaxPayloadLength = 600;
        private const int SpMaxQrLength = 256;
        private const int SpMaxItemLength = 24;

        private readonly StockTpRepository _stockRepository;
        private readonly InPhieuService _printService;
        private readonly SQLPROVIDER _sql;
        private readonly DataTable _temTable;   // mỗi dòng = một TEM ghép
        private readonly DataTable _lineTable;  // mỗi dòng = một LOT thuộc một tem
        private readonly DataView _lineData;
        // Quy cách đóng gói (B20Item.MinCloseQty) theo mã hàng; tránh truy vấn lại mỗi lần quét/sửa.
        private readonly Dictionary<string, int> _packCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private TextEdit _txtQr;
        private LabelControl _lblCurrent;
        private LabelControl _lblStatus;
        private GridControl _temGrid;
        private GridView _temView;
        private GridControl _lineGrid;
        private GridView _lineView;
        private SimpleButton _btnRead;
        private SimpleButton _btnClose;
        private SimpleButton _btnReopen;
        private SimpleButton _btnPrint;

        private int _temSeq;
        private int _currentTemId; // 0 = chưa có tem đang ghép

        public FormGhepLotThuCong()
        {
            var sql = new SQLPROVIDER();
            _sql = sql;
            var executor = new PhieuSqlExecutor(sql);
            var unitOfWork = new UnitOfWork(sql);

            _stockRepository = new StockTpRepository(executor, unitOfWork);
            var sqlRepository = new SqlRepository(executor, unitOfWork);
            // InGhepLot chỉ dùng ISqlRepository nên tái sử dụng đúng service in hiện hành.
            _printService = new InPhieuService(null, null, sqlRepository, null, null, null);

            _temTable = CreateTemTable();
            _lineTable = CreateLineTable();
            _lineData = new DataView(_lineTable) { RowFilter = "TEMID = 0" };
            InitializeUi();
        }

        private static DataTable CreateTemTable()
        {
            var table = new DataTable("GhepLotTem");
            table.Columns.Add("TEMID", typeof(int));
            table.Columns.Add("IN", typeof(bool));
            table.Columns.Add("MAHANG", typeof(string));
            table.Columns.Add("GEAR", typeof(string));
            table.Columns.Add("SOLOT", typeof(int));
            table.Columns.Add("TONGSL", typeof(int));
            table.Columns.Add("TRANGTHAI", typeof(string));
            table.Columns.Add("LOTGHEP", typeof(string));
            table.Columns.Add("OPEN", typeof(bool));
            return table;
        }

        private static DataTable CreateLineTable()
        {
            var table = new DataTable("GhepLotLine");
            table.Columns.Add("TEMID", typeof(int));
            table.Columns.Add("LOT", typeof(string));
            table.Columns.Add("MAHANG", typeof(string));
            table.Columns.Add("GEAR", typeof(string));
            table.Columns.Add("TON", typeof(int));
            table.Columns.Add("QRSL", typeof(int));
            table.Columns.Add("SL", typeof(int));
            table.Columns.Add("SEL", typeof(object));
            return table;
        }

        private void InitializeUi()
        {
            Text = "GHÉP LOT THỦ CÔNG";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(1120, 720);
            Size = new Size(1420, 900);
            Font = new Font("Tahoma", 9F);
            KeyPreview = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 10, 10, 14),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            Controls.Add(root);

            // --- Ô quét QR duy nhất + điều khiển tem
            var scanGroup = CreateGroup("QUÉT QR TEM THÙNG (4 PHẦN) — MỖI LẦN QUÉT THÊM MỘT LOT VÀO TEM ĐANG GHÉP");
            var scan = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                ColumnCount = 4,
                RowCount = 2
            };
            scan.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scan.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            scan.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            scan.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            scan.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            scan.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _txtQr = new TextEdit
            {
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 11F, FontStyle.Bold),
                Properties =
                {
                    NullValuePrompt = "Quét QR tem thùng 4 phần của LOT cần ghép rồi nhấn Enter...",
                    NullValuePromptShowForEmptyValue = true
                }
            };
            _txtQr.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.Handled = true;
                e.SuppressKeyPress = true;
                ReadLot();
            };
            _btnRead = new SimpleButton { Text = "ĐỌC LOT", Dock = DockStyle.Fill };
            _btnRead.Click += (s, e) => ReadLot();
            _btnClose = new SimpleButton { Text = "KẾT THÚC TEM (F2)", Dock = DockStyle.Fill, Enabled = false };
            _btnClose.Click += (s, e) => CloseCurrentTem();
            _btnReopen = new SimpleButton { Text = "MỞ LẠI TEM", Dock = DockStyle.Fill, Enabled = false };
            _btnReopen.Click += (s, e) => ReopenFocusedTem();
            _lblCurrent = new LabelControl
            {
                Text = "Chưa có tem đang ghép. Quét QR của LOT đầu tiên để bắt đầu tem mới.",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            _lblCurrent.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            _lblCurrent.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            scan.Controls.Add(_txtQr, 0, 0);
            scan.Controls.Add(_btnRead, 1, 0);
            scan.Controls.Add(_btnClose, 2, 0);
            scan.Controls.Add(_btnReopen, 3, 0);
            scan.Controls.Add(_lblCurrent, 0, 1);
            scan.SetColumnSpan(_lblCurrent, 4);
            scanGroup.Controls.Add(scan);
            root.Controls.Add(scanGroup, 0, 0);

            // --- Lưới tem (mỗi tem một dòng) với cột "In tem"
            var temGroup = CreateGroup("DANH SÁCH TEM GHÉP — TÍCH \"IN TEM\" Ở CÁC TEM CẦN IN (MỖI TEM LÀ MỘT DÒNG)");
            _temGrid = new GridControl { Dock = DockStyle.Fill, DataSource = _temTable };
            _temView = new GridView(_temGrid);
            ConfigureView(_temGrid, _temView);
            var checkEdit = new RepositoryItemCheckEdit();
            checkEdit.EditValueChanged += (s, e) => _temView.PostEditor();
            _temGrid.RepositoryItems.Add(checkEdit);
            AddColumn(_temView, "IN", "In tem", 70, true, checkEdit);
            AddColumn(_temView, "TEMID", "Tem", 60);
            AddColumn(_temView, "MAHANG", "Mã hàng", 150);
            AddColumn(_temView, "GEAR", "Gear (YMVN)", 110);
            AddColumn(_temView, "SOLOT", "Số LOT", 70);
            AddColumn(_temView, "TONGSL", "Tổng SL in", 90);
            AddColumn(_temView, "TRANGTHAI", "Trạng thái", 420);
            AddColumn(_temView, "LOTGHEP", "Dữ liệu LOT ghép đưa đi in", 560);
            _temView.OptionsSelection.MultiSelect = false;
            _temView.FocusedRowChanged += (s, e) => OnFocusedTemChanged();
            _temView.RowStyle += (s, e) =>
            {
                if (e.RowHandle < 0) return;
                DataRow row = _temView.GetDataRow(e.RowHandle);
                if (row == null) return;
                if (Convert.ToBoolean(row["OPEN"])) e.Appearance.BackColor = Color.FromArgb(255, 248, 205);
                else if (ReadText(row, "TRANGTHAI").StartsWith("Lỗi", StringComparison.Ordinal)) e.Appearance.BackColor = Color.FromArgb(255, 225, 225);
                else if (Convert.ToBoolean(row["IN"])) e.Appearance.BackColor = Color.FromArgb(225, 245, 225);
            };
            _temView.ValidatingEditor += (s, e) =>
            {
                if (_temView.FocusedColumn == null || _temView.FocusedColumn.FieldName != "IN") return;
                if (!(e.Value is bool) || !(bool)e.Value) return;
                DataRow row = _temView.GetFocusedDataRow();
                string reason = row == null ? "Không xác định tem." : GetPrintBlockReason(row);
                if (reason != null)
                {
                    e.Valid = false;
                    e.ErrorText = reason;
                }
            };
            temGroup.Controls.Add(_temGrid);
            root.Controls.Add(temGroup, 0, 1);

            // --- Lưới chi tiết LOT của tem đang chọn (sửa "SL lấy" tại đây)
            var lineGroup = CreateGroup("CÁC LOT CỦA TEM ĐANG CHỌN — SỬA CỘT \"SL LẤY\" ĐỂ CHIA SỐ LƯỢNG (KHÔNG VƯỢT TỒN STOCKTP VÀ QUY CÁCH ĐÓNG GÓI)");
            _lineGrid = new GridControl { Dock = DockStyle.Fill, DataSource = _lineData };
            _lineView = new GridView(_lineGrid);
            ConfigureView(_lineGrid, _lineView);
            var spin = new RepositoryItemSpinEdit();
            spin.IsFloatValue = false;
            spin.MinValue = 1;
            spin.MaxValue = 100000000;
            spin.Increment = 1;
            _lineGrid.RepositoryItems.Add(spin);
            AddColumn(_lineView, "LOT", "LOT", 230);
            AddColumn(_lineView, "MAHANG", "Mã hàng", 150);
            AddColumn(_lineView, "GEAR", "Gear (YMVN)", 110);
            AddColumn(_lineView, "TON", "Tồn STOCKTP", 110);
            AddColumn(_lineView, "QRSL", "SL trên QR", 100);
            AddColumn(_lineView, "SL", "SL lấy (sửa)", 120, true, spin);
            _lineView.ValidatingEditor += (s, e) =>
            {
                if (_lineView.FocusedColumn == null || _lineView.FocusedColumn.FieldName != "SL") return;
                DataRow row = _lineView.GetFocusedDataRow();
                if (row == null) return;
                int qty = SafeInt(e.Value);
                int available = SafeInt(row["TON"]);
                if (qty <= 0 || qty > available)
                {
                    e.Valid = false;
                    e.ErrorText = "Số lượng phải lớn hơn 0 và không vượt tồn STOCKTP (" + available + ").";
                    return;
                }

                // Tổng SL in của tem không được vượt quy cách đóng gói (B20Item.MinCloseQty).
                string packError = CheckPackLimit(SafeInt(row["TEMID"]), row, qty);
                if (packError != null)
                {
                    e.Valid = false;
                    e.ErrorText = packError;
                }
            };
            _lineView.CellValueChanged += (s, e) =>
            {
                if (e.Column == null || e.Column.FieldName != "SL") return;
                DataRow row = _lineView.GetDataRow(e.RowHandle);
                if (row != null) RecomputeTem(SafeInt(row["TEMID"]));
            };
            lineGroup.Controls.Add(_lineGrid);
            root.Controls.Add(lineGroup, 0, 2);

            // --- Chân trang
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 760));
            _lblStatus = new LabelControl
            {
                Text = "Quét QR từng LOT; bấm KẾT THÚC TEM khi đủ số LOT cần ghép; tích \"In tem\" rồi bấm IN.",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            _lblStatus.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            _lblStatus.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            footer.Controls.Add(_lblStatus, 0, 0);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 6)
            };
            _btnPrint = new SimpleButton { Text = "IN CÁC TEM ĐÃ CHỌN", Width = 190, Height = 36, Enabled = false };
            var btnClear = new SimpleButton { Text = "XÓA TẤT CẢ", Width = 110, Height = 36 };
            var btnRemoveTem = new SimpleButton { Text = "XÓA TEM", Width = 100, Height = 36 };
            var btnRemoveLot = new SimpleButton { Text = "XÓA LOT", Width = 100, Height = 36 };
            var btnCheckAll = new SimpleButton { Text = "TÍCH/BỎ TÍCH TẤT CẢ", Width = 170, Height = 36 };
            _btnPrint.Click += (s, e) => PrintSelectedTems();
            btnClear.Click += (s, e) => ClearAll();
            btnRemoveTem.Click += (s, e) => RemoveFocusedTem();
            btnRemoveLot.Click += (s, e) => RemoveFocusedLine();
            btnCheckAll.Click += (s, e) => ToggleCheckAll();
            actions.Controls.Add(_btnPrint);
            actions.Controls.Add(btnClear);
            actions.Controls.Add(btnRemoveTem);
            actions.Controls.Add(btnRemoveLot);
            actions.Controls.Add(btnCheckAll);
            footer.Controls.Add(actions, 1, 0);
            root.Controls.Add(footer, 0, 3);
        }

        private static GroupControl CreateGroup(string title)
        {
            var group = new GroupControl { Text = title, Dock = DockStyle.Fill };
            group.AppearanceCaption.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            group.AppearanceCaption.Options.UseFont = true;
            return group;
        }

        private static void ConfigureView(GridControl grid, GridView view)
        {
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.AutoPopulateColumns = false;
            view.OptionsBehavior.Editable = true; // chỉ các cột được bật AllowEdit mới sửa được
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.ShowIndicator = true;
        }

        private static void AddColumn(GridView view, string field, string caption, int width,
            bool editable = false, RepositoryItem editor = null)
        {
            var column = new GridColumn
            {
                FieldName = field,
                Caption = caption,
                Visible = true,
                VisibleIndex = view.Columns.Count,
                Width = width
            };
            column.OptionsColumn.AllowEdit = editable;
            column.OptionsColumn.ReadOnly = !editable;
            if (editor != null) column.ColumnEdit = editor;
            view.Columns.Add(column);
        }

        // ------------------------------------------------------------------ kích thước cửa sổ
        // Form 1420x900 lớn hơn vùng làm việc của màn hình nhỏ/DPI cao nên phần dưới (thanh trạng thái, nút)
        // bị tràn ra ngoài màn hình. Ép form nằm gọn trong WorkingArea (không che taskbar).
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int width = Math.Min(Width, area.Width - 16);
            int height = Math.Min(Height, area.Height - 16);
            MinimumSize = new Size(Math.Min(MinimumSize.Width, width), Math.Min(MinimumSize.Height, height));
            Size = new Size(width, height);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
            int top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
            if (left != Left || top != Top) Location = new Point(left, top);
            _txtQr.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F2)
            {
                CloseCurrentTem();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ đọc QR và thêm LOT vào tem đang ghép
        private void ReadLot()
        {
            try
            {
                string rawQr = (_txtQr.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(rawQr))
                    throw new InvalidOperationException("Vui lòng quét QR trước khi đọc LOT.");

                var fields = rawQr.Split(':');
                if (fields.Length != 4)
                    throw new FormatException("Ghép LOT thủ công chỉ nhận QR tem thùng đúng 4 phần: LOT: Mã hàng: Ngày SX: Số lượng.");

                var qr = QRCodeParser.ParseQRCode(rawQr);
                string stockKey = LotNoHelper.GetStockTpKey(qr.LotNo);
                if (string.IsNullOrWhiteSpace(stockKey))
                    throw new FormatException("Không xác định được khóa LOT trong QR.");

                // Tra cứu bằng khóa STOCKTP chuẩn (không dùng nguyên token QR 28 ký tự).
                DataTable stockRows = _stockRepository.GetTonKhoTheoLot(new List<string> { stockKey });
                var matchingRows = stockRows.AsEnumerable()
                    .Where(row => NormalizeItem(ReadText(row, "PART")) == NormalizeItem(qr.ItemCode))
                    .ToList();

                if (matchingRows.Count == 0)
                    throw new InvalidOperationException("Không tìm thấy LOT này trong STOCKTP với đúng mã hàng trên QR.");
                if (matchingRows.Count > 1)
                    throw new InvalidOperationException("Khóa LOT này đang khớp nhiều dòng STOCKTP. Không thể tự chọn LOT; cần xử lý dữ liệu trùng/LOT cũ trước.");

                DataRow stock = matchingRows[0];
                string storedLot = ReadText(stock, "LOT");
                int available = SafeInt(stock["SLCONLAI"]);
                if (string.IsNullOrWhiteSpace(storedLot))
                    throw new InvalidOperationException("Dòng STOCKTP không có LOT hợp lệ.");
                if (available <= 0)
                    throw new InvalidOperationException("LOT đã hết tồn trong STOCKTP; không cho phép ghép.");

                var selection = new StockLotSelection
                {
                    LotNo = storedLot,
                    ItemCode = ReadText(stock, "PART"),
                    ItemName = ReadText(stock, "NAME"),
                    Available = available,
                    QrQuantity = qr.Quantity,
                    RawQr = rawQr
                };

                // Hàng YMVN: Gear là ký tự thứ 13 của LOT (B20Gear.Code). LOT khác Gear không được ghép chung
                // vì luồng ghép tự động cũng cấp phát riêng theo từng Gear.
                selection.IsYmvn = IsYmvnItem(selection.ItemCode);
                if (selection.IsYmvn)
                {
                    int gearCode;
                    if (!TryGetGearCode(storedLot, out gearCode))
                        throw new InvalidOperationException("LOT YMVN " + storedLot + " không có ký tự Gear hợp lệ ở vị trí thứ 13.");
                    selection.GearCode = gearCode;
                    selection.GearName = GetGearName(gearCode);
                }

                AddLotToCurrentTem(selection);

                // Xóa ô để chờ QR kế tiếp.
                _txtQr.EditValue = null;
                _txtQr.Focus();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Không đạt validation — LOT chưa được thêm vào tem.";
                XtraMessageBox.Show(ex.Message, "Không thể đọc LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtQr.Focus();
                _txtQr.SelectAll(); // quét lại sẽ ghi đè mã lỗi
            }
        }

        private void AddLotToCurrentTem(StockLotSelection selection)
        {
            DataRow tem = FindTem(_currentTemId);
            List<StockLotSelection> existing = tem == null
                ? new List<StockLotSelection>()
                : GetLines(_currentTemId).Select(x => (StockLotSelection)x["SEL"]).ToList();

            if (existing.Count > 0)
            {
                if (!string.Equals(NormalizeItem(existing[0].ItemCode), NormalizeItem(selection.ItemCode), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Tem đang ghép là mã hàng " + existing[0].ItemCode
                        + ", còn LOT vừa quét thuộc mã hàng " + selection.ItemCode
                        + ". Bấm KẾT THÚC TEM (F2) để chốt tem hiện tại rồi quét lại để bắt đầu tem mới.");
                string key = LotNoHelper.GetStockTpKey(selection.LotNo);
                if (existing.Any(x => string.Equals(LotNoHelper.GetStockTpKey(x.LotNo), key, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("LOT " + selection.LotNo + " đã có trong tem đang ghép. Mỗi LOT chỉ ghép một lần trong một tem.");
                if (existing.Count >= MaxLots)
                    throw new InvalidOperationException("Một tem ghép tối đa " + MaxLots + " LOT. Bấm KẾT THÚC TEM (F2) để chốt tem.");
                var candidate = new List<StockLotSelection>(existing) { selection };
                CheckGearSet(candidate);
            }

            // Quy cách đóng gói: tổng SL của tem không được vượt B20Item.MinCloseQty.
            int pack = GetPackQty(selection.ItemCode);
            if (pack <= 0)
                throw new InvalidOperationException("Mã hàng " + selection.ItemCode
                    + " chưa khai báo quy cách đóng gói (B20Item.MinCloseQty), không thể ghép LOT.");
            int usedQty = tem == null ? 0 : GetLines(_currentTemId).Sum(x => SafeInt(x["SL"]));
            int remaining = pack - usedQty;
            if (remaining <= 0)
                throw new InvalidOperationException("Tem #" + _currentTemId + " đã đủ quy cách đóng gói (" + pack
                    + "). Bấm KẾT THÚC TEM (F2) để chốt tem rồi quét LOT cho tem mới.");

            if (tem == null)
            {
                _temSeq++;
                _currentTemId = _temSeq;
                tem = _temTable.NewRow();
                tem["TEMID"] = _currentTemId;
                tem["IN"] = false;
                tem["MAHANG"] = selection.ItemCode;
                tem["GEAR"] = string.Empty;
                tem["SOLOT"] = 0;
                tem["TONGSL"] = 0;
                tem["TRANGTHAI"] = string.Empty;
                tem["LOTGHEP"] = string.Empty;
                tem["OPEN"] = true;
                _temTable.Rows.Add(tem);
            }

            // Số lượng mặc định = số lượng trên QR (không vượt tồn và không vượt phần còn lại của quy cách đóng gói);
            // sửa lại ở lưới chi tiết nếu chỉ lấy một phần.
            int defaultQty = Math.Max(1, Math.Min(Math.Min(selection.QrQuantity, selection.Available), remaining));
            DataRow line = _lineTable.NewRow();
            line["TEMID"] = _currentTemId;
            line["LOT"] = selection.LotNo;
            line["MAHANG"] = selection.ItemCode;
            line["GEAR"] = selection.IsYmvn ? FormatGear(selection) : string.Empty;
            line["TON"] = selection.Available;
            line["QRSL"] = selection.QrQuantity;
            line["SL"] = defaultQty;
            line["SEL"] = selection;
            _lineTable.Rows.Add(line);

            RecomputeTem(_currentTemId);
            FocusTem(_currentTemId);
            _lblStatus.Text = "Đã thêm LOT " + selection.LotNo + " vào tem #" + _currentTemId
                + ". Quét tiếp LOT khác hoặc bấm KẾT THÚC TEM (F2).";
        }

        // ------------------------------------------------------------------ tem
        private DataRow FindTem(int temId)
        {
            if (temId <= 0) return null;
            DataRow[] rows = _temTable.Select("TEMID = " + temId.ToString(CultureInfo.InvariantCulture));
            return rows.Length == 0 ? null : rows[0];
        }

        private List<DataRow> GetLines(int temId)
        {
            return _lineTable.Select("TEMID = " + temId.ToString(CultureInfo.InvariantCulture)).ToList();
        }

        private void FocusTem(int temId)
        {
            for (int i = 0; i < _temView.DataRowCount; i++)
            {
                DataRow row = _temView.GetDataRow(i);
                if (row != null && SafeInt(row["TEMID"]) == temId)
                {
                    _temView.FocusedRowHandle = i;
                    break;
                }
            }
            OnFocusedTemChanged();
        }

        private void OnFocusedTemChanged()
        {
            DataRow row = _temView.GetFocusedDataRow();
            int id = row == null ? 0 : SafeInt(row["TEMID"]);
            _lineData.RowFilter = "TEMID = " + id.ToString(CultureInfo.InvariantCulture);
            UpdateButtons();
        }

        /// <summary>Tính lại số LOT, tổng SL, payload và trạng thái của một tem sau khi thêm/sửa/xóa LOT.</summary>
        private void RecomputeTem(int temId)
        {
            DataRow tem = FindTem(temId);
            if (tem == null) return;

            List<DataRow> lines = GetLines(temId);
            var lots = lines.Select(x => (StockLotSelection)x["SEL"]).ToList();
            var quantities = lines.Select(x => SafeInt(x["SL"])).ToList();
            bool open = Convert.ToBoolean(tem["OPEN"]);

            tem["SOLOT"] = lines.Count;
            tem["TONGSL"] = quantities.Sum();
            tem["LOTGHEP"] = lots.Count == 0 ? string.Empty : BuildPayload(lots, quantities);
            tem["MAHANG"] = lots.Count == 0 ? string.Empty : lots[0].ItemCode;
            tem["GEAR"] = lots.Count > 0 && lots[0].IsYmvn ? FormatGear(lots[0]) : string.Empty;

            if (open)
            {
                tem["TRANGTHAI"] = "Đang ghép — " + lines.Count + " LOT" + (lines.Count < MinLots ? " (cần ít nhất " + MinLots + " LOT)" : string.Empty);
                tem["IN"] = false;
            }
            else
            {
                string error = EvaluateTem(temId);
                tem["TRANGTHAI"] = error == null ? "Hợp lệ" : "Lỗi: " + error;
                if (error != null) tem["IN"] = false;
            }
            UpdateButtons();
        }

        /// <summary>Trả về null nếu tem hợp lệ để in, ngược lại là lý do.</summary>
        private string EvaluateTem(int temId)
        {
            List<DataRow> lines = GetLines(temId);
            try
            {
                var lots = lines.Select(x => (StockLotSelection)x["SEL"]).ToList();
                ValidateSet(
                    lots,
                    lines.Select(x => SafeInt(x["SL"])).ToList(),
                    lots.Count == 0 ? 0 : GetPackQty(lots[0].ItemCode));
                return null;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        private string GetPrintBlockReason(DataRow tem)
        {
            if (Convert.ToBoolean(tem["OPEN"]))
                return "Tem #" + tem["TEMID"] + " đang ghép. Bấm KẾT THÚC TEM (F2) trước khi tích in.";
            return EvaluateTem(SafeInt(tem["TEMID"]));
        }

        private void CloseCurrentTem()
        {
            DataRow tem = FindTem(_currentTemId);
            if (tem == null)
            {
                _lblStatus.Text = "Chưa có tem đang ghép. Quét QR để bắt đầu tem mới.";
                _txtQr.Focus();
                return;
            }

            int temId = _currentTemId;
            if (GetLines(temId).Count < MinLots)
            {
                if (XtraMessageBox.Show("Tem #" + temId + " mới có " + GetLines(temId).Count + " LOT, chưa đủ " + MinLots
                    + " LOT để ghép.\r\nXóa tem này và bắt đầu lại?", "Kết thúc tem",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    DeleteTem(temId);
                    _lblStatus.Text = "Đã xóa tem #" + temId + " (chưa đủ LOT).";
                }
                _txtQr.Focus();
                return;
            }

            tem["OPEN"] = false;
            _currentTemId = 0;
            RecomputeTem(temId);
            string error = EvaluateTem(temId);
            if (error == null)
            {
                tem["IN"] = true; // tem hợp lệ mặc định được tích in; bỏ tích nếu chưa muốn in
                _lblStatus.Text = "Đã kết thúc tem #" + temId + ". Quét QR LOT tiếp theo để bắt đầu tem mới.";
            }
            else
            {
                _lblStatus.Text = "Tem #" + temId + " chưa hợp lệ: " + error + " Sửa ở lưới chi tiết hoặc bấm MỞ LẠI TEM.";
            }
            UpdateButtons();
            _txtQr.Focus();
        }

        private void ReopenFocusedTem()
        {
            DataRow tem = _temView.GetFocusedDataRow();
            if (tem == null) return;
            if (_currentTemId != 0)
            {
                XtraMessageBox.Show("Đang có tem #" + _currentTemId + " đang ghép. Bấm KẾT THÚC TEM (F2) trước khi mở lại tem khác.",
                    "Mở lại tem", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int temId = SafeInt(tem["TEMID"]);
            tem["OPEN"] = true;
            tem["IN"] = false;
            _currentTemId = temId;
            RecomputeTem(temId);
            _lblStatus.Text = "Đã mở lại tem #" + temId + ". Quét QR để thêm LOT hoặc sửa số lượng rồi KẾT THÚC TEM.";
            _txtQr.Focus();
        }

        private void DeleteTem(int temId)
        {
            foreach (DataRow line in GetLines(temId)) _lineTable.Rows.Remove(line);
            DataRow tem = FindTem(temId);
            if (tem != null) _temTable.Rows.Remove(tem);
            if (_currentTemId == temId) _currentTemId = 0;
            OnFocusedTemChanged();
        }

        private void RemoveFocusedTem()
        {
            DataRow tem = _temView.GetFocusedDataRow();
            if (tem == null) return;
            int temId = SafeInt(tem["TEMID"]);
            if (XtraMessageBox.Show("Xóa tem #" + temId + " cùng các LOT của tem?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            DeleteTem(temId);
            _lblStatus.Text = "Đã xóa tem #" + temId + ".";
        }

        private void RemoveFocusedLine()
        {
            DataRow line = _lineView.GetFocusedDataRow();
            if (line == null) return;
            int temId = SafeInt(line["TEMID"]);
            _lineTable.Rows.Remove(line);
            if (GetLines(temId).Count == 0) DeleteTem(temId);
            else RecomputeTem(temId);
            _lblStatus.Text = "Đã xóa LOT khỏi tem #" + temId + ".";
        }

        private void ClearAll()
        {
            if (_temTable.Rows.Count == 0) return;
            if (XtraMessageBox.Show("Xóa toàn bộ các tem ghép đang lập?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _lineTable.Clear();
            _temTable.Clear();
            _currentTemId = 0;
            OnFocusedTemChanged();
            _lblStatus.Text = "Đã xóa tất cả tem.";
            _txtQr.Focus();
        }

        private void ToggleCheckAll()
        {
            var eligible = _temTable.AsEnumerable()
                .Where(r => !Convert.ToBoolean(r["OPEN"]) && EvaluateTem(SafeInt(r["TEMID"])) == null)
                .ToList();
            if (eligible.Count == 0) return;
            bool setTo = eligible.Any(r => !Convert.ToBoolean(r["IN"]));
            foreach (DataRow row in eligible) row["IN"] = setTo;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (_btnClose == null || _btnReopen == null || _btnPrint == null || _lblCurrent == null) return;
            _btnClose.Enabled = _currentTemId != 0;
            DataRow focused = _temView == null ? null : _temView.GetFocusedDataRow();
            _btnReopen.Enabled = _currentTemId == 0 && focused != null && !Convert.ToBoolean(focused["OPEN"]);
            _btnPrint.Enabled = _temTable.Rows.Count > 0;

            DataRow current = FindTem(_currentTemId);
            _lblCurrent.Text = current == null
                ? "Chưa có tem đang ghép. Quét QR của LOT đầu tiên để bắt đầu tem mới."
                : "Tem đang ghép #" + _currentTemId + " — mã hàng " + ReadText(current, "MAHANG")
                    + " — " + SafeInt(current["SOLOT"]) + " LOT — tổng SL " + SafeInt(current["TONGSL"])
                    + ". Quét tiếp LOT hoặc bấm KẾT THÚC TEM (F2).";
        }

        // ------------------------------------------------------------------ payload và kiểm tra
        private static string BuildPayload(IList<StockLotSelection> lots, IList<int> quantities)
        {
            var tokens = new List<string>(lots.Count);
            for (int i = 0; i < lots.Count; i++)
                tokens.Add(lots[i].LotNo + "-" + quantities[i].ToString(CultureInfo.InvariantCulture));
            return string.Join(",", tokens);
        }

        private static IEnumerable<KeyValuePair<string, decimal>> ParsePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return new List<KeyValuePair<string, decimal>>();
            // Dùng đúng bộ tách LOT ghép dùng chung toàn hệ thống (LOT-SL, ngăn bởi dấu phẩy).
            return PCTP.Common.LotCodeHelper.ParseCompositeLot(payload);
        }

        private static void ValidateSet(IList<StockLotSelection> lots, IList<int> quantities, int packQty)
        {
            if (lots == null || lots.Count < MinLots)
                throw new InvalidOperationException("Phải ghép từ " + MinLots + " LOT trở lên.");
            if (lots.Count > MaxLots)
                throw new InvalidOperationException("Tối đa " + MaxLots + " LOT cho một tem.");

            string firstItem = NormalizeItem(lots[0].ItemCode);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lots.Count; i++)
            {
                StockLotSelection lot = lots[i];
                if (lot == null)
                    throw new InvalidOperationException("Thiếu dữ liệu LOT của tem.");
                if (!string.Equals(NormalizeItem(lot.ItemCode), firstItem, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Tất cả LOT phải cùng mã hàng. LOT 1: " + lots[0].ItemCode
                        + "; LOT " + (i + 1) + ": " + lot.ItemCode + ".");
                if (!keys.Add(LotNoHelper.GetStockTpKey(lot.LotNo)))
                    throw new InvalidOperationException("Các LOT phải khác LOTNO. LOT " + (i + 1) + " bị trùng với một LOT khác.");
                if (quantities[i] <= 0 || quantities[i] > lot.Available)
                    throw new InvalidOperationException("Số lượng lấy từ LOT " + lot.LotNo
                        + " phải lớn hơn 0 và không vượt tồn STOCKTP (" + lot.Available + ").");
            }

            if (packQty <= 0)
                throw new InvalidOperationException("Mã hàng " + lots[0].ItemCode
                    + " chưa khai báo quy cách đóng gói (B20Item.MinCloseQty).");
            if (quantities.Sum() > packQty)
                throw new InvalidOperationException("Tổng SL in (" + quantities.Sum()
                    + ") vượt quy cách đóng gói của mã hàng " + lots[0].ItemCode + " (" + packQty + ").");

            CheckGearSet(lots);
            CheckPrintLimits(lots[0].ItemCode, BuildPayload(lots, quantities), quantities.Sum());
        }

        /// <summary>Quy cách đóng gói của mã hàng = B20Item.MinCloseQty (0 nếu chưa khai báo).</summary>
        private int GetPackQty(string itemCode)
        {
            string key = NormalizeItem(itemCode);
            if (string.IsNullOrEmpty(key)) return 0;
            int cached;
            if (_packCache.TryGetValue(key, out cached)) return cached;

            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT TOP 1 CAST(ISNULL(MinCloseQty, 0) AS INT) FROM B20Item WHERE REPLACE(Code, '-', '') = REPLACE(N'"
                + Esc(itemCode) + "', '-', '')");
            int pack;
            if (!int.TryParse((raw ?? string.Empty).Trim(), out pack) || pack < 0) pack = 0;
            _packCache[key] = pack;
            return pack;
        }

        /// <summary>Kiểm tra tổng SL của tem nếu dòng <paramref name="row"/> đổi thành <paramref name="newQty"/>. Null = hợp lệ.</summary>
        private string CheckPackLimit(int temId, DataRow row, int newQty)
        {
            int pack = GetPackQty(ReadText(row, "MAHANG"));
            if (pack <= 0)
                return "Mã hàng " + ReadText(row, "MAHANG") + " chưa khai báo quy cách đóng gói (B20Item.MinCloseQty).";
            int others = GetLines(temId).Where(x => !ReferenceEquals(x, row)).Sum(x => SafeInt(x["SL"]));
            if (others + newQty > pack)
                return "Tổng SL in của tem không được vượt quy cách đóng gói (" + pack + "). Tối đa cho LOT này: "
                    + Math.Max(0, pack - others) + ".";
            return null;
        }

        private static string Esc(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private bool IsYmvnItem(string itemCode)
        {
            // Cùng điều kiện với UF_TACHLOT: hàng thuộc khách YMVN (CustomerCode 0100002) theo B20ItemQuyCach.
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT COUNT(*) FROM B20ItemQuyCach WHERE REPLACE(ItemCode, '-', '') = REPLACE(N'"
                + Esc(itemCode) + "', '-', '') AND CustomerCode = '0100002'");
            int count;
            return int.TryParse((raw ?? string.Empty).Trim(), out count) && count > 0;
        }

        private string GetGearName(int gearCode)
        {
            // Code 0 là dòng hợp lệ có Name rỗng (không phân loại Gear).
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT ISNULL(Name,'') FROM B20Gear WHERE Code = " + gearCode.ToString(CultureInfo.InvariantCulture));
            return (raw ?? string.Empty).Trim();
        }

        private static bool TryGetGearCode(string lot, out int gearCode)
        {
            gearCode = 0;
            string text = (lot ?? string.Empty).Trim();
            if (text.Length < 13 || !char.IsDigit(text[12])) return false;
            gearCode = text[12] - '0';
            return true;
        }

        private static string FormatGear(StockLotSelection lot)
        {
            return string.IsNullOrWhiteSpace(lot.GearName)
                ? "(không Gear, mã " + lot.GearCode + ")"
                : lot.GearName + " (mã " + lot.GearCode + ")";
        }

        private static void CheckGearSet(IList<StockLotSelection> lots)
        {
            if (lots == null || lots.Count == 0 || !lots.Any(x => x != null && x.IsYmvn)) return;
            if (lots.Any(x => x != null && !x.IsYmvn))
                throw new InvalidOperationException("Không ghép chung LOT YMVN (có Gear) với LOT của khách khác.");

            StockLotSelection first = lots[0];
            for (int i = 1; i < lots.Count; i++)
            {
                if (lots[i].GearCode != first.GearCode)
                    throw new InvalidOperationException("YMVN: các LOT ghép phải cùng Gear. LOT 1: Gear "
                        + FormatGear(first) + "; LOT " + (i + 1) + ": Gear " + FormatGear(lots[i]) + ".");
            }
        }

        /// <summary>
        /// Chặn trước các trường hợp Usp_gheplotPrint sẽ cắt cụt dữ liệu (token LOT-SL, chuỗi ghép, chuỗi QR).
        /// </summary>
        private static void CheckPrintLimits(string itemCode, string payload, int totalQuantity)
        {
            string item = (itemCode ?? string.Empty).Trim();
            if (item.Length > SpMaxItemLength)
                throw new InvalidOperationException("Mã hàng dài " + item.Length + " ký tự, vượt giới hạn " + SpMaxItemLength + " của chức năng in ghép LOT.");

            foreach (string token in payload.Split(','))
            {
                if (token.Length > SpMaxTokenLength)
                    throw new InvalidOperationException("Mục '" + token + "' dài " + token.Length + " ký tự, vượt giới hạn "
                        + SpMaxTokenLength + " (LOT-SL) của chức năng in ghép LOT. Số lượng quá nhiều chữ số.");
            }

            if (payload.Length > SpMaxPayloadLength)
                throw new InvalidOperationException("Chuỗi LOT ghép dài " + payload.Length + " ký tự, vượt giới hạn " + SpMaxPayloadLength + ". Giảm số LOT.");

            // QR in ra: "<chuỗi LOT>:<mã hàng>:<dd/MM/yy>:<tổng SL>"
            int qrLength = payload.Length + 1 + item.Length + 1 + 8 + 1
                + totalQuantity.ToString(CultureInfo.InvariantCulture).Length;
            if (qrLength > SpMaxQrLength)
                throw new InvalidOperationException("Chuỗi QR của tem ghép dài " + qrLength + " ký tự, vượt giới hạn "
                    + SpMaxQrLength + ". Giảm số LOT ghép trong một tem.");
        }

        // ------------------------------------------------------------------ in
        private void PrintSelectedTems()
        {
            List<DataRow> picked = _temTable.AsEnumerable()
                .Where(r => Convert.ToBoolean(r["IN"]))
                .ToList();
            if (picked.Count == 0)
            {
                XtraMessageBox.Show("Chưa có tem nào được tích \"In tem\". Kết thúc tem hợp lệ rồi tích chọn các tem cần in.",
                    "Ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 1) Mọi tem được tích phải còn hợp lệ ở thời điểm in.
                foreach (DataRow tem in picked)
                {
                    string reason = GetPrintBlockReason(tem);
                    if (reason != null)
                        throw new InvalidOperationException("Tem #" + tem["TEMID"] + " không hợp lệ: " + reason);
                }

                // 2) Usp_gheplotPrint dùng SELECT DISTINCT LOT, MAHANG: hai tem trùng payload chỉ in một tem.
                var payloads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow tem in picked)
                    if (!payloads.Add(ReadText(tem, "LOTGHEP")))
                        throw new InvalidOperationException("Hai tem được tích có cùng LOT và số lượng (" + ReadText(tem, "LOTGHEP")
                            + "). Bỏ tích một tem, hoặc đổi số lượng, nếu không hệ thống chỉ in một tem.");

                // 3) Kiểm tra lại tồn thực tế, cộng dồn lượng lấy của cùng một LOT qua các tem được in.
                RevalidateStock(picked);

                var printRows = picked.Select(tem => new GhepLotItem
                {
                    MaHang = ReadText(tem, "MAHANG"),
                    GioXuat = 0,
                    Lot = ReadText(tem, "LOTGHEP")
                }).ToList();

                DataTable reportData = _printService.InGhepLot(printRows, Environment.MachineName);
                if (reportData == null || reportData.Rows.Count == 0)
                    throw new InvalidOperationException("Usp_gheplotPrint không trả dữ liệu tem. Không mở màn hình in.");

                XtraReport report = BuildReport(reportData, picked);
                new ReportPrintTool(report).ShowPreviewDialog();
                _lblStatus.Text = "Đã dựng dữ liệu in " + picked.Count + " tem ghép LOT bằng luồng in hiện có.";
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message, "Không thể in ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblStatus.Text = "In không thành công. Kiểm tra tồn và dữ liệu ghép LOT trước khi thử lại.";
            }
        }

        /// <summary>
        /// Hàng YMVN: dùng mẫu tem GHEPLOT_YMVN và điền Gear vào cột Model (ô dưới nhãn "Gear" của mẫu tem YMVN
        /// đọc [Model]); Usp_gheplotPrint chỉ trả B20Item.model nên nếu không thay thì tem in sai Gear.
        /// Khách khác: giữ nguyên mẫu GHEPLOT.
        /// </summary>
        private XtraReport BuildReport(DataTable reportData, IList<DataRow> picked)
        {
            var gearByPayload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow tem in picked)
            {
                StockLotSelection first = GetLines(SafeInt(tem["TEMID"])).Select(x => (StockLotSelection)x["SEL"]).First();
                if (first.IsYmvn) gearByPayload[ReadText(tem, "LOTGHEP")] = first.GearName ?? string.Empty;
            }

            if (gearByPayload.Count == 0) return new GHEPLOT { DataSource = reportData };
            if (gearByPayload.Count != picked.Count)
                throw new InvalidOperationException("Các tem được tích đang lẫn tem YMVN (có Gear) và tem khách khác. Hãy in riêng từng loại.");
            if (!reportData.Columns.Contains("Model") || !reportData.Columns.Contains("ItemLotCode"))
                throw new InvalidOperationException("Dữ liệu in thiếu cột Model/ItemLotCode nên không điền được Gear.");

            foreach (DataRow row in reportData.Rows)
            {
                string payload = Convert.ToString(row["ItemLotCode"]).Trim();
                string gear;
                if (!gearByPayload.TryGetValue(payload, out gear))
                    throw new InvalidOperationException("Không tìm thấy Gear cho tem LOT '" + payload + "'. Hãy lập lại tem.");
                row["Model"] = gear;
            }
            return new GHEPLOT_YMVN { DataSource = reportData };
        }

        private void RevalidateStock(IList<DataRow> picked)
        {
            // Tổng lượng cần lấy theo từng LOT trên TẤT CẢ các tem được in.
            var required = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var itemByLot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow tem in picked)
            {
                string item = ReadText(tem, "MAHANG");
                foreach (KeyValuePair<string, decimal> token in ParsePayload(ReadText(tem, "LOTGHEP")))
                {
                    int existing;
                    required.TryGetValue(token.Key, out existing);
                    required[token.Key] = existing + (int)token.Value;
                    itemByLot[token.Key] = item;
                }
            }

            foreach (KeyValuePair<string, int> need in required)
            {
                DataTable current = _stockRepository.GetTonKhoTheoLot(new List<string> { need.Key });
                var matches = current.AsEnumerable()
                    .Where(row => string.Equals(ReadText(row, "LOT"), need.Key, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count != 1)
                    throw new InvalidOperationException("Không còn xác định duy nhất LOT " + need.Key + " trong STOCKTP. Hãy đọc lại QR.");

                if (!string.Equals(NormalizeItem(ReadText(matches[0], "PART")), NormalizeItem(itemByLot[need.Key]), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Mã hàng của LOT " + need.Key + " đã thay đổi so với tem. Hãy đọc lại QR.");

                int available = SafeInt(matches[0]["SLCONLAI"]);
                if (available < need.Value)
                    throw new InvalidOperationException("LOT " + need.Key + " hiện chỉ còn " + available
                        + ", thấp hơn tổng số lượng cần lấy " + need.Value + " trong các tem được in.");
            }
        }

        private static string NormalizeItem(string value)
        {
            return (value ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Trim();
        }

        private static string ReadText(DataRow row, string column)
        {
            if (row == null || !row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
                return string.Empty;
            return Convert.ToString(row[column])?.Trim() ?? string.Empty;
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            try
            {
                if (value is decimal || value is double || value is float || value is int || value is long || value is short)
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                return 0;
            }

            if (decimal.TryParse(Convert.ToString(value), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)
                && number == decimal.Truncate(number)
                && number >= int.MinValue && number <= int.MaxValue)
                return decimal.ToInt32(number);
            return 0;
        }


        private sealed class StockLotSelection
        {
            public string LotNo { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public int Available { get; set; }
            public int QrQuantity { get; set; }
            public string RawQr { get; set; }
            public bool IsYmvn { get; set; }
            public int GearCode { get; set; }
            public string GearName { get; set; }
        }
    }
}
