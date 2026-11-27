using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.Utils.Layout;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Modules.KhoLinhKien.UI
{
    /// <summary>
    /// Khai báo quy cách đóng gói linh kiện theo Site (thay B20ItemQuyCach của Bravo):
    ///   SL / thùng (Quantity) → tem thùng: phát hành PO chia số lượng đặt thành các thùng theo số này;
    ///   SL / palet (MaxCloseQty) → tem palet: gom các thùng liên tiếp lên palet cho đến khi vượt số này.
    /// Giao diện theo chuẩn DevExpress (GroupControl + TablePanel, SplitContainerControl, GridView với FormatRules /
    /// unbound column, SpinEdit + ErrorText). Có panel "Xem trước chia tem" (nhập SL đặt → số thùng, số palet),
    /// Upload Excel dùng chung bộ đọc Bravo, và Đồng bộ trực tiếp từ Bravo dbo.B20ItemQuyCach (có chạy thử trước khi ghi).
    /// Quyền và Site được service kiểm lại, SQL kiểm lại mã vật tư.
    /// </summary>
    public sealed class FormComponentPackSpec : XtraForm
    {
        private const string QtyFormat = "#,##0.###";

        private readonly string _site;
        private readonly ComponentPackSpecService _service;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
        private readonly LabelControl _previewPart = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
        private readonly SpinEdit _previewQty = new SpinEdit();
        private readonly MemoEdit _previewPlan = new MemoEdit();

        public FormComponentPackSpec(string siteCode)
            : this(siteCode, new ComponentPackSpecService(new ComponentPackSpecRepository())) { }

        public FormComponentPackSpec(string siteCode, ComponentPackSpecService service)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _site = siteCode.Trim().ToUpperInvariant();
            _service = service ?? throw new ArgumentNullException("service");
            _view = new GridView(_grid);
            Text = "Quy cách đóng gói linh kiện - Site " + _site;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 680);
            MinimumSize = new Size(900, 520);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { e.Handled = true; Reload(); } };
            BuildUi();
            Load += (s, e) => Reload();
        }

        // ───────────────────────────── Giao diện ─────────────────────────────

        private void BuildUi()
        {
            // 1) Thao tác
            var actions = new GroupControl { Text = "Thao tác", Dock = DockStyle.Top, Height = 70 };
            var actionTable = new TablePanel { Dock = DockStyle.Fill };
            int[] widths = { 100, 90, 90, 100, 150, 150, 140 };
            foreach (int w in widths)
                actionTable.Columns.Add(new TablePanelColumn { Style = TablePanelEntityStyle.Absolute, Width = w });
            actionTable.Columns.Add(new TablePanelColumn { Style = TablePanelEntityStyle.Relative, Width = 1F });
            actionTable.Rows.Add(new TablePanelRow { Style = TablePanelEntityStyle.Relative, Height = 1F });
            string[] captions = { "Tải lại (F5)", "Thêm…", "Sửa…", "Vô hiệu", "Upload Excel…", "Đồng bộ từ Bravo…", "File Excel mẫu…" };
            EventHandler[] handlers =
            {
                (s, e) => Reload(), (s, e) => Edit(null), (s, e) => EditFocused(),
                (s, e) => DeactivateFocused(), (s, e) => UploadExcel(), (s, e) => SyncBravo(), (s, e) => SaveTemplate()
            };
            string[] tips =
            {
                "Nạp lại danh sách quy cách của Site.", "Thêm quy cách cho một mã vật tư.", "Sửa dòng đang chọn (hoặc nhấp đúp).",
                "Ngừng dùng quy cách của dòng đang chọn.", "Upload Excel quy cách (cột như Bravo B20ItemQuyCach: ItemCode, Quantity, MaxCloseQty…).",
                "Đọc trực tiếp bảng B20ItemQuyCach của Bravo, xem kết quả chạy thử rồi mới ghi.", "Tải file Excel mẫu."
            };
            for (int i = 0; i < captions.Length; i++)
            {
                var b = new SimpleButton { Text = captions[i], Dock = DockStyle.Fill, ToolTip = tips[i] };
                b.Margin = new Padding(0, 2, 8, 2);
                b.Click += handlers[i];
                actionTable.Controls.Add(b);
                actionTable.SetCell(b, 0, i);
            }
            actions.Controls.Add(actionTable);

            // 2) Lưới quy cách
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsView.EnableAppearanceEvenRow = true;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.OptionsFind.AlwaysVisible = false;
            _view.DoubleClick += (s, e) => EditFocused();
            _view.FocusedRowChanged += (s, e) => UpdatePreview();

            // 3) Xem trước chia tem
            var preview = new GroupControl { Text = "Xem trước chia tem (theo dòng đang chọn)", Dock = DockStyle.Fill };
            var previewTable = new TablePanel { Dock = DockStyle.Fill };
            previewTable.Columns.Add(new TablePanelColumn { Style = TablePanelEntityStyle.AutoSize });
            previewTable.Columns.Add(new TablePanelColumn { Style = TablePanelEntityStyle.Relative, Width = 1F });
            previewTable.Rows.Add(new TablePanelRow { Style = TablePanelEntityStyle.Absolute, Height = 28F });
            previewTable.Rows.Add(new TablePanelRow { Style = TablePanelEntityStyle.Absolute, Height = 32F });
            previewTable.Rows.Add(new TablePanelRow { Style = TablePanelEntityStyle.Relative, Height = 1F });

            _previewPart.Appearance.FontStyleDelta = FontStyle.Bold;
            _previewPart.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            _previewQty.Properties.Mask.EditMask = "n3";
            _previewQty.Properties.Mask.UseMaskAsDisplayFormat = true;
            _previewQty.Properties.MinValue = 0;
            _previewQty.Properties.MaxValue = 1000000000;
            _previewQty.Properties.Increment = 100;
            _previewQty.EditValueChanged += (s, e) => UpdatePreview();
            _previewPlan.Properties.ReadOnly = true;
            _previewPlan.Properties.WordWrap = true;
            _previewPlan.Properties.ScrollBars = ScrollBars.Vertical;

            AddCaption(previewTable, "Mã vật tư", 0);
            AddCell(previewTable, _previewPart, 0, 1);
            AddCaption(previewTable, "SL đặt (1 dòng PO)", 1);
            AddCell(previewTable, _previewQty, 1, 1);
            _previewPlan.Dock = DockStyle.Fill;
            _previewPlan.Margin = new Padding(0, 6, 0, 0);
            previewTable.Controls.Add(_previewPlan);
            previewTable.SetCell(_previewPlan, 2, 0);
            previewTable.SetColumnSpan(_previewPlan, 2);
            preview.Controls.Add(previewTable);

            var split = new SplitContainerControl
            {
                Dock = DockStyle.Fill, Horizontal = false, FixedPanel = SplitFixedPanel.Panel2, SplitterPosition = 380
            };
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(preview);

            // 4) Thanh trạng thái
            var statusBar = new PanelControl { Dock = DockStyle.Bottom, Height = 30, BorderStyle = BorderStyles.NoBorder };
            _status.Padding = new Padding(8, 0, 8, 0);
            _status.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            statusBar.Controls.Add(_status);

            Controls.Add(split);
            Controls.Add(statusBar);
            Controls.Add(actions);
        }

        private static void AddCaption(TablePanel t, string caption, int row)
        {
            var l = new LabelControl { Text = caption };
            l.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            l.Padding = new Padding(0, 0, 8, 0);
            t.Controls.Add(l);
            t.SetCell(l, row, 0);
        }

        private static void AddCell(TablePanel t, Control c, int row, int col)
        {
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 2, 0, 2);
            t.Controls.Add(c);
            t.SetCell(c, row, col);
        }

        private void ConfigureColumns()
        {
            _view.BeginUpdate();
            try
            {
                foreach (GridColumn c in _view.Columns) c.Visible = false;
                Col("SupplierCode", "Nhà cung cấp (trống = mọi NCC)", 190, 0);
                Col("PartNo", "Mã vật tư", 170, 1);
                GridColumn pack = Col("PackQty", "SL / thùng (Quantity)", 130, 2, QtyFormat);
                GridColumn max = Col("MaxCloseQty", "SL / palet (MaxCloseQty)", 150, 3, QtyFormat);
                Col("IsActive", "Đang dùng", 80, 6);
                Col("LastUpdateUtc", "Cập nhật (UTC)", 140, 7, "dd/MM/yyyy HH:mm");
                Col("UpdatedBy", "Người sửa", 100, 8);
                Col("CreatedBy", "Người tạo", 100, 9);

                // Cột tính sẵn từ quy cách: palet chứa bao nhiêu thùng và SL palet thực tế (thùng đóng sẵn không chia nhỏ).
                GridColumn perPallet = Unbound("CartonsPerPallet", "Thùng / palet", 95, 4,
                    "Iif(IsNull([MaxCloseQty]), 0, Iif(Floor([MaxCloseQty] / [PackQty]) < 1, 1, Floor([MaxCloseQty] / [PackQty])))");
                GridColumn palletQty = Unbound("PalletQtyActual", "SL palet thực tế", 120, 5, "[CartonsPerPallet] * [PackQty]");

                _view.FormatRules.Clear();
                _view.FormatRules.Add(Rule("inactive", null, "[IsActive] = False", Color.FromArgb(120, 120, 120), null));
                _view.FormatRules.Add(Rule("noMax", max, "IsNull([MaxCloseQty])", null, Color.FromArgb(255, 241, 204)));
                _view.FormatRules.Add(Rule("notMultiple", palletQty,
                    "[CartonsPerPallet] > 0 And [PalletQtyActual] != [MaxCloseQty]", null, Color.FromArgb(255, 226, 190)));
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

        private GridColumn Unbound(string field, string caption, int width, int order, string expression)
        {
            GridColumn c = _view.Columns[field] ?? _view.Columns.AddField(field);
            c.Caption = caption;
            c.UnboundType = UnboundColumnType.Decimal;
            c.UnboundExpression = expression;
            c.Width = width; c.Visible = true; c.VisibleIndex = order;
            c.OptionsColumn.AllowEdit = false;
            c.DisplayFormat.FormatType = FormatType.Custom;
            c.DisplayFormat.FormatString = "#,##0.###;;";   // 0 = chưa có SL / palet → để trống
            return c;
        }

        private static GridFormatRule Rule(string name, GridColumn column, string expression, Color? fore, Color? back)
        {
            var cond = new FormatConditionRuleExpression { Expression = expression };
            if (fore.HasValue) { cond.Appearance.ForeColor = fore.Value; cond.Appearance.Options.UseForeColor = true; }
            if (back.HasValue) { cond.Appearance.BackColor = back.Value; cond.Appearance.Options.UseBackColor = true; }
            var rule = new GridFormatRule { Name = name, Rule = cond };
            if (column != null) rule.Column = column; else rule.ApplyToRow = true;
            return rule;
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = error ? Color.Firebrick : Color.FromArgb(70, 85, 100);
            _status.Appearance.Options.UseForeColor = true;
        }

        // ───────────────────────────── Dữ liệu ─────────────────────────────

        private void Reload()
        {
            try
            {
                DataTable table = _service.List(_site);
                bool first = _grid.DataSource == null;
                _grid.DataSource = table;
                if (first) ConfigureColumns();
                UpdatePreview();
                int noMax = table.Rows.Cast<DataRow>().Count(r => Convert.ToBoolean(r["IsActive"]) && r.IsNull("MaxCloseQty"));
                SetStatus(table.Rows.Count + " quy cách, Site " + _site + "." +
                          (noMax > 0 ? " " + noMax + " dòng đang dùng chưa có SL / palet (không in được tem palet)." : string.Empty), false);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private DataRow Focused() { return _view.GetFocusedDataRow(); }

        private void EditFocused()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            Edit(row);
        }

        private static ComponentPackSpecRow ToSpec(DataRow row)
        {
            decimal? max = row.IsNull("MaxCloseQty") ? (decimal?)null : Convert.ToDecimal(row["MaxCloseQty"]);
            return new ComponentPackSpecRow
            {
                SiteCode = Convert.ToString(row["SiteCode"]),
                SupplierCode = Convert.ToString(row["SupplierCode"]),
                PartNo = Convert.ToString(row["PartNo"]),
                PackQty = Convert.ToDecimal(row["PackQty"]),
                MaxCloseQty = max,
                IsActive = Convert.ToBoolean(row["IsActive"])
            };
        }

        // ───────────────────────────── Xem trước chia tem ─────────────────────────────

        private void UpdatePreview()
        {
            DataRow row = Focused();
            if (row == null)
            {
                _previewPart.Text = "(chưa chọn dòng)";
                _previewPlan.Text = "Chọn một quy cách ở lưới để xem số thùng / palet khi phát hành PO.";
                return;
            }
            ComponentPackSpecRow spec = ToSpec(row);
            _previewPart.Text = spec.PartNo + (spec.SupplierCode.Length > 0 ? " · NCC " + spec.SupplierCode : " · mọi NCC");
            if (_previewQty.Value <= 0)
            {
                _previewQty.EditValue = spec.PackQty * 3 + Math.Floor(spec.PackQty / 2m);   // ví dụ: 3 thùng rưỡi
                return;   // đổi EditValue sẽ gọi lại UpdatePreview
            }
            _previewPlan.Text = DescribePlan(_previewQty.Value, spec);
        }

        private static string DescribePlan(decimal orderQty, ComponentPackSpecRow spec)
        {
            CultureInfo ci = CultureInfo.CurrentCulture;
            try
            {
                ComponentPackSplitResult plan = ComponentPackSplit.Plan(orderQty, spec.PackQty, spec.MaxCloseQty);
                var sb = new StringBuilder();
                int full = plan.Cartons.Count(q => q == spec.PackQty);
                decimal rest = plan.Cartons.Count > full ? plan.Cartons[plan.Cartons.Count - 1] : 0m;
                sb.AppendLine("TEM THÙNG: " + plan.Cartons.Count + " tem");
                sb.AppendLine("  " + full + " × " + spec.PackQty.ToString(QtyFormat, ci) +
                              (rest > 0 ? " + 1 × " + rest.ToString(QtyFormat, ci) + " (thùng cuối giữ phần dư)" : string.Empty));
                sb.AppendLine();
                if (plan.Pallets.Count == 0)
                    sb.AppendLine("TEM PALET: chưa khai báo SL / palet.");
                else
                {
                    sb.AppendLine("TEM PALET: " + plan.Pallets.Count + " palet (tối đa " + spec.MaxCloseQty.Value.ToString(QtyFormat, ci) + " / palet)");
                    foreach (ComponentPalletPlan p in plan.Pallets.Take(30))
                        sb.AppendLine("  Palet " + p.Seq + "/" + plan.Pallets.Count + ": " + p.CartonCount + " thùng = " + p.Qty.ToString(QtyFormat, ci));
                    if (plan.Pallets.Count > 30) sb.AppendLine("  …");
                }
                foreach (string w in plan.Warnings) { sb.AppendLine(); sb.AppendLine("⚠ " + w); }
                return sb.ToString();
            }
            catch (Exception ex) { return "⚠ " + ex.Message; }
        }

        // ───────────────────────────── Thêm / sửa / vô hiệu ─────────────────────────────

        private void Edit(DataRow row)
        {
            try
            {
                ComponentPackSpecRow spec = row == null ? new ComponentPackSpecRow { SiteCode = _site } : ToSpec(row);
                using (var dlg = new EditDialog(spec, row != null))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    _service.Save(new[] { dlg.Result });
                }
                Reload();
                SetStatus("Đã lưu quy cách.", false);
            }
            catch (Exception ex) { Fail("Lưu quy cách", ex); }
        }

        private void DeactivateFocused()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            try
            {
                ComponentPackSpecRow spec = ToSpec(row);
                if (!spec.IsActive) { SetStatus("Quy cách này đã vô hiệu.", true); return; }
                if (XtraMessageBox.Show(this, "Vô hiệu quy cách của " + spec.PartNo + "?\r\nPO phát hành sau đó sẽ không có quy cách cho mã này.",
                        "Quy cách đóng gói", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                spec.IsActive = false;
                _service.Save(new[] { spec });
                Reload();
                SetStatus("Đã vô hiệu quy cách " + spec.PartNo + ".", false);
            }
            catch (Exception ex) { Fail("Vô hiệu quy cách", ex); }
        }

        // ───────────────────────────── Excel / Bravo ─────────────────────────────

        private void UploadExcel()
        {
            try
            {
                string path;
                using (var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", Title = "Chọn file quy cách đóng gói" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    path = dlg.FileName;
                }
                var rows = ComponentPackSpecExcel.Read(path, _site);
                string message = "File " + Path.GetFileName(path) + ": " + rows.Count + " dòng quy cách.\r\n" +
                                 "Dòng đã có (cùng Site / nhà cung cấp / mã vật tư) sẽ được cập nhật. Toàn bộ file chạy trong một transaction.\r\n\r\nTiếp tục?";
                if (XtraMessageBox.Show(this, message, "Upload quy cách đóng gói", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                int n = _service.Save(rows);
                Reload();
                SetStatus("Đã upload " + n + " dòng quy cách.", false);
            }
            catch (Exception ex) { Fail("Upload quy cách", ex); }
        }

        /// <summary>Đồng bộ từ Bravo dbo.B20ItemQuyCach: chạy thử → hiện kết quả → hỏi → ghi.</summary>
        private void SyncBravo()
        {
            try
            {
                ComponentPackSpecImportResult dry = _service.ImportFromBravo(_site, true);
                string summary = DescribeImport(dry);
                if (dry.InsertRows + dry.UpdateRows == 0)
                {
                    XtraMessageBox.Show(this, summary + "\r\n\r\nKhông có gì để ghi.", "Đồng bộ từ Bravo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (XtraMessageBox.Show(this, summary + "\r\n\r\nGhi vào quy cách đóng gói của Site " + _site + "?",
                        "Đồng bộ từ Bravo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                ComponentPackSpecImportResult done = _service.ImportFromBravo(_site, false);
                Reload();
                SetStatus("Đã đồng bộ từ Bravo: thêm " + done.InsertRows + ", cập nhật " + done.UpdateRows + ", bỏ qua " + done.SkippedRows + ".", false);
            }
            catch (Exception ex) { Fail("Đồng bộ từ Bravo", ex); }
        }

        private static string DescribeImport(ComponentPackSpecImportResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bravo B20ItemQuyCach: " + r.TotalRows + " dòng (đã bỏ dòng nhóm, lấy dòng mới nhất cho mỗi NCC + mã).");
            sb.AppendLine("  Thêm mới: " + r.InsertRows);
            sb.AppendLine("  Cập nhật: " + r.UpdateRows);
            sb.AppendLine("  Không đổi: " + r.UnchangedRows);
            sb.AppendLine("  Bỏ qua: " + r.SkippedRows);
            foreach (var group in r.Skipped.GroupBy(s => s.Reason))
            {
                sb.AppendLine();
                sb.AppendLine(ReasonText(group.Key) + " (" + group.Count() + "): " +
                              string.Join(", ", group.Select(s => s.PartNo).Distinct().Take(10)) + (group.Count() > 10 ? ", …" : string.Empty));
            }
            return sb.ToString().TrimEnd();
        }

        private static string ReasonText(string reason)
        {
            switch (reason)
            {
                case "PACK_QTY_INVALID": return "SL / thùng (Quantity) trống hoặc ≤ 0";
                case "MAX_LT_PACK": return "SL / palet nhỏ hơn SL / thùng";
                case "PART_NOT_IN_IFS": return "Mã chưa có trong danh mục IFS của Site";
                default: return reason;
            }
        }

        private void SaveTemplate()
        {
            try
            {
                using (var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "QuyCachDongGoi_mau.xlsx" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    ComponentPackSpecExcel.WriteTemplate(dlg.FileName, _site);
                    SetStatus("Đã tạo file mẫu: " + dlg.FileName, false);
                }
            }
            catch (Exception ex) { Fail("File Excel mẫu", ex); }
        }

        private void Fail(string caption, Exception ex)
        {
            string message = ex.GetBaseException().Message;
            SetStatus(caption + " thất bại: " + message, true);
            XtraMessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ───────────────────────────── Hộp thoại thêm / sửa ─────────────────────────────

        private sealed class EditDialog : XtraForm
        {
            private readonly TextEdit _supplier = new TextEdit();
            private readonly TextEdit _part = new TextEdit();
            private readonly SpinEdit _qty = new SpinEdit();
            private readonly SpinEdit _max = new SpinEdit();
            private readonly CheckEdit _active = new CheckEdit { Text = "Đang dùng" };
            private readonly LabelControl _hint = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None };
            private readonly string _site;

            public ComponentPackSpecRow Result { get; private set; }

            public EditDialog(ComponentPackSpecRow spec, bool existing)
            {
                _site = spec.SiteCode;
                Text = existing ? "Sửa quy cách đóng gói" : "Thêm quy cách đóng gói";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(500, 330);

                _supplier.Properties.MaxLength = 60;
                _supplier.Properties.NullValuePrompt = "(trống = áp dụng mọi nhà cung cấp)";
                _supplier.Properties.NullValuePromptShowForEmptyValue = true;
                _part.Properties.MaxLength = 100;
                foreach (SpinEdit s in new[] { _qty, _max })
                {
                    s.Properties.Mask.EditMask = "n3";
                    s.Properties.Mask.UseMaskAsDisplayFormat = true;
                    s.Properties.MinValue = 0;
                    s.Properties.MaxValue = 1000000000;
                    s.Properties.Increment = 100;
                    s.EditValueChanged += (a, b) => RefreshHint();
                }
                _max.Properties.AllowNullInput = DefaultBoolean.True;
                _max.Properties.NullText = "(chưa khai báo)";

                var group = new GroupControl { Text = "Site " + _site, Dock = DockStyle.Top, Height = 250 };
                var table = new TablePanel { Dock = DockStyle.Fill };
                table.Columns.Add(new TablePanelColumn { Style = TablePanelEntityStyle.AutoSize });
                table.Columns.Add(new TablePanelColumn { Style = TablePanelEntityStyle.Relative, Width = 1F });
                for (int i = 0; i < 5; i++)
                    table.Rows.Add(new TablePanelRow { Style = TablePanelEntityStyle.Absolute, Height = 34F });
                table.Rows.Add(new TablePanelRow { Style = TablePanelEntityStyle.Relative, Height = 1F });
                AddRow(table, 0, "Nhà cung cấp", _supplier);
                AddRow(table, 1, "Mã vật tư (PartNo)", _part);
                AddRow(table, 2, "SL / thùng (Quantity)", _qty);
                AddRow(table, 3, "SL / palet (MaxCloseQty)", _max);
                AddRow(table, 4, string.Empty, _active);
                _hint.Dock = DockStyle.Fill;
                _hint.Appearance.TextOptions.WordWrap = WordWrap.Wrap;
                _hint.Appearance.Options.UseTextOptions = true;
                table.Controls.Add(_hint);
                table.SetCell(_hint, 5, 0);
                table.SetColumnSpan(_hint, 2);
                group.Controls.Add(table);

                var ok = new SimpleButton { Text = "Lưu", Width = 100, Location = new Point(280, 285) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 100, Location = new Point(390, 285), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(ok); Controls.Add(cancel); Controls.Add(group);
                AcceptButton = ok; CancelButton = cancel;

                _supplier.Text = spec.SupplierCode;
                _part.Text = spec.PartNo;
                _qty.EditValue = spec.PackQty > 0 ? spec.PackQty : 0m;
                _max.EditValue = spec.MaxCloseQty.HasValue ? (object)spec.MaxCloseQty.Value : null;
                _active.Checked = spec.IsActive;
                if (existing) { _supplier.Properties.ReadOnly = true; _part.Properties.ReadOnly = true; }   // khóa khóa chính
                RefreshHint();
            }

            private static void AddRow(TablePanel table, int row, string caption, Control editor)
            {
                if (caption.Length > 0)
                {
                    var l = new LabelControl { Text = caption };
                    l.Appearance.TextOptions.VAlignment = VertAlignment.Center;
                    l.Padding = new Padding(0, 0, 10, 0);
                    table.Controls.Add(l);
                    table.SetCell(l, row, 0);
                }
                editor.Dock = DockStyle.Fill;
                editor.Margin = new Padding(0, 3, 0, 3);
                table.Controls.Add(editor);
                table.SetCell(editor, row, 1);
            }

            private decimal Qty { get { return _qty.EditValue == null ? 0m : Convert.ToDecimal(_qty.EditValue); } }

            private decimal? Max
            {
                get
                {
                    if (_max.EditValue == null || _max.EditValue == DBNull.Value) return null;
                    decimal m = Convert.ToDecimal(_max.EditValue);
                    return m > 0 ? m : (decimal?)null;
                }
            }

            private void RefreshHint()
            {
                decimal qty = Qty;
                decimal? max = Max;
                if (qty <= 0) { _hint.Text = "Nhập SL / thùng: phát hành PO sẽ chia số lượng đặt thành các thùng theo số này."; return; }
                if (!max.HasValue) { _hint.Text = "Chưa có SL / palet: vẫn in được tem thùng, chưa in được tem palet."; return; }
                if (max.Value < qty) { _hint.Text = "⚠ SL / palet phải lớn hơn hoặc bằng SL / thùng."; return; }
                int per = ComponentPackSplit.CartonsPerPallet(qty, max);
                string actual = (per * qty).ToString(QtyFormat, CultureInfo.CurrentCulture);
                _hint.Text = "1 palet chứa tối đa " + per + " thùng = " + actual + "." +
                             (per * qty != max.Value ? "\r\n⚠ SL / palet không là bội số của SL / thùng: thùng đóng sẵn không chia nhỏ, palet thực tế là " + actual + "." : string.Empty);
            }

            private void Accept()
            {
                _part.ErrorText = null; _qty.ErrorText = null; _max.ErrorText = null;
                bool ok = true;
                if (string.IsNullOrWhiteSpace(_part.Text)) { _part.ErrorText = "Chưa nhập mã vật tư."; ok = false; }
                if (Qty <= 0) { _qty.ErrorText = "SL / thùng phải lớn hơn 0."; ok = false; }
                decimal? max = Max;
                if (max.HasValue && Qty > 0 && max.Value < Qty) { _max.ErrorText = "SL / palet không được nhỏ hơn SL / thùng."; ok = false; }
                if (!ok) return;
                Result = new ComponentPackSpecRow
                {
                    SiteCode = _site,
                    SupplierCode = (_supplier.Text ?? string.Empty).Trim(),
                    PartNo = _part.Text.Trim(),
                    PackQty = Qty,
                    MaxCloseQty = max,
                    IsActive = _active.Checked
                };
                DialogResult = DialogResult.OK;
            }
        }
    }
}
