using DevExpress.Data;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrintingLinks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SS = DevExpress.Spreadsheet;

namespace PCTP.Modules.BaoCao.UI
{
    /// <summary>Một lưới đang hiển thị trên form, được đưa nguyên trạng (cột đang hiện, thứ tự, bộ lọc) vào báo cáo.</summary>
    internal sealed class ReportGridSection
    {
        public ReportGridSection(string sheetName, string title, GridControl grid, GridView view)
        {
            SheetName = sheetName;
            Title = title;
            Grid = grid;
            View = view;
        }

        public string SheetName { get; private set; }
        public string Title { get; private set; }
        public GridControl Grid { get; private set; }
        public GridView View { get; private set; }
    }

    internal sealed class ReportOverviewRow
    {
        [DisplayName("Mục")] public string Muc { get; set; }
        [DisplayName("Giá trị")] public string GiaTri { get; set; }
    }

    internal sealed class TimelineReportRow
    {
        [DisplayName("STT")] public int Stt { get; set; }
        [DisplayName("Sự kiện")] public string SuKien { get; set; }
        [DisplayName("Loại sự kiện")] public string LoaiSuKien { get; set; }
        [DisplayName("Thời gian")] public DateTime? ThoiGian { get; set; }
        [DisplayName("LOT")] public string Lot { get; set; }
        [DisplayName("Mã hàng")] public string MaHang { get; set; }
        [DisplayName("Số lượng")] public decimal? SoLuong { get; set; }
        [DisplayName("Từ vị trí")] public string TuViTri { get; set; }
        [DisplayName("Đến vị trí")] public string DenViTri { get; set; }
        [DisplayName("QR")] public string Qr { get; set; }
        [DisplayName("LOT tem KH")] public string CustomerLabel { get; set; }
        [DisplayName("Khách hàng")] public string KhachHang { get; set; }
        [DisplayName("Chứng từ")] public string ChungTu { get; set; }
        [DisplayName("Người thực hiện")] public string NguoiThucHien { get; set; }
        [DisplayName("Nguồn dữ liệu")] public string NguonDuLieu { get; set; }
        [DisplayName("Khóa nguồn")] public string KhoaNguon { get; set; }
        [DisplayName("Trạng thái bằng chứng")] public string TrangThai { get; set; }
        [DisplayName("Mô tả")] public string MoTa { get; set; }
        [DisplayName("Ghi chú")] public string GhiChu { get; set; }
        [DisplayName("Chi tiết")] public string ChiTiet { get; set; }
    }

    /// <summary>
    /// Xuất Excel / in báo cáo đầy đủ cho FormBaoCaoStockHistory và FormBaoCaoTraceability:
    /// tổng quan điều kiện tra cứu + từng lưới đang hiển thị + timeline (kèm chi tiết từng node).
    /// Phải gọi trên UI thread vì đọc trực tiếp dữ liệu đang hiển thị của GridView.
    /// </summary>
    internal static class TraceReportExporter
    {
        private static readonly Color HeaderBack = Color.FromArgb(35, 65, 95);

        public static List<ReportOverviewRow> NewOverview(string title)
        {
            var rows = new List<ReportOverviewRow>();
            rows.Add(new ReportOverviewRow { Muc = "Báo cáo", GiaTri = title });
            rows.Add(new ReportOverviewRow { Muc = "Thời điểm xuất", GiaTri = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) });
            rows.Add(new ReportOverviewRow { Muc = "Người xuất", GiaTri = Environment.UserName });
            return rows;
        }

        public static void AddRow(this List<ReportOverviewRow> rows, string label, string value)
        {
            rows.Add(new ReportOverviewRow { Muc = label, GiaTri = string.IsNullOrWhiteSpace(value) ? "-" : value });
        }

        public static List<TimelineReportRow> BuildTimelineRows(IList<TraceTimelineNode> nodes)
        {
            var list = new List<TimelineReportRow>();
            if (nodes == null) return list;
            int seq = 1;
            foreach (TraceTimelineNode x in nodes)
            {
                if (x == null) continue;
                string detail = string.Empty;
                try { detail = x.DetailText() ?? string.Empty; } catch (Exception) { }
                list.Add(new TimelineReportRow
                {
                    Stt = seq++,
                    SuKien = x.Title,
                    LoaiSuKien = x.EventType,
                    ThoiGian = x.EventTime,
                    Lot = x.LotNo,
                    MaHang = x.PartNo,
                    SoLuong = x.Quantity,
                    TuViTri = x.FromLocation,
                    DenViTri = x.ToLocation,
                    Qr = x.QrCode,
                    CustomerLabel = x.CustomerLabel,
                    KhachHang = x.CustomerName,
                    ChungTu = x.DocumentNo,
                    NguoiThucHien = x.UserName,
                    NguonDuLieu = x.SourceName,
                    KhoaNguon = x.SourceKey,
                    TrangThai = x.EvidenceStatus,
                    MoTa = x.Subtitle,
                    GhiChu = Flatten(x.Note),
                    ChiTiet = Flatten(detail)
                });
            }
            return list;
        }

        // ------------------------------------------------------------------ Excel

        public static void ExportWorkbook(
            string path,
            IList<ReportOverviewRow> overview,
            IList<ReportGridSection> sections,
            IList<TraceTimelineNode> timeline)
        {
            using (var workbook = new SS.Workbook())
            {
                workbook.BeginUpdate();
                try
                {
                    SS.Worksheet first = workbook.Worksheets[0];
                    first.Name = "Tổng quan";
                    WriteObjects(first, "TỔNG QUAN BÁO CÁO", overview ?? new List<ReportOverviewRow>(), new[] { 28, 90 });

                    foreach (ReportGridSection section in sections ?? new List<ReportGridSection>())
                    {
                        if (section == null || section.View == null || section.Grid == null || section.Grid.DataSource == null) continue;
                        SS.Worksheet sheet = workbook.Worksheets.Add(UniqueName(workbook, section.SheetName));
                        WriteGrid(sheet, section.Title, section.View);
                    }

                    List<TimelineReportRow> rows = BuildTimelineRows(timeline);
                    if (rows.Count > 0)
                    {
                        SS.Worksheet sheet = workbook.Worksheets.Add(UniqueName(workbook, "Timeline"));
                        WriteObjects(sheet, "TIMELINE TRUY VẾT: SẢN XUẤT → KHO → GIAO HÀNG", rows, null);
                    }
                }
                finally { workbook.EndUpdate(); }

                workbook.SaveDocument(path, SS.DocumentFormat.Xlsx);
            }
        }

        private static void WriteGrid(SS.Worksheet sheet, string title, GridView view)
        {
            var columns = new List<GridColumn>();
            foreach (GridColumn c in view.VisibleColumns) columns.Add(c);

            sheet.Cells[0, 0].Value = title;
            sheet.Cells[0, 0].Font.Bold = true;
            sheet.Cells[0, 0].Font.Size = 13;

            const int headerRow = 2;
            for (int c = 0; c < columns.Count; c++)
                StyleHeader(sheet.Cells[headerRow, c], string.IsNullOrEmpty(columns[c].Caption) ? columns[c].FieldName : columns[c].Caption);

            var widths = new int[columns.Count];
            for (int c = 0; c < columns.Count; c++) widths[c] = Math.Max(8, (columns[c].Caption ?? string.Empty).Length + 2);

            var sums = new double[columns.Count];
            var hasSum = new bool[columns.Count];
            for (int c = 0; c < columns.Count; c++)
                hasSum[c] = columns[c].SummaryItem != null && columns[c].SummaryItem.SummaryType == SummaryItemType.Sum;

            int row = headerRow + 1;
            int count = view.RowCount;
            for (int i = 0; i < count; i++)
            {
                if (view.IsGroupRow(i)) continue;
                for (int c = 0; c < columns.Count; c++)
                {
                    GridColumn col = columns[c];
                    object value = view.GetRowCellValue(i, col);
                    string text = view.GetRowCellDisplayText(i, col);
                    SS.Cell cell = sheet.Cells[row, c];
                    if (value == null || value == DBNull.Value)
                    {
                        // ô trống
                    }
                    else if (value is DateTime)
                    {
                        cell.Value = (DateTime)value;
                        cell.NumberFormat = ToExcelFormat(col, "dd/MM/yyyy HH:mm:ss");
                    }
                    else if (IsNumeric(value))
                    {
                        double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        cell.Value = number;
                        string fmt = ToExcelFormat(col, null);
                        if (fmt != null) cell.NumberFormat = fmt;
                        if (hasSum[c]) sums[c] += number;
                    }
                    else if (value is bool)
                    {
                        cell.Value = (bool)value;
                    }
                    else
                    {
                        // Chuỗi (LOT 20 số, QR...) giữ nguyên dạng text để Excel không đổi sang 2.61E+19.
                        string s = string.IsNullOrEmpty(text) ? Convert.ToString(value, CultureInfo.InvariantCulture) : text;
                        cell.Value = s;
                    }
                    if (i < 200 && !string.IsNullOrEmpty(text)) widths[c] = Math.Max(widths[c], Math.Min(text.Length + 2, 60));
                }
                row++;
            }

            int lastDataRow = row - 1;
            if (hasSum.Any(x => x))
            {
                sheet.Cells[row, 0].Value = "Tổng cộng";
                sheet.Cells[row, 0].Font.Bold = true;
                for (int c = 0; c < columns.Count; c++)
                {
                    if (!hasSum[c]) continue;
                    sheet.Cells[row, c].Value = sums[c];
                    sheet.Cells[row, c].Font.Bold = true;
                    string fmt = ToExcelFormat(columns[c], null);
                    sheet.Cells[row, c].NumberFormat = fmt == null || fmt.IndexOf(';') >= 0 ? "#,##0" : fmt;
                }
            }

            for (int c = 0; c < columns.Count; c++) sheet.Columns[c].WidthInCharacters = widths[c];
            sheet.FreezeRows(headerRow);
            if (columns.Count > 0 && lastDataRow >= headerRow + 1)
                sheet.AutoFilter.Apply(sheet.Range.FromLTRB(0, headerRow, columns.Count - 1, lastDataRow));
        }

        /// <summary>Ghi danh sách đối tượng; tiêu đề cột lấy từ [DisplayName]. Ngày/số ghi dạng giá trị thật.</summary>
        private static void WriteObjects<T>(SS.Worksheet sheet, string title, IList<T> data, int[] fixedWidths)
        {
            PropertyDescriptorCollection props = TypeDescriptor.GetProperties(typeof(T));

            sheet.Cells[0, 0].Value = title;
            sheet.Cells[0, 0].Font.Bold = true;
            sheet.Cells[0, 0].Font.Size = 13;

            const int headerRow = 2;
            var widths = new int[props.Count];
            for (int c = 0; c < props.Count; c++)
            {
                StyleHeader(sheet.Cells[headerRow, c], props[c].DisplayName);
                widths[c] = Math.Max(8, props[c].DisplayName.Length + 2);
            }

            int row = headerRow + 1;
            for (int i = 0; i < data.Count; i++)
            {
                for (int c = 0; c < props.Count; c++)
                {
                    object value = props[c].GetValue(data[i]);
                    if (value == null) continue;
                    SS.Cell cell = sheet.Cells[row, c];
                    int length;
                    if (value is DateTime)
                    {
                        cell.Value = (DateTime)value;
                        cell.NumberFormat = "dd/MM/yyyy HH:mm:ss";
                        length = 20;
                    }
                    else if (IsNumeric(value))
                    {
                        cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        cell.NumberFormat = value is int ? "0" : "#,##0";
                        length = 10;
                    }
                    else
                    {
                        string s = Convert.ToString(value, CultureInfo.InvariantCulture);
                        cell.Value = s;
                        length = Math.Min(s.Length + 2, 60);
                    }
                    if (i < 200) widths[c] = Math.Max(widths[c], length);
                }
                row++;
            }

            for (int c = 0; c < props.Count; c++)
                sheet.Columns[c].WidthInCharacters = fixedWidths != null && c < fixedWidths.Length ? fixedWidths[c] : widths[c];
            sheet.FreezeRows(headerRow);
            if (fixedWidths == null && data.Count > 0 && props.Count > 0)
                sheet.AutoFilter.Apply(sheet.Range.FromLTRB(0, headerRow, props.Count - 1, row - 1));
        }

        private static void StyleHeader(SS.Cell cell, string text)
        {
            cell.Value = text;
            cell.Font.Bold = true;
            cell.Font.Color = Color.White;
            cell.FillColor = HeaderBack;
        }

        private static string UniqueName(SS.Workbook workbook, string wanted)
        {
            string clean = Regex.Replace(wanted ?? "Sheet", @"[\[\]\:\*\?\/\\]", " ").Trim();
            if (clean.Length == 0) clean = "Sheet";
            if (clean.Length > 28) clean = clean.Substring(0, 28);
            string name = clean;
            int n = 2;
            while (workbook.Worksheets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
                name = clean + " " + (n++);
            return name;
        }

        private static string ToExcelFormat(GridColumn column, string fallback)
        {
            string f = column.DisplayFormat == null ? null : column.DisplayFormat.FormatString;
            if (string.IsNullOrWhiteSpace(f)) return fallback;
            f = f.Trim();
            Match brace = Regex.Match(f, @"^\{0:(.+)\}$");
            if (brace.Success) f = brace.Groups[1].Value;
            Match standard = Regex.Match(f, @"^[nN](\d?)$");
            if (standard.Success)
            {
                int decimals = standard.Groups[1].Length == 0 ? 2 : int.Parse(standard.Groups[1].Value, CultureInfo.InvariantCulture);
                return decimals == 0 ? "#,##0" : "#,##0." + new string('0', decimals);
            }
            return f;
        }

        private static bool IsNumeric(object v)
        {
            return v is byte || v is sbyte || v is short || v is ushort || v is int || v is uint ||
                   v is long || v is ulong || v is float || v is double || v is decimal;
        }

        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(text.Trim(), @"\s*[\r\n]+\s*", " | ");
        }

        // ------------------------------------------------------------------ In báo cáo

        public static void ShowPrintPreview(
            IList<ReportOverviewRow> overview,
            IList<ReportGridSection> sections,
            IList<TraceTimelineNode> timeline)
        {
            var temps = new List<GridControl>();
            var restore = new List<KeyValuePair<GridView, bool>>();
            var ps = new PrintingSystem();
            try
            {
                var composite = new CompositeLink(ps);
                composite.Landscape = true;
                composite.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.A4;

                GridControl overviewGrid = CreatePrintGrid(overview.ToList(), false);
                temps.Add(overviewGrid);
                composite.Links.Add(new PrintableComponentLink(ps) { Component = overviewGrid });

                foreach (ReportGridSection section in sections ?? new List<ReportGridSection>())
                {
                    if (section == null || section.View == null || section.Grid == null || section.Grid.DataSource == null) continue;
                    restore.Add(new KeyValuePair<GridView, bool>(section.View, section.View.OptionsPrint.AutoWidth));
                    section.View.OptionsPrint.AutoWidth = true;
                    section.View.OptionsPrint.PrintHeader = true;
                    section.View.OptionsPrint.PrintFooter = true;
                    composite.Links.Add(new PrintableComponentLink(ps) { Component = section.Grid });
                }

                List<TimelineReportRow> rows = BuildTimelineRows(timeline);
                if (rows.Count > 0)
                {
                    GridControl timelineGrid = CreatePrintGrid(rows, true);
                    temps.Add(timelineGrid);
                    composite.Links.Add(new PrintableComponentLink(ps) { Component = timelineGrid });
                }

                composite.CreateDocument();
                composite.ShowPreviewDialog();
            }
            finally
            {
                foreach (var item in restore) item.Key.OptionsPrint.AutoWidth = item.Value;
                foreach (GridControl g in temps) g.Dispose();
                ps.Dispose();
            }
        }

        /// <summary>Lưới tạm (không gắn form) để đưa dữ liệu dạng danh sách vào bản in.</summary>
        private static GridControl CreatePrintGrid<T>(List<T> data, bool wrapLongText)
        {
            var grid = new GridControl();
            var view = new GridView(grid);
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            grid.DataSource = data;
            grid.ForceInitialize();
            view.PopulateColumns();
            view.OptionsView.ColumnAutoWidth = true;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsPrint.AutoWidth = true;
            view.OptionsView.EnableAppearanceEvenRow = true;

            foreach (GridColumn c in view.Columns)
            {
                if (c.FieldName == "ThoiGian")
                {
                    c.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                    c.DisplayFormat.FormatString = "dd/MM/yyyy HH:mm:ss";
                }
                else if (c.FieldName == "SoLuong")
                {
                    c.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
                    c.DisplayFormat.FormatString = "n0";
                }
            }

            if (wrapLongText)
            {
                var memo = new DevExpress.XtraEditors.Repository.RepositoryItemMemoEdit();
                grid.RepositoryItems.Add(memo);
                foreach (string field in new[] { "MoTa", "GhiChu", "ChiTiet", "Qr", "Lot" })
                {
                    GridColumn c = view.Columns[field];
                    if (c != null) c.ColumnEdit = memo;
                }
                view.OptionsView.RowAutoHeight = true;
            }
            return grid;
        }
    }
}
