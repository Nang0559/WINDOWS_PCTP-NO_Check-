using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

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
    }

    private sealed class ProductionSql
    {
        public string ExportLock = "";
        public string DeliveryLock = "";
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
        var f = new Fixture { Ticket = prefix + "Ticket", Xuat = prefix + "Xuat", Giao = prefix + "Giao" };

        await using var owner = await OpenAsync(connectionString);
        try
        {
            await ExecAsync(owner, null, $@"
CREATE TABLE [{f.Ticket}](Id int NOT NULL PRIMARY KEY, Status int NOT NULL, QuotaRework int NOT NULL, UpdatedAt datetime NULL, UpdatedBy nvarchar(50) NULL);
CREATE TABLE [{f.Xuat}](Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, PhieuId int NOT NULL, LotXuat nvarchar(50) NOT NULL, SoLuongXuat int NOT NULL, OperationId uniqueidentifier NULL);
CREATE UNIQUE INDEX UX_QC_Op ON [{f.Xuat}](OperationId) WHERE OperationId IS NOT NULL;
CREATE TABLE [{f.Giao}](Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, PhieuId int NOT NULL, LotGiao nvarchar(50) NOT NULL, SoLuongGiao int NOT NULL);");

            await DeliveryLockedRaceAsync(connectionString, owner, f, sql);
            await DeliveryUnlockedControlAsync(connectionString, owner, f);
            await DeliveryLockTimeoutFailsClosedAsync(connectionString, owner, f, sql);
            await DeliveryRollbackInjectionAsync(connectionString, owner, f, sql);
            await ExportLockedQuotaRaceAsync(connectionString, owner, f, sql);
            await ExportUnlockedControlAsync(connectionString, owner, f);
            await ExportRowLockAloneAsync(connectionString, owner, f, sql);
            await ExportSameOperationIdAsync(connectionString, owner, f, sql);
            await ExportUniqueIndexBackstopAsync(connectionString, owner, f);

            Console.WriteLine("PASS: Rework two-session lock protocol (B0-02/B0-03) verified on SQL Server with production lock SQL text; protocol-level evidence only, not the C# methods end to end.");
        }
        finally
        {
            foreach (var table in new[] { f.Giao, f.Xuat, f.Ticket })
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

        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in lockBlock.Matches(qt))
        {
            var pm = prefixRegex.Match(qt, m.Index + m.Length);
            Require(pm.Success, "Could not find the lock resource prefix after a production sp_getapplock block.");
            found[pm.Groups["p"].Value] = m.Groups["sql"].Value.Replace("\"\"", "\"");
        }

        // Both Rework operations intentionally share one ticket-scoped application lock.
        // Require two production acquisition sites, but only one distinct resource prefix.
        var resourceAssignments = Regex.Matches(qt, """Value\s*=\s*"(?<p>QT_REWORK_[A-Z_]+:)"\s*\+""", RegexOptions.None, timeout);
        Require(resourceAssignments.Count >= 2,
            "QTChungService.cs must expose application-lock resource assignments for both export and delivery.");
        Require(found.Count == 1 && found.ContainsKey("QT_REWORK_TICKET:"),
            "Rework export and delivery must share the single QT_REWORK_TICKET: application-lock resource.");

        foreach (var pair in found)
        {
            Require(pair.Value.Contains("@LockMode = 'Exclusive'") && pair.Value.Contains("@LockOwner = 'Transaction'"),
                $"{pair.Key} lock must be Exclusive and Transaction-owned.");
            Require(pair.Value.Contains("@LockTimeout = 15000") && pair.Value.Contains("THROW 51000"),
                $"{pair.Key} lock must declare a 15000 ms timeout and fail closed with THROW 51000.");
        }

        var rowLock = new Regex("""string sql = \$@"(?<sql>(?:[^"]|"")*?SET \{statusColumnName\} = @NewStatus(?:[^"]|"")*)"\s*;""", RegexOptions.None, timeout);
        var rm = rowLock.Match(baseSource);
        Require(rm.Success, "Could not extract the production UpdateStatusIfCurrentIs SQL from SqlRepositoryBase.cs.");

        return new ProductionSql
        {
            ExportLock = found["QT_REWORK_TICKET:"],
            DeliveryLock = found["QT_REWORK_TICKET:"],
            ExportPrefix = "QT_REWORK_TICKET:",
            DeliveryPrefix = "QT_REWORK_TICKET:",
            RowLockTemplate = rm.Groups["sql"].Value.Replace("\"\"", "\"")
        };
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
