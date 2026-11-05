using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Globalization;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.XuLyHangLoi.Application;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuatKho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using PCTP.Modules.GiaoHangKhach;

namespace PCTP.Modules.XuatKho
{
    /// <summary>Điều chuyển tồn từ Slot sang vị trí nội bộ không thuộc bản đồ Slot.</summary>
    public sealed class FormChuyenHangNoiBo : XtraForm
    {
        private readonly XuLyHangLoiModuleFactory.Module _module;
        private readonly SlotRepository _slotRepository;
        private readonly StockExportRepository _stockRepository;
        private readonly InternalTransferRepository _transferRepository;
        private readonly LookUpEdit _lookItemCode = new LookUpEdit();
        private readonly LookUpEdit _lookLot = new LookUpEdit();
        private readonly TextEdit _txtQrScan = new TextEdit();
        private readonly TextEdit _txtDestination = new TextEdit();
        private readonly MemoEdit _memoReason = new MemoEdit();
        private readonly SpinEdit _spinQuantity = new SpinEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly LabelControl _lblSummary = new LabelControl();
        private readonly SimpleButton _btnSearch = new SimpleButton();
        private readonly SimpleButton _btnTransfer = new SimpleButton();
        private readonly SimpleButton _btnInternalStock = new SimpleButton();
        private Guid _requestKey = Guid.NewGuid();
        private bool _transferCompleted;
        private bool _sourceBalanceConsistent;

        public FormChuyenHangNoiBo()
        {
            Text = "Chuyển hàng sang vị trí nội bộ";
            Width = 1180;
            Height = 760;
            MinimumSize = new System.Drawing.Size(980, 650);
            StartPosition = FormStartPosition.CenterParent;

            var provider = new PCTP.ClassSQL.SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);
            _module = XuLyHangLoiModuleFactory.Build(sql, uow);
            _slotRepository = new SlotRepository(sql, uow);
            _stockRepository = new StockExportRepository(sql, uow);
            _transferRepository = new InternalTransferRepository(sql, uow);

            _view = new GridView(_grid);
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(root);

            var criteria = new GroupControl { Text = "Thông tin điều chuyển", Dock = DockStyle.Fill };
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            AddField(fields, 0, 0, "Mã hàng", _lookItemCode);
            AddField(fields, 2, 0, "LOT có tồn", _lookLot);
            AddField(fields, 0, 1, "Quét QR", _txtQrScan);
            AddField(fields, 2, 1, "Vị trí nội bộ đích", _txtDestination);
            AddField(fields, 0, 2, "Số lượng", _spinQuantity);
            AddField(fields, 2, 2, "Lý do", _memoReason);
            ConfigureLookup(_lookItemCode, "ItemCode", "Mã hàng");
            ConfigureLookup(_lookLot, "LotNo", "LOT");
            _lookItemCode.EditValueChanged += (s, e) => LoadLotChoices();
            _txtQrScan.Properties.NullValuePrompt = "Quét QR 4 hoặc 6 phần, phân cách bằng dấu :";
            _txtQrScan.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; ApplyQrScan(); } };
            _txtDestination.Properties.NullValuePrompt = "Ví dụ: LINE-A / QC-INTERNAL";
            _memoReason.Properties.NullValuePrompt = "Nhập lý do điều chuyển";
            _spinQuantity.Properties.MinValue = 1;
            _spinQuantity.Properties.MaxValue = int.MaxValue;
            _spinQuantity.Properties.IsFloatValue = false;
            criteria.Controls.Add(fields);
            root.Controls.Add(criteria, 0, 0);

            LoadItemChoices();

            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.FocusRectStyle = DrawFocusRectStyle.RowFullFocus;
            ConfigureSourceColumns();
            root.Controls.Add(_grid, 0, 1);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
            _btnSearch.Text = "Tìm LOT trong kho";
            _btnSearch.Width = 145;
            _btnSearch.Click += (s, e) => SearchLot();
            _btnTransfer.Text = "Xác nhận chuyển";
            _btnTransfer.Width = 145;
            _btnTransfer.Enabled = false;
            _btnTransfer.Click += (s, e) => Transfer();
            _btnInternalStock.Text = "Xem tồn nội bộ";
            _btnInternalStock.Width = 135;
            _btnInternalStock.Click += (s, e) => ShowInternalStock();
            _lblSummary.AutoSizeMode = LabelAutoSizeMode.None;
            _lblSummary.Width = 500;
            _lblSummary.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            footer.Controls.Add(_btnSearch);
            footer.Controls.Add(_btnTransfer);
            footer.Controls.Add(_btnInternalStock);
            footer.Controls.Add(_lblSummary);
            root.Controls.Add(footer, 0, 2);
        }

        private static void AddField(TableLayoutPanel layout, int col, int row, string caption, Control editor)
        {
            var label = new LabelControl { Text = caption, Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            label.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            editor.Dock = DockStyle.Fill;
            layout.Controls.Add(label, col, row);
            layout.Controls.Add(editor, col + 1, row);
        }

        private void ConfigureSourceColumns()
        {
            AddColumn("WarehouseName", "Kho", 130);
            AddColumn("RackName", "Rack", 110);
            AddColumn("SlotNumber", "Slot", 65);
            AddColumn("ItemCode", "Mã hàng", 140);
            AddColumn("LotNo", "LOT", 180);
            AddColumn("Quantity", "SL tại Slot", 95);
            AddColumn("TemCode", "TemCode", 150);
        }

        private void AddColumn(string field, string caption, int width)
        {
            _view.Columns.Add(new GridColumn { FieldName = field, Caption = caption, Width = width, Visible = true });
            var col = _view.Columns[field];
            if (col != null && field == "Quantity")
            {
                col.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
                col.DisplayFormat.FormatString = "n0";
            }
        }


        private static void ConfigureLookup(LookUpEdit editor, string valueMember, string caption)
        {
            editor.Properties.NullText = "Chọn " + caption;
            editor.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            editor.Properties.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoFilter;
            editor.Properties.ValueMember = valueMember;
            editor.Properties.DisplayMember = valueMember;
            editor.Properties.Columns.Clear();
            editor.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo(valueMember, caption));
        }

        private void LoadItemChoices()
        {
            DataTable items = _stockRepository.GetInternalTransferItemChoices();
            _lookItemCode.Properties.DataSource = items;
            _lookItemCode.Properties.ValueMember = "ItemCode";
            _lookItemCode.Properties.DisplayMember = "ItemCode";
            _lookItemCode.Properties.Columns.Clear();
            _lookItemCode.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("ItemCode", "Mã hàng"));
            _lookItemCode.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("StockQuantity", "Tồn STOCKTP"));
            _lookItemCode.EditValue = null;
            LoadLotChoices();
        }

        private void LoadLotChoices()
        {
            string itemCode = Convert.ToString(_lookItemCode.EditValue)?.Trim() ?? string.Empty;
            _lookLot.EditValue = null;
            if (itemCode.Length == 0) { _lookLot.Properties.DataSource = null; return; }
            DataTable lots = _stockRepository.GetInternalTransferLotChoices(itemCode);
            _lookLot.Properties.DataSource = lots;
            _lookLot.Properties.ValueMember = "LotNo";
            _lookLot.Properties.DisplayMember = "LotNo";
            _lookLot.Properties.Columns.Clear();
            _lookLot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("LotNo", "LOT"));
            _lookLot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("StockQuantity", "STOCKTP"));
            _lookLot.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("SlotQuantity", "Tồn Slot"));
            if (lots.Rows.Count == 1) _lookLot.EditValue = Convert.ToString(lots.Rows[0]["LotNo"]);
        }

        private void ApplyQrScan()
        {
            string raw = (_txtQrScan.Text ?? string.Empty).Trim();
            string[] parts = raw.Split(':');
            if (parts.Length != 4 && parts.Length != 6)
            {
                XtraMessageBox.Show(this, "QR không đúng cấu trúc: chỉ chấp nhận 4 hoặc 6 phần phân cách bằng dấu ':'.", Text);
                return;
            }
            string itemCode = parts[1].Trim();
            int quantity;
            if (itemCode.Length == 0 || !int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
            {
                XtraMessageBox.Show(this, "QR thiếu mã hàng hoặc số lượng không hợp lệ. Không tự suy diễn LOT từ mã QR.", Text);
                return;
            }
            DataTable items = _lookItemCode.Properties.DataSource as DataTable;
            bool exists = items != null && items.AsEnumerable().Any(x =>
                string.Equals(Convert.ToString(x["ItemCode"]).Trim(), itemCode, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                XtraMessageBox.Show(this, "Mã hàng trong QR không có tồn khả dụng trong Slot/STOCKTP: " + itemCode, Text);
                return;
            }
            _lookItemCode.EditValue = itemCode;
            LoadLotChoices();
            _spinQuantity.Value = Math.Max(1, quantity);
            _lblSummary.Text = string.Format("Đã đọc QR {0} phần; mã hàng {1}, SL {2:n0}. Chọn đúng LOT trong danh sách tồn.",
                parts.Length, itemCode, quantity);
            _txtQrScan.SelectAll();
        }

        private void SearchLot()
        {
            string lot = Convert.ToString(_lookLot.EditValue)?.Trim() ?? string.Empty;
            if (lot.Length == 0)
            {
                XtraMessageBox.Show(this, "Chọn mã hàng và LOT từ danh sách tồn kho, hoặc quét QR hợp lệ.", Text);
                return;
            }

            try
            {
                _requestKey = Guid.NewGuid();
                _transferCompleted = false;
                _sourceBalanceConsistent = false;
                _btnTransfer.Enabled = false;
                var rows = _slotRepository.GetAllActiveSlotLots()
                    .Where(x => string.Equals((x.LotNo ?? string.Empty).Trim(), lot, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.WarehouseName).ThenBy(x => x.RackName).ThenBy(x => x.SlotNumber)
                    .ToList();
                _grid.DataSource = rows;
                _view.Columns.Clear();
                ConfigureSourceColumns();

                int slotTotal = rows.Sum(x => x.Quantity);
                var itemCodes = rows.Select(x => (x.ItemCode ?? string.Empty).Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var stockRows = itemCodes.Count == 1
                    ? _stockRepository.FindLotsWithStock(itemCodes[0], lot)
                    : new List<StockTpLotInfo>();
                int stock = stockRows.Count == 1 ? stockRows[0].SlConLai : 0;
                _sourceBalanceConsistent = rows.Count > 0 && itemCodes.Count == 1 &&
                    stockRows.Count == 1 && stock == slotTotal;
                _btnTransfer.Enabled = _sourceBalanceConsistent;
                _lblSummary.Text = string.Format("STOCKTP: {0:n0} | Tổng Slot khả dụng: {1:n0}", stock, slotTotal);

                if (rows.Count == 0)
                    XtraMessageBox.Show(this, "Không tìm thấy LOT đang hoạt động trong Slot. Hàng cách ly không được phép chuyển.", Text);
                else if (!_sourceBalanceConsistent)
                    XtraMessageBox.Show(this, "Tổng tồn Slot không khớp duy nhất một dòng STOCKTP theo LOT/PART. Cần đối soát trước khi chuyển.", Text);
                _view.BestFitColumns();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể tra cứu LOT: " + ex.Message, Text);
            }
        }

        private void ShowInternalStock()
        {
            try
            {
                _sourceBalanceConsistent = false;
                _btnTransfer.Enabled = false;
                _grid.DataSource = _transferRepository.GetInternalStock((Convert.ToString(_lookLot.EditValue) ?? string.Empty).Trim());
                _view.Columns.Clear();
                _view.PopulateColumns();
                _view.OptionsView.ShowAutoFilterRow = true;
                _view.BestFitColumns();
                _lblSummary.Text = "Số dư tại các vị trí nội bộ (lọc theo LOT nếu đã nhập).";
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể đọc tồn nội bộ: " + ex.Message, Text);
            }
        }

        private void Transfer()
        {
            var source = _view.GetFocusedRow() as SlotLotViewInfo;
            string destination = (_txtDestination.Text ?? string.Empty).Trim();
            string reason = (_memoReason.Text ?? string.Empty).Trim();
            int quantity = Convert.ToInt32(_spinQuantity.Value);

            if (!_sourceBalanceConsistent) { XtraMessageBox.Show(this, "Chưa có kết quả LOT hợp lệ hoặc tồn Slot không khớp STOCKTP.", Text); return; }
            if (source == null) { XtraMessageBox.Show(this, "Chọn một dòng LOT/Slot nguồn.", Text); return; }
            if (destination.Length == 0) { XtraMessageBox.Show(this, "Nhập vị trí nội bộ đích.", Text); return; }
            if (reason.Length == 0) { XtraMessageBox.Show(this, "Nhập lý do điều chuyển.", Text); return; }
            if (quantity <= 0 || quantity > source.Quantity) { XtraMessageBox.Show(this, "Số lượng phải lớn hơn 0 và không vượt tồn Slot đã chọn.", Text); return; }

            _btnTransfer.Enabled = false;
            try
            {
                var result = _transferRepository.ExecuteTransfer(
                    _module.UnitOfWork, _module.StockMovement, _module.StockHistoryRepo,
                    _module.SlotService, source, destination, reason, quantity, Environment.UserName, _requestKey);
                if (!result.Success) throw new InvalidOperationException(result.Message);

                _transferCompleted = true;
                _lblSummary.Text = "Đã chuyển thành công. Bấm Tìm LOT để tạo giao dịch mới.";
                XtraMessageBox.Show(this,
                    string.Format("Đã chuyển {0:n0} sản phẩm LOT {1} sang {2}. STOCKTP đã giảm và tồn nội bộ đã tăng.",
                        quantity, source.LotNo, destination),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Chuyển hàng thất bại; transaction đã rollback.\r\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _btnTransfer.Enabled = !_transferCompleted && _sourceBalanceConsistent; }
        }
    }
}
