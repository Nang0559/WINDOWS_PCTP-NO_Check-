using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Source-contract guards for findings F1-F6 in docs/TRANSACTION_BOUNDARIES.md.
/// The production repositories target .NET Framework 4.7.2 and cannot be compiled into this
/// project, so these tests read the linked source text (same approach as
/// OrderTableSourceContractTests). They prove the code shape - every statement of a
/// multi-statement write goes through one transaction scope - not runtime behavior.
/// Runtime behavior of the shared scope is covered by InTransactionScenario (G3).
/// </summary>
public sealed class AtomicityFindingsSourceContractTests
{
    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));

    /// <summary>Returns the text of the method that starts at <paramref name="signature"/>, braces balanced.</summary>
    private static string Method(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, "Method not found: " + signature);

        var open = source.IndexOf('{', start);
        Assert.True(open > start, "Method body not found: " + signature);

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }

        throw new InvalidOperationException("Unbalanced braces after: " + signature);
    }

    private static void AssertNoDirectDb(string body, string where)
    {
        Assert.False(Regex.IsMatch(body, @"\bDb\.(ExecuteNonQuery|ExecuteScalar|LoadData|ExecuteStoredProcedure\w*|BulkInsertDataTable)\("),
            where + " must not call Db.* without the UnitOfWork connection/transaction.");
    }

    [Fact]
    public void SqlRepositoryBase_InTransaction_JoinsCallerOrOwnsBeginCommitRollback()
    {
        var source = ReadSource("SqlRepositoryBase.source.cs");
        var body = Method(source, "protected T InTransaction<T>(Func<T> work)");

        Assert.Contains("!HasTransaction", body, StringComparison.Ordinal);
        Assert.Contains("Uow.Begin()", body, StringComparison.Ordinal);
        Assert.Contains("Uow.Commit()", body, StringComparison.Ordinal);
        Assert.Contains("Uow.Rollback()", body, StringComparison.Ordinal);
        Assert.Matches(@"if\s*\(ownTransaction\)\s*Uow\.Commit\(\)", body);
        Assert.Matches(@"if\s*\(ownTransaction\)\s*Uow\.Rollback\(\)", body);
        Assert.Contains("throw;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void F1_UploadMilkrunSP_RunsInOneTransactionWithAtomicCheckAndInsert()
    {
        var source = ReadSource("TableOrderRepo.source.cs");
        var entry = Method(source, "public void UploadMilkrunSP(");
        Assert.Contains("BeginTransaction()", entry, StringComparison.Ordinal);
        Assert.Contains("tran.Commit()", entry, StringComparison.Ordinal);
        Assert.Contains("tran.Rollback()", entry, StringComparison.Ordinal);

        var core = Method(source, "private void UploadMilkrunSPCore(");
        Assert.Matches(@"_db\.ExecuteNonQuery\(\s*conn,\s*tran,", core);
        Assert.False(Regex.IsMatch(core, @"_db\.(ExecuteNonQuery|ExecuteScalar)\(\s*@?""", RegexOptions.None),
            "Every statement in the upload loop must pass conn, tran.");
        Assert.DoesNotContain("SELECT COUNT(*)", core, StringComparison.OrdinalIgnoreCase);

        var normalized = Regex.Replace(core, @"\s+", " ");
        Assert.Contains("IF NOT EXISTS", normalized, StringComparison.Ordinal);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void F2_LayLaiLotNo_BothUpdatesRunInOneScopeThroughUnitOfWork()
    {
        var source = ReadSource("PhieuLotRepository.source.cs");
        var entry = Method(source, "public void LayLaiLotNo(int stt, string tenBan, string docQRTable)");
        Assert.Contains("InTransaction(", entry, StringComparison.Ordinal);

        var core = Method(source, "private void LayLaiLotNoCore(");
        AssertNoDirectDb(core, "LayLaiLotNoCore");
        Assert.Equal(2, Regex.Matches(core, @"(?<![\w.])ExecuteNonQuery\(").Count);
        Assert.Matches(@"(?<![\w.])ExecuteScalar\(", core);
    }

    [Fact]
    public void F3_LuuPhieuSP_UpdateAndCountRunInOneScopeThroughUnitOfWork()
    {
        var source = ReadSource("PhieuLuuTruRepository.source.cs");
        var entry = Method(source, "public int LuuPhieuSP(");
        Assert.Contains("InTransaction(", entry, StringComparison.Ordinal);

        var core = Method(source, "private int LuuPhieuSPCore(");
        AssertNoDirectDb(core, "LuuPhieuSPCore");
        Assert.Matches(@"(?<![\w.])ExecuteNonQuery\(", core);
        Assert.Matches(@"(?<![\w.])ExecuteScalar\(", core);
    }

    [Fact]
    public void F4_LuuGiaoDB_AllFourStepsShareTheUnitOfWorkTransaction()
    {
        var source = ReadSource("PhieuGiaoDBRepository.source.cs");
        var entry = Method(source, "public void LuuGiaoDB(");
        Assert.Contains("InTransaction(", entry, StringComparison.Ordinal);

        var core = Method(source, "private void LuuGiaoDBCore(");
        AssertNoDirectDb(core, "LuuGiaoDBCore");
        Assert.DoesNotContain("Db.DropCreate(ifsTable", core, StringComparison.Ordinal);
        Assert.Matches(@"Db\.DropCreate\(\s*Connection,\s*Transaction,", core);
        Assert.Matches(@"Db\.BulkInsert\(\s*Connection,\s*Transaction,", core);
        Assert.Matches(@"Db\.CallPhieuSP\(\s*Connection,\s*Transaction,", core);
        Assert.Matches(@"(?<![\w.])ExecuteNonQuery\(", core);
    }

    [Fact]
    public void F5_DeleteCascade_DeletesSlotThenRackInsideOneScope()
    {
        var source = ReadSource("RackRepository.source.cs");
        var body = Method(source, "public void DeleteCascade(int rackId)");

        var scope = body.IndexOf("InTransaction(", StringComparison.Ordinal);
        var slot = body.IndexOf("DELETE FROM Slot", StringComparison.Ordinal);
        var rack = body.IndexOf("DELETE FROM Rack", StringComparison.Ordinal);
        Assert.True(scope >= 0, "DeleteCascade must use InTransaction.");
        Assert.True(slot > scope && rack > slot, "Both DELETEs must be inside the scope, Slot before Rack.");
    }

    [Fact]
    public void F6_Insert_HeaderAndItemLinkRunInsideOneScope()
    {
        var source = ReadSource("PhieuXuLyBatThuongRepository.source.cs");
        var entry = Method(source, "public int Insert(");
        Assert.Contains("InTransaction(", entry, StringComparison.Ordinal);
        Assert.Contains("InsertCore(", entry, StringComparison.Ordinal);

        var core = Method(source, "private int InsertCore(");
        Assert.Contains("INSERT INTO FVN_PhieuXuLyBatThuong", core, StringComparison.Ordinal);
        Assert.Contains("UPDATE FVN_PhieuTraHangCT", core, StringComparison.Ordinal);
        AssertNoDirectDb(core, "InsertCore");
    }

    [Fact]
    public void Delivery2405_BusinessValidationReturnsRollbackTheirTransactionScope()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock2405.sql");
        Assert.Contains("ROLLBACK TRANSACTION UspQrcodeUpdateStock2405;", sql, StringComparison.Ordinal);
        Assert.Contains("ELSE IF XACT_STATE() = -1", sql, StringComparison.Ordinal);
        Assert.Contains("-- No stock rows are eligible", sql, StringComparison.Ordinal);
        Assert.Contains("IF @StartedTransaction = 1\n                ROLLBACK TRANSACTION;", sql, StringComparison.Ordinal);
        Assert.True(Regex.Matches(sql, @"IF EXISTS\s*\(\s*SELECT 1\s*FROM #STNG\s*\)[\s\S]*?ROLLBACK TRANSACTION").Count >= 1);
    }

    [Fact]
    public void LegacyFifoProcedure_IsAtomicAndArchivesOnlyRowsCompletedByThisExecution()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock_SP.sql");
        Assert.Contains("BEGIN TRY\n        BEGIN TRANSACTION;", sql, StringComparison.Ordinal);
        Assert.Contains("@Resource = N'PCTP_DELIVERY_FIFO_SP'", sql, StringComparison.Ordinal);
        Assert.Contains("COMMIT TRANSACTION;", sql, StringComparison.Ordinal);
        Assert.Contains("IF XACT_STATE() <> 0\n            ROLLBACK TRANSACTION;", sql, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN #STOK_SP AS completed", sql, StringComparison.Ordinal);
        Assert.Contains("AND ISNULL(SLCONLAI, 0) >= @slCan", sql, StringComparison.Ordinal);
        Assert.Contains("IF @StockUpdated <> 1", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentStockProcedure_UsesExactPhysicalLotAndGuardsRootLotAmbiguity()
    {
        var sql = ReadSource("sp_GetCurrentStockStatus.sql");
        Assert.Contains("RTRIM(sl.LotNo) = RTRIM(stp.LOT)", sql, StringComparison.Ordinal);
        Assert.Contains("lk.PhysicalLotCount = 1", sql, StringComparison.Ordinal);
        Assert.Contains("sl.PhieuStatus = 0", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentStockProcedure_IsReadOnlyAndKeepsTheColumnsTheRepositoryReads()
    {
        var sql = ReadSource("sp_GetCurrentStockStatus.sql");
        var bodyStart = sql.IndexOf("AS", StringComparison.OrdinalIgnoreCase);
        Assert.True(bodyStart >= 0, "Procedure body not found.");
        var procedureBody = sql.Substring(bodyStart + 2);
        Assert.False(Regex.IsMatch(procedureBody, @"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|DROP|EXEC|EXECUTE)\b", RegexOptions.IgnoreCase),
            "sp_GetCurrentStockStatus must stay read-only; deployment DDL is outside the procedure body.");

        foreach (var alias in new[] { "ItemCode", "LotNo", "QrData", "Quantity", "LocationCode", "Status" })
            Assert.Matches(@"\bAS\s+" + alias + @"\b", sql);

        // The DB column is spelled SATUS (typo kept by the schema).
        Assert.Contains("stp.SATUS", sql, StringComparison.Ordinal);
    }
}
