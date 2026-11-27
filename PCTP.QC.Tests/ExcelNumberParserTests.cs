using System.Globalization;
using System.Threading;
using PCTP.Modules.KhoLinhKien;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Số trong ô Excel phải ra cùng giá trị trên mọi máy (en-US, vi-VN, de-DE).</summary>
public sealed class ExcelNumberParserTests
{
    [Theory]
    [InlineData("2.400", 2400)]
    [InlineData("2,400", 2400)]
    [InlineData("2 400", 2400)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("1,234,567", 1234567)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("2.5", 2.5)]
    [InlineData("2,5", 2.5)]
    [InlineData("0.125", 0.125)]
    [InlineData("0,125", 0.125)]
    [InlineData("2400", 2400)]
    [InlineData("-1.200", -1200)]
    [InlineData(".5", 0.5)]
    public void Text_is_read_the_same_on_every_culture(string text, double expected)
    {
        foreach (string culture in new[] { "en-US", "vi-VN", "de-DE" })
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.True(ExcelNumberParser.TryParseText(text, out decimal value), text + " @ " + culture);
                Assert.Equal((decimal)expected, value);
            }
            finally { Thread.CurrentThread.CurrentCulture = previous; }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("1.23.456")]
    [InlineData("1,2345,678")]
    [InlineData("1.234,56,7")]
    [InlineData("5.")]
    public void Malformed_text_is_rejected(string text)
    {
        Assert.False(ExcelNumberParser.TryParseText(text, out _));
    }

    [Fact]
    public void Real_numeric_cell_is_used_as_is()
    {
        Assert.True(ExcelNumberParser.TryParseCell(2400d, "2.400", out decimal value));
        Assert.Equal(2400m, value);
        Assert.True(ExcelNumberParser.TryParseCell(2.4d, "2,4", out value));
        Assert.Equal(2.4m, value);
    }

    [Fact]
    public void Text_cell_falls_back_to_text_rules()
    {
        Assert.True(ExcelNumberParser.TryParseCell("2.400", "2.400", out decimal value));
        Assert.Equal(2400m, value);
    }
}
