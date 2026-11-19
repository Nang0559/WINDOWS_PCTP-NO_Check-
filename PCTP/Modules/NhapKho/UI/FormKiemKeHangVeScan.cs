using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Modules.NhapKho.ComponentReceiving;
using PCTP.Security;
using System;
using System.Data;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace PCTP.Modules.NhapKho.UI
{
    /// <summary>
    /// Màn hình "KIỂM KÊ HÀNG VỀ" của một yêu cầu: quét QR tem → nhận hàng vào Slot PART
    /// (MES.usp_FVN_ComponentReceipt_Post qua IComponentReceivingService) và cập nhật SL thực tế / chênh lệch.
    /// </summary>
    public sealed class FormKiemKeHangVeScan : XtraForm
    {
        private static readonly Color Warn = Color.FromArgb(255, 255, 0);

        private readonly IComponentReceivingService _receiving;
        private readonly IComponentReceivingQueryService _query;
        private readonly IComponentSlotLookupService _slotLookup;

        private readonly long _purchaseOrderId;
        private readonly string _siteCode;
        private readonly string _purchaseOrderNo;

        private readonly TextEdit _txtQr = new TextEdit();
        private readonly LookUpEdit _slot = new LookUpEdit();
        private readonly SpinEdit _qty = new SpinEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view = new GridView();
        private readonly LabelControl _status = new LabelControl();
        private readonly LabelControl _summary = new LabelControl();

        private string _pendingPayload;        // QR vừa lỗi — giữ khóa chống trùng để quét lại không ghi 2 lần
        private string _pendingKey;
        private bool _busy;

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
            BuildLayout(orderDate, createdBy);
            Load += (s, e) =>
            {
                LoadSlots();
                try { LoadLots(null); }
                catch (Exception ex) { SetStatus("Không tải được danh sách tem: " + ex.Message, Color.DarkRed); }
                _txtQr.Focus();
            };
        }

        // ───────────────────────────── Layout ─────────────────────────────

        private void BuildLayout(DateTime orderDate, string createdBy)
        {
            var title = new LabelControl
            {
                Text = "KIỂM KÊ HÀNG VỀ", Dock = DockStyle.Top, AutoSizeMode = LabelAutoSizeMode.None, Height = 34,
                Padding = new Padding(10, 6, 0, 0)
            };
            title.Appearance.Font = new Font("Tahoma", 13F, FontStyle.Bold);
            title.Appearance.ForeColor = Color.FromArgb(0, 70, 150);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 96, ColumnCount = 6, RowCount = 2, Padding = new Padding(10, 2, 10, 2)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            // Hàng 1: QR | Số | Ngày | Người lập
            _txtQr.Properties.NullValuePrompt = "Quét QR tem linh kiện (FVN1|LOT) rồi nhấn Enter";
            _txtQr.Properties.NullValuePromptShowForEmptyValue = true;
            _txtQr.Properties.Appearance.Font = new Font("Tahoma", 12F);
            _txtQr.KeyDown += TxtQrKeyDown;

            Add(header, 0, 0, "Quét QR tem");  header.Controls.Add(_txtQr, 1, 0);
            Add(header, 2, 0, "2. Số");        header.Controls.Add(RO(_purchaseOrderNo), 3, 0);
            Add(header, 4, 0, "1. Ngày");      header.Controls.Add(RO(orderDate.ToString("dd/MM/yyyy")), 5, 0);

            // Hàng 2: Slot | SL mỗi lần | Người lập
            _slot.Properties.NullText = "Chọn Slot nhập kho PART";
            _slot.Properties.Columns.Add(new LookUpColumnInfo("SlotDisplay", "Slot"));
            _slot.Properties.DisplayMember = "SlotDisplay";
            _slot.Properties.ValueMember = "SlotId";
            _slot.Properties.ShowHeader = false;
            _slot.Properties.SearchMode = SearchMode.AutoComplete;

            _qty.Properties.Increment = 1;
            _qty.Properties.DecimalPlaces = 3;
            _qty.Properties.MinValue = 0;
            _qty.Properties.MaxValue = 1000000000;
            _qty.Value = 0;

            Add(header, 0, 1, "Slot nhập kho"); header.Controls.Add(_slot, 1, 1);
            Add(header, 2, 1, "SL mỗi lần*");   header.Controls.Add(_qty, 3, 1);
            Add(header, 4, 1, "3. Người lập");  header.Controls.Add(RO(createdBy ?? string.Empty), 5, 1);

            var hint = new LabelControl
            {
                Dock = DockStyle.Top, AutoSizeMode = LabelAutoSizeMode.None, Height = 22,
                Text = "* SL mỗi lần = 0: nhận hết phần còn lại của tem vừa quét; nhập số > 0 để nhận từng phần.",
                Padding = new Padding(10, 3, 0, 0)
            };
            hint.Appearance.ForeColor = Color.DimGray;

            _grid.Dock = DockStyle.Fill;
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.EnableAppearanceEvenRow = true;
            _view.IndicatorWidth = 40;
            _view.CustomDrawRowIndicator += (s, e) =>
            { if (e.RowHandle >= 0) e.Info.DisplayText = (e.RowHandle + 1).ToString(); };
            _view.RowCellStyle += ViewRowCellStyle;
            _view.Appearance.HeaderPanel.Options.UseTextOptions = true;
            _view.Appearance.HeaderPanel.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            _view.ColumnPanelRowHeight = 40;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);

            _summary.Dock = DockStyle.Bottom;
            _summary.AutoSizeMode = LabelAutoSizeMode.None;
            _summary.Height = 24;
            _summary.Padding = new Padding(10, 3, 0, 0);
            _summary.Appearance.Font = new Font("Tahoma", 9F, FontStyle.Bold);

            _status.Dock = DockStyle.Bottom;
            _status.AutoSizeMode = LabelAutoSizeMode.None;
            _status.Height = 28;
            _status.Padding = new Padding(10, 5, 0, 0);
            _status.Appearance.Font = new Font("Tahoma", 10F);

            var btnClose = new SimpleButton { Text = "Đóng", Width = 90, Height = 28 };
            btnClose.Click += (s, e) => Close();
            var bottomBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.RightToLeft };
            bottomBar.Controls.Add(btnClose);

            Controls.Add(_grid);
            Controls.Add(hint);
            Controls.Add(header);
            Controls.Add(title);
            Controls.Add(_summary);
            Controls.Add(_status);
            Controls.Add(bottomBar);

            // Không có quyền xác nhận nhận hàng → chỉ xem.
            var session = UserSession.Current;
            if (session == null || !session.HasPermission("WAREHOUSE.COMPONENT_RECEIPT", "CONFIRM"))
            {
                _txtQr.Enabled = false; _slot.Enabled = false; _qty.Enabled = false;
                SetStatus("Tài khoản chỉ có quyền xem — không được xác nhận nhận hàng.", Color.DarkOrange);
            }
        }

        private static void Add(TableLayoutPanel p, int col, int row, string caption)
        {
            p.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold) }, col, row);
        }

        private static TextEdit RO(string text)
        {
            var t = new TextEdit { Text = text };
            t.Properties.ReadOnly = true;
            return t;
        }

        // ───────────────────────────── Dữ liệu ─────────────────────────────

        private void LoadSlots()
        {
            try
            {
                DataTable slots = _slotLookup.GetActiveSlots(_siteCode);
                _slot.Properties.DataSource = slots;
                if (slots.Rows.Count == 0)
                    SetStatus("Site " + _siteCode + " chưa có Slot PART hoạt động được map sang MES Location.", Color.DarkOrange);
                else if (slots.Rows.Count == 1)
                    _slot.EditValue = slots.Rows[0]["SlotId"];
            }
            catch (Exception ex)
            {
                SetStatus("Không tải được Slot: " + ex.Message, Color.DarkRed);
            }
        }

        private void LoadLots(string focusQr)
        {
            DataTable lots = _query.ListLots(_purchaseOrderId);
            _grid.DataSource = lots;
            ConfigureColumns();
            UpdateSummary(lots);

            if (!string.IsNullOrEmpty(focusQr))
                for (int i = 0; i < _view.DataRowCount; i++)
                    if (string.Equals(Convert.ToString(_view.GetRowCellValue(i, "QrPayload")), focusQr,
                        StringComparison.OrdinalIgnoreCase))
                    { _view.FocusedRowHandle = i; break; }
        }

        private void ConfigureColumns()
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
            Col("IssuedQty", "SL theo tem", 85, 11, "#,##0.###");
            Col("ReceivedQty", "SL thực tế", 85, 12, "#,##0.###");
            Col("DiffQty", "Chênh lệch", 85, 13, "#,##0.###");
        }

        private void Col(string field, string caption, int width, int order, string format = null)
        {
            GridColumn c = _view.Columns[field];
            if (c == null) return;
            c.Caption = caption; c.Width = width; c.Visible = true; c.VisibleIndex = order;
            if (format != null)
            {
                c.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Custom;
                c.DisplayFormat.FormatString = format;
            }
        }

        /// <summary>Tô vàng ô SL thực tế và Chênh lệch khi dòng còn lệch so với SL đặt.</summary>
        private void ViewRowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (e.Column == null) return;
            if (e.Column.FieldName != "ReceivedQty" && e.Column.FieldName != "DiffQty") return;
            object diff = _view.GetRowCellValue(e.RowHandle, "DiffQty");
            if (diff != null && diff != DBNull.Value && Convert.ToDecimal(diff) != 0m)
                e.Appearance.BackColor = Warn;
        }

        private void UpdateSummary(DataTable lots)
        {
            int total = lots.Rows.Count, done = 0, partial = 0;
            foreach (DataRow r in lots.Rows)
            {
                decimal issued = Convert.ToDecimal(r["IssuedQty"]), received = Convert.ToDecimal(r["ReceivedQty"]);
                if (received >= issued) done++; else if (received > 0) partial++;
            }
            _summary.Text = string.Format("Tổng {0} tem — đã nhận đủ {1}, nhận một phần {2}, chưa nhận {3}",
                total, done, partial, total - done - partial);
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
            if (_busy) return;
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

                _pendingPayload = null;
                _pendingKey = null;
                LoadLots(payload);
                SetStatus(string.Format("✔ Đã nhận {0:N3} — lô {1}. Phiếu #{2}, giao dịch kho #{3}.",
                    result.ReceivedQty, lot["LotNo"], result.ComponentReceiptId, result.InventoryTransactionId),
                    Color.DarkGreen);
                _txtQr.EditValue = null;
            }
            catch (Exception ex)
            {
                SystemSounds.Hand.Play();
                SetStatus("✘ " + ex.Message, Color.DarkRed);
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
            var table = _grid.DataSource as DataTable;
            if (table == null) return null;
            foreach (DataRow r in table.Rows)
                if (string.Equals(Convert.ToString(r["QrPayload"]), payload, StringComparison.OrdinalIgnoreCase))
                    return r;
            return null;
        }

        private void Reject(string message)
        {
            SystemSounds.Beep.Play();
            SetStatus("✘ " + message, Color.DarkRed);
            _txtQr.Focus();
            _txtQr.SelectAll();
        }

        private void SetStatus(string text, Color color)
        {
            _status.Appearance.ForeColor = color;
            _status.Text = text;
        }
    }
}
