
using PCTP.Framework.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PCTP.Framework.UiLayout
{
    public sealed class UiSqlCommand
    {
        public string Sql { get; internal set; }
        public List<KeyValuePair<string, object>> Parameters { get; private set; }
        public UiSqlCommand() { Parameters = new List<KeyValuePair<string, object>>(); }
        internal void Add(string name, object value) { Parameters.Add(new KeyValuePair<string, object>(name, value)); }
    }

    /// <summary>
    /// Turns a validated layout into SELECT statements. Identifiers come from the approved view name and from column names
    /// that exist in the sys.columns catalogue (bracket-quoted); every value is a parameter; filters can only be
    /// column + operator + (closed token | typed literal). The Site predicate is always present.
    /// </summary>
    public static class UiQueryBuilder
    {
        public const int ChildRowLimit = 2000;

        public static UiSqlCommand BuildMainCount(UiScreenLayout layout, IDictionary<string, List<UiSourceColumn>> catalog,
            string siteCode, UiQuickFilter filter, DateTime today)
        {
            var cmd = new UiSqlCommand();
            var sb = new StringBuilder("SELECT COUNT_BIG(*) FROM ").Append(View(layout.ViewName));
            AppendMainWhere(sb, cmd, layout, catalog, siteCode, filter, today);
            cmd.Sql = sb.ToString();
            return cmd;
        }

        public static UiSqlCommand BuildMainPage(UiScreenLayout layout, IDictionary<string, List<UiSourceColumn>> catalog,
            string siteCode, UiQuickFilter filter, DateTime today, int pageIndex)
        {
            if (pageIndex < 0) throw new ArgumentOutOfRangeException("pageIndex");
            var cmd = new UiSqlCommand();
            var columns = Ordered(layout, UiRegion.Main, catalog);
            var sb = new StringBuilder("SELECT ").Append(string.Join(", ", columns.Select(c => Quote(c.ColumnName))))
                .Append(" FROM ").Append(View(layout.ViewName));
            AppendMainWhere(sb, cmd, layout, catalog, siteCode, filter, today);
            sb.Append(" ORDER BY ").Append(OrderBy(layout.DefaultSortColumn, layout.DefaultSortDesc, columns[0].ColumnName));
            sb.Append(" OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY");
            cmd.Add("@Skip", pageIndex * layout.PageSize);
            cmd.Add("@Take", layout.PageSize);
            cmd.Sql = sb.ToString();
            return cmd;
        }

        public static UiSqlCommand BuildChild(UiScreenLayout layout, UiChildLayout child, IDictionary<string, List<UiSourceColumn>> catalog,
            string siteCode, object parentKey)
        {
            if (child == null) throw new ArgumentNullException("child");
            if (parentKey == null || parentKey == DBNull.Value) throw new ArgumentException("Thiếu giá trị khóa cha.", "parentKey");
            var cmd = new UiSqlCommand();
            var columns = Ordered(layout, child.ChildCode, catalog);
            var childColumn = Existing(catalog, child.ChildCode, child.ChildColumn);
            var site = DocumentIdentifiers.Identifier(child.SiteColumn, "SiteColumn");
            var sb = new StringBuilder("SELECT TOP (").Append(ChildRowLimit).Append(") ")
                .Append(string.Join(", ", columns.Select(c => Quote(c.ColumnName))))
                .Append(" FROM ").Append(View(child.ViewName))
                .Append(" WHERE ").Append(Quote(childColumn.ColumnName)).Append(" = @Key AND ").Append(Quote(site)).Append(" = @Site");
            cmd.Add("@Key", parentKey);
            cmd.Add("@Site", siteCode);
            sb.Append(" ORDER BY ").Append(OrderBy(child.SortColumn, child.SortDesc, columns[0].ColumnName));
            cmd.Sql = sb.ToString();
            return cmd;
        }

        public static object ResolveToken(string tokenCode, DateTime today, string siteCode)
        {
            switch (tokenCode)
            {
                case UiFilterToken.Today: return today.Date;
                case UiFilterToken.MonthStart: return new DateTime(today.Year, today.Month, 1);
                case UiFilterToken.YearStart: return new DateTime(today.Year, 1, 1);
                case UiFilterToken.Site: return siteCode;
                default: throw new ArgumentException("Token không nằm trong danh sách cho phép: " + tokenCode);
            }
        }

        private static void AppendMainWhere(StringBuilder sb, UiSqlCommand cmd, UiScreenLayout layout,
            IDictionary<string, List<UiSourceColumn>> catalog, string siteCode, UiQuickFilter filter, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            string site = DocumentIdentifiers.Identifier(layout.SiteColumn, "SiteColumn");
            sb.Append(" WHERE ").Append(Quote(site)).Append(" = @Site");
            cmd.Add("@Site", siteCode);
            if (filter == null) return;

            var column = Existing(catalog, UiRegion.Main, filter.ColumnName);
            sb.Append(" AND ").Append(Quote(column.ColumnName));
            switch (filter.OperatorCode)
            {
                case UiFilterOperator.Equal: sb.Append(" = @f0"); break;
                case UiFilterOperator.NotEqual: sb.Append(" <> @f0"); break;
                case UiFilterOperator.GreaterOrEqual: sb.Append(" >= @f0"); break;
                case UiFilterOperator.LessOrEqual: sb.Append(" <= @f0"); break;
                case UiFilterOperator.Contains: sb.Append(" LIKE @f0 ESCAPE '\\'"); break;
                default: throw new ArgumentException("Toán tử không hợp lệ: " + filter.OperatorCode);
            }
            object value = !string.IsNullOrEmpty(filter.TokenCode)
                ? ResolveToken(filter.TokenCode, today, siteCode)
                : UiValueParser.Parse(column.TypeGroup, filter.LiteralValue);
            if (filter.OperatorCode == UiFilterOperator.Contains)
                value = "%" + EscapeLike(Convert.ToString(value)) + "%";
            cmd.Add("@f0", value);
        }

        private static List<UiColumnLayout> Ordered(UiScreenLayout layout, string region, IDictionary<string, List<UiSourceColumn>> catalog)
        {
            var list = layout.ColumnsOf(region).OrderBy(c => c.SequenceNo).ToList();
            if (list.Count == 0) throw new InvalidOperationException("Vùng " + region + " chưa có cột.");
            foreach (var c in list) Existing(catalog, region, c.ColumnName);
            return list;
        }

        private static UiSourceColumn Existing(IDictionary<string, List<UiSourceColumn>> catalog, string region, string name)
        {
            List<UiSourceColumn> columns;
            UiSourceColumn found = catalog != null && catalog.TryGetValue(region, out columns)
                ? UiLayoutValidator.FindColumn(columns, name) : null;
            if (found == null) throw new InvalidOperationException("Cột '" + name + "' không có trong view đã duyệt (vùng " + region + ").");
            DocumentIdentifiers.Identifier(found.ColumnName, "ColumnName");
            return found;
        }

        private static string OrderBy(string sortColumn, bool desc, string fallback)
        {
            string first = Quote(DocumentIdentifiers.Identifier(fallback, "ColumnName"));
            if (string.IsNullOrEmpty(sortColumn) || string.Equals(sortColumn, fallback, StringComparison.OrdinalIgnoreCase))
                return first + (!string.IsNullOrEmpty(sortColumn) && desc ? " DESC" : string.Empty);
            return Quote(DocumentIdentifiers.Identifier(sortColumn, "SortColumn")) + (desc ? " DESC" : " ASC") + ", " + first;
        }

        private static string View(string viewName) { return DocumentIdentifiers.SqlObject(viewName, "ViewName"); }
        private static string Quote(string identifier) { return DocumentIdentifiers.Bracket(DocumentIdentifiers.Identifier(identifier, "ColumnName")); }

        internal static string EscapeLike(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
        }
    }
}