using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using PCTP.Common;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// B0-01 characterization ("specification") suite for the YMVN customer-delivery stock path,
/// i.e. PhieuKhoRepository.CapNhapKhoYMVN -> TruStockTpFifo -> BulkStockAdjustService.TruKhoAoTheoLot.
///
/// Purpose: freeze the CURRENT observable behaviour before B0-01 replaces the legacy writer with a
/// delivery command, so the replacement can be proven to keep parity (FIFO, multi-LOT, partial,
/// reconciliation, rollback, retry, concurrency).
///
/// What runs on SQL Server: the three STOCKTP statements below are extracted AT RUN TIME from the
/// production source (PhieuKhoRepository.cs), so a change to them breaks this scenario instead of
/// drifting silently:
///   1. the lock-read   SELECT ISNULL(SUM(SLCONLAI),0) FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE {matchCondition}
///   2. the FIFO select SELECT LOT, SLCONLAI FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE ... ORDER BY NGAYNHAP, LOT
///   3. the guarded     UPDATE STOCKTP SET SLXUAT = ..., SLCONLAI = SLCONLAI - @sl ... AND SLCONLAI >= @sl
/// The LOT match predicate is the real LotCodeHelper.BuildLotMatchSql (compiled into this harness).
///
/// What does NOT run here: the C# method CapNhapKhoYMVN itself and BulkStockAdjustService (they need the
/// WinForms/DevExpress dependency graph). Their step ORDER is mirrored below and kept honest by
/// PCTP.QC.Tests/YmvnDeliverySpecContractTests (source contract). The SlotLot pick is a SQL mirror of
/// "virtual slot A0 first, then real slots FIFO, throw when short" (manageTransaction:false behaviour).
/// Therefore this is protocol-level evidence only; it does NOT close B0-01.
///
/// Controls: Y12 strips the locks and the quantity guard and MUST oversell; if it does not, the race is
/// not detectable and the scenario fails.
/// All fixtures are uniquely named ##PCTP_QC_YMVN_* tempdb tables; no dbo table is touched.
/// </summary>
internal static class YmvnDeliverySpecScenario
{
    private enum Result { Committed, Invalid, Shortage, Failed }

    private sealed record Outcome(Result Kind, string Message = "", int Slc = 0, int Sltk = 0, int Slt = 0);

    private sealed class Fx
    {
        public string Stock = "", Slot = "", Tmp = "", DocQr = "", LuuDoc = "", LuuPhieu = "";
    }

    private sealed class ProdSql
    {
        public string LockRead = "", FifoSelect = "", FifoUpdate = "";
    }

    private sealed class Opt
    {
        public bool StripLocks;
        public bool StripGuard;
        public bool SkipSlotLot;
        public bool FailBeforeCommit;
        public Func<Task>? AfterLockRead;
        public Func<Task>? AfterFifoSelect;
    }

    private static int _stt = 1000;
    private static int NextStt() => Interlocked.Increment(ref _stt);

    // 13-char legacy key = date(6) + id(5) + shift(1) + gear(1); 20-char key adds line(3)+machine(4).
    private static string Base13(int n) => $"2610{n:D2}0001910";
    private static string Key20(int n, int suffix) => Base13(n) + suffix.ToString("D7");

    public static async Task RunAsync(string connectionString)
    {
        var sql = LoadProductionSql();
        var prefix = "##PCTP_QC_YMVN_" + Guid.NewGuid().ToString("N") + "_";
        var f = new Fx
        {
            Stock = prefix + "STOCKTP",
            Slot = prefix + "SlotLot",
            Tmp = prefix + "Tmp",
            DocQr = prefix + "DocQr",
            LuuDoc = prefix + "LuuDoc",
            LuuPhieu = prefix + "LuuPhieu"
        };

        await using var owner = await OpenAsync(connectionString);
        try
        {
            await ExecAsync(owner, null, $@"
CREATE TABLE [{f.Stock}](LOT nvarchar(100) NOT NULL PRIMARY KEY, SLNHAP int NOT NULL, SLXUAT int NULL, SLCONLAI int NOT NULL, NGAYNHAP datetime NOT NULL, NGAYXUAT datetime NULL);
CREATE TABLE [{f.Slot}](SlotLotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, SlotId int NOT NULL, LotNo nvarchar(100) NOT NULL, Quantity int NOT NULL CHECK(Quantity >= 0), PhieuStatus tinyint NOT NULL, ImportDate datetime NOT NULL);
CREATE TABLE [{f.Tmp}](STT int NOT NULL PRIMARY KEY, MAHANG nvarchar(100) NOT NULL, LOT nvarchar(500) NOT NULL, SOLUONG int NOT NULL, STATUS nvarchar(10) NOT NULL);
CREATE TABLE [{f.DocQr}](Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, MAHANGFCC nvarchar(100) NOT NULL, STT int NOT NULL, KETQUA nvarchar(10) NOT NULL);
CREATE TABLE [{f.LuuDoc}](Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, MAHANGFCC nvarchar(100) NOT NULL, STT int NOT NULL, KETQUA nvarchar(10) NOT NULL);
CREATE TABLE [{f.LuuPhieu}](Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, STT int NOT NULL, MAHANG nvarchar(100) NOT NULL, SOLUONG int NOT NULL, STATUS nvarchar(10) NOT NULL);");

            await Y1_FifoOrderTieBreakAndPartialRowAsync(connectionString, owner, f, sql);
            await Y2_ExactTwentyCharKeyIsolatesNeighbourAsync(connectionString, owner, f, sql);
            await Y3_LegacyShortLiteralMatchesThirteenCharKeyAsync(connectionString, owner, f, sql);
            await Y4_MultiLotCompositeAsync(connectionString, owner, f, sql);
            await Y5_PartialDeliveryKeepsStockEquationAsync(connectionString, owner, f, sql);
            await Y6_ShortageOnSecondLotRollsBackFirstLotAsync(connectionString, owner, f, sql);
            await Y7_InvalidFormatsRejectedBeforeAnyWriteAsync(connectionString, owner, f, sql);
            await Y8_SlotLotShortFailsClosedAsync(connectionString, owner, f, sql);
            await Y9_InjectedFailureRollsBackEverythingAsync(connectionString, owner, f, sql);
            await Y10_RetrySameSttDoesNotDeductTwiceAsync(connectionString, owner, f, sql);
            await Y11_TwoSessionsSameRemainingQuantityAsync(connectionString, owner, f, sql);
            await Y12_ControlWithoutLocksAndGuardOversellsAsync(connectionString, owner, f, sql);
            await Y13_GuardAloneStillPreventsOversellAsync(connectionString, owner, f, sql);
            await Y14_TwoSessionsSameSttDeductOnceAsync(connectionString, owner, f, sql);
            await Y15_DocQrRowsAreScopedByMaHangNotSttAsync(connectionString, owner, f, sql);

            Console.WriteLine("PASS: YMVN delivery specification (B0-01) verified on SQL Server with production STOCKTP SQL text; protocol-level characterization only, B0-01 stays OPEN.");
        }
        finally
        {
            foreach (var table in new[] { f.LuuPhieu, f.LuuDoc, f.DocQr, f.Tmp, f.Slot, f.Stock })
                await ExecAsync(owner, null, $"IF OBJECT_ID(N'tempdb..{table}') IS NOT NULL DROP TABLE [{table}];");
        }
    }

    // ------------------------------------------------------------------ cases

    private static async Task Y1_FifoOrderTieBreakAndPartialRowAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var b = Base13(1);
        string s1 = b + "01", s2 = b + "02", s3 = b + "03";       // short (legacy) LOT values, 15 chars
        await SeedStockAsync(o, f, s1, 10, new DateTime(2026, 10, 2));
        await SeedStockAsync(o, f, s2, 10, new DateTime(2026, 10, 1));
        await SeedStockAsync(o, f, s3, 10, new DateTime(2026, 10, 1)); // same receipt date as s2 -> LOT ASC decides
        await SeedSlotAsync(o, f, 1, s1, 10, new DateTime(2026, 10, 2));   // slot 1 = virtual A0
        await SeedSlotAsync(o, f, 2, s2, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 3, s3, 10, new DateTime(2026, 10, 1));

        var stt = await SeedTmpAsync(o, f, "QC-MH-01");
        var r = await DeliverAsync(cs, f, sql, stt, b + "0000000-25", "QC-MH-01", new Opt());
        Require(r.Kind == Result.Committed, $"Y1 expected Committed, got {r}.");
        Require(await StockAsync(o, f, s2) == 0 && await StockAsync(o, f, s3) == 0 && await StockAsync(o, f, s1) == 5,
            "Y1 FIFO must consume (NGAYNHAP ASC, LOT ASC): 02 then 03 then 01 (10 + 10 + 5).");
        Require(await ReconciledAsync(o, f, b + "0000000"), "Y1 STOCKTP must equal active SlotLot for the LOT key.");
        Console.WriteLine("PASS: Y1 FIFO by NGAYNHAP then LOT, partial last row, STOCKTP == SlotLot.");
    }

    private static async Task Y2_ExactTwentyCharKeyIsolatesNeighbourAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        string e1 = Key20(2, 1), e2 = Key20(2, 2);   // same 13-char prefix, different 20-char keys
        await SeedStockAsync(o, f, e1, 10, new DateTime(2026, 10, 1));
        await SeedStockAsync(o, f, e2, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 2, e1, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 3, e2, 10, new DateTime(2026, 10, 1));

        var stt = await SeedTmpAsync(o, f, "QC-MH-02");
        var ok = await DeliverAsync(cs, f, sql, stt, e1 + "-4", "QC-MH-02", new Opt());
        Require(ok.Kind == Result.Committed && await StockAsync(o, f, e1) == 6 && await StockAsync(o, f, e2) == 10,
            "Y2 delivering a 20-char key must not touch a neighbour that shares only the 13-char prefix.");

        var stt2 = await SeedTmpAsync(o, f, "QC-MH-02");
        var over = await DeliverAsync(cs, f, sql, stt2, e1 + "-7", "QC-MH-02", new Opt());
        Require(over.Kind == Result.Shortage && over.Sltk == 6 && over.Slt == 1 && await StockAsync(o, f, e2) == 10,
            $"Y2 over-request must report shortage against its own key only (SLTK=6, SLT=1); got {over}.");
        Console.WriteLine("PASS: Y2 exact 20-char match isolates same-prefix neighbour; shortage reported per key.");
    }

    private static async Task Y3_LegacyShortLiteralMatchesThirteenCharKeyAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        // CHARACTERIZATION / FINDING F-Y2: a literal shorter than 20 chars falls back to the 13-char legacy key,
        // so it can consume a 20-char lot that merely shares the prefix. Frozen here; the delivery command must
        // either keep this on purpose or replace it with an explicit rule.
        var b = Base13(3);
        string legacy = b + "A1", exact = Key20(3, 9);
        await SeedStockAsync(o, f, legacy, 5, new DateTime(2026, 10, 1));
        await SeedStockAsync(o, f, exact, 5, new DateTime(2026, 10, 2));
        await SeedSlotAsync(o, f, 2, legacy, 5, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 3, exact, 5, new DateTime(2026, 10, 2));

        var stt = await SeedTmpAsync(o, f, "QC-MH-03");
        var r = await DeliverAsync(cs, f, sql, stt, legacy + "-7", "QC-MH-03", new Opt());
        Require(r.Kind == Result.Committed && await StockAsync(o, f, legacy) == 0 && await StockAsync(o, f, exact) == 3,
            $"Y3 current behaviour: short literal consumes the legacy row first, then the 20-char row sharing the prefix; got {r}.");
        Console.WriteLine("PASS: Y3 (characterization) short literal matches by 13-char key and spills into a same-prefix 20-char lot. FINDING F-Y2 for the owner.");
    }

    private static async Task Y4_MultiLotCompositeAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        string k4 = Key20(4, 1), k5 = Key20(5, 1);
        await SeedStockAsync(o, f, k4, 10, new DateTime(2026, 10, 1));
        await SeedStockAsync(o, f, k5, 8, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k4, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 2, k5, 8, new DateTime(2026, 10, 1));

        var stt = await SeedTmpAsync(o, f, "QC-MH-04");
        await SeedDocQrAsync(o, f, "QC-MH-04", stt, "DG");
        var r = await DeliverAsync(cs, f, sql, stt, $"{k4}-10, {k5}-5", "QC-MH-04", new Opt());
        Require(r.Kind == Result.Committed, $"Y4 expected Committed, got {r}.");
        Require(await StockAsync(o, f, k4) == 0 && await StockAsync(o, f, k5) == 3, "Y4 both LOTs of the composite must be deducted.");
        Require(await ReconciledAsync(o, f, k4) && await ReconciledAsync(o, f, k5), "Y4 each LOT must reconcile with SlotLot.");
        Require(await ScalarAsync(o, null, $"SELECT STATUS FROM [{f.Tmp}] WHERE STT={stt}") as string == "OK", "Y4 TMP row must become OK.");
        Require(await CountAsync(o, $"SELECT COUNT(*) FROM [{f.LuuPhieu}] WHERE STT={stt}") == 1, "Y4 exactly one delivery ledger row.");
        Require(await CountAsync(o, $"SELECT COUNT(*) FROM [{f.DocQr}] WHERE MAHANGFCC='QC-MH-04' AND KETQUA='DG'") == 0, "Y4 DG scan rows must be consumed.");
        Console.WriteLine("PASS: Y4 multi-LOT composite deducts every LOT, writes one ledger row, closes TMP and DG rows.");
    }

    private static async Task Y5_PartialDeliveryKeepsStockEquationAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(6, 1);
        await SeedStockAsync(o, f, k, 100, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 100, new DateTime(2026, 10, 1));
        for (var i = 0; i < 2; i++)
        {
            var stt = await SeedTmpAsync(o, f, "QC-MH-06");
            var r = await DeliverAsync(cs, f, sql, stt, k + "-30", "QC-MH-06", new Opt());
            Require(r.Kind == Result.Committed, $"Y5 delivery {i + 1} expected Committed, got {r}.");
        }
        Require(await ScalarIntAsync(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='{k}'") == 40, "Y5 remaining must be 40 after two partial deliveries of 30.");
        Require(await ScalarIntAsync(o, $"SELECT CASE WHEN SLNHAP-ISNULL(SLXUAT,0)=SLCONLAI THEN 1 ELSE 0 END FROM [{f.Stock}] WHERE LOT='{k}'") == 1, "Y5 SLNHAP - SLXUAT must equal SLCONLAI.");
        Require(await ReconciledAsync(o, f, k), "Y5 STOCKTP must equal active SlotLot.");
        Console.WriteLine("PASS: Y5 partial deliveries keep SLNHAP - SLXUAT = SLCONLAI and reconcile with SlotLot.");
    }

    private static async Task Y6_ShortageOnSecondLotRollsBackFirstLotAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        string k7 = Key20(7, 1), k8 = Key20(8, 1);
        await SeedStockAsync(o, f, k7, 10, new DateTime(2026, 10, 1));
        await SeedStockAsync(o, f, k8, 3, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k7, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k8, 3, new DateTime(2026, 10, 1));

        var stt = await SeedTmpAsync(o, f, "QC-MH-07");
        var r = await DeliverAsync(cs, f, sql, stt, $"{k7}-10,{k8}-5", "QC-MH-07", new Opt());
        Require(r.Kind == Result.Shortage && r.Slc == 5 && r.Sltk == 3 && r.Slt == 2, $"Y6 expected Shortage SLC=5 SLTK=3 SLT=2, got {r}.");
        Require(await StockAsync(o, f, k7) == 10 && await StockAsync(o, f, k8) == 3, "Y6 first lot's deduction must be rolled back with the whole delivery.");
        Require(await ScalarIntAsync(o, $"SELECT SUM(Quantity) FROM [{f.Slot}] WHERE LotNo='{k7}'") == 10, "Y6 SlotLot of the first lot must be restored.");
        Require(await ScalarAsync(o, null, $"SELECT STATUS FROM [{f.Tmp}] WHERE STT={stt}") as string == "NG", "Y6 TMP row must stay NG.");
        Require(await CountAsync(o, $"SELECT COUNT(*) FROM [{f.LuuPhieu}] WHERE STT={stt}") == 0, "Y6 no ledger row.");
        Console.WriteLine("PASS: Y6 shortage on lot 2 reports SLC/SLTK/SLT and rolls back lot 1, SlotLot, TMP and ledger.");
    }

    private static async Task Y7_InvalidFormatsRejectedBeforeAnyWriteAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(9, 1);
        await SeedStockAsync(o, f, k, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 10, new DateTime(2026, 10, 1));
        var stt = await SeedTmpAsync(o, f, "QC-MH-09");
        foreach (var bad in new[] { "", "   ", ",", "abc", "abc-", "-5", k + "-0", k + "-x", k + "--0" })
        {
            var r = await DeliverAsync(cs, f, sql, stt, bad, "QC-MH-09", new Opt());
            Require(r.Kind == Result.Invalid, $"Y7 '{bad}' must be rejected as Invalid, got {r}.");
        }
        Require(await StockAsync(o, f, k) == 10 && await ScalarAsync(o, null, $"SELECT STATUS FROM [{f.Tmp}] WHERE STT={stt}") as string == "NG",
            "Y7 invalid input must not write anything.");
        Console.WriteLine("PASS: Y7 malformed LOT-quantity input is rejected before any database write.");
    }

    private static async Task Y8_SlotLotShortFailsClosedAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(10, 1);
        await SeedStockAsync(o, f, k, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 6, new DateTime(2026, 10, 1));   // SlotLot lags STOCKTP by 4
        var stt = await SeedTmpAsync(o, f, "QC-MH-10");
        var r = await DeliverAsync(cs, f, sql, stt, k + "-10", "QC-MH-10", new Opt());
        Require(r.Kind == Result.Failed && r.Message.Contains("SLOTLOT_SHORT"), $"Y8 expected fail-closed SLOTLOT_SHORT, got {r}.");
        Require(await StockAsync(o, f, k) == 10 && await ScalarIntAsync(o, $"SELECT SUM(Quantity) FROM [{f.Slot}] WHERE LotNo='{k}'") == 6,
            "Y8 STOCKTP deduction must be rolled back when SlotLot cannot cover it (manageTransaction:false).");
        Console.WriteLine("PASS: Y8 SlotLot shortage inside the shared transaction fails closed and restores STOCKTP.");
    }

    private static async Task Y9_InjectedFailureRollsBackEverythingAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(11, 1);
        await SeedStockAsync(o, f, k, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 10, new DateTime(2026, 10, 1));
        var stt = await SeedTmpAsync(o, f, "QC-MH-11");
        await SeedDocQrAsync(o, f, "QC-MH-11", stt, "DG");
        var r = await DeliverAsync(cs, f, sql, stt, k + "-10", "QC-MH-11", new Opt { FailBeforeCommit = true });
        Require(r.Kind == Result.Failed && r.Message.Contains("INJECTED"), $"Y9 expected injected failure, got {r}.");
        Require(await StockAsync(o, f, k) == 10, "Y9 STOCKTP must be restored.");
        Require(await ScalarIntAsync(o, $"SELECT SUM(Quantity) FROM [{f.Slot}] WHERE LotNo='{k}'") == 10, "Y9 SlotLot must be restored.");
        Require(await ScalarAsync(o, null, $"SELECT STATUS FROM [{f.Tmp}] WHERE STT={stt}") as string == "NG", "Y9 TMP must stay NG.");
        Require(await CountAsync(o, $"SELECT COUNT(*) FROM [{f.LuuPhieu}] WHERE STT={stt}") == 0
            && await CountAsync(o, $"SELECT COUNT(*) FROM [{f.LuuDoc}] WHERE MAHANGFCC='QC-MH-11'") == 0
            && await CountAsync(o, $"SELECT COUNT(*) FROM [{f.DocQr}] WHERE MAHANGFCC='QC-MH-11' AND KETQUA='DG'") == 1,
            "Y9 ledgers must be empty and the DG scan rows must still exist.");
        Console.WriteLine("PASS: Y9 failure after every write and before commit leaves stock, SlotLot, TMP, ledgers and scan rows untouched.");
    }

    private static async Task Y10_RetrySameSttDoesNotDeductTwiceAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        // Ample stock: the retry passes the shortage check, deducts, then fails on the TMP guard and rolls back.
        var k = Key20(12, 1);
        await SeedStockAsync(o, f, k, 30, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 30, new DateTime(2026, 10, 1));
        var stt = await SeedTmpAsync(o, f, "QC-MH-12");
        var first = await DeliverAsync(cs, f, sql, stt, k + "-10", "QC-MH-12", new Opt());
        var retry = await DeliverAsync(cs, f, sql, stt, k + "-10", "QC-MH-12", new Opt());
        Require(first.Kind == Result.Committed, $"Y10 first delivery expected Committed, got {first}.");
        Require(retry.Kind == Result.Failed && retry.Message.Contains("TMP_NOT_PENDING"), $"Y10 retry with ample stock must fail on the TMP guard, got {retry}.");
        Require(await StockAsync(o, f, k) == 20 && await ReconciledAsync(o, f, k), "Y10 retry must not deduct a second time.");

        // Scarce stock: the same retry is reported as a SHORTAGE, not as "already delivered". Frozen as current behaviour (finding F-Y3).
        var s = Key20(13, 1);
        await SeedStockAsync(o, f, s, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, s, 10, new DateTime(2026, 10, 1));
        var stt2 = await SeedTmpAsync(o, f, "QC-MH-13");
        var first2 = await DeliverAsync(cs, f, sql, stt2, s + "-10", "QC-MH-13", new Opt());
        var retry2 = await DeliverAsync(cs, f, sql, stt2, s + "-10", "QC-MH-13", new Opt());
        Require(first2.Kind == Result.Committed && retry2.Kind == Result.Shortage && retry2.Sltk == 0 && await StockAsync(o, f, s) == 0,
            $"Y10 scarce-stock retry must surface as Shortage with SLTK=0; got first={first2}, retry={retry2}.");
        Console.WriteLine("PASS: Y10 retry of the same STT never deducts twice (ample stock: TMP guard; scarce stock: reported as shortage, FINDING F-Y3).");
    }

    private static async Task Y11_TwoSessionsSameRemainingQuantityAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(14, 1);
        await SeedStockAsync(o, f, k, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 10, new DateTime(2026, 10, 1));
        var sttA = await SeedTmpAsync(o, f, "QC-MH-14A");
        var sttB = await SeedTmpAsync(o, f, "QC-MH-14B");
        var (a, b) = await LockedRaceAsync(cs, f, sql, sttA, sttB, k + "-10", "QC-MH-14A", "QC-MH-14B");
        Require(a.Kind == Result.Committed && b.Kind == Result.Shortage && b.Sltk == 0,
            $"Y11 expected A=Committed, B=Shortage(SLTK=0); got A={a}, B={b}.");
        Require(await StockAsync(o, f, k) == 0 && await ReconciledAsync(o, f, k), "Y11 stock must be exactly 0 and reconciled.");
        Console.WriteLine("PASS: Y11 two sessions delivering the same remaining 10 under the production STOCKTP lock: one committed, one shortage, stock=0.");
    }

    private static async Task Y12_ControlWithoutLocksAndGuardOversellsAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(15, 1);
        await SeedStockAsync(o, f, k, 10, new DateTime(2026, 10, 1));
        var sttA = await SeedTmpAsync(o, f, "QC-MH-15A");
        var sttB = await SeedTmpAsync(o, f, "QC-MH-15B");
        using var barrier = new Barrier(2);
        Func<Task> meet = () => MeetAsync(barrier);
        var opt = () => new Opt { StripLocks = true, StripGuard = true, SkipSlotLot = true, AfterFifoSelect = meet };
        var ta = Task.Run(() => DeliverAsync(cs, f, sql, sttA, k + "-10", "QC-MH-15A", opt()));
        var tb = Task.Run(() => DeliverAsync(cs, f, sql, sttB, k + "-10", "QC-MH-15B", opt()));
        var a = await ta;
        var b = await tb;
        var left = await StockAsync(o, f, k);
        Require(a.Kind == Result.Committed && b.Kind == Result.Committed && left == -10,
            $"Y12 negative control must oversell (SLCONLAI=-10) to prove the race is detectable; got A={a}, B={b}, SLCONLAI={left}.");
        Console.WriteLine("PASS: Y12 negative control without locks and guard oversold (SLCONLAI=-10), so Y11/Y13 are meaningful.");
    }

    private static async Task Y13_GuardAloneStillPreventsOversellAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(16, 1);
        await SeedStockAsync(o, f, k, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 10, new DateTime(2026, 10, 1));
        var sttA = await SeedTmpAsync(o, f, "QC-MH-16A");
        var sttB = await SeedTmpAsync(o, f, "QC-MH-16B");
        using var barrier = new Barrier(2);
        Func<Task> meet = () => MeetAsync(barrier);
        var opt = () => new Opt { StripLocks = true, AfterFifoSelect = meet };
        var ta = Task.Run(() => DeliverAsync(cs, f, sql, sttA, k + "-10", "QC-MH-16A", opt()));
        var tb = Task.Run(() => DeliverAsync(cs, f, sql, sttB, k + "-10", "QC-MH-16B", opt()));
        var results = new[] { await ta, await tb };
        var committed = results.Count(r => r.Kind == Result.Committed);
        var changed = results.Count(r => r.Kind == Result.Failed && r.Message.Contains("STOCK_CHANGED"));
        Require(committed == 1 && changed == 1 && await StockAsync(o, f, k) == 0 && await ReconciledAsync(o, f, k),
            $"Y13 with locks removed the guarded UPDATE must still stop the oversell (1 committed, 1 STOCK_CHANGED); got {results[0]} / {results[1]}.");
        Console.WriteLine("PASS: Y13 defense in depth: the 'SLCONLAI >= @sl' guard alone prevents oversell; the locks turn the failure into a clean shortage.");
    }

    private static async Task Y14_TwoSessionsSameSttDeductOnceAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        var k = Key20(17, 1);
        await SeedStockAsync(o, f, k, 30, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 30, new DateTime(2026, 10, 1));
        var stt = await SeedTmpAsync(o, f, "QC-MH-17");
        var (a, b) = await LockedRaceAsync(cs, f, sql, stt, stt, k + "-10", "QC-MH-17", "QC-MH-17");
        Require(a.Kind == Result.Committed && b.Kind == Result.Failed && b.Message.Contains("TMP_NOT_PENDING"),
            $"Y14 expected A=Committed, B=Failed(TMP_NOT_PENDING); got A={a}, B={b}.");
        Require(await StockAsync(o, f, k) == 20 && await ReconciledAsync(o, f, k)
            && await CountAsync(o, $"SELECT COUNT(*) FROM [{f.LuuPhieu}] WHERE STT={stt}") == 1,
            "Y14 the same STT must deduct once and write one ledger row.");
        Console.WriteLine("PASS: Y14 two sessions confirming the same STT: stock deducted once, loser rolled back by the TMP guard.");
    }

    private static async Task Y15_DocQrRowsAreScopedByMaHangNotSttAsync(string cs, SqlConnection o, Fx f, ProdSql sql)
    {
        // CHARACTERIZATION / FINDING F-Y1: the scan-row archive + delete uses MAHANGFCC and KETQUA='DG' only,
        // so delivering one STT also archives and deletes DG rows that carry another STT of the same part.
        // Needs product-owner confirmation: intended ("complete the whole part") or cross-STT bleed.
        var k = Key20(18, 1);
        await SeedStockAsync(o, f, k, 10, new DateTime(2026, 10, 1));
        await SeedSlotAsync(o, f, 1, k, 10, new DateTime(2026, 10, 1));
        var a = await SeedTmpAsync(o, f, "QC-MH-18");
        var b = await SeedTmpAsync(o, f, "QC-MH-18");
        await SeedDocQrAsync(o, f, "QC-MH-18", a, "DG");
        await SeedDocQrAsync(o, f, "QC-MH-18", b, "DG");
        await SeedDocQrAsync(o, f, "QC-MH-18", b, "NG");
        var r = await DeliverAsync(cs, f, sql, a, k + "-3", "QC-MH-18", new Opt());
        Require(r.Kind == Result.Committed, $"Y15 expected Committed, got {r}.");
        Require(await CountAsync(o, $"SELECT COUNT(*) FROM [{f.LuuDoc}] WHERE MAHANGFCC='QC-MH-18'") == 2
            && await CountAsync(o, $"SELECT COUNT(*) FROM [{f.LuuDoc}] WHERE MAHANGFCC='QC-MH-18' AND STT={b}") == 1
            && await CountAsync(o, $"SELECT COUNT(*) FROM [{f.DocQr}] WHERE MAHANGFCC='QC-MH-18'") == 1,
            "Y15 current behaviour: both DG rows (STT a and STT b) are archived and deleted; only the non-DG row remains.");
        Console.WriteLine("PASS: Y15 (characterization) DG scan rows are consumed per part (MAHANGFCC), not per STT. FINDING F-Y1 for the owner.");
    }

    // ------------------------------------------------------------------ mirror of CapNhapKhoYMVN

    private static async Task<Outcome> DeliverAsync(string cs, Fx f, ProdSql sql, int stt, string lotSl, string maHang, Opt o)
    {
        // 1) parse, exactly as CapNhapKhoYMVN does before Uow.Begin()
        if (string.IsNullOrWhiteSpace(lotSl)) return new Outcome(Result.Invalid, "empty");
        var items = new List<(string Lot, int Qty)>();
        foreach (var raw in lotSl.Split(','))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var part = raw.Trim();
            var sep = part.LastIndexOf('-');
            if (sep <= 0 || sep >= part.Length - 1 || !int.TryParse(part.Substring(sep + 1).Trim(), out var sl) || sl <= 0)
                return new Outcome(Result.Invalid, "format");
            var lot = LotCodeHelper.TrimTo(part.Substring(0, sep).Trim(), LotCodeHelper.LEN_HEAD_FIXED);
            if (string.IsNullOrWhiteSpace(lot)) return new Outcome(Result.Invalid, "lot");
            items.Add((lot, sl));
        }
        if (items.Count == 0) return new Outcome(Result.Invalid, "none");

        await using var c = await OpenAsync(cs);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
        try
        {
            foreach (var (lot, qty) in items)
            {
                var match = LotCodeHelper.BuildLotMatchSql("LOT", "'" + lot.Replace("'", "''") + "'");

                // 2) lock-read + shortage check
                var remaining = Convert.ToInt32(await ScalarAsync(c, tx, Shape(sql.LockRead, f, match, o.StripLocks, false)) ?? 0);
                if (remaining < qty)
                {
                    await tx.RollbackAsync();
                    return new Outcome(Result.Shortage, "shortage", qty, remaining, qty - remaining);
                }
                if (o.AfterLockRead != null) await o.AfterLockRead();

                // 3) TruStockTpFifo
                var rows = new List<(string Lot, int Qty)>();
                await using (var select = new SqlCommand(Shape(sql.FifoSelect, f, match, o.StripLocks, false), c, tx) { CommandTimeout = 30 })
                await using (var reader = await select.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        rows.Add((reader.GetString(0), Convert.ToInt32(reader.GetValue(1))));
                }
                if (o.AfterFifoSelect != null) await o.AfterFifoSelect();

                var need = qty;
                foreach (var (rowLot, rowQty) in rows)
                {
                    if (need <= 0) break;
                    var take = Math.Min(rowQty, need);
                    await using var update = new SqlCommand(Shape(sql.FifoUpdate, f, match, false, o.StripGuard), c, tx) { CommandTimeout = 30 };
                    update.Parameters.AddWithValue("@sl", take);
                    update.Parameters.AddWithValue("@lot", rowLot);
                    if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("STOCK_CHANGED");
                    need -= take;
                }
                if (need != 0) throw new InvalidOperationException("STOCK_CHANGED");

                // 4) SlotLot pick (mirror of BulkStockAdjustService.TruKhoAoTheoLot, manageTransaction:false)
                if (!o.SkipSlotLot) await PickSlotLotAsync(c, tx, f, lot, qty);
            }

            // 5) ledgers + TMP guard + scan rows (mirror, same order as the source)
            await ExecAsync(c, tx, $"INSERT INTO [{f.LuuDoc}](MAHANGFCC, STT, KETQUA) SELECT MAHANGFCC, STT, KETQUA FROM [{f.DocQr}] WHERE MAHANGFCC=@m AND KETQUA='DG'", ("@m", maHang));
            await ExecAsync(c, tx, $"INSERT INTO [{f.LuuPhieu}](STT, MAHANG, SOLUONG, STATUS) SELECT STT, MAHANG, SOLUONG, 'OK' FROM [{f.Tmp}] WHERE STT=@s AND STATUS='NG'", ("@s", stt));
            var updated = await ExecAsync(c, tx, $"UPDATE [{f.Tmp}] SET STATUS='OK' WHERE STT=@s AND STATUS='NG'", ("@s", stt));
            if (updated != 1) throw new InvalidOperationException("TMP_NOT_PENDING");
            await ExecAsync(c, tx, $"DELETE FROM [{f.DocQr}] WHERE MAHANGFCC=@m AND KETQUA='DG'", ("@m", maHang));

            if (o.FailBeforeCommit) throw new InvalidOperationException("INJECTED");
            await tx.CommitAsync();
            return new Outcome(Result.Committed);
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(); } catch { /* transaction already doomed */ }
            return new Outcome(Result.Failed, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static async Task PickSlotLotAsync(SqlConnection c, SqlTransaction tx, Fx f, string lot, int qty)
    {
        var match = LotCodeHelper.BuildLotMatchSql("LotNo", "@l");
        var rows = new List<(int Id, int Qty)>();
        await using (var cmd = new SqlCommand($@"SELECT SlotLotId, Quantity FROM [{f.Slot}] WITH (UPDLOCK, ROWLOCK)
WHERE PhieuStatus = 0 AND Quantity > 0 AND {match}
ORDER BY CASE WHEN SlotId = 1 THEN 0 ELSE 1 END, ImportDate, SlotLotId", c, tx) { CommandTimeout = 30 })
        {
            cmd.Parameters.AddWithValue("@l", lot);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add((reader.GetInt32(0), reader.GetInt32(1)));
        }

        var need = qty;
        foreach (var (id, q) in rows)
        {
            if (need <= 0) break;
            var take = Math.Min(q, need);
            await ExecAsync(c, tx, $"UPDATE [{f.Slot}] SET Quantity = Quantity - @t WHERE SlotLotId = @id", ("@t", take), ("@id", id));
            need -= take;
        }
        if (need > 0) throw new InvalidOperationException("SLOTLOT_SHORT");
    }

    // ------------------------------------------------------------------ race helper

    private static async Task<(Outcome A, Outcome B)> LockedRaceAsync(string cs, Fx f, ProdSql sql, int sttA, int sttB, string lotSl, string mhA, string mhB)
    {
        var aLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = Task.Run(() => DeliverAsync(cs, f, sql, sttA, lotSl, mhA, new Opt
        {
            AfterLockRead = async () => { aLocked.TrySetResult(); await Task.Delay(500); }
        }));
        await aLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var b = Task.Run(() => DeliverAsync(cs, f, sql, sttB, lotSl, mhB, new Opt()));
        return (await a, await b);
    }

    private static Task MeetAsync(Barrier barrier) => Task.Run(() =>
    {
        if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20)))
            throw new TimeoutException("Second session never reached the barrier; the control race did not start.");
    });

    // ------------------------------------------------------------------ production SQL extraction

    private static ProdSql LoadProductionSql()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PhieuKhoRepository.source.cs");
        Require(File.Exists(path), "Linked PhieuKhoRepository.source.cs is missing from the harness output.");
        var source = File.ReadAllText(path);
        var timeout = TimeSpan.FromSeconds(5);

        string Pick(string pattern, string what)
        {
            var m = Regex.Match(source, pattern, RegexOptions.None, timeout);
            Require(m.Success, $"Could not extract the production {what} from PhieuKhoRepository.cs; the YMVN design changed, update this scenario.");
            Require(!m.NextMatch().Success, $"The production {what} now appears more than once; update this scenario.");
            return m.Groups["sql"].Value;
        }

        var result = new ProdSql
        {
            LockRead = Pick(@"\$""(?<sql>SELECT ISNULL\(SUM\(SLCONLAI\),0\) FROM STOCKTP[^""]*)""", "YMVN STOCKTP lock-read"),
            FifoSelect = Pick(@"\$""(?<sql>SELECT LOT, SLCONLAI FROM STOCKTP[^""]*)""", "TruStockTpFifo select"),
            FifoUpdate = Pick(@"(?<![\$@])""(?<sql>UPDATE STOCKTP SET SLXUAT[^""]*)""", "TruStockTpFifo update")
        };

        Require(result.LockRead.Contains("WITH (UPDLOCK, HOLDLOCK)") && result.LockRead.Contains("{matchCondition}"),
            "Production lock-read must keep WITH (UPDLOCK, HOLDLOCK) and the LOT match condition.");
        Require(result.FifoSelect.Contains("WITH (UPDLOCK, HOLDLOCK)") && result.FifoSelect.Contains("{matchCondition}")
            && result.FifoSelect.Contains("ORDER BY NGAYNHAP ASC, LOT ASC") && result.FifoSelect.Contains("SLCONLAI > 0"),
            "Production FIFO select must keep the lock hints, the match condition, SLCONLAI > 0 and ORDER BY NGAYNHAP ASC, LOT ASC.");
        Require(result.FifoUpdate.Contains("SLCONLAI >= @sl") && result.FifoUpdate.Contains("SLCONLAI = SLCONLAI - @sl"),
            "Production FIFO update must keep the SLCONLAI >= @sl guard.");
        return result;
    }

    /// <summary>Binds the production text to the fixture: STOCKTP -> fixture table, {matchCondition} -> predicate, optional stripping for controls.</summary>
    private static string Shape(string template, Fx f, string match, bool stripLocks, bool stripGuard)
    {
        var text = template.Replace("{matchCondition}", match);
        text = Regex.Replace(text, @"\bSTOCKTP\b", "[" + f.Stock + "]", RegexOptions.None, TimeSpan.FromSeconds(5));
        if (stripLocks)
        {
            var stripped = text.Replace("WITH (UPDLOCK, HOLDLOCK)", "");
            Require(stripped != text, "Control could not strip the lock hints; the production text changed.");
            text = stripped;
        }
        if (stripGuard)
        {
            var stripped = text.Replace("AND SLCONLAI >= @sl", "");
            Require(stripped != text, "Control could not strip the quantity guard; the production text changed.");
            text = stripped;
        }
        return text;
    }

    // ------------------------------------------------------------------ fixtures and plumbing

    private static Task SeedStockAsync(SqlConnection o, Fx f, string lot, int qty, DateTime nhap) =>
        ExecAsync(o, null, $"INSERT INTO [{f.Stock}](LOT, SLNHAP, SLXUAT, SLCONLAI, NGAYNHAP) VALUES(@lot, @q, 0, @q, @d)", ("@lot", lot), ("@q", qty), ("@d", nhap));

    private static Task SeedSlotAsync(SqlConnection o, Fx f, int slotId, string lot, int qty, DateTime imported) =>
        ExecAsync(o, null, $"INSERT INTO [{f.Slot}](SlotId, LotNo, Quantity, PhieuStatus, ImportDate) VALUES(@s, @lot, @q, 0, @d)", ("@s", slotId), ("@lot", lot), ("@q", qty), ("@d", imported));

    private static async Task<int> SeedTmpAsync(SqlConnection o, Fx f, string maHang)
    {
        var stt = NextStt();
        await ExecAsync(o, null, $"INSERT INTO [{f.Tmp}](STT, MAHANG, LOT, SOLUONG, STATUS) VALUES(@s, @m, '', 0, 'NG')", ("@s", stt), ("@m", maHang));
        return stt;
    }

    private static Task SeedDocQrAsync(SqlConnection o, Fx f, string maHang, int stt, string ketQua) =>
        ExecAsync(o, null, $"INSERT INTO [{f.DocQr}](MAHANGFCC, STT, KETQUA) VALUES(@m, @s, @k)", ("@m", maHang), ("@s", stt), ("@k", ketQua));

    private static async Task<int> StockAsync(SqlConnection o, Fx f, string lot) =>
        await ScalarIntAsync(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT = '{lot}'");

    /// <summary>STOCKTP == active SlotLot for one LOT key, using the production LOT match predicate on both sides.</summary>
    private static async Task<bool> ReconciledAsync(SqlConnection o, Fx f, string lotKey)
    {
        var literal = "'" + lotKey + "'";
        var stock = await ScalarIntAsync(o, $"SELECT ISNULL(SUM(SLCONLAI),0) FROM [{f.Stock}] WHERE {LotCodeHelper.BuildLotMatchSql("LOT", literal)}");
        var slot = await ScalarIntAsync(o, $"SELECT ISNULL(SUM(Quantity),0) FROM [{f.Slot}] WHERE PhieuStatus = 0 AND {LotCodeHelper.BuildLotMatchSql("LotNo", literal)}");
        return stock == slot;
    }

    private static async Task<SqlConnection> OpenAsync(string cs)
    {
        var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<int> ExecAsync(SqlConnection c, SqlTransaction? tx, string text, params (string Name, object Value)[] args)
    {
        await using var cmd = new SqlCommand(text, c, tx) { CommandTimeout = 30 };
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqlConnection c, SqlTransaction? tx, string text)
    {
        await using var cmd = new SqlCommand(text, c, tx) { CommandTimeout = 30 };
        var value = await cmd.ExecuteScalarAsync();
        return value == DBNull.Value ? null : value;
    }

    private static async Task<int> ScalarIntAsync(SqlConnection c, string text) =>
        Convert.ToInt32(await ScalarAsync(c, null, text) ?? 0);

    private static Task<int> CountAsync(SqlConnection c, string text) => ScalarIntAsync(c, text);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
