
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// Client-side twin of MES.usp_FVN_UiScreen_Save. SQL stays the security boundary; this check gives the designer
    /// early messages and makes the runtime fail closed if stored layout and view catalogue ever disagree.
    /// </summary>
    public static class UiLayoutValidator
    {
        public const int MaxChildren = 8;
        public const int MaxFilters = 12;
        public const int MaxColumnsPerRegion = 80;

        public static IReadOnlyList<string> Validate(UiScreenLayout layout, IDictionary<string, List<UiSourceColumn>> catalog)
        {
            var errors = new List<string>();
            if (layout == null) { errors.Add("Layout trống."); return errors; }
            if (catalog == null) { errors.Add("Thiếu danh mục cột của view."); return errors; }
            if (string.IsNullOrWhiteSpace(layout.Title)) errors.Add("Thiếu tiêu đề màn hình.");
            if (layout.PageSize < 20 || layout.PageSize > 500) errors.Add("PageSize phải từ 20 đến 500.");
            if (layout.Children.Count > MaxChildren) errors.Add("Tối đa " + MaxChildren + " tab con.");
            if (layout.Filters.Count > MaxFilters) errors.Add("Tối đa " + MaxFilters + " bộ lọc nhanh.");

            var regions = new List<string> { UiRegion.Main };
            foreach (var child in layout.Children)
            {
                if (!IsCode(child.ChildCode) || string.Equals(child.ChildCode, UiRegion.Main, StringComparison.OrdinalIgnoreCase))
                    errors.Add("Mã tab con không hợp lệ: " + child.ChildCode);
                else if (regions.Contains(child.ChildCode, StringComparer.OrdinalIgnoreCase))
                    errors.Add("Trùng mã tab con: " + child.ChildCode);
                else regions.Add(child.ChildCode);
                if (string.IsNullOrWhiteSpace(child.TabCaption)) errors.Add("Tab " + child.ChildCode + " thiếu tiêu đề.");
            }

            foreach (var region in regions)
            {
                List<UiSourceColumn> columns;
                if (!catalog.TryGetValue(region, out columns)) { errors.Add("Không có danh mục cột cho vùng " + region + "."); continue; }
                var items = layout.ColumnsOf(region).ToList();
                if (items.Count > MaxColumnsPerRegion) errors.Add("Vùng " + region + " vượt " + MaxColumnsPerRegion + " cột.");
                if (!items.Any(c => c.IsVisible)) errors.Add("Vùng " + region + " phải còn ít nhất một cột hiển thị.");
                foreach (var dup in items.GroupBy(c => c.ColumnName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                    errors.Add("Cột trùng trong vùng " + region + ": " + dup.Key);
                foreach (var item in items)
                {
                    if (FindColumn(columns, item.ColumnName) == null)
                        errors.Add("Cột '" + item.ColumnName + "' không có trong view của vùng " + region + ".");
                    if (string.IsNullOrWhiteSpace(item.Caption)) errors.Add("Cột " + item.ColumnName + " thiếu tiêu đề.");
                    if (item.Width.HasValue && (item.Width < 20 || item.Width > 1000))
                        errors.Add("Độ rộng cột " + item.ColumnName + " phải từ 20 đến 1000.");
                    if (Array.IndexOf(new[] { "LEFT", "RIGHT", "CENTER" }, item.Align) < 0)
                        errors.Add("Căn lề cột " + item.ColumnName + " không hợp lệ.");
                    if (!string.IsNullOrEmpty(item.DisplayFormat) && Array.IndexOf(UiDisplayFormats.All, item.DisplayFormat) < 0)
                        errors.Add("Định dạng cột " + item.ColumnName + " không thuộc danh sách cho phép.");
                }
            }
            foreach (var orphan in layout.Columns.Where(c => !regions.Contains(c.RegionCode, StringComparer.OrdinalIgnoreCase)))
                errors.Add("Cột " + orphan.ColumnName + " thuộc vùng không tồn tại: " + orphan.RegionCode);

            if (errors.Count > 0) return errors;   // the checks below rely on a sane column set

            var main = layout.ColumnsOf(UiRegion.Main).ToList();
            if (layout.DefaultSortColumn != null && !InLayout(main, layout.DefaultSortColumn)) errors.Add("Cột sắp xếp mặc định phải là cột của layout.");
            if (layout.GroupColumn != null && !InLayout(main, layout.GroupColumn)) errors.Add("Cột nhóm phải là cột của layout.");

            foreach (var child in layout.Children)
            {
                var childCols = layout.ColumnsOf(child.ChildCode).ToList();
                if (!InLayout(main, child.ParentColumn) || !InLayout(childCols, child.ChildColumn))
                {
                    errors.Add("Tab " + child.ChildCode + ": cặp cột khóa cha-con phải là cột có trong layout của từng bên.");
                    continue;
                }
                if (child.SortColumn != null && !InLayout(childCols, child.SortColumn))
                    errors.Add("Tab " + child.ChildCode + ": cột sắp xếp phải là cột của tab.");
                string pg = FindColumn(catalog[UiRegion.Main], child.ParentColumn).TypeGroup;
                string cg = FindColumn(catalog[child.ChildCode], child.ChildColumn).TypeGroup;
                if (pg != cg) errors.Add("Tab " + child.ChildCode + ": cột khóa cha và con phải cùng nhóm kiểu dữ liệu.");
            }

            foreach (var f in layout.Filters)
            {
                if (!IsCode(f.FilterCode)) { errors.Add("Mã bộ lọc không hợp lệ: " + f.FilterCode); continue; }
                if (string.IsNullOrWhiteSpace(f.Caption)) errors.Add("Bộ lọc " + f.FilterCode + " thiếu tiêu đề.");
                if (!InLayout(main, f.ColumnName)) { errors.Add("Bộ lọc " + f.FilterCode + " phải dùng cột của layout chính."); continue; }
                string group = FindColumn(catalog[UiRegion.Main], f.ColumnName).TypeGroup;
                if (Array.IndexOf(UiFilterOperator.All, f.OperatorCode) < 0) { errors.Add("Bộ lọc " + f.FilterCode + ": toán tử không hợp lệ."); continue; }
                if (f.OperatorCode == UiFilterOperator.Contains && group != "TEXT") errors.Add("Bộ lọc " + f.FilterCode + ": CONTAINS chỉ dùng cho cột chữ.");
                if ((f.OperatorCode == UiFilterOperator.GreaterOrEqual || f.OperatorCode == UiFilterOperator.LessOrEqual) && group != "NUM" && group != "DATE")
                    errors.Add("Bộ lọc " + f.FilterCode + ": >= / <= chỉ dùng cho cột số hoặc ngày.");
                bool hasToken = !string.IsNullOrEmpty(f.TokenCode), hasLiteral = f.LiteralValue != null;
                if (hasToken == hasLiteral) { errors.Add("Bộ lọc " + f.FilterCode + ": phải có đúng một trong token hoặc giá trị cố định."); continue; }
                if (hasToken)
                {
                    if (Array.IndexOf(UiFilterToken.All, f.TokenCode) < 0) errors.Add("Bộ lọc " + f.FilterCode + ": token không nằm trong danh sách cho phép.");
                    else if (f.TokenCode == UiFilterToken.Site && group != "TEXT") errors.Add("Bộ lọc " + f.FilterCode + ": token SITE chỉ dùng cho cột chữ.");
                    else if (f.TokenCode != UiFilterToken.Site && group != "DATE") errors.Add("Bộ lọc " + f.FilterCode + ": token ngày chỉ dùng cho cột ngày.");
                }
                else
                {
                    try { UiValueParser.Parse(group, f.LiteralValue); }
                    catch (FormatException ex) { errors.Add("Bộ lọc " + f.FilterCode + ": " + ex.Message); }
                }
            }
            if (layout.Filters.Count(f => f.IsDefault) > 1) errors.Add("Chỉ được một bộ lọc mặc định.");
            return errors;
        }

        public static UiSourceColumn FindColumn(IEnumerable<UiSourceColumn> columns, string name)
        {
            return columns == null ? null : columns.FirstOrDefault(c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool InLayout(IEnumerable<UiColumnLayout> items, string name)
        {
            return name != null && items.Any(c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCode(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Length <= 40 &&
                   value.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_');
        }
    }
}