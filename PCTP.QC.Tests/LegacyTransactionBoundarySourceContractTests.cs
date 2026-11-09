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
        string source = Read("ComparePartRepository.source.cs");
        string insert = Method(source, "public bool TryInsert(");
        Assert.Contains("IsolationLevel.Serializable", insert, StringComparison.Ordinal);
        Assert.Contains("UPDLOCK, HOLDLOCK", insert, StringComparison.Ordinal);
        Assert.Contains("transaction.Commit()", insert, StringComparison.Ordinal);
        Assert.Contains("transaction.Rollback()", insert, StringComparison.Ordinal);

        string delete = Method(source, "public int Delete(");
        Assert.Contains("BeginTransaction()", delete, StringComparison.Ordinal);
        Assert.Contains("transaction.Commit()", delete, StringComparison.Ordinal);
        Assert.Contains("transaction.Rollback()", delete, StringComparison.Ordinal);
        Assert.Contains("keys.Distinct()", delete, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparePart_InsertAllocatesSttAndStoresPartName()
    {
        // tbl_QR_ComparePart.STT is int NULL (not identity): without MAX(STT)+1 every new row has a NULL STT
        // and can no longer be selected for delete. PartName must be stored too (nvarchar(30)).
        string insert = Method(Read("ComparePartRepository.source.cs"), "public bool TryInsert(");
        Assert.Contains("MAX(STT)", insert, StringComparison.Ordinal);
        Assert.Contains("COLUMNPROPERTY", insert, StringComparison.Ordinal);
        Assert.Contains("LEFT(Name, 30)", insert, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparePart_DeleteMatchesFullKeyAndIsNullSafe()
    {
        string delete = Method(Read("ComparePartRepository.source.cs"), "public int Delete(");
        Assert.Contains("ISNULL(PartNo, '') = @PartNo", delete, StringComparison.Ordinal);
        Assert.Contains("ISNULL(PartNoCompare, '') = @PartNoCompare", delete, StringComparison.Ordinal);
        Assert.Contains("@STT IS NULL AND STT IS NULL", delete, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparePart_FormUsesRepositoryOnly()
    {
        string form = Read("ComaparePart.source.cs");
        Assert.Contains("IComparePartRepository", form, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlConnection", form, StringComparison.Ordinal);
        Assert.DoesNotContain("SQLPROVIDER _sql", form, StringComparison.Ordinal);
        Assert.Contains("private void cmd_Huy_Click(", form, StringComparison.Ordinal);
        // Changing the source part must clear the previously chosen target part.
        string changed = Method(form, "private void lup_Ma_EditValueChanged(");
        Assert.Contains("LuKToPart.EditValue = null", changed, StringComparison.Ordinal);
    }

    [Fact]
    public void HvnLotReassignment_ResetsAndUpdatesSelectedItemInOneTransaction()
    {
        string body = Method(Read("FRM_SUALOTHVN.source.cs"), "private void simpleButton1_Click(");
        Assert.Contains("BeginTransaction()", body, StringComparison.Ordinal);
        Assert.Contains("transaction.Commit()", body, StringComparison.Ordinal);
        Assert.Contains("transaction.Rollback()", body, StringComparison.Ordinal);
        Assert.Contains("AND MAHANGFCC = @MaHang", body, StringComparison.Ordinal);
        Assert.Contains("connection, transaction", body, StringComparison.Ordinal);
        Assert.DoesNotContain("sqlBRV.ExecuteNonQuery", body, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyDeliveryLotEdit_UsesParametersAndChecksAffectedRows()
    {
        string source = Read("Edit_DH.source.cs");
        string lookup = Method(source, "private void load()");
        Assert.Contains("WHERE part = @Part", lookup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("new SqlParameter(\"@Part\"", lookup, StringComparison.Ordinal);
        string body = Method(source, "private void simpleButton3_Click_1(");
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
