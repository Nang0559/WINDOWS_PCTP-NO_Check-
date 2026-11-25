using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// Plain data of a Bravo-style screen layout. There is deliberately no SQL text, expression, procedure,
    /// DLL or class name anywhere: a layout can only name columns of an approved view.
    /// </summary>
    public static class UiRegion
    {
        public const string Main = "MAIN";
    }

    public static class UiFilterOperator
    {
        public const string Equal = "EQ";
        public const string NotEqual = "NE";
        public const string GreaterOrEqual = "GE";
        public const string LessOrEqual = "LE";
        public const string Contains = "CONTAINS";
        public static readonly string[] All = { Equal, NotEqual, GreaterOrEqual, LessOrEqual, Contains };
    }

    /// <summary>Closed list of values a quick filter may take besides a literal. Resolved on the client, sent as a parameter.</summary>
    public static class UiFilterToken
    {
        public const string Today = "TODAY";
        public const string MonthStart = "MONTH_START";
        public const string YearStart = "YEAR_START";
        public const string Site = "SITE";
        public static readonly string[] All = { Today, MonthStart, YearStart, Site };
    }

    public static class UiDisplayFormats
    {
        public static readonly string[] All = { "", "N0", "N2", "N3", "dd/MM/yyyy", "dd/MM/yyyy HH:mm" };
    }

    public sealed class UiDataSourceInfo
    {
        public string SourceCode { get; set; }
        public string ViewName { get; set; }
        public string DisplayName { get; set; }
        public string ViewFunctionCode { get; set; }
        public string SiteColumn { get; set; }
        public bool IsActive { get; set; }
        public override string ToString() { return SourceCode + " - " + DisplayName; }
    }

    /// <summary>A column of an approved view as reported by sys.columns. TypeGroup is NUM, DATE, TEXT or BIT.</summary>
    public sealed class UiSourceColumn
    {
        public string ColumnName { get; set; }
        public string TypeName { get; set; }
        public string TypeGroup { get; set; }
        public bool IsNullable { get; set; }
        public override string ToString() { return ColumnName + " (" + TypeName + ")"; }
    }

    /// <summary>Presentation of one column. Public properties with attributes feed the DevExpress property grid.</summary>
    public sealed class UiColumnLayout
    {
        [Browsable(false)] public string RegionCode { get; set; }
        [Browsable(false)] public string ColumnName { get; set; }
        [Browsable(false)] public int SequenceNo { get; set; }

        [Category("Hiển thị"), DisplayName("Tiêu đề")]
        public string Caption { get; set; }

        [Category("Hiển thị"), DisplayName("Hiện cột")]
        public bool IsVisible { get; set; }

        [Category("Hiển thị"), DisplayName("Độ rộng (px, 20-1000, trống = tự động)")]
        public int? Width { get; set; }

        [Category("Hiển thị"), DisplayName("Căn lề")]
        [TypeConverter(typeof(UiAlignConverter))]
        public string Align { get; set; }

        [Category("Hiển thị"), DisplayName("Định dạng")]
        [TypeConverter(typeof(UiFormatConverter))]
        public string DisplayFormat { get; set; }

        public UiColumnLayout Clone() { return (UiColumnLayout)MemberwiseClone(); }
    }

    public sealed class UiChildLayout
    {
        public string ChildCode { get; set; }
        public string TabCaption { get; set; }
        public string SourceCode { get; set; }
        /// <summary>Column of the main view.</summary>
        public string ParentColumn { get; set; }
        /// <summary>Column of the child view.</summary>
        public string ChildColumn { get; set; }
        public string SortColumn { get; set; }
        public bool SortDesc { get; set; }
        public int SequenceNo { get; set; }
        // Supplied by the server from the approved source; never sent back on save.
        public string ViewName { get; set; }
        public string ViewFunctionCode { get; set; }
        public string SiteColumn { get; set; }
    }

    public sealed class UiQuickFilter
    {
        public string FilterCode { get; set; }
        public string Caption { get; set; }
        public string ColumnName { get; set; }
        public string OperatorCode { get; set; }
        /// <summary>One of <see cref="UiFilterToken"/>; null when <see cref="LiteralValue"/> is used.</summary>
        public string TokenCode { get; set; }
        public string LiteralValue { get; set; }
        public int SequenceNo { get; set; }
        public bool IsDefault { get; set; }
        public override string ToString() { return Caption; }
    }

    public sealed class UiScreenLayout
    {
        public string ScreenCode { get; set; }
        public string Title { get; set; }
        public string SourceCode { get; set; }
        public string ViewName { get; set; }
        public string ViewFunctionCode { get; set; }
        public string SiteColumn { get; set; }
        public string DefaultSortColumn { get; set; }
        public bool DefaultSortDesc { get; set; }
        public string GroupColumn { get; set; }
        public int PageSize { get; set; }
        public int ConfigVersion { get; set; }
        public List<UiColumnLayout> Columns { get; private set; }
        public List<UiChildLayout> Children { get; private set; }
        public List<UiQuickFilter> Filters { get; private set; }

        public UiScreenLayout()
        {
            PageSize = 200;
            Columns = new List<UiColumnLayout>();
            Children = new List<UiChildLayout>();
            Filters = new List<UiQuickFilter>();
        }

        public IEnumerable<UiColumnLayout> ColumnsOf(string regionCode)
        {
            foreach (var c in Columns)
                if (string.Equals(c.RegionCode, regionCode, StringComparison.OrdinalIgnoreCase)) yield return c;
        }
    }

    /// <summary>A loaded screen plus the column catalogue of every region's view, keyed by region code.</summary>
    public sealed class UiScreenLoadResult
    {
        public UiScreenLayout Layout { get; set; }
        public Dictionary<string, List<UiSourceColumn>> Catalog { get; private set; }
        public UiScreenLoadResult() { Catalog = new Dictionary<string, List<UiSourceColumn>>(StringComparer.OrdinalIgnoreCase); }
    }

    public sealed class UiConfigConflictException : InvalidOperationException
    {
        public UiConfigConflictException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class UiAlignConverter : StringConverter
    {
        private static readonly string[] Values = { "LEFT", "RIGHT", "CENTER" };
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) { return true; }
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) { return true; }
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) { return new StandardValuesCollection(Values); }
    }

    public sealed class UiFormatConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) { return true; }
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) { return true; }
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) { return new StandardValuesCollection(UiDisplayFormats.All); }
    }

    internal static class UiValueParser
    {
        /// <summary>Converts a literal quick-filter value to a typed parameter value for the column's type group.</summary>
        public static object Parse(string typeGroup, string literal)
        {
            switch (typeGroup)
            {
                case "NUM":
                    decimal d;
                    if (!decimal.TryParse(literal, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
                        throw new FormatException("Giá trị số không hợp lệ: " + literal);
                    return d;
                case "DATE":
                    DateTime dt;
                    if (!DateTime.TryParseExact(literal, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        throw new FormatException("Ngày phải theo dạng yyyy-MM-dd: " + literal);
                    return dt;
                case "BIT":
                    if (literal == "0" || literal == "1") return literal == "1";
                    throw new FormatException("Giá trị bit phải là 0 hoặc 1.");
                default:
                    return literal;
            }
        }
    }
}