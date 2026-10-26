using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Exercises the production SqlRepositoryBase.InTransaction (the scope used by the
/// F1-F6 atomicity fixes) against a uniquely named tempdb global fixture.
/// No dbo business table is created, read or changed.
///
/// Covered: own transaction commits; own transaction rolls back when the second of two
/// statements fails (real SQL error) or when the work throws; inside a caller's
/// transaction the scope neither commits nor rolls back, so the caller decides.
/// NOT covered: the repositories fixed for findings F1-F6 are not executed here - see
/// docs/TRANSACTION_BOUNDARIES.md.
/// </summary>
internal static class InTransactionScenario
{
    public static async Task RunAsync(string connectionString)
    {
        var legacy = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
        var table = "##PCTP_QC_INTX_" + Guid.NewGuid().ToString("N");

        using var owner = new SqlConnection(legacy);
        await owner.OpenAsync();

        try
        {
            await ExecAsync(owner, $"CREATE TABLE [{table}](Id int NOT NULL PRIMARY KEY);");

            var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = legacy };
            using var uow = new UnitOfWork(provider);
            var probe = new SqlRepositoryTransactionProbe(new PhieuSqlExecutor(), uow);

            string Insert(int id) => $"INSERT INTO [{table}](Id) VALUES({id});";

            // 1. No caller transaction: scope opens, commits and closes its own.
            Require(!uow.IsInTransaction, "precondition: no ambient transaction.");
            int affected = probe.RunFunc(() => probe.ExecuteUpdate(Insert(1)));
            Require(affected == 1, "own-transaction INSERT must affect one row.");
            Require(!uow.IsInTransaction, "own transaction must be closed after commit.");
            Require(await CountAsync(owner, table) == 1, "committed row must be visible to another session.");

            // 2. Own transaction, second statement fails (duplicate key): first must be rolled back.
            ExpectFailure<SqlException>(() => probe.RunAction(() =>
            {
                probe.ExecuteUpdate(Insert(2));
                probe.ExecuteUpdate(Insert(2));
            }));
            Require(!uow.IsInTransaction, "own transaction must be closed after rollback.");
            Require(await CountAsync(owner, table) == 1, "failed two-statement scope must leave no partial row.");

            // 3. Own transaction, work throws after a successful write.
            ExpectFailure<InvalidOperationException>(() => probe.RunAction(() =>
            {
                probe.ExecuteUpdate(Insert(3));
                throw new InvalidOperationException("QC-forced failure");
            }));
            Require(await CountAsync(owner, table) == 1, "exception after a write must roll the write back.");

            // 4. Caller transaction: scope joins it, does not commit; caller rollback removes the write.
            uow.Begin();
            probe.RunAction(() => probe.ExecuteUpdate(Insert(4)));
            Require(uow.IsInTransaction, "inner scope must not close the caller's transaction.");
            Require(CountInTransaction(uow, table) == 2, "caller must see the inner write before deciding.");
            uow.Rollback();
            Require(!uow.IsInTransaction, "caller rollback must close the transaction.");
            Require(await CountAsync(owner, table) == 1, "caller rollback must remove the inner scope's write.");

            // 5. Caller transaction with two scopes, caller commits.
            uow.Begin();
            probe.RunAction(() => probe.ExecuteUpdate(Insert(5)));
            probe.RunAction(() => probe.ExecuteUpdate(Insert(6)));
            uow.Commit();
            Require(await CountAsync(owner, table) == 3, "caller commit must persist both scopes.");

            // 6. Inner failure inside a caller transaction: the scope rethrows and leaves the decision to the caller.
            uow.Begin();
            ExpectFailure<SqlException>(() => probe.RunAction(() =>
            {
                probe.ExecuteUpdate(Insert(7));
                probe.ExecuteUpdate(Insert(7));
            }));
            Require(uow.IsInTransaction, "inner failure must not roll back the caller's transaction by itself.");
            uow.Rollback();
            Require(await CountAsync(owner, table) == 3, "caller rollback after inner failure must restore the committed state.");

            Console.WriteLine("PASS: SqlRepositoryBase.InTransaction own-commit, own-rollback (SQL error and exception), join-caller commit/rollback verified in tempdb.");
        }
        finally
        {
            try { await ExecAsync(owner, $"IF OBJECT_ID(N'tempdb..[{table}]') IS NOT NULL DROP TABLE [{table}];"); }
            catch { /* the global temp table is dropped when the owner connection closes */ }
        }
    }

    private static async Task ExecAsync(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 5;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(SqlConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 5;
        command.CommandText = $"SELECT COUNT(*) FROM [{table}];";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static int CountInTransaction(UnitOfWork uow, string table)
    {
        using var command = uow.Connection.CreateCommand();
        command.Transaction = uow.Transaction;
        command.CommandTimeout = 5;
        command.CommandText = $"SELECT COUNT(*) FROM [{table}];";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void ExpectFailure<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} but no exception was thrown.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("InTransaction scenario failed: " + message);
    }
}
