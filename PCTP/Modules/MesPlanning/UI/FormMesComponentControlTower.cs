using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.Data;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Security;
using PCTP.Shared.MesControls;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// MES CONTROL TOWER - Dòng chảy linh kiện. Màn hình giám sát chỉ đọc 6 chặng:
    /// 01 PO linh kiện → 02 Tem thùng / pallet → 03 Nhập kho PART → 04 Yêu cầu → 05 Xuất kho → 06 Tồn LotNo &amp; toàn nhà máy.
    /// Trả lời "LotNo nào đang kẹt ở chặng nào". CHỌN DÒNG LINH KIỆN chuyển ngữ cảnh sang
    /// QUẢN LÝ LINH KIỆN MES (FormKhoLinhKien) để làm sâu; màn hình này không ghi, không chuyển trạng thái.
    /// Dữ liệu: MES.usp_FVN_ComponentFlow_Grid / _Summary (migration 082).
    /// QC / đóng gói / yêu cầu khác chưa có bảng dữ liệu; chặng 04 chỉ đếm yêu cầu sản xuất theo BOM.
    /// </summary>
    public sealed class FormMesComponentControlTower : XtraForm
    {
        private static readonly string[] StageKeys = { "PO", "LABEL", "RECEIPT", "REQUEST", "ISSUE", "STOCK" };

        private readonly IComponentFlowService _service;
        private readonly FvnSiteDateRangeFilterControl _filters = new FvnSiteDateRangeFilterControl();
        private readonly FvnFlowStageCardsControl _stageCards = new FvnFlowStageCardsControl();
        private readonly FvnDepartmentTaskGridControl _grid = new FvnDepartmentTaskGridControl();
        private readonly FvnConsoleStatusBarControl _statusBar = new FvnConsoleStatusBarControl();
        private readonly SimpleButton _selectLine = new SimpleButton();
        private readonly SimpleButton _filterMenu = new SimpleButton();
        private readonly LabelControl _filterChip = new LabelControl();
        private readonly LabelControl _status = new LabelControl();
        private readonly LabelControl _refreshStamp = new LabelControl();
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private readonly System.Windows.Forms.Timer _refreshTimer = new System.Windows.Forms.Timer();
        private DataTable _data = new DataTable();
        private DataRow _summary;
        private string _filterMode = "ACTIVE";

        public FormMesComponentControlTower()
            : this(MesComponentFlowComposition.Create())
        {
        }

        public FormMesComponentControlTower(IComponentFlowService service)
        {
            AuthorizationGuard.Demand(ComponentFlowService.Function, "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Text = "MES CONTROL TOWER - Dòng chảy linh kiện";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1440, 720);
            _filters.RefreshRequested += (s, e) => StartLoad();
            BuildUi();
            ConfigureWorker();
            Load += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_filters.SiteCode))
                {
                    var scope = UserSession.Current.DataScopes.FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x.SiteCode) &&
                        (string.IsNullOrWhiteSpace(x.FunctionCode) ||
                         string.Equals(x.FunctionCode, ComponentFlowService.Function, StringComparison.OrdinalIgnoreCase)));
                    if (scope != null) _filters.SiteCode = scope.SiteCode;
                }
                StartLoad();
            };
            FormClosed += (s, e) => _refreshTimer.Stop();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 4),
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var header = new PanelControl { Dock = DockStyle.Fill, BorderStyle = BorderStyles.Simple, Padding = new Padding(8, 6, 8, 4) };
            _filters.Dock = DockStyle.Left;
            _filters.Width = 700;
            header.Controls.Add(_filters);
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 230,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 6, 0, 0)
            };
            _selectLine.Text = "CHỌN DÒNG LINH KIỆN";
            _selectLine.Width = 200;
            _selectLine.Height = 32;
            _selectLine.Enabled = UserSession.Current.HasPermission(ComponentFlowService.Function, "VIEW");
            _selectLine.ToolTip = "Chuyển ngữ cảnh dòng đang chọn sang màn hình Quản lý linh kiện MES.";
            _selectLine.Click += (s, e) => OpenComponentConsole();
            actions.Controls.Add(_selectLine);
            header.Controls.Add(actions);

            _stageCards.Dock = DockStyle.Fill;
            _stageCards.CardClicked += (s, e) => ApplyFilter();

            _grid.Dock = DockStyle.Fill;
            _grid.View.RowStyle += View_RowStyle;
            _grid.View.CustomUnboundColumnData += View_CustomUnboundColumnData;
            _grid.View.DoubleClick += (s, e) => OpenComponentConsole();

            var gridHost = new Panel { Dock = DockStyle.Fill };
            var filterBar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 4) };
            _filterMenu.Text = "▼  Lọc";
            _filterMenu.Width = 84;
            _filterMenu.Height = 30;
            _filterMenu.Dock = DockStyle.Left;
            _filterMenu.Click += (s, e) => ShowFilterMenu();
            _filterChip.AutoSizeMode = LabelAutoSizeMode.None;
            _filterChip.Dock = DockStyle.Fill;
            _filterChip.Padding = new Padding(10, 0, 0, 0);
            _filterChip.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            filterBar.Controls.Add(_filterChip);
            filterBar.Controls.Add(_filterMenu);
            gridHost.Controls.Add(_grid);
            gridHost.Controls.Add(filterBar);

            var footer = new PanelControl { Dock = DockStyle.Fill, BorderStyle = BorderStyles.NoBorder };
            _status.Dock = DockStyle.Fill;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _refreshStamp.Dock = DockStyle.Right;
            _refreshStamp.Width = 220;
            _refreshStamp.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Far;
            _refreshStamp.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            footer.Controls.Add(_status);
            footer.Controls.Add(_refreshStamp);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_stageCards, 0, 1);
            root.Controls.Add(gridHost, 0, 2);
            root.Controls.Add(footer, 0, 3);

            _statusBar.UserName = UserSession.Current.DisplayName ?? UserSession.Current.UserName;
            Controls.Add(root);
            Controls.Add(_statusBar);
            UpdateFilterChip();
        }

        // ------------------------------------------------------------------ nạp dữ liệu
        private void ConfigureWorker()
        {
            _worker.DoWork += (s, e) =>
            {
                var a = (LoadArgs)e.Argument;
                e.Result = _service.GetSnapshot(a.Site, a.From, a.To);
            };
            _worker.RunWorkerCompleted += (s, e) =>
            {
                _filters.SetBusy(false);
                if (e.Error != null)
                {
                    _status.Text = "⚠  Không tải được dòng chảy linh kiện: " + e.Error.GetBaseException().Message;
                    _statusBar.IsConnected = false;
                    return;
                }
                _statusBar.IsConnected = true;
                var snapshot = (ComponentFlowSnapshot)e.Result;
                _data = snapshot.Grid ?? new DataTable();
                _summary = snapshot.Summary;
                PrepareColumns(_data);
                _grid.Bind(_data);
                ConfigureColumns();
                ApplyFilter();
                UpdateCards();
                _refreshStamp.Text = "Cập nhật: " + DateTime.Now.ToString("HH:mm:ss");
                _status.Text = "Đang theo dõi " + _data.Rows.Count.ToString("N0") +
                    " dòng PO / LotNo trong phạm vi Site được cấp. Màn hình chỉ đọc.";
            };
            _refreshTimer.Interval = 15000;
            _refreshTimer.Tick += (s, e) => StartLoad();
            _refreshTimer.Start();
        }

        private void StartLoad()
        {
            if (_worker.IsBusy) return;
            if (string.IsNullOrWhiteSpace(_filters.SiteCode))
            {
                _status.Text = "⚠  Không tìm thấy Site trong phạm vi được cấp cho tài khoản.";
                return;
            }
            _filters.SetBusy(true);
            _status.Text = "Đang đồng bộ PO, tem, nhập kho, yêu cầu, xuất kho và tồn LotNo...";
            _worker.RunWorkerAsync(new LoadArgs { Site = _filters.SiteCode, From = _filters.FromDate, To = _filters.ToDate });
        }

        // ------------------------------------------------------------------ cột hiển thị
        private static string StageName(string stage)
        {
            switch (stage)
            {
                case "PO": return "01 PO linh kiện";
                case "LABEL": return "02 Tem thùng / Pallet";
                case "RECEIPT": return "03 Nhập kho PART";
                case "REQUEST": return "04 Yêu cầu SX";
                case "ISSUE": return "05 Xuất kho";
                case "STOCK": return "06 Tồn WIP / LotNo";
                default: return stage;
            }
        }

        private static void PrepareColumns(DataTable table)
        {
            foreach (var name in new[] { "StageText", "PartText", "WarningText" })
                if (!table.Columns.Contains(name)) table.Columns.Add(name, typeof(string));
            if (!table.Columns.Contains("HasWarning")) table.Columns.Add("HasWarning", typeof(bool));
            foreach (DataRow row in table.Rows)
            {
                row["StageText"] = StageName(Convert.ToString(row["FlowStage"]));
                string desc = Convert.ToString(row["PartDescription"]);
                row["PartText"] = string.IsNullOrWhiteSpace(desc) ? Convert.ToString(row["PartNo"]) : desc;
                bool warn = row["Warning"] != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(row["Warning"]));
                row["HasWarning"] = warn;
                row["WarningText"] = warn ? Convert.ToString(row["Warning"]) : "—";
            }
        }

        private void ConfigureColumns()
        {
            var view = _grid.View;
            foreach (DevExpress.XtraGrid.Columns.GridColumn column in view.Columns) column.Visible = false;
            if (view.Columns["RowNo"] == null)
                view.Columns.AddField("RowNo").UnboundType = UnboundColumnType.Integer;

            string[,] layout =
            {
                { "RowNo", "STT", "56" }, { "PoLotKey", "Mã PO / LotNo", "250" }, { "PartText", "Tên linh kiện", "230" },
                { "SiteCode", "Site", "90" }, { "LocationText", "Vị trí / Tuyến", "170" }, { "StageText", "Công đoạn di chuyển", "170" },
                { "Qty", "Số lượng", "100" }, { "UnitCode", "Đơn vị", "70" }, { "TransactionDate", "Ngày giao dịch", "120" },
                { "MaterialStatus", "Trạng thái vật tư", "170" }, { "WarningText", "Cảnh báo / Điểm nghẽn", "250" },
                { "StatusText", "Trạng thái", "120" }
            };
            for (int i = 0; i < layout.GetLength(0); i++)
            {
                var column = view.Columns[layout[i, 0]];
                if (column == null) continue;
                column.Caption = layout[i, 1];
                column.Width = int.Parse(layout[i, 2]);
                column.Visible = true;
                column.VisibleIndex = i;
            }
            var qty = view.Columns["Qty"];
            if (qty != null)
            {
                qty.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
                qty.DisplayFormat.FormatString = "n0";
                qty.SummaryItem.SummaryType = SummaryItemType.Sum;
                qty.SummaryItem.DisplayFormat = "{0:n0}";
            }
            var date = view.Columns["TransactionDate"];
            if (date != null)
            {
                date.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                date.DisplayFormat.FormatString = "dd/MM/yyyy";
            }
            view.OptionsView.ShowFooter = true;
            view.OptionsView.ShowAutoFilterRow = false;
        }

        private void View_CustomUnboundColumnData(object sender, DevExpress.XtraGrid.Views.Base.CustomColumnDataEventArgs e)
        {
            if (e.Column.FieldName == "RowNo" && e.IsGetData) e.Value = e.ListSourceRowIndex + 1;
        }

        private void View_RowStyle(object sender, RowStyleEventArgs e)
        {
            if (e.RowHandle < 0) return;
            var row = _grid.View.GetDataRow(e.RowHandle);
            if (row == null || !row.Table.Columns.Contains("HasWarning")) return;
            if (Convert.ToBoolean(row["HasWarning"]) && e.RowHandle != _grid.View.FocusedRowHandle)
            {
                e.Appearance.BackColor = Color.FromArgb(255, 243, 214);
                e.Appearance.Options.UseBackColor = true;
            }
        }

        // ------------------------------------------------------------------ lọc
        private void ShowFilterMenu()
        {
            var menu = new ContextMenuStrip();
            Action<string, string> add = (caption, mode) =>
            {
                var item = new ToolStripMenuItem(caption) { Checked = _filterMode == mode };
                item.Click += (s, e) => { _filterMode = mode; ApplyFilter(); };
                menu.Items.Add(item);
            };
            add("Dòng chảy Đang hoạt động & Cảnh báo", "ACTIVE");
            add("Chỉ dòng có cảnh báo", "WARNING");
            add("Hoàn thành", "DONE");
            add("Tất cả dòng chảy", "ALL");
            menu.Show(_filterMenu, new Point(0, _filterMenu.Height));
        }

        private void ApplyFilter()
        {
            if (!_data.Columns.Contains("StatusText")) { UpdateFilterChip(); return; }
            var parts = new List<string>();
            switch (_filterMode)
            {
                case "ACTIVE": parts.Add("([StatusText] <> 'Hoàn thành' OR [HasWarning] = True)"); break;
                case "WARNING": parts.Add("[HasWarning] = True"); break;
                case "DONE": parts.Add("[StatusText] = 'Hoàn thành'"); break;
            }
            int selected = _stageCards.SelectedIndex;
            if (selected >= 0 && selected < StageKeys.Length) parts.Add("[FlowStage] = '" + StageKeys[selected] + "'");
            _grid.View.ActiveFilterString = string.Join(" AND ", parts);
            UpdateFilterChip();
        }

        private void UpdateFilterChip()
        {
            string mode = _filterMode == "WARNING" ? "Chỉ dòng có cảnh báo" :
                _filterMode == "DONE" ? "Hoàn thành" :
                _filterMode == "ALL" ? "Tất cả dòng chảy" : "Dòng chảy Linh kiện Đang hoạt động & Cảnh báo";
            string[] names = { "01 PO linh kiện", "02 Tem thùng / Pallet", "03 Nhập kho PART", "04 Yêu cầu", "05 Xuất kho", "06 Tồn LotNo" };
            int selected = _stageCards.SelectedIndex;
            _filterChip.Text = "Bộ lọc: " + mode +
                (selected >= 0 && selected < names.Length ? "  ›  Chặng " + names[selected] : string.Empty);
        }

        // ------------------------------------------------------------------ 6 thẻ chặng
        private static string Compact(decimal value)
        {
            if (value >= 1000000m) return (value / 1000000m).ToString("0.#") + "M";
            if (value >= 1000m) return (value / 1000m).ToString(value >= 100000m ? "0" : "0.#") + "K";
            return value.ToString("N0");
        }

        private static decimal Dec(DataRow row, string column)
        {
            if (row == null || !row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return 0m;
            return Convert.ToDecimal(row[column]);
        }

        private void UpdateCards()
        {
            var poStatus = new Dictionary<long, string>();
            int lots = 0, printed = 0, received = 0, waitingReceive = 0;
            foreach (DataRow row in _data.Rows)
            {
                poStatus[Convert.ToInt64(row["PurchaseOrderId"])] = Convert.ToString(row["PoStatus"]);
                if (row["ComponentLotId"] == DBNull.Value) continue;
                lots++;
                if (Convert.ToBoolean(row["IsPrinted"])) printed++;
                decimal qty = Dec(row, "Qty"), got = Dec(row, "ReceivedQty");
                if (got > 0m) received++;
                if (got < qty) waitingReceive++;
            }
            int poTotal = poStatus.Count;
            int poReleased = poStatus.Values.Count(v => v != "DRAFT" && v != "VALIDATED");
            int poWaiting = poTotal - poReleased;

            decimal partQty = Dec(_summary, "PartOnHandQty");
            decimal wipQty = Dec(_summary, "WipOnHandQty");
            int openDemand = (int)Dec(_summary, "OpenDemandCount");
            int shortDemand = (int)Dec(_summary, "ShortDemandCount");
            int readyDemand = (int)Dec(_summary, "ReadyDemandCount");
            int issues = (int)Dec(_summary, "PostedIssueCount");

            var K = PCTP.Shared.MesControls.FvnCardBadgeKind.Info;
            var W = PCTP.Shared.MesControls.FvnCardBadgeKind.Warning;
            var N = PCTP.Shared.MesControls.FvnCardBadgeKind.None;
            var cards = new List<FvnFlowStageCard>
            {
                new FvnFlowStageCard
                {
                    Caption = "01  PO LINH KIỆN", Value = poTotal.ToString("N0"), Unit = "PO", Glyph = "☰",
                    Accent = Color.FromArgb(46, 110, 190), SubText = "Draft → Released",
                    BadgeText = poReleased + " Đã phát hành", BadgeKind = K,
                    BadgeText2 = poWaiting + " Chờ", BadgeKind2 = N
                },
                new FvnFlowStageCard
                {
                    Caption = "02  TEM THÙNG / PALLET", Value = lots.ToString("N0"), Unit = "Lô", Glyph = "▥",
                    Accent = Color.FromArgb(0, 150, 160), SubText = "In mã vạch & QR LotNo",
                    BadgeText = printed + " Đã in", BadgeKind = K,
                    BadgeText2 = (lots - printed) + " Chờ in", BadgeKind2 = N
                },
                new FvnFlowStageCard
                {
                    Caption = "03  NHẬP KHO PART", Value = received.ToString("N0"), Unit = "Lô", Glyph = "⛟",
                    Accent = Color.FromArgb(92, 84, 200), SubText = "Kho linh kiện Part",
                    BadgeText = waitingReceive > 0 ? waitingReceive + " Chờ nhập" : string.Empty, BadgeKind = W
                },
                new FvnFlowStageCard
                {
                    Caption = "04  YÊU CẦU", Value = openDemand.ToString("N0"), Unit = "Yêu cầu", Glyph = "✎",
                    Accent = Color.FromArgb(232, 130, 20), SubText = "Yêu cầu SX theo BOM",
                    BadgeText = shortDemand > 0 ? shortDemand + " Thiếu linh kiện" : string.Empty, BadgeKind = W
                },
                new FvnFlowStageCard
                {
                    Caption = "05  XUẤT KHO", Value = issues.ToString("N0"), Unit = "Phiếu", Glyph = "⇥",
                    Accent = Color.FromArgb(40, 120, 210), SubText = "Part → WIP / Sản xuất",
                    BadgeText = readyDemand + " Chờ xuất", BadgeKind = K
                },
                new FvnFlowStageCard
                {
                    Caption = "06  TỒN LOTNO & TOÀN NHÀ MÁY", Value = Compact(partQty + wipQty), Unit = "pcs", Glyph = "✔",
                    Accent = Color.FromArgb(34, 160, 84), SubText = "Tồn WIP: " + Compact(wipQty),
                    BadgeText = wipQty > 0m ? "Tồn WIP chưa tiêu hao" : string.Empty, BadgeKind = W
                }
            };
            _stageCards.SetCards(cards);
        }

        // ------------------------------------------------------------------ chuyển ngữ cảnh sang Quản lý linh kiện MES
        private void OpenComponentConsole()
        {
            var row = _grid.View.GetFocusedDataRow();
            if (row == null)
            {
                XtraMessageBox.Show(this, "Hãy chọn một dòng PO / LotNo trong danh sách trước.",
                    "Chọn dòng linh kiện", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                using (var form = new PCTP.Modules.KhoLinhKien.UI.FormKhoLinhKien())
                {
                    form.PreloadContext(
                        Convert.ToString(row["SiteCode"]),
                        Convert.ToString(row["PurchaseOrderNo"]),
                        row["LotNo"] == DBNull.Value ? string.Empty : Convert.ToString(row["LotNo"]),
                        Convert.ToString(row["PartNo"]),
                        Convert.ToString(row["FlowStage"]));
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Quản lý linh kiện MES", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            StartLoad();
        }

        private sealed class LoadArgs
        {
            public string Site;
            public DateTime? From;
            public DateTime? To;
        }
    }
}