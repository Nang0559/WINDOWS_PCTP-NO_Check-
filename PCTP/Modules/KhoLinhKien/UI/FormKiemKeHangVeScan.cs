using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.Utils.Layout;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Modules.NhapKho.ComponentReceiving;
using PCTP.Security;
using System;
using System.Data;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace PCTP.Modules.KhoLinhKien.UI
{
    /// <summary>
    /// Màn hình "KIỂM KÊ HÀNG VỀ" của một yêu cầu: quét QR tem → nhận hàng vào Slot PART
    /// (MES.usp_FVN_ComponentReceipt_Post qua IComponentReceivingService) và cập nhật SL thực tế / chênh lệch.
    ///
    /// Giao diện theo chuẩn DevExpress: GroupControl + TablePanel (không còn TableLayoutPanel/Label WinForms, không cố định font),
    /// khung kết quả quét màu theo trạng thái, ProgressBar tiến độ, GridView dùng FormatRules / unbound progress column /
    /// footer summary / auto-filter row. Font và màu nền lấy theo skin của ứng dụng.
    /// </summary>
    public sealed class FormKiemKeHangVeScan : XtraForm
    {
        private enum Kind { Info, Success, Warning, Error }

        // Slot gần nhất người dùng chọn — nhớ trong phiên làm việc để mỗi yêu cầu mới không phải chọn lại.
        private static int? _lastSlotId;

        private readonly IComponentReceivingService _receiving;
        private readonly IComponentReceivingQueryService _query;
        private readonly IComponentSlotLookupService _slotLookup;

        private readonly long _purchaseOrderId;
        private readonly string _siteCode;
        private readonly string _purchaseOrderNo;

        private readonly TextEdit _txtQr = new TextEdit();
        private readonly LookUpEdit _slot = new LookUpEdit();
        private readonly SpinEdit _qty = new SpinEdit();
        private readonly CheckEdit _onlyPending = new CheckEdit();
        private readonly TextEdit _txtSupplier = new TextEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly PanelControl _bannerPanel = new PanelControl();
        private readonly LabelControl _banner = new LabelControl();
        private readonly LabelControl _summary = new LabelControl();
        private readonly ProgressBarControl _progress = new ProgressBarControl();

        private DataTable _lots;               // một DataTable duy nhất, nạp lại bằng Merge để giữ cột / bộ lọc / vị trí cuộn
        private string _pendingPayload;        // QR vừa lỗi — giữ khóa chống trùng để quét lại không ghi 2 lần
        private string _pendingKey;
        private bool _busy;
        private bool _canReceive = true;

        public FormKiemKeHangVeScan(long purchaseOrderId, string siteCode, string purchaseOrderNo,
            DateTime orderDate, string createdBy)
            : this(purchaseOrderId, siteCode, purchaseOrderNo, orderDate, createdBy,
                new ComponentReceivingService(new ComponentReceivingRepository()),
                new ComponentReceivingQueryService(new ComponentReceivingQueryRepository()),
                new ComponentSlotLookupService(new ComponentSlotLookupRepository()))
        {
        }

        public FormKiemKeHangVeScan(long purchaseOrderId, string siteCode, string purchaseOrderNo,
            DateTime orderDate, string createdBy,
            IComponentReceivingService receiving, IComponentReceivingQueryService query,
            IComponentSlotLookupService slotLookup)
        {
            AuthorizationGuard.DemandScope("WAREHOUSE.COMPONENT_RECEIPT", "SITE", siteCode, siteCode);
            _receiving = receiving ?? throw new ArgumentNullException(nameof(receiving));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _slotLookup = slotLookup ?? throw new ArgumentNullException(nameof(slotLookup));
            _purchaseOrderId = purchaseOrderId;
            _siteCode = (siteCode ?? string.Empty).Trim().ToUpperInvariant();
            _purchaseOrderNo = purchaseOrderNo;

            Text = "Kiểm kê hàng về — " + purchaseOrderNo;
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1000, 600);
            KeyPreview = true;
            KeyDown += FormKeyDown;

            BuildLayout(orderDate, createdBy);
            Load += (s, e) =>
            {
                LoadSlots();
                try { LoadLots(null); }
                catch (Exception ex) { SetStatus("Không tải được danh sách tem: " + ex.Message, Kind.Error); }
                _txtQr.Focus();
            };
        }

        // ───────────────────────────── Layout ─────────────────────────────

        private void BuildLayout(DateTime orderDate, string createdBy)
        {
            // 1) Chứng từ (chỉ đọc): Số | Ngày | Nhà cung cấp | Người lập
            var info = new GroupControl { Text = "Chứng từ", Dock = DockStyle.Top, Height = 74 };
            var infoTable = NewTable(info, 1, new[]
            {
                Auto(), Rel(18), Auto(), Rel(14), Auto(), Rel(36), Auto(), Rel(18)
            });
            AddCaption(infoTable, "Số", 0, 0);            AddEditor(infoTable, ReadOnly(new TextEdit(), _purchaseOrderNo), 0, 1);
            AddCaption(infoTable, "Ngày", 0, 2);          AddEditor(infoTable, ReadOnly(new TextEdit(), orderDate.ToString("dd/MM/yyyy")), 0, 3);
            AddCaption(infoTable, "Nhà cung cấp", 0, 4);  AddEditor(infoTable, ReadOnly(_txtSupplier, string.Empty), 0, 5);
            AddCaption(infoTable, "Người lập", 0, 6);     AddEditor(infoTable, ReadOnly(new TextEdit(), createdBy ?? string.Empty), 0, 7);

            // 2) Quét tem: QR (lớn) | Slot | SL mỗi lần | chỉ tem chưa nhận
            var scan = new GroupControl { Text = "Quét tem nhập kho", Dock = DockStyle.Top, Height = 118 };
            var scanTable = NewTable(scan, 2, new[]
            {
                Auto(), Rel(46), Auto(), Rel(30), Auto(), Rel(24)
            });

            _txtQr.Properties.NullValuePrompt = "Quét QR tem linh kiện (FVN1|LOT) rồi nhấn Enter";
            _txtQr.Properties.NullValuePromptShowForEmptyValue = true;
            _txtQr.Properties.Appearance.FontSizeDelta = 4;
            _txtQr.Properties.Appearance.Options.UseFont = true;
            _txtQr.KeyDown += TxtQrKeyDown;

            _slot.Properties.NullText = "Chọn Slot nhập kho PART";
            _slot.Properties.Columns.Add(new LookUpColumnInfo("SlotDisplay", "Slot"));
            _slot.Properties.DisplayMember = "SlotDisplay";
            _slot.Properties.ValueMember = "SlotId";
            _slot.Properties.ShowHeader = false;
            _slot.Properties.SearchMode = SearchMode.AutoComplete;
            _slot.Properties.TextEditStyle = TextEditStyles.Standard;
            _slot.EditValueChanged += (s, e) =>
            {
                if (_slot.EditValue != null && _slot.EditValue != DBNull.Value)
                    _lastSlotId = Convert.ToInt32(_slot.EditValue);
            };

            _qty.Properties.Increment = 1;
            // RepositoryItemSpinEdit không có DecimalPlaces; cấu hình độ chính xác qua numeric mask.
            _qty.Properties.Mask.EditMask = "n3";
            _qty.Properties.Mask.UseMaskAsDisplayFormat = true;
            _qty.Properties.MinValue = 0;
            _qty.Properties.MaxValue = 1000000000;
            _qty.Value = 0;
            _qty.ToolTip = "0 = nhận hết phần còn lại của tem vừa quét; nhập số > 0 để nhận từng phần.";

            _onlyPending.Text = "Chỉ hiện tem chưa nhận đủ";
            _onlyPending.CheckedChanged += (s, e) =>
                _view.ActiveFilterString = _onlyPending.Checked ? "[ReceivedQty] < [IssuedQty]" : string.Empty;

            AddCaption(scanTable, "Quét QR tem", 0, 0);   AddEditor(scanTable, _txtQr, 0, 1, 5);
            AddCaption(scanTable, "Slot nhập kho", 1, 0); AddEditor(scanTable, _slot, 1, 1);
            AddCaption(scanTable, "SL mỗi lần", 1, 2);    AddEditor(scanTable, _qty, 1, 3);
            AddEditor(scanTable, _onlyPending, 1, 4, 2);

            // 3) Khung kết quả quét (màu theo trạng thái)
            _bannerPanel.Dock = DockStyle.Top;
            _bannerPanel.Height = 52;
            _bannerPanel.BorderStyle = BorderStyles.NoBorder;
            _banner.Dock = DockStyle.Fill;
            _banner.AutoSizeMode = LabelAutoSizeMode.None;
            _banner.Appearance.FontSizeDelta = 3;
            _banner.Appearance.FontStyleDelta = FontStyle.Bold;
            _banner.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _banner.Padding = new Padding(12, 0, 12, 0);
            _bannerPanel.Controls.Add(_banner);

            // 4) Lưới tem
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ShowFooter = true;
            _view.OptionsView.EnableAppearanceEvenRow = true;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.IndicatorWidth = 40;
            _view.CustomDrawRowIndicator += (s, e) =>
            { if (e.RowHandle >= 0) e.Info.DisplayText = (e.RowHandle + 1).ToString(); };
            _view.Appearance.HeaderPanel.Options.UseTextOptions = true;
            _view.Appearance.HeaderPanel.TextOptions.WordWrap = WordWrap.Wrap;
            _view.Appearance.HeaderPanel.TextOptions.HAlignment = HorzAlignment.Center;
            _view.ColumnPanelRowHeight = 40;

            // 5) Thanh dưới: tiến độ + tổng hợp + nút
            var bottom = new PanelControl { Dock = DockStyle.Bottom, Height = 60, BorderStyle = BorderStyles.NoBorder };
            var btnClose = new SimpleButton { Text = "Đóng", Width = 100, Dock = DockStyle.Right };
            btnClose.Click += (s, e) => Close();
            var btnRefresh = new SimpleButton { Text = "Làm mới (F5)", Width = 120, Dock = DockStyle.Right };
            btnRefresh.Click += (s, e) => Reload();
            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 22;
            _progress.Properties.Minimum = 0;
            _progress.Properties.Maximum = 100;
            _progress.Properties.ShowTitle = true;
            _progress.Properties.PercentView = true;
            _summary.Dock = DockStyle.Fill;
            _summary.AutoSizeMode = LabelAutoSizeMode.None;
            _summary.Appearance.FontStyleDelta = FontStyle.Bold;
            _summary.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _summary.Padding = new Padding(6, 0, 0, 0);
            bottom.Controls.Add(_summary);
            bottom.Controls.Add(btnRefresh);
            bottom.Controls.Add(btnClose);
            bottom.Controls.Add(_progress);

            // Thứ tự add: Fill trước, các vùng Dock sau (add sau = nằm trên cùng).
            Controls.Add(_grid);
            Controls.Add(bottom);
            Controls.Add(_bannerPanel);
            Controls.Add(scan);
            Controls.Add(info);

            // Không có quyền xác nhận nhận hàng → chỉ xem.
            var session = UserSession.Current;
            if (session == null || !session.HasPermission("WAREHOUSE.COMPONENT_RECEIPT", "CONFIRM"))
            {
                _canReceive = false;
                _txtQr.Enabled = false; _slot.Enabled = false; _qty.Enabled = false;
                SetStatus("Tài khoản chỉ có quyền xem — không được xác nhận nhận hàng.", Kind.Warning);
            }
            else
            {
                SetStatus("Sẵn sàng — quét QR tem để nhận hàng. F2: về ô quét · F5: làm mới.", Kind.Info);
            }
        }

        private static TablePanelColumn Auto() { return new TablePanelColumn { Style = TablePanelEntityStyle.AutoSize }; }
        private static TablePanelColumn Rel(float w) { return new TablePanelColumn { Style = TablePanelEntityStyle.Relative, Width = w }; }

        private static TablePanel NewTable(GroupControl host, int rows, TablePanelColumn[] columns)
        {
            var t = new TablePanel { Dock = DockStyle.Fill };
            t.Columns.AddRange(columns);
            for (int i = 0; i < rows; i++)
                t.Rows.Add(new TablePanelRow { Style = TablePanelEntityStyle.Relative, Height = 1F });
            host.Controls.Add(t);
            return t;
        }

        private static void AddCaption(TablePanel t, string caption, int row, int col)
        {
            var l = new LabelControl { Text = caption };
            l.Appearance.FontStyleDelta = FontStyle.Bold;
            l.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            l.Padding = new Padding(0, 0, 8, 0);
            t.Controls.Add(l);
            t.SetCell(l, row, col);
        }

        private static void AddEditor(TablePanel t, Control editor, int row, int col, int colSpan = 1)
        {
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 2, 12, 2);
            t.Controls.Add(editor);
            t.SetCell(editor, row, col);
            if (colSpan > 1) t.SetColumnSpan(editor, colSpan);
        }

        private static TextEdit ReadOnly(TextEdit edit, string text)
        {
            edit.Text = text;
            edit.Properties.ReadOnly = true;
            return edit;
        }

        // ───────────────────────────── Dữ liệu ─────────────────────────────

        private void LoadSlots()
        {
            try
            {
                DataTable slots = _slotLookup.GetActiveSlots(_siteCode);
                _slot.Properties.DataSource = slots;
                if (slots.Rows.Count == 0)
                    SetStatus("Site " + _siteCode + " chưa có Slot PART hoạt động được map sang MES Location.", Kind.Warning);
                else if (slots.Rows.Count == 1)
                    _slot.EditValue = slots.Rows[0]["SlotId"];
                else if (_lastSlotId.HasValue && slots.Select("SlotId = " + _lastSlotId.Value).Length > 0)
                    _slot.EditValue = _lastSlotId.Value;
            }
            catch (Exception ex)
            {
                SetStatus("Không tải được Slot: " + ex.Message, Kind.Error);
            }
        }

        private void Reload()
        {
            try { LoadLots(null); SetStatus("Đã làm mới danh sách tem.", Kind.Info); }
            catch (Exception ex) { SetStatus("Không tải được danh sách tem: " + ex.Message, Kind.Error); }
            _txtQr.Focus();
        }

        private void LoadLots(string focusQr)
        {
            DataTable fresh = _query.ListLots(_purchaseOrderId);
            if (_lots == null)
            {
                _lots = fresh;
                _grid.DataSource = _lots;
                ConfigureColumns();
            }
            else
            {
                _lots.BeginLoadData();
                _lots.Clear();
                _lots.Merge(fresh);
                _lots.EndLoadData();
            }

            if (_lots.Rows.Count > 0) _txtSupplier.Text = Convert.ToString(_lots.Rows[0]["SupplierName"]);
            UpdateSummary(_lots);

            if (!string.IsNullOrEmpty(focusQr))
                for (int i = 0; i < _view.DataRowCount; i++)
                    if (string.Equals(Convert.ToString(_view.GetRowCellValue(i, "QrPayload")), focusQr,
                        StringComparison.OrdinalIgnoreCase))
                    { _view.FocusedRowHandle = i; _view.MakeRowVisible(i); break; }
        }

        private void ConfigureColumns()
        {
            _view.BeginUpdate();
            try
            {
                foreach (GridColumn c in _view.Columns) c.Visible = false;
                Col("PurchaseOrderNo", "Đơn hàng", 100, 0);
                Col("SupplierName", "Nhà cung cấp", 230, 1);
                Col("SupplierCode", "Mã Maker", 80, 2);
                Col("PartNo", "Mặt hàng", 150, 3);
                Col("PartDescription", "Tên hàng hóa, dịch vụ", 240, 4);
                Col("UnitCode", "Đvt", 55, 5);
                Col("OrderDate", "Ngày đặt", 90, 6, "dd/MM/yyyy");
                Col("NeedByDate", "Ngày yêu cầu", 90, 7, "dd/MM/yyyy");
                Col("OrderQty", "SL đặt", 80, 8, "#,##0.###");
                Col("LotNo", "Số lô", 190, 9);
                Col("ArrivalDateUtc", "Ngày về", 95, 10, "dd/MM/yyyy");
                GridColumn issued = Col("IssuedQty", "SL theo tem", 85, 11, "#,##0.###");
                GridColumn received = Col("ReceivedQty", "SL thực tế", 85, 12, "#,##0.###");
                GridColumn diff = Col("DiffQty", "Chênh lệch", 85, 13, "#,##0.###");
                Sum(issued); Sum(received); Sum(diff);

                // Cột tiến độ nhận (unbound, tính bằng expression nên không cần code cập nhật).
                GridColumn pct = _view.Columns.AddField("PctReceived");
                pct.Caption = "% đã nhận";
                pct.UnboundType = UnboundColumnType.Decimal;
                pct.UnboundExpression = "Iif([IssuedQty] > 0, [ReceivedQty] * 100 / [IssuedQty], 0)";
                var bar = new RepositoryItemProgressBar { Minimum = 0, Maximum = 100, ShowTitle = true, PercentView = false };
                bar.DisplayFormat.FormatType = FormatType.Numeric;
                bar.DisplayFormat.FormatString = "n0";
                _grid.RepositoryItems.Add(bar);
                pct.ColumnEdit = bar;
                pct.Width = 110; pct.Visible = true; pct.VisibleIndex = 14;

                // Tô màu theo trạng thái nhận: đủ = xanh, một phần = vàng (thay cho vẽ tay trong RowCellStyle).
                _view.FormatRules.Clear();
                _view.FormatRules.Add(Rule("done", "[IssuedQty] > 0 And [ReceivedQty] >= [IssuedQty]", Color.FromArgb(220, 244, 224)));
                _view.FormatRules.Add(Rule("partial", "[ReceivedQty] > 0 And [ReceivedQty] < [IssuedQty]", Color.FromArgb(255, 245, 195)));
            }
            finally { _view.EndUpdate(); }
        }

        private GridColumn Col(string field, string caption, int width, int order, string format = null)
        {
            GridColumn c = _view.Columns[field];
            if (c == null) return null;
            c.Caption = caption; c.Width = width; c.Visible = true; c.VisibleIndex = order;
            if (format != null)
            {
                c.DisplayFormat.FormatType = FormatType.Custom;
                c.DisplayFormat.FormatString = format;
            }
            return c;
        }

        private static void Sum(GridColumn c)
        {
            if (c == null) return;
            c.Summary.Clear();
            c.Summary.Add(SummaryItemType.Sum, c.FieldName, "{0:#,##0.###}");
        }

        private static GridFormatRule Rule(string name, string expression, Color back)
        {
            var cond = new FormatConditionRuleExpression { Expression = expression };
            cond.Appearance.BackColor = back;
            cond.Appearance.Options.UseBackColor = true;
            return new GridFormatRule { Name = name, ApplyToRow = true, Rule = cond };
        }

        private void UpdateSummary(DataTable lots)
        {
            int total = lots.Rows.Count, done = 0, partial = 0;
            decimal issuedSum = 0m, receivedSum = 0m;
            foreach (DataRow r in lots.Rows)
            {
                decimal issued = Convert.ToDecimal(r["IssuedQty"]), received = Convert.ToDecimal(r["ReceivedQty"]);
                issuedSum += issued; receivedSum += received;
                if (received >= issued) done++; else if (received > 0) partial++;
            }
            _summary.Text = string.Format("Tổng {0} tem — đã nhận đủ {1}, nhận một phần {2}, chưa nhận {3}",
                total, done, partial, total - done - partial);
            _progress.Position = issuedSum <= 0m ? 0 : (int)Math.Min(100m, Math.Floor(receivedSum * 100m / issuedSum));
        }

        // ───────────────────────────── Phím tắt ─────────────────────────────

        private void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2) { e.Handled = true; _txtQr.Focus(); _txtQr.SelectAll(); }
            else if (e.KeyCode == Keys.F5) { e.Handled = true; Reload(); }
        }

        // ───────────────────────────── Quét QR ─────────────────────────────

        private void TxtQrKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            ProcessScan();
        }

        private void ProcessScan()
        {
            if (_busy || !_canReceive) return;
            string payload = Convert.ToString(_txtQr.EditValue ?? string.Empty).Trim();
            if (payload.Length == 0) return;

            DataRow lot = FindLot(payload);
            if (lot == null)
            {
                Reject("QR không thuộc yêu cầu " + _purchaseOrderNo + ": " + payload);
                return;
            }

            decimal remaining = Convert.ToDecimal(lot["IssuedQty"]) - Convert.ToDecimal(lot["ReceivedQty"]);
            if (remaining <= 0)
            {
                Reject("Tem " + lot["LotNo"] + " đã nhận đủ.");
                return;
            }
            if (_slot.EditValue == null || _slot.EditValue == DBNull.Value)
            {
                Reject("Chưa chọn Slot nhập kho PART.");
                _slot.Focus();
                return;
            }

            decimal qty = _qty.Value > 0 ? _qty.Value : remaining;
            if (qty > remaining)
            {
                Reject(string.Format("SL nhận {0:N3} vượt phần còn lại của tem ({1:N3}).", qty, remaining));
                return;
            }

            // Quét lại đúng QR vừa lỗi → dùng lại khóa cũ để không nhận trùng nếu lần trước đã ghi.
            if (!string.Equals(_pendingPayload, payload, StringComparison.OrdinalIgnoreCase))
            {
                _pendingPayload = payload;
                _pendingKey = Guid.NewGuid().ToString("N");
            }

            _busy = true;
            _txtQr.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                ComponentReceiptResult result = _receiving.Receive(new ComponentReceiptRequest
                {
                    SiteCode = _siteCode,
                    ReceiptNo = "KK-" + _purchaseOrderNo + "-" + DateTime.Now.ToString("yyMMdd"),
                    QrPayload = payload,
                    Quantity = qty,
                    TargetSlotId = Convert.ToInt32(_slot.EditValue),
                    IdempotencyKey = _pendingKey
                });

                string lotNo = Convert.ToString(lot["LotNo"]);
                _pendingPayload = null;
                _pendingKey = null;
                LoadLots(payload);
                SetStatus(string.Format("✔ Đã nhận {0:N3} — lô {1}. Phiếu #{2}, giao dịch kho #{3}.",
                    result.ReceivedQty, lotNo, result.ComponentReceiptId, result.InventoryTransactionId),
                    Kind.Success);
                _txtQr.EditValue = null;
            }
            catch (Exception ex)
            {
                SystemSounds.Hand.Play();
                SetStatus("✘ " + ex.Message, Kind.Error);
                XtraMessageBox.Show(this, ex.Message, "Không thể nhận linh kiện",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
                _txtQr.Enabled = true;
                _busy = false;
                _txtQr.Focus();
                _txtQr.SelectAll();
            }
        }

        private DataRow FindLot(string payload)
        {
            if (_lots == null) return null;
            foreach (DataRow r in _lots.Rows)
                if (string.Equals(Convert.ToString(r["QrPayload"]), payload, StringComparison.OrdinalIgnoreCase))
                    return r;
            return null;
        }

        private void Reject(string message)
        {
            SystemSounds.Beep.Play();
            SetStatus("✘ " + message, Kind.Error);
            _txtQr.Focus();
            _txtQr.SelectAll();
        }

        private void SetStatus(string text, Kind kind)
        {
            Color back, fore;
            switch (kind)
            {
                case Kind.Success: back = Color.FromArgb(214, 241, 220); fore = Color.FromArgb(18, 94, 38); break;
                case Kind.Error:   back = Color.FromArgb(251, 221, 221); fore = Color.FromArgb(150, 20, 20); break;
                case Kind.Warning: back = Color.FromArgb(255, 241, 204); fore = Color.FromArgb(120, 76, 0); break;
                default:           back = Color.FromArgb(230, 238, 249); fore = Color.FromArgb(30, 60, 110); break;
            }
            _bannerPanel.Appearance.BackColor = back;
            _bannerPanel.Appearance.Options.UseBackColor = true;
            _banner.Appearance.ForeColor = fore;
            _banner.Appearance.Options.UseForeColor = true;
            _banner.Text = text;
        }
    }
}
