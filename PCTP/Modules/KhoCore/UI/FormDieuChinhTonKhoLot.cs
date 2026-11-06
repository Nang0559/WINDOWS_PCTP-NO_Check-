using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.KhoCore.UI
{
    /// <summary>
    /// Màn hình điều chỉnh số lượng đích của một ItemCode + LOT.
    /// STOCKTP là số chuẩn; toàn bộ SlotLot đang hoạt động được cân đối trong một transaction.
    /// </summary>
    public sealed class FormDieuChinhTonKhoLot : XtraForm
    {
        private readonly UnitOfWork _uow;
        private readonly StockTpRepository _stockTp;
        private readonly SlotRepository _slotRepo;
        private readonly BulkStockSlotRepository _adjustmentRepo;
        private readonly string _databaseTarget;

        private readonly GridLookUpEdit _lookupKey = new GridLookUpEdit();
        private DataTable _itemLookup;
        private readonly SimpleButton _search = new SimpleButton();
        private readonly GridControl _lotGrid = new GridControl();
        private readonly GridView _lotView = new GridView();
        private readonly GridControl _slotGrid = new GridControl();
        private readonly GridView _slotView = new GridView();
        private readonly SpinEdit _target = new SpinEdit();
        private readonly TextEdit _destinationSlot = new TextEdit();
        private readonly MemoEdit _reason = new MemoEdit();
        private readonly LabelControl _summary = new LabelControl();
        private readonly SimpleButton _apply = new SimpleButton();

        private DataTable _searchResults;
        private List<PCTP.Modules.KhoCore.Models.SlotLotViewInfo> _activeSlotLots;
        private string _selectedItemCode;
        private string _selectedLot;
        private int _stockBefore;
        private int _slotBefore;
        private bool _hasPreview;
        private Guid _requestId = Guid.NewGuid();

        public FormDieuChinhTonKhoLot()
        {
            Text = "Điều chỉnh tồn kho LOT";
            Width = 1180;
            Height = 780;
            MinimumSize = new System.Drawing.Size(980, 680);
            StartPosition = FormStartPosition.CenterParent;

            var provider = new SQLPROVIDER();
            var connection = new System.Data.SqlClient.SqlConnectionStringBuilder(provider.B7R2_FCCdb);
            _databaseTarget = string.Format("{0} / {1}",
                string.IsNullOrWhiteSpace(connection.DataSource) ? "(server chưa cấu hình)" : connection.DataSource,
                string.IsNullOrWhiteSpace(connection.InitialCatalog) ? "(database chưa cấu hình)" : connection.InitialCatalog);
            Text = "Điều chỉnh tồn kho LOT — " + _databaseTarget;

            var db = new PhieuSqlExecutor(provider);
            _uow = new UnitOfWork(provider);
            _stockTp = new StockTpRepository(db, _uow);
            _slotRepo = new SlotRepository(db, _uow);
            _adjustmentRepo = new BulkStockSlotRepository(db, _uow);

            BuildUi();
            FormClosed += (s, e) => _uow.Dispose();
        }

        private void BuildUi()
        {
            // Form co giãn được; khu vực điều chỉnh luôn có chiều cao tối thiểu để không mất ô Lý do.
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            MinimumSize = new System.Drawing.Size(1080, 760);
            AutoScaleMode = AutoScaleMode.Font;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var searchGroup = new GroupControl
            {
                Text = "Tra cứu mã hàng / LOT / QR",
                Dock = DockStyle.Fill
            };
            var searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 3,
                RowCount = 2
            };
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var keyLabel = new LabelControl
            {
                Text = "LookupKey",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            keyLabel.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _lookupKey.Dock = DockStyle.Fill;
            _lookupKey.Properties.NullValuePrompt = "Chọn / nhập mã hàng, nhập LOT hoặc bắn QR 4 / 6 trường";
            _lookupKey.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            _lookupKey.Properties.PopupFilterMode = DevExpress.XtraEditors.PopupFilterMode.Contains;
            _lookupKey.Properties.ImmediatePopup = true;
            _lookupKey.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
            _lookupKey.Properties.DisplayMember = "PART";
            _lookupKey.Properties.ValueMember = "PART";
            _lookupKey.Properties.View.OptionsView.ShowAutoFilterRow = true;
            _lookupKey.Properties.View.OptionsView.ShowGroupPanel = false;
            _lookupKey.Properties.View.OptionsBehavior.Editable = false;
            _itemLookup = _stockTp.GetItemLookup();
            _lookupKey.Properties.DataSource = _itemLookup;
            _lookupKey.Properties.PopulateViewColumns();
            if (_lookupKey.Properties.View.Columns["PART"] != null)
            {
                _lookupKey.Properties.View.Columns["PART"].Caption = "Mã hàng";
                _lookupKey.Properties.View.Columns["PART"].Visible = true;
            }
            if (_lookupKey.Properties.View.Columns["NAME"] != null)
            {
                _lookupKey.Properties.View.Columns["NAME"].Caption = "Tên hàng";
                _lookupKey.Properties.View.Columns["NAME"].Visible = true;
            }
            _lookupKey.Properties.View.BestFitColumns();
            _lookupKey.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    SearchLots();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            _search.Text = "Tra cứu";
            _search.Dock = DockStyle.Fill;
            _search.Click += (s, e) => SearchLots();
            searchLayout.Controls.Add(keyLabel, 0, 0);
            searchLayout.Controls.Add(_lookupKey, 1, 0);
            searchLayout.Controls.Add(_search, 2, 0);

            var help = new LabelControl
            {
                Text = "Nhập mã hàng để xem các LOT; nhập LOT để tìm trực tiếp; QR sẽ tự tách LOT và mã hàng. Chọn một LOT ở danh sách bên dưới để điều chỉnh.",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            help.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            help.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            searchLayout.Controls.Add(help, 0, 1);
            searchLayout.SetColumnSpan(help, 3);
            searchGroup.Controls.Add(searchLayout);
            root.Controls.Add(searchGroup, 0, 0);

            // Splitter ngoài cho phép kéo thay đổi diện tích khu vực danh sách và khu vực điều chỉnh.
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Size = new System.Drawing.Size(1100, 600),
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2,
                IsSplitterFixed = false,
                SplitterWidth = 7,
                Panel1MinSize = 220,
                Panel2MinSize = 270,
                SplitterDistance = 310
            };
            root.Controls.Add(mainSplit, 0, 1);

            // Splitter trong cho phép kéo tăng/giảm chiều cao bảng LOT và SlotLot.
            var listSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Size = new System.Drawing.Size(1100, 400),
                Orientation = Orientation.Horizontal,
                IsSplitterFixed = false,
                SplitterWidth = 6,
                Panel1MinSize = 120,
                Panel2MinSize = 90,
                SplitterDistance = 190
            };
            mainSplit.Panel1.Controls.Add(listSplit);

            var lotGroup = new GroupControl
            {
                Text = "Danh sách LOT tìm được — chọn đúng LOT cần điều chỉnh",
                Dock = DockStyle.Fill
            };
            ConfigureGrid(_lotGrid, _lotView);
            _lotView.FocusedRowChanged += (s, e) => LoadSelectedLot();
            _lotView.RowClick += (s, e) => LoadSelectedLot();
            lotGroup.Controls.Add(_lotGrid);
            listSplit.Panel1.Controls.Add(lotGroup);

            var slotGroup = new GroupControl
            {
                Text = "Vị trí SlotLot đang chứa LOT được chọn",
                Dock = DockStyle.Fill
            };
            ConfigureGrid(_slotGrid, _slotView);
            slotGroup.Controls.Add(_slotGrid);
            listSplit.Panel2.Controls.Add(slotGroup);

            // Khu vực điều chỉnh luôn nằm trong Panel2; ô Lý do có chiều cao tối thiểu 68px.
            var bottomLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0)
            };
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            mainSplit.Panel2.Controls.Add(bottomLayout);

            var adjustGroup = new GroupControl
            {
                Text = "Số lượng đích và thông tin điều chỉnh LOT đang chọn",
                Dock = DockStyle.Fill,
                MinimumSize = new System.Drawing.Size(0, 205)
            };
            var adjustLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 4,
                RowCount = 3
            };
            adjustLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            adjustLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            adjustLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            adjustLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            adjustLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            adjustLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            adjustLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            AddField(adjustLayout, 0, 0, "Số lượng đích", _target);
            AddField(adjustLayout, 2, 0, "Slot nhận thêm", _destinationSlot);
            AddField(adjustLayout, 0, 1, "LOT đang chọn", _selectedLotLabel);
            AddField(adjustLayout, 2, 1, "Mã hàng", _selectedItemLabel);
            AddField(adjustLayout, 0, 2, "Lý do", _reason, 3);
            _target.Properties.MinValue = 0;
            _target.Properties.MaxValue = int.MaxValue;
            _target.Properties.IsFloatValue = false;
            _destinationSlot.Properties.NullValuePrompt = "Để trống = A0 mặc định";
            _reason.Properties.NullValuePrompt = "Bắt buộc nhập lý do điều chỉnh";
            _reason.Properties.ScrollBars = ScrollBars.Vertical;
            _reason.Properties.WordWrap = true;
            _reason.MinimumSize = new System.Drawing.Size(0, 72);
            adjustGroup.Controls.Add(adjustLayout);
            bottomLayout.Controls.Add(adjustGroup, 0, 0);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(2),
                ColumnCount = 2,
                RowCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _apply.Text = "Xác nhận điều chỉnh LOT đã chọn";
            _apply.Dock = DockStyle.Fill;
            _apply.Enabled = false;
            _apply.Click += (s, e) => ApplyAdjustment();
            footer.Controls.Add(_apply, 0, 0);
            _summary.Dock = DockStyle.Fill;
            _summary.AutoSizeMode = LabelAutoSizeMode.None;
            _summary.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _summary.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            footer.Controls.Add(_summary, 1, 0);
            bottomLayout.Controls.Add(footer, 0, 1);
        }

        private readonly LabelControl _selectedLotLabel = new LabelControl();
        private readonly LabelControl _selectedItemLabel = new LabelControl();

        private static void ConfigureGrid(GridControl grid, GridView view)
        {
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            view.FocusRectStyle = DrawFocusRectStyle.RowFocus;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ShowAutoFilterRow = true;
            view.OptionsView.ColumnAutoWidth = false;
        }

        private static void AddField(TableLayoutPanel layout, int col, int row, string caption, Control editor, int span = 1)
        {
            var label = new LabelControl { Text = caption, Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            label.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            editor.Dock = DockStyle.Fill;
            layout.Controls.Add(label, col, row);
            layout.Controls.Add(editor, col + 1, row);
            if (span > 1) layout.SetColumnSpan(editor, span);
        }

        private void SearchLots()
        {
            string raw = (_lookupKey.Text ?? string.Empty).Trim();
            if (raw.Length == 0 && _lookupKey.EditValue != null)
                raw = Convert.ToString(_lookupKey.EditValue)?.Trim() ?? string.Empty;
            if (raw.Length == 0)
            {
                XtraMessageBox.Show(this, "Nhập mã hàng, LOT hoặc quét QR trước khi tra cứu.", Text);
                _lookupKey.Focus();
                return;
            }

            try
            {
                string itemKey = null;
                string lotKey = null;
                string[] qrParts = raw.Split(':');
                if (qrParts.Length == 4 || qrParts.Length == 6)
                {
                    // QR FCC có dạng LOT:PART:NGAYSX:SLTEM hoặc thêm 2 trường tem tổng.
                    // STOCKTP lưu LOT khóa gốc; QR có thể mang cả counter + quantity ở 8 ký tự cuối.
                    lotKey = PCTP.Common.LotCodeHelper.StripCounterAndQty(qrParts[0].Trim());
                    itemKey = qrParts[1].Replace(" ", string.Empty).Trim();
                }

                DataTable stockRows = _stockTp.GetAllLotsForAdjustment();
                _activeSlotLots = _slotRepo.GetAllActiveSlotLots();
                _searchResults = new DataTable();
                _searchResults.Columns.Add("LOT", typeof(string));
                _searchResults.Columns.Add("ItemCode", typeof(string));
                _searchResults.Columns.Add("TenHang", typeof(string));
                _searchResults.Columns.Add("STOCKTP", typeof(int));
                _searchResults.Columns.Add("TongSlotLot", typeof(int));
                _searchResults.Columns.Add("ChenhLech", typeof(int));
                _searchResults.Columns.Add("SoSlot", typeof(int));
                _searchResults.Columns.Add("ViTri", typeof(string));

                foreach (DataRow stock in stockRows.Rows)
                {
                    string lot = Convert.ToString(stock["LOT"])?.Trim() ?? string.Empty;
                    string item = Convert.ToString(stock["PART"])?.Trim() ?? string.Empty;
                    string name = Convert.ToString(stock["NAME"]) ?? string.Empty;
                    bool match;

                    if (lotKey != null)
                    {
                        match = string.Equals(
                                item.Replace("-", string.Empty).Replace(" ", string.Empty),
                                itemKey.Replace("-", string.Empty).Replace(" ", string.Empty),
                                StringComparison.OrdinalIgnoreCase)
                            && (LotCodeHelper.AreLotKeysEquivalent(lot, lotKey)
                                || LotCodeHelper.AreLotKeysEquivalent(lot, qrParts[0].Trim()));
                    }
                    else
                    {
                        match = item.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0
                            || lot.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0
                            || LotCodeHelper.AreLotKeysEquivalent(lot, raw);
                    }
                    if (!match) continue;

                    var locations = _activeSlotLots
                        .Where(x => LotCodeHelper.AreLotKeysEquivalent((x.LotNo ?? string.Empty).Trim(), lot))
                        .ToList();
                    int slotQty = locations.Where(x => string.Equals((x.ItemCode ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity);
                    string locationText = string.Join(", ", locations.Select(x =>
                        string.Format("{0}/{1}/{2} (SlotId:{3}, SL:{4:n0})", x.WarehouseName, x.RackName, x.SlotNumber, x.SlotId, x.Quantity)).Distinct());
                    _searchResults.Rows.Add(lot, item, name, Convert.ToInt32(stock["SLCONLAI"]),
                        slotQty, Convert.ToInt32(stock["SLCONLAI"]) - slotQty,
                        locations.Select(x => x.SlotId).Distinct().Count(), locationText);
                }

                _lotGrid.DataSource = _searchResults;
                _lotView.PopulateColumns();
                _lotView.Columns["LOT"].Caption = "LOT";
                _lotView.Columns["ItemCode"].Caption = "Mã hàng";
                _lotView.Columns["TenHang"].Caption = "Tên hàng";
                _lotView.Columns["STOCKTP"].Caption = "Tồn STOCKTP";
                _lotView.Columns["TongSlotLot"].Caption = "Tổng SlotLot";
                _lotView.Columns["ChenhLech"].Caption = "Chênh lệch";
                _lotView.Columns["SoSlot"].Caption = "Số Slot";
                _lotView.Columns["ViTri"].Caption = "Các vị trí";
                _lotView.BestFitColumns();

                _hasPreview = false;
                _apply.Enabled = false;
                _slotGrid.DataSource = null;
                _selectedLot = null;
                _selectedItemCode = null;
                _selectedLotLabel.Text = string.Empty;
                _selectedItemLabel.Text = string.Empty;

                if (_searchResults.Rows.Count == 0)
                {
                    _summary.Text = "Không tìm thấy LOT phù hợp trong STOCKTP.";
                    return;
                }

                _lotView.FocusedRowHandle = 0;
                LoadSelectedLot();
                _summary.Text = "Tìm thấy " + _searchResults.Rows.Count + " LOT. Chọn LOT cụ thể để xem các SlotLot và điều chỉnh.";
            }
            catch (Exception ex)
            {
                _summary.Text = "Tra cứu thất bại: " + ex.Message;
                XtraMessageBox.Show(this, _summary.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadSelectedLot()
        {
            if (_lotView.FocusedRowHandle < 0) return;
            DataRow row = _lotView.GetDataRow(_lotView.FocusedRowHandle);
            if (row == null) return;

            _selectedLot = Convert.ToString(row["LOT"])?.Trim();
            _selectedItemCode = Convert.ToString(row["ItemCode"])?.Trim();
            _stockBefore = Convert.ToInt32(row["STOCKTP"]);
            _slotBefore = Convert.ToInt32(row["TongSlotLot"]);
            _selectedLotLabel.Text = _selectedLot;
            _selectedItemLabel.Text = _selectedItemCode;
            _target.Value = _stockBefore;

            var details = (_activeSlotLots ?? new List<PCTP.Modules.KhoCore.Models.SlotLotViewInfo>())
                .Where(x => LotCodeHelper.AreLotKeysEquivalent((x.LotNo ?? string.Empty).Trim(), _selectedLot))
                .Where(x => string.Equals((x.ItemCode ?? string.Empty).Trim(), _selectedItemCode, StringComparison.OrdinalIgnoreCase))
                .Select(x => new
                {
                    x.LotNo, x.ItemCode, x.WarehouseName, x.RackName, x.SlotNumber,
                    x.SlotId, x.SlotLotId, x.Quantity, x.TemCode
                }).ToList();

            _slotGrid.DataSource = details;
            _slotView.PopulateColumns();
            if (_slotView.Columns["LotNo"] != null) _slotView.Columns["LotNo"].Caption = "LOT";
            if (_slotView.Columns["ItemCode"] != null) _slotView.Columns["ItemCode"].Caption = "Mã hàng";
            if (_slotView.Columns["WarehouseName"] != null) _slotView.Columns["WarehouseName"].Caption = "Kho";
            if (_slotView.Columns["RackName"] != null) _slotView.Columns["RackName"].Caption = "Kệ";
            if (_slotView.Columns["SlotNumber"] != null) _slotView.Columns["SlotNumber"].Caption = "Ô";
            if (_slotView.Columns["SlotId"] != null) _slotView.Columns["SlotId"].Caption = "SlotId";
            if (_slotView.Columns["SlotLotId"] != null) _slotView.Columns["SlotLotId"].Caption = "SlotLotId";
            if (_slotView.Columns["Quantity"] != null) _slotView.Columns["Quantity"].Caption = "Số lượng";
            if (_slotView.Columns["TemCode"] != null) _slotView.Columns["TemCode"].Caption = "Mã tem";
            _slotView.BestFitColumns();

            _summary.Text = string.Format(
                "LOT {0} / Mã hàng {1} — STOCKTP: {2:n0}; tổng SlotLot: {3:n0}; chênh lệch: {4:+#;-#;0}.",
                _selectedLot, _selectedItemCode, _stockBefore, _slotBefore, _stockBefore - _slotBefore);
            _hasPreview = true;
            _apply.Enabled = true;
        }

        private void ApplyAdjustment()
        {
            if (!_hasPreview || string.IsNullOrWhiteSpace(_selectedLot) || string.IsNullOrWhiteSpace(_selectedItemCode))
            {
                XtraMessageBox.Show(this, "Hãy tra cứu và chọn một LOT cụ thể trước khi điều chỉnh.", Text);
                return;
            }

            string reason = (_reason.Text ?? string.Empty).Trim();
            if (reason.Length == 0)
            {
                XtraMessageBox.Show(this, "Phải nhập lý do điều chỉnh.", Text);
                _reason.Focus();
                return;
            }

            int? destination = null;
            string destinationText = (_destinationSlot.Text ?? string.Empty).Trim();
            if (destinationText.Length > 0)
            {
                if (!int.TryParse(destinationText, out int parsed) || parsed <= 0)
                {
                    XtraMessageBox.Show(this, "Slot nhận phải là SlotId nguyên dương; để trống nếu dùng A0.", Text);
                    return;
                }
                destination = parsed;
            }

            int target = Convert.ToInt32(_target.Value);
            string prompt = string.Format(
                "CƠ SỞ DỮ LIỆU ĐÍCH: {5}\r\n\r\nXác nhận điều chỉnh LOT [{0}] / Item [{1}] về số lượng đích {2:n0}?\r\nSTOCKTP: {3:n0} → {2:n0}\r\nSlotLot: {4:n0} → {2:n0}\r\n\r\nHệ thống sẽ ghi lịch sử và rollback toàn bộ nếu invariant không đạt.",
                _selectedLot, _selectedItemCode, target, _stockBefore, _slotBefore, _databaseTarget);
            if (XtraMessageBox.Show(this, prompt, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _apply.Enabled = false;
            try
            {
                _uow.Begin();
                _adjustmentRepo.AdjustLotToTarget(
                    _selectedItemCode, _selectedLot, target, destination,
                    reason, Environment.UserName, _requestId);
                _uow.Commit();
                XtraMessageBox.Show(this,
                    "Đã điều chỉnh LOT và xác minh STOCKTP = tổng SlotLot = " + target.ToString("n0") + ".",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                _requestId = Guid.NewGuid();
                _reason.Text = string.Empty;
                SearchLots();
            }
            catch (Exception ex)
            {
                try { _uow.Rollback(); } catch { }
                XtraMessageBox.Show(this,
                    "Điều chỉnh thất bại; transaction đã được rollback.\r\n\r\n" + ex.Message +
                    "\r\n\r\nNếu lỗi thiếu bảng PCTPStockLotAdjustmentHistory, hãy chạy SQL/PCTP_StockLotAdjustmentHistory.sql trên đúng cơ sở dữ liệu đang cấu hình trước khi sử dụng.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                _apply.Enabled = _hasPreview;
            }
        }
    }
}
