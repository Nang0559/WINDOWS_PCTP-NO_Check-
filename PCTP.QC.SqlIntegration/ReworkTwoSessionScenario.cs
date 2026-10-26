using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Services;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Two-session SQL Server acceptance for the Rework export (B0-02) and Rework delivery (B0-03)
/// locking protocol.
///
/// What is exercised: the exact application-lock SQL text and the exact row-lock UPDATE text
/// that production uses, extracted at run time from the production source files
/// (QTChungService.cs, SqlRepositoryBase.cs), run by two independent connections against
/// uniquely named tempdb global fixtures. No dbo business table is touched.
///
/// What is NOT exercised: the C# methods QTChungService.GiaoHangRework / ReworkStockService.XuatKhoRework
/// themselves (they need the full WinForms dependency graph). The ledger read/check/insert steps
/// below mirror the documented production order; source-contract tests keep that order honest.
/// Each race has a negative control (same steps, no lock) that MUST oversell, which proves the
/// test would detect a missing lock.
/// </summary>
internal static class ReworkTwoSessionScenario
{
    private enum Outcome { Committed, Rejected, AlreadyProcessed }

    private sealed class Fixture
    {
        public string Ticket = "";
        public string Xuat = "";
        public string Giao = "";
        public string Stock = "";
    }

    private sealed class ProductionSql
    {
        public string ExportLock = "";
        public string DeliveryLock = "";
        public string QcFinalLock = "";
        public string QcFinalPrefix = "";
        public string ExportPrefix = "";
        public string DeliveryPrefix = "";
        public string RowLockTemplate = "";
    }

    private sealed class Options
    {
        public string? AppLockSql;
        public string AppLockResource = "";
        public string? RowLockSql;
        public Action? OnLocked;
        public Func<Task>? AfterRead;
        public bool FailAfterFirstInsert;
        public Guid? OperationId;
    }

    public static async Task RunAsync(string connectionString)
    {
        var sql = LoadProductionSql();
        var prefix = "##PCTP_QC_" + Guid.NewGuid().ToString("N") + "_";
        var f = new Fixture { Ticket = prefix + "Ticket", Xuat = prefix + "Xuat", Giao = prefix + "Giao", Stock = prefix + "Stock" };

        await using var owner = await OpenAsync(connectionString);
        try
        {
            await ExecAsync(owner, null, $@"
CREATE TABLE [{f.Ticket}](Id int NOT NULL PRIMARY KEY, Status int NOT NULL, QuotaRework int NOT NULL, UpdatedAt datetime NULL, UpdatedBy nvarchar(50) NULL);
CREATE TABLE [{f.Xuat}](Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, PhieuId int NOT NULL, LotXuat nvarchar(50) NOT NULL, SoLuongXuat int NOT NULL, OperationId uniqueidentifier NULL);
CREATE UNIQUE INDEX UX_QC_Op ON [{f.Xuat}](OperationId) WHERE OperationId IS NOT NULL;
CREATE TABLE [{f.Giao}](Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, PhieuId int NOT NULL, LotGiao nvarchar(50) NOT NULL, SoLuongGiao int NOT NULL);
CREATE TABLE [{f.Stock}](Lot nvarchar(50) NOT NULL PRIMARY KEY, Qty int NOT NULL);");

            await DeliveryLockedRaceAsync(connectionString, owner, f, sql);
            await DeliveryUnlockedControlAsync(connectionString, owner, f);
            await DeliveryLockTimeoutFailsClosedAsync(connectionString, owner, f, sql);
            await DeliveryRollbackInjectionAsync(connectionString, owner, f, sql);
            await ExportLockedQuotaRaceAsync(connectionString, owner, f, sql);
            await ExportUnlockedControlAsync(connectionString, owner, f);
            await ExportRowLockAloneAsync(connectionString, owner, f, sql);
            await ExportSameOperationIdAsync(connectionString, owner, f, sql);
            await ExportUniqueIndexBackstopAsync(connectionString, owner, f);
            await CrossPathSerializationAsync(connectionString, owner, f, sql);
            await PolicyRacesAsync(connectionString, owner, f, sql);

            Console.WriteLine("PASS: Rework two-session lock protocol (B0-02/B0-03) verified on SQL Server with production lock SQL text; protocol-level evidence only, not the C# methods end to end.");
        }
        finally
        {
            foreach (var table in new[] { f.Stock, f.Giao, f.Xuat, f.Ticket })
            {
                await ExecAsync(owner, null, $"IF OBJECT_ID(N'tempdb..{table}') IS NOT NULL DROP TABLE [{table}];");
            }
        }
    }

    // ---------------------------------------------------------------- B0-03 delivery

    private static async Task DeliveryLockedRaceAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 100);
        await SeedExportAsync(owner, f, ticket, "LOT-D1", 10);

        var aLocked = new TaskCompletionSource();
        var a = Task.Run(() => DeliverAsync(cs, f, ticket, new[] { ("LOT-D1", 10) }, new Options
        {
            AppLockSql = sql.DeliveryLock,
            AppLockResource = sql.DeliveryPrefix + ticket,
            OnLocked = () => aLocked.TrySetResult(),
            AfterRead = () => Task.Delay(400)
        }));
        await aLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var b = Task.Run(() => DeliverAsync(cs, f, ticket, new[] { ("LOT-D1", 10) }, new Options
        {
            AppLockSql = sql.DeliveryLock,
            AppLockResource = sql.DeliveryPrefix + ticket
        }));

        var ra = await a;
        var rb = await b;
        var total = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId={ticket}");
        Require(ra == Outcome.Committed && rb == Outcome.Rejected && total == 10,
            $"D1 delivery race: expected A=Committed, B=Rejected, delivered=10; got A={ra}, B={rb}, delivered={total}.");
        Console.WriteLine("PASS: D1 two sessions delivering the same remaining 10 under the production delivery lock: one committed, one rejected, delivered=10.");
    }

    private static async Task DeliveryUnlockedControlAsync(string cs, SqlConnection owner, Fixture f)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 100);
        await SeedExportAsync(owner, f, ticket, "LOT-D2", 10);

        using var barrier = new Barrier(2);
        Func<Task> meet = () => MeetAsync(barrier);
        var a = Task.Run(() => DeliverAsync(cs, f, ticket, new[] { ("LOT-D2", 10) }, new Options { AfterRead = meet }));
        var b = Task.Run(() => DeliverAsync(cs, f, ticket, new[] { ("LOT-D2", 10) }, new Options { AfterRead = meet }));
        await Task.WhenAll(a, b);

        var total = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId={ticket}");
        Require(total == 20,
            $"D2 negative control must oversell (delivered 20 of 10) to prove the race is detectable; got delivered={total}.");
        Console.WriteLine("PASS: D2 negative control without lock oversold (delivered=20 of 10), so D1 is a meaningful test.");
    }

    private static async Task DeliveryLockTimeoutFailsClosedAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 100);
        await SeedExportAsync(owner, f, ticket, "LOT-D3", 10);

        var shortTimeoutSql = sql.DeliveryLock.Replace("@LockTimeout = 15000", "@LockTimeout = 500");
        Require(!string.Equals(shortTimeoutSql, sql.DeliveryLock, StringComparison.Ordinal),
            "D3 expected the production delivery lock to declare '@LockTimeout = 15000'; source changed, update the scenario.");

        var aLocked = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var a = Task.Run(() => DeliverAsync(cs, f, ticket, new[] { ("LOT-D3", 4) }, new Options
        {
            AppLockSql = sql.DeliveryLock,
            AppLockResource = sql.DeliveryPrefix + ticket,
            OnLocked = () => aLocked.TrySetResult(),
            AfterRead = () => release.Task
        }));
        await aLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));

        SqlException? failure = null;
        try
        {
            await DeliverAsync(cs, f, ticket, new[] { ("LOT-D3", 4) }, new Options
            {
                AppLockSql = shortTimeoutSql,
                AppLockResource = sql.DeliveryPrefix + ticket
            });
        }
        catch (SqlException ex)
        {
            failure = ex;
        }
        release.SetResult();
        var ra = await a;

        var total = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId={ticket}");
        Require(failure != null && failure.Number == 51000,
            "D3 expected the lock-timeout session to fail closed with THROW 51000.");
        Require(ra == Outcome.Committed && total == 4,
            $"D3 expected only the lock holder to write (delivered=4); got A={ra}, delivered={total}.");
        Console.WriteLine("PASS: D3 a session that cannot get the delivery lock fails closed (51000) and writes nothing.");
    }

    private static async Task DeliveryRollbackInjectionAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 100);
        await SeedExportAsync(owner, f, ticket, "LOT-D4A", 4);
        await SeedExportAsync(owner, f, ticket, "LOT-D4B", 4);

        var threw = false;
        try
        {
            await DeliverAsync(cs, f, ticket, new[] { ("LOT-D4A", 4), ("LOT-D4B", 4) }, new Options
            {
                AppLockSql = sql.DeliveryLock,
                AppLockResource = sql.DeliveryPrefix + ticket,
                FailAfterFirstInsert = true
            });
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        var rows = await SumAsync(owner, $"SELECT COUNT(*) FROM [{f.Giao}] WHERE PhieuId={ticket}");
        Require(threw && rows == 0,
            $"D4 expected rollback of the first ledger insert after an injected failure; threw={threw}, ledger rows={rows}.");

        var shortTimeoutSql = sql.DeliveryLock.Replace("@LockTimeout = 15000", "@LockTimeout = 1000");
        var again = await DeliverAsync(cs, f, ticket, new[] { ("LOT-D4A", 4) }, new Options
        {
            AppLockSql = shortTimeoutSql,
            AppLockResource = sql.DeliveryPrefix + ticket
        });
        Require(again == Outcome.Committed, "D4 expected the lock to be released after rollback so a retry can proceed.");
        Console.WriteLine("PASS: D4 failure after the first ledger insert rolled back all rows and released the lock; retry committed.");
    }

    // ---------------------------------------------------------------- B0-02 export

    private static async Task ExportLockedQuotaRaceAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 10);
        var rowLock = RowLockSql(sql, f);

        var aLocked = new TaskCompletionSource();
        var a = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E1", 6, new Options
        {
            AppLockSql = sql.ExportLock,
            AppLockResource = sql.ExportPrefix + ticket,
            RowLockSql = rowLock,
            OperationId = Guid.NewGuid(),
            OnLocked = () => aLocked.TrySetResult(),
            AfterRead = () => Task.Delay(400)
        }));
        await aLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var b = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E1", 6, new Options
        {
            AppLockSql = sql.ExportLock,
            AppLockResource = sql.ExportPrefix + ticket,
            RowLockSql = rowLock,
            OperationId = Guid.NewGuid()
        }));

        var ra = await a;
        var rb = await b;
        var total = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId={ticket}");
        Require(ra == Outcome.Committed && rb == Outcome.Rejected && total == 6,
            $"E1 expected A=Committed, B=Rejected, exported=6 of quota 10; got A={ra}, B={rb}, exported={total}.");
        Console.WriteLine("PASS: E1 two sessions exporting 6 against quota 10 under the production export lock: one committed, one rejected, exported=6.");
    }

    private static async Task ExportUnlockedControlAsync(string cs, SqlConnection owner, Fixture f)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 10);

        using var barrier = new Barrier(2);
        Func<Task> meet = () => MeetAsync(barrier);
        var a = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E2", 6, new Options { AfterRead = meet, OperationId = Guid.NewGuid() }));
        var b = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E2", 6, new Options { AfterRead = meet, OperationId = Guid.NewGuid() }));
        await Task.WhenAll(a, b);

        var total = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId={ticket}");
        Require(total == 12,
            $"E2 negative control must exceed the quota (exported 12 of 10); got exported={total}.");
        Console.WriteLine("PASS: E2 negative control without locks exceeded the quota (exported=12 of 10), so E1 is a meaningful test.");
    }

    private static async Task ExportRowLockAloneAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 10);
        var rowLock = RowLockSql(sql, f);

        var aReady = new TaskCompletionSource();
        var a = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E5", 6, new Options
        {
            RowLockSql = rowLock,
            OperationId = Guid.NewGuid(),
            AfterRead = async () => { aReady.TrySetResult(); await Task.Delay(400); }
        }));
        await aReady.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var b = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E5", 6, new Options
        {
            RowLockSql = rowLock,
            OperationId = Guid.NewGuid()
        }));

        var ra = await a;
        var rb = await b;
        var total = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId={ticket}");
        Require(ra == Outcome.Committed && rb == Outcome.Rejected && total == 6,
            $"E5 row lock alone expected A=Committed, B=Rejected, exported=6; got A={ra}, B={rb}, exported={total}.");
        Console.WriteLine("PASS: E5 the ticket row lock alone (second line of defense) also prevents exceeding the quota.");
    }

    private static async Task ExportSameOperationIdAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 100);
        var op = Guid.NewGuid();
        var rowLock = RowLockSql(sql, f);

        var aLocked = new TaskCompletionSource();
        var a = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E3", 6, new Options
        {
            AppLockSql = sql.ExportLock,
            AppLockResource = sql.ExportPrefix + ticket,
            RowLockSql = rowLock,
            OperationId = op,
            OnLocked = () => aLocked.TrySetResult(),
            AfterRead = () => Task.Delay(400)
        }));
        await aLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var b = Task.Run(() => ExportAsync(cs, f, ticket, "LOT-E3", 6, new Options
        {
            AppLockSql = sql.ExportLock,
            AppLockResource = sql.ExportPrefix + ticket,
            RowLockSql = rowLock,
            OperationId = op
        }));

        var ra = await a;
        var rb = await b;
        var rows = await SumAsync(owner, $"SELECT COUNT(*) FROM [{f.Xuat}] WHERE PhieuId={ticket} AND OperationId='{op}'");
        Require(ra == Outcome.Committed && rb == Outcome.AlreadyProcessed && rows == 1,
            $"E3 expected A=Committed, B=AlreadyProcessed, one ledger row; got A={ra}, B={rb}, rows={rows}.");
        Console.WriteLine("PASS: E3 the same OperationId retried concurrently mutates once; the second session sees it as already processed.");
    }

    private static async Task ExportUniqueIndexBackstopAsync(string cs, SqlConnection owner, Fixture f)
    {
        var ticket = NewTicket();
        await SeedTicketAsync(owner, f, ticket, quota: 100);
        var op = Guid.NewGuid();

        using var barrier = new Barrier(2);
        Func<Task> meet = () => MeetAsync(barrier);
        var a = CaptureAsync(() => ExportAsync(cs, f, ticket, "LOT-E4", 6, new Options { OperationId = op, AfterRead = meet }));
        var b = CaptureAsync(() => ExportAsync(cs, f, ticket, "LOT-E4", 6, new Options { OperationId = op, AfterRead = meet }));
        var results = await Task.WhenAll(a, b);

        var committed = results.Count(r => r.Outcome == Outcome.Committed);
        var uniqueViolations = results.Count(r => r.Error != null && (r.Error.Number == 2601 || r.Error.Number == 2627));
        var rows = await SumAsync(owner, $"SELECT COUNT(*) FROM [{f.Xuat}] WHERE PhieuId={ticket} AND OperationId='{op}'");
        Require(committed == 1 && uniqueViolations == 1 && rows == 1,
            $"E4 expected exactly one commit and one unique-index violation; committed={committed}, violations={uniqueViolations}, rows={rows}.");
        Console.WriteLine("PASS: E4 without locks the unique filtered OperationId index still blocks a duplicate ledger row (last line of defense).");
    }

    // ---------------------------------------------------------------- protocol mirrors

    // ---------------------------------------------------------------- F1/F2 export <-> delivery on one ticket

    private static async Task CrossPathSerializationAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        var shortExport = sql.ExportLock.Replace("@LockTimeout = 15000", "@LockTimeout = 500");
        var shortDelivery = sql.DeliveryLock.Replace("@LockTimeout = 15000", "@LockTimeout = 500");
        Require(!string.Equals(shortExport, sql.ExportLock, StringComparison.Ordinal) &&
                !string.Equals(shortDelivery, sql.DeliveryLock, StringComparison.Ordinal),
            "F1/F2 expected both production locks to declare '@LockTimeout = 15000'; source changed, update the scenario.");

        // F1: export holds the ticket lock, delivery on the same ticket must fail closed.
        var ticket1 = NewTicket();
        await SeedTicketAsync(owner, f, ticket1, quota: 100);
        await SeedExportAsync(owner, f, ticket1, "LOT-F1", 10);
        var exportLocked = new TaskCompletionSource();
        var releaseExport = new TaskCompletionSource();
        var exporter = Task.Run(() => ExportAsync(cs, f, ticket1, "LOT-F1", 5, new Options
        {
            AppLockSql = sql.ExportLock,
            AppLockResource = sql.ExportPrefix + ticket1,
            OnLocked = () => exportLocked.TrySetResult(),
            AfterRead = () => releaseExport.Task,
            OperationId = Guid.NewGuid()
        }));
        await exportLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));
        SqlException? deliveryFailure = null;
        try
        {
            await DeliverAsync(cs, f, ticket1, new[] { ("LOT-F1", 4) }, new Options
            {
                AppLockSql = shortDelivery,
                AppLockResource = sql.DeliveryPrefix + ticket1
            });
        }
        catch (SqlException ex) { deliveryFailure = ex; }
        releaseExport.SetResult();
        var exportOutcome = await exporter;
        var delivered1 = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId={ticket1}");
        Require(deliveryFailure != null && deliveryFailure.Number == 51000 && exportOutcome == Outcome.Committed && delivered1 == 0,
            $"F1 expected delivery to fail closed (51000) while export holds the ticket lock; got failure={(deliveryFailure == null ? "none" : deliveryFailure.Number.ToString())}, export={exportOutcome}, delivered={delivered1}.");
        Console.WriteLine("PASS: F1 delivery cannot enter while an export holds the shared ticket lock.");

        // F2: delivery holds the ticket lock, export on the same ticket must fail closed.
        var ticket2 = NewTicket();
        await SeedTicketAsync(owner, f, ticket2, quota: 100);
        await SeedExportAsync(owner, f, ticket2, "LOT-F2", 10);
        var deliveryLocked = new TaskCompletionSource();
        var releaseDelivery = new TaskCompletionSource();
        var deliverer = Task.Run(() => DeliverAsync(cs, f, ticket2, new[] { ("LOT-F2", 4) }, new Options
        {
            AppLockSql = sql.DeliveryLock,
            AppLockResource = sql.DeliveryPrefix + ticket2,
            OnLocked = () => deliveryLocked.TrySetResult(),
            AfterRead = () => releaseDelivery.Task
        }));
        await deliveryLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));
        SqlException? exportFailure = null;
        try
        {
            await ExportAsync(cs, f, ticket2, "LOT-F2", 5, new Options
            {
                AppLockSql = shortExport,
                AppLockResource = sql.ExportPrefix + ticket2,
                OperationId = Guid.NewGuid()
            });
        }
        catch (SqlException ex) { exportFailure = ex; }
        releaseDelivery.SetResult();
        var deliveryOutcome = await deliverer;
        var exported2 = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId={ticket2}");
        Require(exportFailure != null && exportFailure.Number == 51000 && deliveryOutcome == Outcome.Committed && exported2 == 10,
            $"F2 expected export to fail closed (51000) while delivery holds the ticket lock; got failure={(exportFailure == null ? "none" : exportFailure.Number.ToString())}, delivery={deliveryOutcome}, exported={exported2}.");
        Console.WriteLine("PASS: F2 export cannot enter while a delivery holds the shared ticket lock.");
    }

    // ---------------------------------------------------------------- R1..R5 policy races (A / X / D)

    private const int StockStart = 100;

    private static Options Locked(ProductionSql sql, bool isExport, int ticket) => new Options
    {
        AppLockSql = isExport ? sql.ExportLock : sql.DeliveryLock,
        AppLockResource = (isExport ? sql.ExportPrefix : sql.DeliveryPrefix) + ticket
    };

    private static async Task PolicyRacesAsync(string cs, SqlConnection owner, Fixture f, ProductionSql sql)
    {
        // R1: two sessions deliver 6 against X=6 -> total delivered must not exceed exported.
        var t1 = NewTicket();
        await SeedPolicyAsync(owner, f, t1, QTChungStatus.DaXuatKhoRework, approved: 10, exported: 6, delivered: 0, lot: "LOT-R1");
        var r1 = await Task.WhenAll(
            Task.Run(() => PolicyMoveAsync(cs, f, t1, false, "LOT-R1", 6, Locked(sql, false, t1))),
            Task.Run(() => PolicyMoveAsync(cs, f, t1, false, "LOT-R1", 6, Locked(sql, false, t1))));
        Require(r1.Count(x => x == Outcome.Committed) == 1 && r1.Count(x => x == Outcome.Rejected) == 1,
            $"R1 expected one committed and one rejected delivery; got {string.Join(",", r1)}.");
        await AssertStateAsync(owner, f, t1, "LOT-R1", expectedX: 6, expectedD: 6, expectedStatus: QTChungStatus.DaXuatKhoRework, "R1");
        Console.WriteLine("PASS: R1 two deliveries of 6 against exported 6 -> delivered 6 (D<=X), status unchanged.");

        // R1 negative control: same race without the lock must oversell, otherwise R1 proves nothing.
        var t1c = NewTicket();
        await SeedPolicyAsync(owner, f, t1c, QTChungStatus.DaXuatKhoRework, approved: 10, exported: 6, delivered: 0, lot: "LOT-R1C");
        using (var barrier = new Barrier(2))
        {
            Func<Task> meet = () => MeetAsync(barrier);
            await Task.WhenAll(
                Task.Run(() => PolicyMoveAsync(cs, f, t1c, false, "LOT-R1C", 6, new Options { AfterRead = meet })),
                Task.Run(() => PolicyMoveAsync(cs, f, t1c, false, "LOT-R1C", 6, new Options { AfterRead = meet })));
        }
        var oversold = await SumAsync(owner, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId={t1c}");
        Require(oversold == 12, $"R1 negative control must oversell (delivered 12 of exported 6); got {oversold}.");
        Console.WriteLine("PASS: R1 negative control without lock oversold (delivered=12 of exported 6).");

        // R2: deliver 6 + two exports of 4 with X=6, A=10 -> exactly one export fits, D<=X<=A.
        var t2 = NewTicket();
        await SeedPolicyAsync(owner, f, t2, QTChungStatus.DaXuatKhoRework, approved: 10, exported: 6, delivered: 0, lot: "LOT-R2");
        var r2 = await Task.WhenAll(
            Task.Run(() => PolicyMoveAsync(cs, f, t2, false, "LOT-R2", 6, Locked(sql, false, t2))),
            Task.Run(() => PolicyMoveAsync(cs, f, t2, true, "LOT-R2", 4, WithOp(Locked(sql, true, t2)))),
            Task.Run(() => PolicyMoveAsync(cs, f, t2, true, "LOT-R2", 4, WithOp(Locked(sql, true, t2)))));
        Require(r2[0] == Outcome.Committed && r2.Skip(1).Count(x => x == Outcome.Committed) == 1 && r2.Skip(1).Count(x => x == Outcome.Rejected) == 1,
            $"R2 expected delivery committed and exactly one of two exports committed; got {string.Join(",", r2)}.");
        await AssertStateAsync(owner, f, t2, "LOT-R2", expectedX: 10, expectedD: 6, expectedStatus: QTChungStatus.DaXuatKhoRework, "R2");
        Console.WriteLine("PASS: R2 delivery and competing exports keep D<=X<=A (X=10, D=6).");

        // R3: export holds the lock while a delivery waits; the delivery must re-read X after the lock.
        var t3 = NewTicket();
        await SeedPolicyAsync(owner, f, t3, QTChungStatus.DaXuatKhoRework, approved: 10, exported: 6, delivered: 6, lot: "LOT-R3");
        var exportLocked = new TaskCompletionSource();
        var releaseExport = new TaskCompletionSource();
        var exportOptions = WithOp(Locked(sql, true, t3));
        exportOptions.OnLocked = () => exportLocked.TrySetResult();
        exportOptions.AfterRead = () => releaseExport.Task;
        var a3 = Task.Run(() => PolicyMoveAsync(cs, f, t3, true, "LOT-R3", 4, exportOptions));
        await exportLocked.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var b3 = Task.Run(() => PolicyMoveAsync(cs, f, t3, false, "LOT-R3", 4, Locked(sql, false, t3)));
        await Task.Delay(500);
        releaseExport.SetResult();
        var ra3 = await a3;
        var rb3 = await b3;
        Require(ra3 == Outcome.Committed && rb3 == Outcome.Committed,
            $"R3 expected export then delivery to both commit (delivery re-reads X=10 after the lock); got export={ra3}, delivery={rb3}.");
        await AssertStateAsync(owner, f, t3, "LOT-R3", expectedX: 10, expectedD: 10, expectedStatus: QTChungStatus.DaGiaoSanXuat, "R3");
        Console.WriteLine("PASS: R3 delivery waited for the ticket lock and used the post-lock export total (X=10, D=10, completed).");

        // R4: two sessions try to deliver the last 4 -> only one completes the ticket.
        var t4 = NewTicket();
        await SeedPolicyAsync(owner, f, t4, QTChungStatus.DaXuatKhoRework, approved: 10, exported: 10, delivered: 6, lot: "LOT-R4");
        var r4 = await Task.WhenAll(
            Task.Run(() => PolicyMoveAsync(cs, f, t4, false, "LOT-R4", 4, Locked(sql, false, t4))),
            Task.Run(() => PolicyMoveAsync(cs, f, t4, false, "LOT-R4", 4, Locked(sql, false, t4))));
        Require(r4.Count(x => x == Outcome.Committed) == 1 && r4.Count(x => x == Outcome.Rejected) == 1,
            $"R4 expected exactly one final delivery to commit; got {string.Join(",", r4)}.");
        await AssertStateAsync(owner, f, t4, "LOT-R4", expectedX: 10, expectedD: 10, expectedStatus: QTChungStatus.DaGiaoSanXuat, "R4");
        Console.WriteLine("PASS: R4 only one session completes the ticket (D=10, status DaGiaoSanXuat).");

        // R5: retry with the same OperationId racing a different export; no duplicate row, quota respected.
        var t5 = NewTicket();
        await SeedPolicyAsync(owner, f, t5, QTChungStatus.DaDinhHuong, approved: 10, exported: 0, delivered: 0, lot: "LOT-R5");
        var op = Guid.NewGuid();
        var o1 = Locked(sql, true, t5); o1.OperationId = op;
        var o2 = Locked(sql, true, t5); o2.OperationId = op;
        var o3 = Locked(sql, true, t5); o3.OperationId = Guid.NewGuid();
        var r5 = await Task.WhenAll(
            Task.Run(() => PolicyMoveAsync(cs, f, t5, true, "LOT-R5", 6, o1)),
            Task.Run(() => PolicyMoveAsync(cs, f, t5, true, "LOT-R5", 6, o2)),
            Task.Run(() => PolicyMoveAsync(cs, f, t5, true, "LOT-R5", 6, o3)));
        var rowsForOp = await SumAsync(owner, $"SELECT COUNT(*) FROM [{f.Xuat}] WHERE PhieuId={t5} AND OperationId='{op}'");
        var rows = await SumAsync(owner, $"SELECT COUNT(*) FROM [{f.Xuat}] WHERE PhieuId={t5}");
        Require(rows == 1 && rowsForOp <= 1 && r5.Count(x => x == Outcome.Committed) == 1,
            $"R5 expected exactly one committed export row and no duplicate OperationId row; got rows={rows}, rowsForOp={rowsForOp}, outcomes={string.Join(",", r5)}.");
        await AssertStateAsync(owner, f, t5, "LOT-R5", expectedX: 6, expectedD: 0, expectedStatus: QTChungStatus.DaXuatKhoRework, "R5");
        Console.WriteLine("PASS: R5 retry of the same OperationId plus a competing export wrote one row and stayed within quota.");
    }

    private static Options WithOp(Options o)
    {
        o.OperationId = Guid.NewGuid();
        return o;
    }

    private static async Task SeedPolicyAsync(SqlConnection c, Fixture f, int ticket, QTChungStatus status, int approved, int exported, int delivered, string lot)
    {
        await ExecAsync(c, null, $"INSERT INTO [{f.Ticket}](Id,Status,QuotaRework) VALUES(@id,@s,@a)",
            P("@id", ticket), P("@s", (int)status), P("@a", approved));
        if (exported > 0)
            await ExecAsync(c, null, $"INSERT INTO [{f.Xuat}](PhieuId,LotXuat,SoLuongXuat,OperationId) VALUES(@id,@lot,@q,NULL)",
                P("@id", ticket), P("@lot", lot), P("@q", exported));
        if (delivered > 0)
            await ExecAsync(c, null, $"INSERT INTO [{f.Giao}](PhieuId,LotGiao,SoLuongGiao) VALUES(@id,@lot,@q)",
                P("@id", ticket), P("@lot", lot), P("@q", delivered));
        await ExecAsync(c, null, $"INSERT INTO [{f.Stock}](Lot,Qty) VALUES(@lot,@q)",
            P("@lot", lot), P("@q", StockStart - exported));
    }

    private static async Task AssertStateAsync(SqlConnection c, Fixture f, int ticket, string lot, int expectedX, int expectedD, QTChungStatus expectedStatus, string name)
    {
        var x = await SumAsync(c, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId={ticket}");
        var d = await SumAsync(c, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId={ticket}");
        var a = await SumAsync(c, $"SELECT QuotaRework FROM [{f.Ticket}] WHERE Id={ticket}");
        var status = await SumAsync(c, $"SELECT Status FROM [{f.Ticket}] WHERE Id={ticket}");
        var stock = await SumAsync(c, $"SELECT Qty FROM [{f.Stock}] WHERE Lot='{lot}'");
        Require(d <= x && x <= a, $"{name} invariant D<=X<=A violated: A={a}, X={x}, D={d}.");
        Require(x == expectedX && d == expectedD, $"{name} expected X={expectedX}, D={expectedD}; got X={x}, D={d}.");
        Require(status == (int)expectedStatus, $"{name} expected status {expectedStatus}({(int)expectedStatus}); got {status}.");
        Require(stock == StockStart - x, $"{name} stock must equal {StockStart} - exported ({StockStart - x}); got {stock}.");
    }

    /// <summary>
    /// Mirrors the production step order (lock -> OperationId -> read status/A/X/D -> policy -> write)
    /// but evaluates the REAL ReworkQuantityPolicy linked from production source.
    /// </summary>
    private static async Task<Outcome> PolicyMoveAsync(string cs, Fixture f, int ticket, bool isExport, string lot, int qty, Options o)
    {
        await using var c = await OpenAsync(cs);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
        try
        {
            if (o.AppLockSql != null)
            {
                await AcquireLockAsync(c, tx, o.AppLockSql, o.AppLockResource);
                o.OnLocked?.Invoke();
            }

            if (isExport && o.OperationId.HasValue)
            {
                var previous = await SumAsync(c, tx, $"SELECT COUNT(*) FROM [{f.Xuat}] WHERE OperationId=@op", P("@op", o.OperationId.Value));
                if (previous > 0)
                {
                    await tx.RollbackAsync();
                    return Outcome.AlreadyProcessed;
                }
            }

            var status = await SumAsync(c, tx, $"SELECT Status FROM [{f.Ticket}] WHERE Id=@id", P("@id", ticket));
            var approved = await SumAsync(c, tx, $"SELECT QuotaRework FROM [{f.Ticket}] WHERE Id=@id", P("@id", ticket));
            var exported = await SumAsync(c, tx, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId=@id", P("@id", ticket));
            var delivered = await SumAsync(c, tx, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId=@id", P("@id", ticket));

            if (o.AfterRead != null) await o.AfterRead();

            var decision = isExport
                ? ReworkQuantityPolicy.EvaluateExport((QTChungStatus)status, true, approved, exported, delivered, qty)
                : ReworkQuantityPolicy.EvaluateDelivery((QTChungStatus)status, true, approved, exported, delivered, qty);
            if (!decision.Allowed)
            {
                await tx.RollbackAsync();
                return Outcome.Rejected;
            }

            if (isExport)
            {
                await ExecAsync(c, tx, $"INSERT INTO [{f.Xuat}](PhieuId,LotXuat,SoLuongXuat,OperationId) VALUES(@id,@lot,@q,@op)",
                    P("@id", ticket), P("@lot", lot), P("@q", qty),
                    new SqlParameter("@op", System.Data.SqlDbType.UniqueIdentifier) { Value = o.OperationId.HasValue ? (object)o.OperationId.Value : DBNull.Value });
                var moved = await ExecAsync(c, tx, $"UPDATE [{f.Stock}] SET Qty=Qty-@q WHERE Lot=@lot AND Qty>=@q", P("@q", qty), P("@lot", lot));
                if (moved != 1) throw new InvalidOperationException("Stock row missing or insufficient for the export.");
                if ((QTChungStatus)status == QTChungStatus.DaDinhHuong)
                    await ExecAsync(c, tx, $"UPDATE [{f.Ticket}] SET Status=@s WHERE Id=@id", P("@s", (int)QTChungStatus.DaXuatKhoRework), P("@id", ticket));
            }
            else
            {
                await ExecAsync(c, tx, $"INSERT INTO [{f.Giao}](PhieuId,LotGiao,SoLuongGiao) VALUES(@id,@lot,@q)", P("@id", ticket), P("@lot", lot), P("@q", qty));
                if (decision.CompletesDelivery)
                    await ExecAsync(c, tx, $"UPDATE [{f.Ticket}] SET Status=@s WHERE Id=@id", P("@s", (int)QTChungStatus.DaGiaoSanXuat), P("@id", ticket));
            }

            await tx.CommitAsync();
            return Outcome.Committed;
        }
        catch
        {
            await SafeRollbackAsync(tx);
            throw;
        }
    }

    private static async Task<Outcome> DeliverAsync(string cs, Fixture f, int ticket, (string Lot, int Qty)[] lines, Options o)
    {
        await using var c = await OpenAsync(cs);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
        try
        {
            if (o.AppLockSql != null)
            {
                await AcquireLockAsync(c, tx, o.AppLockSql, o.AppLockResource);
                o.OnLocked?.Invoke();
            }

            foreach (var line in lines)
            {
                var exported = await SumAsync(c, tx, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId=@id AND LotXuat=@lot", P("@id", ticket), P("@lot", line.Lot));
                var delivered = await SumAsync(c, tx, $"SELECT ISNULL(SUM(SoLuongGiao),0) FROM [{f.Giao}] WHERE PhieuId=@id AND LotGiao=@lot", P("@id", ticket), P("@lot", line.Lot));
                if (delivered + line.Qty > exported)
                {
                    await tx.RollbackAsync();
                    return Outcome.Rejected;
                }
            }

            if (o.AfterRead != null) await o.AfterRead();

            var first = true;
            foreach (var line in lines)
            {
                await ExecAsync(c, tx, $"INSERT INTO [{f.Giao}](PhieuId,LotGiao,SoLuongGiao) VALUES(@id,@lot,@qty)", P("@id", ticket), P("@lot", line.Lot), P("@qty", line.Qty));
                if (first && o.FailAfterFirstInsert)
                    throw new InvalidOperationException("QC injected failure after the first delivery ledger insert.");
                first = false;
            }

            await tx.CommitAsync();
            return Outcome.Committed;
        }
        catch
        {
            await SafeRollbackAsync(tx);
            throw;
        }
    }

    private static async Task<Outcome> ExportAsync(string cs, Fixture f, int ticket, string lot, int qty, Options o)
    {
        await using var c = await OpenAsync(cs);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
        try
        {
            if (o.AppLockSql != null)
            {
                await AcquireLockAsync(c, tx, o.AppLockSql, o.AppLockResource);
                o.OnLocked?.Invoke();
            }

            if (o.OperationId.HasValue)
            {
                var previous = await SumAsync(c, tx, $"SELECT COUNT(*) FROM [{f.Xuat}] WHERE OperationId=@op", P("@op", o.OperationId.Value));
                if (previous > 0)
                {
                    await tx.RollbackAsync();
                    return Outcome.AlreadyProcessed;
                }
            }

            var status = await SumAsync(c, tx, $"SELECT Status FROM [{f.Ticket}] WHERE Id=@id", P("@id", ticket));
            if (o.RowLockSql != null)
            {
                var affected = await ExecAsync(c, tx, o.RowLockSql,
                    P("@Id", ticket), P("@NewStatus", status), P("@ExpectedFrom", status), P("@UpdatedBy", "qc"));
                if (affected < 1)
                {
                    await tx.RollbackAsync();
                    return Outcome.Rejected;
                }
            }

            var quota = await SumAsync(c, tx, $"SELECT QuotaRework FROM [{f.Ticket}] WHERE Id=@id", P("@id", ticket));
            var exported = await SumAsync(c, tx, $"SELECT ISNULL(SUM(SoLuongXuat),0) FROM [{f.Xuat}] WHERE PhieuId=@id", P("@id", ticket));

            if (o.AfterRead != null) await o.AfterRead();

            if (exported + qty > quota)
            {
                await tx.RollbackAsync();
                return Outcome.Rejected;
            }

            await ExecAsync(c, tx, $"INSERT INTO [{f.Xuat}](PhieuId,LotXuat,SoLuongXuat,OperationId) VALUES(@id,@lot,@qty,@op)",
                P("@id", ticket), P("@lot", lot), P("@qty", qty),
                new SqlParameter("@op", System.Data.SqlDbType.UniqueIdentifier) { Value = o.OperationId.HasValue ? (object)o.OperationId.Value : DBNull.Value });

            await tx.CommitAsync();
            return Outcome.Committed;
        }
        catch
        {
            await SafeRollbackAsync(tx);
            throw;
        }
    }

    // ---------------------------------------------------------------- production SQL extraction

    private static ProductionSql LoadProductionSql()
    {
        var qt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "QTChungService.source.cs"));
        var baseSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SqlRepositoryBase.source.cs"));
        var timeout = TimeSpan.FromSeconds(5);

        var lockBlock = new Regex("""CommandText\s*=\s*@"(?<sql>(?:[^"]|"")*?sp_getapplock(?:[^"]|"")*)"\s*;""", RegexOptions.None, timeout);
        var prefixRegex = new Regex("""Value\s*=\s*"(?<p>QT_REWORK_[A-Z_]+:)"\s*\+""", RegexOptions.None, timeout);
        var resourceReturn = new Regex("""return\s*"(?<p>QT_REWORK_[A-Z_]+:)"\s*\+""", RegexOptions.None, timeout);

        // Extract per business path. Never take "the first sp_getapplock block in the file":
        // QCXacNhanCuoi reaches the same lock through AcquireReworkTicketLock(), which sits
        // between the other two sites and would otherwise be mistaken for them.
        var exportBody = MethodBody(qt,
            "public ScanResult XuatKhoRework(int phieuXuLyId, int slotId, string lotNo, int soLuong, string nguoiXuat, Guid operationId)",
            "public ScanResult GiaoHangRework(");
        var deliveryBody = MethodBody(qt, "public ScanResult GiaoHangRework(", "public void GhiNhanDangRework(");
        var qcBody = MethodBody(qt, "public ScanResult QCXacNhanCuoi(", "public void GhiNhanKiemTraTem(");
        var helperBody = MethodBody(qt, "private void AcquireReworkTicketLock(", "public ScanResult QCXacNhanCuoi(");
        var resourceBody = MethodBody(qt, "private static string TicketLockResource(", "private void AcquireReworkTicketLock(");

        var (exportSql, exportPrefix) = InlineLock(exportBody, "XuatKhoRework", lockBlock, prefixRegex);
        var (deliverySql, deliveryPrefix) = InlineLock(deliveryBody, "GiaoHangRework", lockBlock, prefixRegex);

        Require(qcBody.Contains("AcquireReworkTicketLock(", StringComparison.Ordinal),
            "QCXacNhanCuoi must take the ticket lock through AcquireReworkTicketLock().");
        Require(lockBlock.Matches(qcBody).Count == 0,
            "QCXacNhanCuoi must not carry its own sp_getapplock block; it shares AcquireReworkTicketLock().");
        var helperLocks = lockBlock.Matches(helperBody);
        Require(helperLocks.Count == 1, "AcquireReworkTicketLock must contain exactly one sp_getapplock block.");
        Require(helperBody.Contains("TicketLockResource(", StringComparison.Ordinal),
            "AcquireReworkTicketLock must derive its resource from TicketLockResource().");
        var rm0 = resourceReturn.Match(resourceBody);
        Require(rm0.Success, "Could not find the QT_REWORK_ resource prefix returned by TicketLockResource().");
        var qcSql = helperLocks[0].Groups["sql"].Value.Replace("\"\"", "\"");
        var qcPrefix = rm0.Groups["p"].Value;

        // All three business paths must serialize on ONE ticket-scoped application lock.
        Require(exportPrefix == "QT_REWORK_TICKET:" && deliveryPrefix == exportPrefix && qcPrefix == exportPrefix,
            $"Export ({exportPrefix}), delivery ({deliveryPrefix}) and final QC ({qcPrefix}) must share the single QT_REWORK_TICKET: resource.");

        foreach (var (name, lockSql) in new[] { ("export", exportSql), ("delivery", deliverySql), ("final QC", qcSql) })
        {
            Require(lockSql.Contains("@LockMode = 'Exclusive'") && lockSql.Contains("@LockOwner = 'Transaction'"),
                $"{name} lock must be Exclusive and Transaction-owned.");
            Require(lockSql.Contains("@LockTimeout = 15000") && lockSql.Contains("THROW 51000"),
                $"{name} lock must declare a 15000 ms timeout and fail closed with THROW 51000.");
        }

        var rowLock = new Regex("""string sql = \$@"(?<sql>(?:[^"]|"")*?SET \{statusColumnName\} = @NewStatus(?:[^"]|"")*)"\s*;""", RegexOptions.None, timeout);
        var rm = rowLock.Match(baseSource);
        Require(rm.Success, "Could not extract the production UpdateStatusIfCurrentIs SQL from SqlRepositoryBase.cs.");

        return new ProductionSql
        {
            ExportLock = exportSql,
            DeliveryLock = deliverySql,
            QcFinalLock = qcSql,
            ExportPrefix = exportPrefix,
            DeliveryPrefix = deliveryPrefix,
            QcFinalPrefix = qcPrefix,
            RowLockTemplate = rm.Groups["sql"].Value.Replace("\"\"", "\"")
        };
    }

    private static string MethodBody(string source, string startToken, string endToken)
    {
        var start = source.IndexOf(startToken, StringComparison.Ordinal);
        Require(start >= 0, $"Could not find '{startToken}' in QTChungService.cs.");
        var end = source.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
        Require(end > start, $"Could not find the end boundary '{endToken}' after '{startToken}'.");
        return source.Substring(start, end - start);
    }

    private static (string Sql, string Prefix) InlineLock(string body, string method, Regex lockBlock, Regex prefixRegex)
    {
        var locks = lockBlock.Matches(body);
        Require(locks.Count == 1, $"{method} must contain exactly one sp_getapplock block (found {locks.Count}).");
        var prefixes = prefixRegex.Matches(body);
        Require(prefixes.Count == 1, $"{method} must assign exactly one QT_REWORK_ lock resource (found {prefixes.Count}).");
        Require(prefixes[0].Index > locks[0].Index, $"{method} must assign the lock resource after its sp_getapplock command text.");
        return (locks[0].Groups["sql"].Value.Replace("\"\"", "\""), prefixes[0].Groups["p"].Value);
    }

    private static string RowLockSql(ProductionSql sql, Fixture f) =>
        sql.RowLockTemplate
            .Replace("{tableName}", "[" + f.Ticket + "]")
            .Replace("{statusColumnName}", "Status")
            .Replace("{idColumnName}", "Id");

    // ---------------------------------------------------------------- plumbing

    private static async Task<(Outcome? Outcome, SqlException? Error)> CaptureAsync(Func<Task<Outcome>> action)
    {
        try
        {
            return (await action(), null);
        }
        catch (SqlException ex)
        {
            return (null, ex);
        }
    }

    private static Task MeetAsync(Barrier barrier)
    {
        if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20)))
            throw new TimeoutException("Both sessions did not reach the race point in time.");
        return Task.CompletedTask;
    }

    private static int NewTicket() => Random.Shared.Next(1_000_000, int.MaxValue);

    private static async Task SeedTicketAsync(SqlConnection c, Fixture f, int ticket, int quota) =>
        await ExecAsync(c, null, $"INSERT INTO [{f.Ticket}](Id,Status,QuotaRework) VALUES(@id,1,@q)", P("@id", ticket), P("@q", quota));

    private static async Task SeedExportAsync(SqlConnection c, Fixture f, int ticket, string lot, int qty) =>
        await ExecAsync(c, null, $"INSERT INTO [{f.Xuat}](PhieuId,LotXuat,SoLuongXuat) VALUES(@id,@lot,@qty)", P("@id", ticket), P("@lot", lot), P("@qty", qty));

    private static async Task AcquireLockAsync(SqlConnection c, SqlTransaction tx, string lockSql, string resource)
    {
        await using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandTimeout = 60;
        command.CommandText = lockSql;
        command.Parameters.Add(new SqlParameter("@Resource", resource));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<SqlConnection> OpenAsync(string cs)
    {
        var c = new SqlConnection(cs);
        await c.OpenAsync();
        return c;
    }

    private static SqlParameter P(string name, object value) => new SqlParameter(name, value);

    private static async Task<int> ExecAsync(SqlConnection c, SqlTransaction? tx, string sql, params SqlParameter[] parameters)
    {
        await using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandTimeout = 60;
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> SumAsync(SqlConnection c, SqlTransaction? tx, string sql, params SqlParameter[] parameters)
    {
        await using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandTimeout = 60;
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }

    private static Task<int> SumAsync(SqlConnection c, string sql) => SumAsync(c, null, sql);

    private static async Task SafeRollbackAsync(SqlTransaction tx)
    {
        try
        {
            if (tx.Connection != null) await tx.RollbackAsync();
        }
        catch
        {
            // The original failure is the one worth reporting.
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
