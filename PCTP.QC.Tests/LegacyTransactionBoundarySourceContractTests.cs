using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class LegacyTransactionBoundarySourceContractTests
{
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));

    private static string Method(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, "Method not found: " + signature);
        int open = source.IndexOf('{', start);
        Assert.True(open > start, "Method body not found: " + signature);
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }
        throw new InvalidOperationException("Unbalanced braces: " + signature);
    }

    [Fact]
    public void CheckInfor_AddRackAndAllSlotsShareOneTransaction()
    {
        string body = Method(Read("CheckInfor.source.cs"), "public void AddRackToExistingWarehouse(");
        Assert.Contains("BeginTransaction()", body, StringComparison.Ordinal);
        Assert.Contains("transaction.Commit()", body, StringComparison.Ordinal);
        Assert.Contains("transaction.Rollback()", body, StringComparison.Ordinal);
        Assert.Contains("new SqlCommand", body, StringComparison.Ordinal);
        Assert.Contains("conn, transaction", body, StringComparison.Ordinal);
        Assert.Contains("foreach (var slot in newRack.Slots)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.ExecuteScalar", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparePart_CreateAndDeleteAreAtomicAndRaceSafe()
    {
        string source = Read("ComaparePart.source.cs");
        string insert = Method(source, "private bool TryInsertComparePart(");
        Assert.Contains("IsolationLevel.Serializable", insert, StringComparison.Ordinal);
        Assert.Contains("UPDLOCK, HOLDLOCK", insert, StringComparison.Ordinal);
        Assert.Contains("transaction.Commit()", insert, StringComparison.Ordinal);
        Assert.Contains("transaction.Rollback()", insert, StringComparison.Ordinal);

        string delete = Method(source, "private void DeleteCompareParts(");
        Assert.Contains("BeginTransaction()", delete, StringComparison.Ordinal);
        Assert.Contains("transaction.Commit()", delete, StringComparison.Ordinal);
        Assert.Contains("transaction.Rollback()", delete, StringComparison.Ordinal);
        Assert.Contains("ids.Distinct()", delete, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyDeliveryLotEdit_UsesParametersAndChecksAffectedRows()
    {
        string body = Method(Read("Edit_DH.source.cs"), "private void simpleButton3_Click_1(");
        Assert.Contains("UPDATE tmpphieugiaohang", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@Lot", body, StringComparison.Ordinal);
        Assert.Contains("@Stt", body, StringComparison.Ordinal);
        Assert.Contains("affected != 1", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" + lot.Text + ", body, StringComparison.Ordinal);
    }

    [Fact]
    public void QrChangeTimeUpdate_UsesParametersAndChecksSingleRow()
    {
        string body = Method(Read("UF_CHANGETIME.source.cs"), "private void cmdsua_ButtonClick(");
        Assert.Contains("UPDATE QRCODE_CHANGETIME", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@GioFCCVP", body, StringComparison.Ordinal);
        Assert.Contains("@GIOFCCHN", body, StringComparison.Ordinal);
        Assert.Contains("@GIOHVN", body, StringComparison.Ordinal);
        Assert.Contains("affected != 1", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" + GIOfcc", body, StringComparison.Ordinal);
    }
}
