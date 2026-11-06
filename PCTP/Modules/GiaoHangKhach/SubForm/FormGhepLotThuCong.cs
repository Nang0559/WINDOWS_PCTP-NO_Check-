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
    /// Ghép LOT thủ công: đọc hai QR tem thùng (4 phần), xác minh hai LOT
    /// trong STOCKTP, cho nhập lượng lấy từ từng LOT và tạo payload in dùng
    /// chung TMPLOTGHEP -> Usp_gheplotPrint -> GHEPLOT như luồng tự động.
    ///
    /// Form chỉ kiểm tra tồn và chuẩn bị dữ liệu in; không cập nhật STOCKTP.
    /// </summary>
    public sealed class FormGhepLotThuCong : XtraForm
    {
        private readonly StockTpRepository _stockRepository;
        private readonly InPhieuService _printService;
        private readonly DataTable _workRows;

        private TextEdit _txtQrA;
        private TextEdit _txtQrB;
        private LabelControl _lblLotA;
        private LabelControl _lblLotB;
        private LabelControl _lblStatus;
        private SpinEdit _spinQtyA;
        private SpinEdit _spinQtyB;
        private GridControl _grid;
        private GridView _view;
        private SimpleButton _btnAdd;
        private SimpleButton _btnPrint;

        private StockLotSelection _lotA;
        private StockLotSelection _lotB;

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
            table.Columns.Add("LOTA", typeof(string));
            table.Columns.Add("SLA", typeof(int));
            table.Columns.Add("LOTB", typeof(string));
            table.Columns.Add("SLB", typeof(int));
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
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(root);

            var scanRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 0, 0, 8)
            };
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.Controls.Add(scanRow, 0, 0);

            var groupA = CreateScanGroup("LOT A — LOT NGUỒN");
            var groupB = CreateScanGroup("LOT B — LOT CẦN GHÉP");
            _txtQrA = CreateQrEditor("Quét QR tem thùng 4 phần của LOT A...");
            _txtQrB = CreateQrEditor("Quét QR tem thùng 4 phần của LOT B...");
            _lblLotA = CreateInfoLabel("Chưa đọc LOT A");
            _lblLotB = CreateInfoLabel("Chưa đọc LOT B");
            var btnReadA = new SimpleButton { Text = "ĐỌC LOT A", Dock = DockStyle.Fill, Height = 34 };
            var btnReadB = new SimpleButton { Text = "ĐỌC LOT B", Dock = DockStyle.Fill, Height = 34 };
            btnReadA.Click += (s, e) => ReadLot(true);
            btnReadB.Click += (s, e) => ReadLot(false);
            _txtQrA.KeyDown += (s, e) => ReadOnEnter(e, true);
            _txtQrB.KeyDown += (s, e) => ReadOnEnter(e, false);
            _txtQrA.EditValueChanged += (s, e) => InvalidateScannedLot(true);
            _txtQrB.EditValueChanged += (s, e) => InvalidateScannedLot(false);
            AddScanControls(groupA, _txtQrA, btnReadA, _lblLotA);
            AddScanControls(groupB, _txtQrB, btnReadB, _lblLotB);
            scanRow.Controls.Add(groupA, 0, 0);
            scanRow.Controls.Add(groupB, 1, 0);

            var qtyGroup = new GroupControl
            {
                Text = "SỐ LƯỢNG LẤY TỪ TỪNG LOT — KHÔNG ĐƯỢC VƯỢT TỒN STOCKTP",
                Dock = DockStyle.Fill
            };
            qtyGroup.AppearanceCaption.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            qtyGroup.AppearanceCaption.Options.UseFont = true;
            var qtyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(8, 4, 8, 4)
            };
            qtyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            qtyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            qtyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            qtyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            qtyLayout.Controls.Add(CreateCaption("SL lấy từ LOT A"), 0, 0);
            _spinQtyA = CreateQuantityEditor();
            qtyLayout.Controls.Add(_spinQtyA, 1, 0);
            qtyLayout.Controls.Add(CreateCaption("SL cần ghép từ LOT B"), 2, 0);
            _spinQtyB = CreateQuantityEditor();
            _spinQtyA.EditValueChanged += (s, e) => UpdateButtons();
            _spinQtyB.EditValueChanged += (s, e) => UpdateButtons();
            qtyLayout.Controls.Add(_spinQtyB, 3, 0);
            qtyGroup.Controls.Add(qtyLayout);
            root.Controls.Add(qtyGroup, 0, 1);

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
            AddGridColumn("LOTA", "LOT A", 210);
            AddGridColumn("SLA", "SL lấy A", 90);
            AddGridColumn("LOTB", "LOT B", 210);
            AddGridColumn("SLB", "SL ghép B", 100);
            AddGridColumn("TONGSL", "Tổng SL in", 100);
            AddGridColumn("LOTGHEP", "Dữ liệu LOT ghép đưa đi in", 430);
            gridGroup.Controls.Add(_grid);
            root.Controls.Add(gridGroup, 0, 2);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));
            _lblStatus = new LabelControl
            {
                Text = "Quét đủ hai QR, kiểm tra tồn, nhập số lượng rồi bấm THÊM VÀO BẢNG.",
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
                Padding = new Padding(0, 8, 0, 0)
            };
            _btnPrint = new SimpleButton { Text = "IN GHÉP LOT", Width = 140, Height = 36, Enabled = false };
            var btnRemove = new SimpleButton { Text = "XÓA DÒNG", Width = 110, Height = 36 };
            var btnClear = new SimpleButton { Text = "XÓA TẤT CẢ", Width = 110, Height = 36 };
            _btnAdd = new SimpleButton { Text = "THÊM VÀO BẢNG", Width = 145, Height = 36, Enabled = false };
            _btnAdd.Click += (s, e) => AddPair();
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

        private static GroupControl CreateScanGroup(string title)
        {
            var group = new GroupControl { Text = title, Dock = DockStyle.Fill };
            group.AppearanceCaption.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            group.AppearanceCaption.Options.UseFont = true;
            return group;
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

        private static void AddScanControls(GroupControl group, TextEdit qr, SimpleButton read, LabelControl info)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 2,
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(qr, 0, 0);
            layout.Controls.Add(read, 1, 0);
            layout.Controls.Add(info, 0, 1);
            layout.SetColumnSpan(info, 2);
            group.Controls.Add(layout);
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

        private void InvalidateScannedLot(bool isLotA)
        {
            if (isLotA)
            {
                _lotA = null;
                if (_lblLotA != null) _lblLotA.Text = "QR đã thay đổi — cần đọc lại LOT A";
            }
            else
            {
                _lotB = null;
                if (_lblLotB != null) _lblLotB.Text = "QR đã thay đổi — cần đọc lại LOT B";
            }
            UpdateButtons();
        }

        private void ReadOnEnter(KeyEventArgs e, bool isLotA)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            ReadLot(isLotA);
        }

        private void ReadLot(bool isLotA)
        {
            TextEdit editor = isLotA ? _txtQrA : _txtQrB;
            LabelControl label = isLotA ? _lblLotA : _lblLotB;
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

                var selection = new StockLotSelection
                {
                    LotNo = storedLot,
                    ItemCode = ReadText(stock, "PART"),
                    ItemName = ReadText(stock, "NAME"),
                    Available = available,
                    QrQuantity = qr.Quantity,
                    RawQr = rawQr
                };

                if (isLotA)
                {
                    _lotA = selection;
                    _spinQtyA.EditValue = 1;
                    label.Text = FormatSelection("LOT A", selection);
                }
                else
                {
                    _lotB = selection;
                    _spinQtyB.EditValue = qr.Quantity;
                    label.Text = FormatSelection("LOT B", selection);
                }

                _lblStatus.Text = "Đã đọc LOT. Tiếp tục đọc LOT còn lại và nhập số lượng cần ghép.";
                UpdateButtons();
            }
            catch (Exception ex)
            {
                if (isLotA) _lotA = null; else _lotB = null;
                label.Text = "Không đọc được LOT: " + ex.Message;
                _lblStatus.Text = "Không đạt validation — chưa có dữ liệu nào được thêm vào bảng.";
                UpdateButtons();
                XtraMessageBox.Show(ex.Message, "Không thể đọc LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                editor.Focus();
                editor.SelectAll();
            }
        }

        private void AddPair()
        {
            try
            {
                ValidatePair(_lotA, _lotB, GetQuantity(_spinQtyA), GetQuantity(_spinQtyB));

                string lotPayload = _lotA.LotNo + "-" + GetQuantity(_spinQtyA).ToString(CultureInfo.InvariantCulture)
                    + "," + _lotB.LotNo + "-" + GetQuantity(_spinQtyB).ToString(CultureInfo.InvariantCulture);

                bool duplicate = _workRows.AsEnumerable().Any(row =>
                    string.Equals(ReadText(row, "LOTGHEP"), lotPayload, StringComparison.OrdinalIgnoreCase));
                if (duplicate)
                    throw new InvalidOperationException("Cặp LOT và số lượng này đã có trong bảng.");

                _workRows.Rows.Add(
                    _lotA.ItemCode,
                    _lotA.LotNo,
                    GetQuantity(_spinQtyA),
                    _lotB.LotNo,
                    GetQuantity(_spinQtyB),
                    GetQuantity(_spinQtyA) + GetQuantity(_spinQtyB),
                    lotPayload);

                _view.MoveLast();
                _lblStatus.Text = "Đã thêm một cặp LOT hợp lệ vào bảng. STOCKTP chưa bị thay đổi.";
                UpdateButtons();
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Không đạt validation — không thêm dòng.";
                XtraMessageBox.Show(ex.Message, "Không thể ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void ValidatePair(StockLotSelection lotA, StockLotSelection lotB, int qtyA, int qtyB)
        {
            if (lotA == null || lotB == null)
                throw new InvalidOperationException("Phải đọc đủ QR của LOT A và LOT B.");

            if (!string.Equals(NormalizeItem(lotA.ItemCode), NormalizeItem(lotB.ItemCode), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Hai LOT phải cùng mã hàng. Mã hàng A: " + lotA.ItemCode + "; mã hàng B: " + lotB.ItemCode + ".");

            string keyA = LotNoHelper.GetStockTpKey(lotA.LotNo);
            string keyB = LotNoHelper.GetStockTpKey(lotB.LotNo);
            if (string.Equals(keyA, keyB, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("LOT A và LOT B phải khác LOTNO.");

            if (qtyA <= 0 || qtyA > lotA.Available)
                throw new InvalidOperationException("Số lượng lấy từ LOT A phải lớn hơn 0 và không vượt tồn STOCKTP. Tồn A: " + lotA.Available + ".");
            if (qtyB <= 0 || qtyB > lotB.Available)
                throw new InvalidOperationException("Số lượng ghép từ LOT B phải lớn hơn 0 và không vượt tồn STOCKTP. Tồn B: " + lotB.Available + ".");
        }

        private void PrintWorkRows()
        {
            if (_workRows.Rows.Count == 0)
            {
                XtraMessageBox.Show("Chưa có cặp LOT hợp lệ trong bảng để in.", "Ghép LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var lots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in _workRows.Rows)
            {
                lots.Add(ReadText(row, "LOTA"));
                lots.Add(ReadText(row, "LOTB"));
            }

            var currentStock = new Dictionary<string, Tuple<string, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (string lot in lots.Where(x => !string.IsNullOrWhiteSpace(x)))
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

            foreach (DataRow row in _workRows.Rows)
            {
                string itemCode = ReadText(row, "MAHANG");
                string lotA = ReadText(row, "LOTA");
                string lotB = ReadText(row, "LOTB");
                int qtyA = SafeInt(row["SLA"]);
                int qtyB = SafeInt(row["SLB"]);
                Tuple<string, int> stockA = currentStock[lotA];
                Tuple<string, int> stockB = currentStock[lotB];

                if (!string.Equals(NormalizeItem(stockA.Item1), NormalizeItem(itemCode), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(NormalizeItem(stockB.Item1), NormalizeItem(itemCode), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Mã hàng của LOT đã thay đổi so với dòng ghép " + itemCode + ". Hãy đọc lại QR.");

                if (stockA.Item2 < qtyA)
                    throw new InvalidOperationException("LOT A " + lotA + " hiện chỉ còn " + stockA.Item2 + ", thấp hơn số lượng cần lấy " + qtyA + ".");
                if (stockB.Item2 < qtyB)
                    throw new InvalidOperationException("LOT B " + lotB + " hiện chỉ còn " + stockB.Item2 + ", thấp hơn số lượng cần ghép " + qtyB + ".");
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
            _btnPrint.Enabled = _workRows.Rows.Count > 0;
            bool valid = false;
            try
            {
                ValidatePair(_lotA, _lotB, GetQuantity(_spinQtyA), GetQuantity(_spinQtyB));
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
