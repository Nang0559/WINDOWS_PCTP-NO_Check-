using System;
using System.Globalization;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Contract tests for the two real QR payload formats supplied for the PCTP
/// receiving and customer-delivery workflows. These tests intentionally verify
/// the wire format only; field semantics must be established by production code.
/// </summary>
public sealed class QrCodeFormatContractTests
{
    public const string ReceivingMasterLabel =
        "2608070085810078170000030500:22110-K2T-V000:07/08/2026:500:1:A010000000059136";

    public const string CustomerCartonLabel =
        "260807008581007817008000:22110-K2T-V000:07/08/2026:1000";

    [Fact]
    public void ReceivingMasterLabel_HasSixColonDelimitedFields()
    {
        var fields = ParseFields(ReceivingMasterLabel, expectedCount: 6);

        Assert.Equal("2608070085810078170000030500", fields[0]);
        Assert.Equal("22110-K2T-V000", fields[1]);
        Assert.Equal("07/08/2026", fields[2]);
        Assert.Equal("500", fields[3]);
        Assert.Equal("1", fields[4]);
        Assert.Equal("A010000000059136", fields[5]);
    }

    [Fact]
    public void CustomerCartonLabel_HasFourColonDelimitedFields()
    {
        var fields = ParseFields(CustomerCartonLabel, expectedCount: 4);

        Assert.Equal("260807008581007817008000", fields[0]);
        Assert.Equal("22110-K2T-V000", fields[1]);
        Assert.Equal("07/08/2026", fields[2]);
        Assert.Equal("1000", fields[3]);
    }

    [Fact]
    public void BothLabelTypes_ReferToSamePartAndManufacturingDate()
    {
        var receiving = ParseFields(ReceivingMasterLabel, 6);
        var carton = ParseFields(CustomerCartonLabel, 4);

        Assert.Equal(receiving[1], carton[1]);
        Assert.Equal(receiving[2], carton[2]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQr_IsRejected(string qr)
    {
        Assert.Throws<ArgumentException>(() => ParseFields(qr, 4));
    }

    [Theory]
    [InlineData("a:b:c:d", 6)]
    [InlineData("a:b:c:d:e:f:g", 6)]
    [InlineData("a:b:c", 4)]
    [InlineData("a:b:c:d:e", 4)]
    public void WrongFieldCount_IsRejected(string qr, int expectedCount)
    {
        Assert.Throws<FormatException>(() => ParseFields(qr, expectedCount));
    }

    [Theory]
    [InlineData("2608070085810078170000030500::07/08/2026:500:1:A010000000059136", 6)]
    [InlineData(":22110-K2T-V000:07/08/2026:500:1:A010000000059136", 6)]
    [InlineData("260807008581007817008000:22110-K2T-V000:07/08/2026:", 4)]
    public void MissingRequiredWireField_IsRejected(string qr, int expectedCount)
    {
        Assert.Throws<FormatException>(() => ParseFields(qr, expectedCount));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void QuantityMustBePositiveInteger(string quantity)
    {
        Assert.Throws<FormatException>(() => ParsePositiveQuantity(quantity));
    }

    [Fact]
    public void SuppliedQuantities_ArePositiveIntegers()
    {
        Assert.Equal(500, ParsePositiveQuantity(ParseFields(ReceivingMasterLabel, 6)[3]));
        Assert.Equal(1000, ParsePositiveQuantity(ParseFields(CustomerCartonLabel, 4)[3]));
    }

    [Theory]
    [InlineData("31/13/2026")]
    [InlineData("2026-08-07")]
    [InlineData("")]
    public void ManufacturingDateMustMatchDdMmYyyyAndBeValid(string date)
    {
        Assert.Throws<FormatException>(() => ParseDate(date));
    }

    [Fact]
    public void SuppliedDate_IsParsedWithExplicitDayFirstFormat()
    {
        var date = ParseDate(ParseFields(ReceivingMasterLabel, 6)[2]);

        Assert.Equal(new DateTime(2026, 8, 7), date);
    }

    [Fact]
    public void ReceivingAndCartonFormats_AreNotInterchangeable()
    {
        Assert.Throws<FormatException>(() => ParseFields(ReceivingMasterLabel, 4));
        Assert.Throws<FormatException>(() => ParseFields(CustomerCartonLabel, 6));
    }

    private static string[] ParseFields(string qr, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(qr))
            throw new ArgumentException("QR code cannot be empty.", nameof(qr));

        var fields = qr.Split(':');
        if (fields.Length != expectedCount)
            throw new FormatException($"Expected {expectedCount} QR fields, received {fields.Length}.");

        for (var i = 0; i < fields.Length; i++)
        {
            fields[i] = fields[i].Trim();
            if (fields[i].Length == 0)
                throw new FormatException($"QR field {i + 1} cannot be empty.");
        }

        return fields;
    }

    private static int ParsePositiveQuantity(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) ||
            quantity <= 0)
            throw new FormatException("QR quantity must be a positive integer.");

        return quantity;
    }

    private static DateTime ParseDate(string value)
    {
        if (!DateTime.TryParseExact(
                value,
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            throw new FormatException("QR date must be a valid dd/MM/yyyy date.");

        return date;
    }
}
