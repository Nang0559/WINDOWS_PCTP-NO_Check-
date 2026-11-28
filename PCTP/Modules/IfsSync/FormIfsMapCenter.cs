using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Modules.IfsSync
{
    /// <summary>
    /// Trung tâm đồng bộ &amp; map IFS (cùng kiểu Control Tower của Điều hành sản xuất): một màn hình cho cả chuỗi
    /// IFS → Site → Work Center → Chuyền → Kho → Location → Slot. Mỗi công đoạn là một thẻ cho biết đã map / tổng số và còn bao nhiêu chưa map;
    /// bấm thẻ mở đúng màn hình xử lý, đóng màn hình đó thì số liệu tự làm mới. Nút "Đồng bộ IFS" mở màn đồng bộ dữ liệu IFS.
    /// Chỉ đọc số liệu từ MES.vw_FVN_IfsMapHub (migration 085); mọi thay đổi vẫn do từng màn hình riêng thực hiện với quyền riêng.
    /// </summary>
    public sealed class FormIfsMapCenter : XtraForm
    {
        private sealed class Stage
        {
            public string Code, Title, Hint;
            public Color Accent;
            public KeyValuePair<string, Func<Form>>[] Actions;
        }

        private readonly string _site;
        private readonly IIfsMapCenterRepository _repository;
        private readonly Stage[] _stages;
        private readonly Dictionary<string, Panel> _cards = new Dictionary<string, Panel>();
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _next = new LabelControl { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(12, 6, 0, 0) };
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };

        public FormIfsMapCenter(string siteCode) : this(siteCode, new IfsMapCenterRepository()) { }

        public FormIfsMapCenter(string siteCode, IIfsMapCenterRepository repository)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            AuthorizationGuard.Demand(IfsMapCenterRepository.HubFunctionCode, "VIEW");
            _site = siteCode.Trim().ToUpperInvariant();
            _repository = repository ?? throw new ArgumentNullException("repository");
            _view = new GridView(_grid);
            _stages = BuildStages();
            Text = "Trung tâm đồng bộ & Map IFS - Site " + _site;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1280, 720);
            BuildUi();
            Load += (s, e) => Reload();
        }

        private Stage[] BuildStages()
        {
            Func<string, Func<Form>, KeyValuePair<string, Func<Form>>> a = (t, f) => new KeyValuePair<string, Func<Form>>(t, f);
            string site = _site;
            return new[]
            {
                new Stage { Code = "SITE", Title = "01 SITE", Hint = "Site IFS được phép dùng ở PCTP", Accent = Color.FromArgb(52, 120, 220),
                    Actions = new[] { a("Khai báo Site PCTP…", () => new FormSiteSetup()) } },
                new Stage { Code = "WORK_CENTER", Title = "02 WORK CENTER", Hint = "Work Center IFS ↔ bộ phận LOT", Accent = Color.FromArgb(30, 160, 170),
                    Actions = new[]
                    {
                        a("① Work Center ↔ mã bộ phận 4 ký tự…", () => new FormDepartmentCodeCrossRef(site)),
                        a("③ Liên kết Work Center ↔ Bộ phận…", () => new FormWorkCenterDepartmentLink(site))
                    } },
                new Stage { Code = "PRODUCTION_LINE", Title = "03 CHUYỀN", Hint = "Chuyền IFS ↔ mã chuyền LOT", Accent = Color.FromArgb(110, 90, 210),
                    Actions = new[]
                    {
                        a("② Mã chuyền 5 ký tự ↔ 3 ký tự…", () => new FormLineCodeCrossRef(site)),
                        a("④ Khai báo chuyền sản xuất…", () => new PCTP.Modules.MesPlanning.UI.FormProductionLineSetup(site))
                    } },
                new Stage { Code = "WAREHOUSE", Title = "04 KHO", Hint = "Kho IFS ↔ dbo.FVN_Warehouse", Accent = Color.FromArgb(235, 140, 40),
                    Actions = new[] { a("Map Kho PCTP ↔ Kho IFS…", () => new FormWarehouseIfsMap(site)) } },
                new Stage { Code = "LOCATION", Title = "05 LOCATION", Hint = "Location IFS đã gán cho Slot", Accent = Color.FromArgb(200, 90, 130),
                    Actions = new[] { a("Map Slot ↔ MES Location…", () => new PCTP.Modules.KhoLinhKien.UI.FormSlotMesLocation(site)) } },
                new Stage { Code = "SLOT", Title = "06 SLOT", Hint = "Slot sẵn sàng nhập / xuất linh kiện", Accent = Color.FromArgb(40, 160, 90),
                    Actions = new[] { a("Map Slot ↔ MES Location…", () => new PCTP.Modules.KhoLinhKien.UI.FormSlotMesLocation(site)) } }
            };
        }

        private void BuildUi()
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            top.Controls.Add(Button("LÀM MỚI", 100, (s, e) => Reload()));
            var sync = Button("ĐỒNG BỘ DỮ LIỆU IFS…", 190, (s, e) => OpenForm(() => new FormIfsSync()));
            top.Controls.Add(sync);
            top.Controls.Add(new LabelControl
            {
                Text = "Chuỗi: IFS → Site → Work Center → Chuyền → Kho → Location → Slot. Bấm thẻ để xử lý phần còn thiếu.",
                Padding = new Padding(12, 8, 0, 0), AutoSizeMode = LabelAutoSizeMode.Default
            });

            var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = _stages.Length, Padding = new Padding(8) };
            for (int i = 0; i < _stages.Length; i++)
            {
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _stages.Length));
                Panel card = BuildCard(_stages[i]);
                _cards[_stages[i].Code] = card;
                cards.Controls.Add(card, i, 0);
            }

            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ColumnAutoWidth = true;
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);

            Controls.Add(_grid);
            Controls.Add(cards);
            Controls.Add(top);
            Controls.Add(_next);
            Controls.Add(_status);
        }

        private Panel BuildCard(Stage stage)
        {
            var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
            var count = new Label { Name = "count", Dock = DockStyle.Top, Height = 44, Font = new Font("Segoe UI", 20F, FontStyle.Bold), Text = "–", Padding = new Padding(8, 4, 0, 0) };
            var pending = new Label { Name = "pending", Dock = DockStyle.Bottom, Height = 24, Padding = new Padding(8, 2, 0, 0), ForeColor = Color.FromArgb(70, 85, 100), Text = "" };
            var hint = new Label { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0), ForeColor = Color.FromArgb(90, 100, 115), Text = stage.Hint };
            var title = new Label { Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Padding = new Padding(8, 6, 0, 0), Text = stage.Title };
            // WinForms: điều khiển Fill thêm trước, các cạnh thêm sau (cạnh thêm sau nằm ngoài cùng).
            card.Controls.Add(hint);
            card.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 5, BackColor = stage.Accent });
            card.Controls.Add(pending);
            card.Controls.Add(count);
            card.Controls.Add(title);
            EventHandler click = (s, e) => RunStage(stage, card);
            foreach (Control c in card.Controls) c.Click += click;
            card.Click += click;
            return card;
        }

        private static SimpleButton Button(string text, int width, EventHandler click)
        {
            var b = new SimpleButton { Text = text, Width = width, Height = 30 };
            b.Click += click;
            return b;
        }

        private void RunStage(Stage stage, Control anchor)
        {
            if (stage.Actions.Length == 1) { OpenForm(stage.Actions[0].Value); return; }
            var menu = new ContextMenuStrip();
            foreach (var action in stage.Actions)
            {
                var factory = action.Value;
                menu.Items.Add(action.Key, null, (s, e) => OpenForm(factory));
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        /// <summary>Mở màn hình xử lý dạng hộp thoại; lỗi quyền / dữ liệu hiện thông báo rõ, đóng xong thì làm mới số liệu.</summary>
        private void OpenForm(Func<Form> factory)
        {
            try
            {
                using (Form f = factory()) f.ShowDialog(this);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể mở màn hình.\r\n\r\n" + ex.GetBaseException().Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Reload();
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = error ? Color.Firebrick : Color.FromArgb(70, 85, 100);
        }

        private void Reload()
        {
            try
            {
                DataTable hub = _repository.GetHub(_site);
                var byStage = new Dictionary<string, DataRow>();
                foreach (DataRow r in hub.Rows) byStage[Convert.ToString(r["StageCode"])] = r;

                string firstPending = null;
                foreach (Stage stage in _stages)
                {
                    Panel card = _cards[stage.Code];
                    var count = (Label)card.Controls.Find("count", false)[0];
                    var pending = (Label)card.Controls.Find("pending", false)[0];
                    DataRow row;
                    if (!byStage.TryGetValue(stage.Code, out row))
                    {
                        count.Text = "0";
                        pending.Text = "Chưa có dữ liệu IFS";
                        pending.ForeColor = Color.DarkOrange;
                        if (firstPending == null) firstPending = stage.Title + ": chưa có dữ liệu IFS, đồng bộ nhóm tương ứng trước.";
                        continue;
                    }
                    int total = Convert.ToInt32(row["TotalCount"]), mapped = Convert.ToInt32(row["MappedCount"]), left = Convert.ToInt32(row["PendingCount"]);
                    count.Text = mapped + " / " + total;
                    pending.Text = left == 0 ? "Hoàn tất" : left + " chưa map";
                    pending.ForeColor = left == 0 ? Color.SeaGreen : Color.DarkOrange;
                    if (left > 0 && firstPending == null) firstPending = stage.Title + ": còn " + left + " mục chưa map — bấm thẻ để xử lý.";
                }

                var display = hub.Copy();
                _grid.DataSource = display.DefaultView;
                Cap("StageCode", "Công đoạn"); Cap("TotalCount", "Tổng (IFS)"); Cap("MappedCount", "Đã map");
                Cap("PendingCount", "Chưa map"); Cap("LastSyncUtc", "Đồng bộ lần cuối (UTC)");
                foreach (string hidden in new[] { "SiteCode", "StageOrder" })
                    if (_view.Columns[hidden] != null) _view.Columns[hidden].Visible = false;
                _next.Text = "Bước tiếp theo: " + (firstPending ?? "tất cả công đoạn của Site " + _site + " đã map đầy đủ.");
                SetStatus(hub.Rows.Count == 0
                    ? "Site " + _site + " chưa có dữ liệu IFS. Mở \"Đồng bộ dữ liệu IFS\" và đồng bộ SITE, WORK_CENTER, PRODUCTION_LINE, WAREHOUSE, INVENTORY_LOCATION."
                    : "Số liệu theo Site " + _site + " lúc " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), hub.Rows.Count == 0);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private void Cap(string field, string caption)
        {
            if (_view.Columns[field] != null) _view.Columns[field].Caption = caption;
        }
    }
}
