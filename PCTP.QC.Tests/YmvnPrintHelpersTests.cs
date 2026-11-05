using PCTP.Applications.Services;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class YmvnPrintHelpersTests
{
    [Theory]
    [InlineData("1289", "#01289")]
    [InlineData("800", "#00800")]
    [InlineData("7", "#00007")]
    [InlineData("01289", "#01289")]
    [InlineData("123456", "#123456")]
    [InlineData("A12", "#A12")]
    [InlineData("#811", "#00811")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void PoFormat_PadsNumericPoItemToFiveDigits(string? input, string expected)
        => Assert.Equal(expected, YmvnPoFormat.Format(input!));

    [Fact]
    public void GioMatcher_NoCheckedHours_MatchesEverything()
        => Assert.True(YmvnGioMatcher.Create(null).Matches(null, "15"));

    [Theory]
    [InlineData("15", true)]
    [InlineData("15:00", true)]
    [InlineData("16", false)]
    public void GioMatcher_FiltersByCheckedHour(string gio, bool expected)
        => Assert.Equal(expected, YmvnGioMatcher.Create(new[] { "15:00:00" }).Matches(null, gio));

    [Fact]
    public void GioMatcher_FallsBackToNgayGiaoHour()
        => Assert.True(YmvnGioMatcher.Create(new[] { "09H" }).Matches(new System.DateTime(2026, 11, 5, 9, 0, 0), ""));
}
