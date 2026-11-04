using System;
using System.Globalization;
using PCTP.Shared.Helpers;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Chuỗi ngày app tự sinh phải cho cùng một ngày trên mọi Regional Settings
/// (dd/MM/yyyy, MM/dd/yyyy, dấu phân cách khác, lịch khác).
/// </summary>
public sealed class DateTextCultureTests
{
    private static readonly string[] Cultures =
        { "en-US", "vi-VN", "en-GB", "de-DE", "fr-FR", "ja-JP", "nl-NL", "fi-FI", "th-TH" };

    private static readonly DateTime Nov4 = new DateTime(2026, 11, 4);
    private static readonly DateTime Apr11 = new DateTime(2026, 4, 11);

    private static void InCulture(string name, Action action)
    {
        var oldCulture = CultureInfo.CurrentCulture;
        var oldUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo ci;
            try { ci = new CultureInfo(name); }
            catch (CultureNotFoundException) { return; }
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUi;
        }
    }

    [Fact]
    public void MdyRoundTrip_IsIdenticalOnEveryCulture()
    {
        foreach (string name in Cultures)
        {
            InCulture(name, () =>
            {
                string text = DateText.ToMdy(Nov4);
                Assert.Equal("11/04/2026", text);
                Assert.True(DateText.TryParseApp(text, out DateTime parsed), name);
                Assert.Equal(Nov4, parsed);

                // Ngày có ngày <= 12 là trường hợp mơ hồ: 11 tháng 4 phải ra 11 tháng 4.
                Assert.True(DateText.TryParseApp(DateText.ToMdy(Apr11), out parsed), name);
                Assert.Equal(Apr11, parsed);
            });
        }
    }

    [Fact]
    public void IsoAndCompactFormats_ParseTheSameOnEveryCulture()
    {
        foreach (string name in Cultures)
        {
            InCulture(name, () =>
            {
                Assert.Equal("2026-11-04", DateText.ToIso(Nov4));
                Assert.Equal("2026-11-04 08:00:00", DateText.ToIsoDateTime(Nov4.AddHours(8)));

                foreach (string text in new[] { "2026-11-04", "2026-11-04 00:00:00", "20261104", "04112026" })
                {
                    Assert.True(DateText.TryParseApp(text, out DateTime parsed), name + " " + text);
                    Assert.Equal(Nov4, parsed);
                }
            });
        }
    }

    [Fact]
    public void TryParseDb_UsesDateTimeValueWithoutGoingThroughText()
    {
        foreach (string name in Cultures)
        {
            InCulture(name, () =>
            {
                var slot = new DateTime(2026, 11, 4, 11, 20, 0);
                Assert.True(DateText.TryParseDb(slot, out DateTime parsed), name);
                Assert.Equal(slot, parsed);
                Assert.False(DateText.TryParseDb(DBNull.Value, out _));
                Assert.False(DateText.TryParseDb(null, out _));
            });
        }
    }

    [Fact]
    public void InvalidText_IsRejected()
    {
        Assert.False(DateText.TryParseApp("", out _));
        Assert.False(DateText.TryParseApp("   ", out _));
        Assert.False(DateText.TryParseApp("không phải ngày", out _));
    }
}
