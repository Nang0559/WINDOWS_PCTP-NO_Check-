using DevExpress.XtraEditors;
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
    /// Ghép LOT thủ công: đọc QR tem thùng (4 phần) của TỪ 2 LOT TRỞ LÊN, xác minh từng LOT
    /// trong STOCKTP, cho nhập lượng lấy từ từng LOT và tạo payload in dùng
    /// chung TMPLOTGHEP -> Usp_gheplotPrint -> GHEPLOT như luồng tự động
    /// (LOT1-SL1,LOT2-SL2,LOT3-SL3,...).
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
        private readonly DataTable _workRows;
        private readonly List<LotSlot> _slots = new List<LotSlot>();

        private FlowLayoutPanel _slotHost;
        private LabelControl _lblStatus;
        private GridControl _grid;
        private GridView _view;
        private SimpleButton _btnAddLot;
        private SimpleButton _btnAdd;
        private SimpleButton _btnPrint;

        public FormGhepLotThuCong()
        {
            var sql = new SQLPROVIDER();
            var executor = new PhieuSqlExecutor(sql);
            var unitOfWork = new UnitOfWork(sql);

            _stockRepository = new StockTpRepository(executor, unitOfWork);
            var sqlRepository = new SqlRepository(executor, unitOfWork);
            // InGhepLot chỉ dùng ISqlRepository nên tái sử dụng đúng service in hiện hành.
            _printService = new InPhieuService(null, null, sqlRepository, null, null, null);

            _workRows = CreateWorkTable();
            InitializeUi();
        }

        private static DataTable CreateWorkTable()
        {
            var table = new DataTable("ManualGhepLot");
            table.Columns.Add("MAHANG", typeof(string));
            table.Columns.Add("SOLOT", typeof(int));
            table.Columns.Add("CHITIET", typeof(string));
            table.Columns.Add("TONGSL", typeof(int));
            table.Columns.Add("LOTGHEP", typeof(string));
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            Controls.Add(root);

            // --- Thanh công cụ danh sách LOT
            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 0, 0, 6)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _btnAddLot = new SimpleButton { Text = "+ THÊM LOT", Dock = DockStyle.Fill };
            _btnAddLot.Click += (s, e) => AddSlot();
            toolbar.Controls.Add(_btnAddLot, 0, 0);
            var hint = new LabelControl
            {
                Text = "Ghép từ " + MinLots + " đến " + MaxLots + " LOT cùng mã hàng. Quét QR tem thùng 4 phần của từng LOT, nhập số lượng lấy rồi bấm THÊM VÀO BẢNG.",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            hint.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            toolbar.Controls.Add(hint, 1, 0);
            root.Controls.Add(toolbar, 0, 0);

            // --- Danh sách LOT (mỗi LOT một dòng: QR + đọc + SL + xóa)
            var slotGroup = new GroupControl
            {
                Text = "DANH SÁCH LOT CẦN GHÉP — SỐ LƯỢNG LẤY TỪ TỪNG LOT KHÔNG ĐƯỢC VƯỢT TỒN STOCKTP",
                Dock = DockStyle.Fill
            };
            slotGroup.AppearanceCaption.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            slotGroup.AppearanceCaption.Options.UseFont = true;
            _slotHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4)
            };
            _slotHost.SizeChanged += (s, e) => ResizeSlots();
            slotGroup.Controls.Add(_slotHost);
            root.Controls.Add(slotGroup, 0, 1);
            for (int i = 0; i < MinLots; i++) AddSlot();

            var gridGroup = new GroupControl
            {
                Text = "DANH SÁCH GHÉP LOT THỦ CÔNG — DỮ LIỆU SẼ ĐƯỢC GỬI SANG CHỨC NĂNG IN GHÉP LOT",
                Dock = DockStyle.Fill
            };
            gridGroup.AppearanceCaption.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            gridGroup.AppearanceCaption.Options.UseFont = true;
            _grid = new GridControl { Dock = DockStyle.Fill, DataSource = _workRows };
            _view = new GridView(_grid);
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsView.ShowIndicator = true;
            _view.OptionsSelection.MultiSelect = true;
            _view.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;
            AddGridColumn("MAHANG", "Mã hàng", 150);
            AddGridColumn("SOLOT", "Số LOT", 70);
            AddGridColumn("CHITIET", "Chi tiết (LOT: SL)", 520);
            AddGridColumn("TONGSL", "Tổng SL in", 100);
            AddGridColumn("LOTGHEP", "Dữ liệu LOT ghép đưa đi in", 600);
            gridGroup.Controls.Add(_grid);
            root.Controls.Add(gridGroup, 0, 2);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 580));
            _lblStatus = new LabelControl
            {
                Text = "Quét đủ QR của ít nhất hai LOT, kiểm tra tồn, nhập số lượng rồi bấm THÊM VÀO BẢNG.",
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
            _btnPrint = new SimpleButton { Text = "IN GHÉP LOT", Width = 140, Height = 36, Enabled = false };
            var btnRemove = new SimpleButton { Text = "XÓA DÒNG", Width = 110, Height = 36 };
            var btnClear = new SimpleButton { Text = "XÓA TẤT CẢ", Width = 110, Height = 36 };
            _btnAdd = new SimpleButton { Text = "THÊM VÀO BẢNG", Width = 145, Height = 36, Enabled = false };
            _btnAdd.Click += (s, e) => AddSet();
            _btnPrint.Click += (s, e) => PrintWorkRows();
            btnRemove.Click += (s, e) => RemoveSelectedRows();
            btnClear.Click += (s, e) => ClearRows();
            actions.Controls.Add(_btnPrint);
            actions.Controls.Add(btnRemove);
            actions.Controls.Add(btnClear);
            actions.Controls.Add(_btnAdd);
            footer.Controls.Add(actions, 1, 0);
            root.Controls.Add(footer, 0, 3);
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
        }

        // ------------------------------------------------------------------ LOT slots
        private void AddSlot()
        {
            if (_slots.Count >= MaxLots)
            {
                XtraMessageBox.Show("Tối đa " + MaxLots + " LOT cho một lần ghép.", "Ghép LOT",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var slot = new LotSlot();
            slot.Host = new TableLayoutPanel
            {
                Height = 78,
                Margin = new Padding(0, 0, 0, 6),
                ColumnCount = 6,
                RowCount = 2
            };
            slot.Host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            slot.Host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            slot.Host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            slot.Host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            slot.Host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            slot.Host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            slot.Host.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            slot.Host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            slot.Title = CreateCaption("LOT");
            slot.Qr = CreateQrEditor("Quét QR tem thùng 4 phần của LOT...");
            slot.Read = new SimpleButton { Text = "ĐỌC LOT", Dock = DockStyle.Fill };
            slot.Qty = CreateQuantityEditor();
            slot.Remove = new SimpleButton { Text = "XÓA", Dock = DockStyle.Fill };
            slot.Info = CreateInfoLabel("Chưa đọc LOT");

            slot.Read.Click += (s, e) => ReadLot(slot);
            slot.Qr.KeyDown += (s, e) => ReadOnEnter(e, slot);
            slot.Qr.EditValueChanged += (s, e) => InvalidateSlot(slot);
            slot.Qty.EditValueChanged += (s, e) => UpdateButtons();
            slot.Remove.Click += (s, e) => RemoveSlot(slot);

            slot.Host.Controls.Add(slot.Title, 0, 0);
            slot.Host.Controls.Add(slot.Qr, 1, 0);
            slot.Host.Controls.Add(slot.Read, 2, 0);
            slot.Host.Controls.Add(CreateCaption("SL"), 3, 0);
            slot.Host.Controls.Add(slot.Qty, 4, 0);
            slot.Host.Controls.Add(slot.Remove, 5, 0);
            slot.Host.Controls.Add(slot.Info, 1, 1);
            slot.Host.SetColumnSpan(slot.Info, 5);

            _slots.Add(slot);
            _slotHost.Controls.Add(slot.Host);
            RenumberSlots();
            ResizeSlots();
            UpdateButtons();
            slot.Qr.Focus();
        }

        private void RemoveSlot(LotSlot slot)
        {
            if (_slots.Count <= MinLots) return;
            _slots.Remove(slot);
            _slotHost.Controls.Remove(slot.Host);
            slot.Host.Dispose();
            RenumberSlots();
            UpdateButtons();
        }

        private void RenumberSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Title.Text = "LOT " + (i + 1);
                // Hai LOT đầu là bắt buộc; chỉ các LOT thêm sau mới được xóa.
                _slots[i].Remove.Enabled = _slots.Count > MinLots && i >= MinLots;
            }
            if (_btnAddLot != null) _btnAddLot.Enabled = _slots.Count < MaxLots;
        }

        private void ResizeSlots()
        {
            int width = Math.Max(_slotHost.ClientSize.Width - _slotHost.Padding.Horizontal - 2, 400);
            foreach (LotSlot slot in _slots) slot.Host.Width = width;
        }

        private static TextEdit CreateQrEditor(string placeholder)
        {
            return new TextEdit
            {
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 11F, FontStyle.Bold),
                Properties =
                {
                    NullValuePrompt = placeholder,
                    NullValuePromptShowForEmptyValue = true
                }
            };
        }

        private static LabelControl CreateInfoLabel(string text)
        {
            return new LabelControl
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
        }

        private static LabelControl CreateCaption(string text)
        {
            var label = new LabelControl
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            label.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            label.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            return label;
        }

        private static SpinEdit CreateQuantityEditor()
        {
            var editor = new SpinEdit { Dock = DockStyle.Fill, Height = 32 };
            editor.Properties.IsFloatValue = false;
            editor.Properties.MinValue = 1;
            editor.Properties.MaxValue = 100000000;
            editor.Properties.Increment = 1;
            editor.EditValue = 1;
            return editor;
        }

        private void AddGridColumn(string field, string caption, int width)
        {
            _view.Columns.Add(new GridColumn
            {
                FieldName = field,
                Caption = caption,
                Visible = true,
                VisibleIndex = _view.Columns.Count,
                Width = width
            });
        }

        private void InvalidateSlot(LotSlot slot)
        {
            slot.Lot = null;
            if (slot.Info != null) slot.Info.Text = "QR đã thay đổi — cần đọc lại " + slot.Title.Text;
            UpdateButtons();
        }

        private void ReadOnEnter(KeyEventArgs e, LotSlot slot)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            ReadLot(slot);
        }

        private void ReadLot(LotSlot slot)
        {
            TextEdit editor = slot.Qr;
            string title = slot.Title.Text;
            try
            {
                string rawQr = (editor.Text ?? string.Empty).Trim();
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

                // Một LOT chỉ được xuất hiện một lần trong cùng lần ghép.
                string key = LotNoHelper.GetStockTpKey(storedLot);
                foreach (LotSlot other in _slots)
                {
                    if (ReferenceEquals(other, slot) || other.Lot == null) continue;
                    if (string.Equals(LotNoHelper.GetStockTpKey(other.Lot.LotNo), key, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("LOT này đã được đọc ở " + other.Title.Text + ". Mỗi LOT chỉ ghép một lần.");
                }

                var selection = new StockLotSelection
                {
                    LotNo = storedLot,
                    ItemCode = ReadText(stock, "PART"),
                    ItemName = ReadText(stock, "NAME"),
                    Available = available,
                    QrQuantity = qr.Quantity,
                    RawQr = rawQr
                };

                slot.Lot = selection;
                // LOT đầu là LOT nguồn (mặc định lấy 1); các LOT sau mặc định lấy theo số lượng trên QR.
                slot.Qty.EditValue = _slots.IndexOf(slot) == 0 ? 1 : qr.Quantity;
                slot.Info.Text = FormatSelection(title, selection);

                _lblStatus.Text = "Đã đọc " + title + ". Tiếp tục đọc các LOT còn lại hoặc bấm + THÊM LOT.";
                UpdateButtons();

                // Quét liên tục: chuyển sang ô QR kế tiếp chưa đọc.
                LotSlot next = _slots.FirstOrDefault(x => x.Lot == null);
                if (next != null) next.Qr.Focus();
            }
            catch (Exception ex)
            {
                slot.Lot = null;
                slot.Info.Text = "Không đọc được LOT: " + ex.Message;
                _lblStatus.Text = "Không đạt validation — chưa có dữ liệu nào được thêm vào bảng.";
                UpdateButtons();
                XtraMessageBox.Show(ex.Message, "Không thể đọc LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                editor.Focus();
                editor.SelectAll();
            }
        }

        // ------------------------------------------------------------------ thêm bộ LOT vào bảng
        private void AddSet()
        {
            try
            {
                List<StockLotSelection> lots;
                List<int> quantities;
                CollectSlots(out lots, out quantities);
                ValidateSet(lots, quantities);

                string lotPayload = BuildPayload(lots, quantities);
                string itemCode = lots[0].ItemCode;
                int total = quantities.Sum();

                bool duplicate = _workRows.AsEnumerable().Any(row =>
                    string.Equals(ReadText(row, "LOTGHEP"), lotPayload, StringComparison.OrdinalIgnoreCase));
                if (duplicate)
                    throw new InvalidOperationException("Bộ LOT và số lượng này đã có trong bảng.");

                // Cùng một LOT ở nhiều dòng bảng: tổng lượng lấy không được vượt tồn.
                for (int i = 0; i < lots.Count; i++)
                {
                    int already = SumQuantityForLot(lots[i].LotNo);
                    if (already + quantities[i] > lots[i].Available)
                        throw new InvalidOperationException("LOT " + lots[i].LotNo + " đã được lấy " + already
                            + " ở các dòng trước; thêm " + quantities[i] + " sẽ vượt tồn " + lots[i].Available + ".");
                }

                _workRows.Rows.Add(
                    itemCode,
                    lots.Count,
                    string.Join(" | ", lots.Select((l, i) => l.LotNo + ": " + quantities[i].ToString(CultureInfo.InvariantCulture))),
                    total,
                    lotPayload);

                _view.MoveLast();
                _lblStatus.Text = "Đã thêm một bộ " + lots.Count + " LOT hợp lệ vào bảng. STOCKTP chưa bị thay đổi.";
                UpdateButtons();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Không đạt validation — không thêm dòng.";
                XtraMessageBox.Show(ex.Message, "Không thể ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private int SumQuantityForLot(string lotNo)
        {
            int sum = 0;
            foreach (DataRow row in _workRows.Rows)
                foreach (KeyValuePair<string, decimal> token in ParsePayload(ReadText(row, "LOTGHEP")))
                    if (string.Equals(token.Key, lotNo, StringComparison.OrdinalIgnoreCase))
                        sum += (int)token.Value;
            return sum;
        }

        private void CollectSlots(out List<StockLotSelection> lots, out List<int> quantities)
        {
            lots = new List<StockLotSelection>();
            quantities = new List<int>();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Lot == null)
                    throw new InvalidOperationException("Phải đọc QR của " + _slots[i].Title.Text
                        + " (hoặc bấm XÓA nếu không dùng LOT này).");
                lots.Add(_slots[i].Lot);
                quantities.Add(GetQuantity(_slots[i].Qty));
            }
        }

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

        private static void ValidateSet(IList<StockLotSelection> lots, IList<int> quantities)
        {
            if (lots == null || lots.Count < MinLots)
                throw new InvalidOperationException("Phải ghép từ " + MinLots + " LOT trở lên.");
            if (lots.Count > MaxLots)
                throw new InvalidOperationException("Tối đa " + MaxLots + " LOT cho một lần ghép.");

            string firstItem = NormalizeItem(lots[0].ItemCode);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lots.Count; i++)
            {
                StockLotSelection lot = lots[i];
                if (lot == null)
                    throw new InvalidOperationException("Phải đọc đủ QR của tất cả các LOT.");
                if (!string.Equals(NormalizeItem(lot.ItemCode), firstItem, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Tất cả LOT phải cùng mã hàng. LOT 1: " + lots[0].ItemCode
                        + "; LOT " + (i + 1) + ": " + lot.ItemCode + ".");
                if (!keys.Add(LotNoHelper.GetStockTpKey(lot.LotNo)))
                    throw new InvalidOperationException("Các LOT phải khác LOTNO. LOT " + (i + 1) + " bị trùng với một LOT khác.");
                if (quantities[i] <= 0 || quantities[i] > lot.Available)
                    throw new InvalidOperationException("Số lượng lấy từ LOT " + (i + 1)
                        + " phải lớn hơn 0 và không vượt tồn STOCKTP. Tồn: " + lot.Available + ".");
            }

            CheckPrintLimits(lots[0].ItemCode, BuildPayload(lots, quantities), quantities.Sum());
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
        private void PrintWorkRows()
        {
            if (_workRows.Rows.Count == 0)
            {
                XtraMessageBox.Show("Chưa có bộ LOT hợp lệ trong bảng để in.", "Ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var printRows = new List<GhepLotItem>();
                foreach (DataRow row in _workRows.Rows)
                {
                    string item = ReadText(row, "MAHANG");
                    string lotPayload = ReadText(row, "LOTGHEP");
                    if (string.IsNullOrWhiteSpace(item) || string.IsNullOrWhiteSpace(lotPayload))
                        throw new InvalidOperationException("Bảng ghép LOT có dòng thiếu mã hàng hoặc dữ liệu LOT.");

                    printRows.Add(new GhepLotItem
                    {
                        MaHang = item,
                        GioXuat = 0,
                        Lot = lotPayload
                    });
                }

                // Recheck live stock immediately before staging/printing to prevent stale validation.
                RevalidateWorkRows();

                DataTable reportData = _printService.InGhepLot(printRows, Environment.MachineName);
                if (reportData == null || reportData.Rows.Count == 0)
                    throw new InvalidOperationException("Usp_gheplotPrint không trả dữ liệu tem. Không mở màn hình in.");

                var report = new GHEPLOT { DataSource = reportData };
                new ReportPrintTool(report).ShowPreviewDialog();
                _lblStatus.Text = "Đã dựng dữ liệu in ghép LOT bằng luồng in hiện có.";
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message, "Không thể in ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblStatus.Text = "In không thành công. Kiểm tra tồn và dữ liệu ghép LOT trước khi thử lại.";
            }
        }

        private void RevalidateWorkRows()
        {
            // Tổng lượng cần lấy theo từng LOT trên TẤT CẢ các dòng bảng.
            var required = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var itemByRow = new List<Tuple<string, List<KeyValuePair<string, decimal>>>>();
            foreach (DataRow row in _workRows.Rows)
            {
                var tokens = ParsePayload(ReadText(row, "LOTGHEP")).ToList();
                if (tokens.Count < MinLots)
                    throw new InvalidOperationException("Dòng ghép LOT '" + ReadText(row, "LOTGHEP") + "' có ít hơn " + MinLots + " LOT.");
                itemByRow.Add(Tuple.Create(ReadText(row, "MAHANG"), tokens));
                foreach (KeyValuePair<string, decimal> token in tokens)
                {
                    int existing;
                    required.TryGetValue(token.Key, out existing);
                    required[token.Key] = existing + (int)token.Value;
                }
            }

            var currentStock = new Dictionary<string, Tuple<string, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (string lot in required.Keys.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                DataTable current = _stockRepository.GetTonKhoTheoLot(new List<string> { lot });
                var matches = current.AsEnumerable()
                    .Where(row => string.Equals(ReadText(row, "LOT"), lot, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count != 1)
                    throw new InvalidOperationException("Không còn xác định duy nhất LOT " + lot + " trong STOCKTP. Hãy đọc lại QR.");

                currentStock[lot] = Tuple.Create(
                    ReadText(matches[0], "PART"),
                    SafeInt(matches[0]["SLCONLAI"]));
            }

            foreach (Tuple<string, List<KeyValuePair<string, decimal>>> row in itemByRow)
            {
                foreach (KeyValuePair<string, decimal> token in row.Item2)
                {
                    Tuple<string, int> stock = currentStock[token.Key];
                    if (!string.Equals(NormalizeItem(stock.Item1), NormalizeItem(row.Item1), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Mã hàng của LOT " + token.Key + " đã thay đổi so với dòng ghép " + row.Item1 + ". Hãy đọc lại QR.");
                }
            }

            foreach (KeyValuePair<string, int> need in required)
            {
                int available = currentStock[need.Key].Item2;
                if (available < need.Value)
                    throw new InvalidOperationException("LOT " + need.Key + " hiện chỉ còn " + available
                        + ", thấp hơn tổng số lượng cần lấy " + need.Value + " trong bảng ghép.");
            }
        }

        private void RemoveSelectedRows()
        {
            int[] handles = _view.GetSelectedRows();
            var indices = handles.Where(handle => handle >= 0)
                .Select(handle => _view.GetDataSourceRowIndex(handle))
                .Where(index => index >= 0)
                .Distinct()
                .OrderByDescending(index => index)
                .ToList();

            foreach (int index in indices)
                if (index < _workRows.Rows.Count)
                    _workRows.Rows.RemoveAt(index);

            UpdateButtons();
        }

        private void ClearRows()
        {
            if (_workRows.Rows.Count == 0) return;
            if (XtraMessageBox.Show("Xóa toàn bộ danh sách ghép LOT đang lập?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _workRows.Clear();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (_btnPrint == null || _btnAdd == null) return;
            _btnPrint.Enabled = _workRows.Rows.Count > 0;
            bool valid = false;
            try
            {
                List<StockLotSelection> lots;
                List<int> quantities;
                CollectSlots(out lots, out quantities);
                ValidateSet(lots, quantities);
                valid = true;
            }
            catch
            {
                valid = false;
            }
            _btnAdd.Enabled = valid;
        }

        private static string FormatSelection(string title, StockLotSelection selection)
        {
            return title + ": " + selection.LotNo
                + " | " + selection.ItemCode
                + " | Tồn STOCKTP: " + selection.Available.ToString(CultureInfo.InvariantCulture)
                + " | SL trên QR: " + selection.QrQuantity.ToString(CultureInfo.InvariantCulture);
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

        private static int GetQuantity(SpinEdit editor)
        {
            return SafeInt(editor.EditValue);
        }

        private sealed class LotSlot
        {
            public TableLayoutPanel Host;
            public LabelControl Title;
            public TextEdit Qr;
            public SimpleButton Read;
            public SpinEdit Qty;
            public SimpleButton Remove;
            public LabelControl Info;
            public StockLotSelection Lot;
        }

        private sealed class StockLotSelection
        {
            public string LotNo { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public int Available { get; set; }
            public int QrQuantity { get; set; }
            public string RawQr { get; set; }
        }
    }
}
