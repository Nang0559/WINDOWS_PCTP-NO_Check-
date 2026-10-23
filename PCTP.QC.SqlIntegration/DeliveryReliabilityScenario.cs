using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// SQL-engine reliability tests on uniquely named global temporary tables in tempdb.
/// These scenarios prove SQL transaction/idempotency behavior;
/// they do not claim to invoke the WinForms customer-delivery orchestration.
/// </summary>
internal static class DeliveryReliabilityScenario
{
    public static async Task RunAsync(string connectionString)
    {
        var table = "##PCTP_QC_REL_" + Guid.NewGuid().ToString("N");
        await CreateAsync(connectionString, table);
        try
        {
            await VerifyIdempotentDeliveryAndInsufficientStockAsync(connectionString, table);
            await VerifyAtomicFailureRollbackAsync(connectionString, table);
            await VerifyDeadlockVictimRollbackAsync(connectionString, table);
            await VerifyCancellationRollbackAsync(connectionString, table);
            Console.WriteLine("PASS: delivery reliability scenarios (idempotent replay, insufficient stock, injected rollback, deadlock victim rollback, cancellation rollback).");
        }
        finally
        {
            await DropAsync(connectionString, table);
        }
    }

    private static async Task CreateAsync(string cs, string table)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new SqlCommand($@"
BEGIN TRANSACTION;
BEGIN TRY
    CREATE TABLE {table}
    (
        Id int NOT NULL PRIMARY KEY,
        Qty int NOT NULL,
        DeliveryKey nvarchar(80) NULL,
        HistoryCount int NOT NULL DEFAULT(0),
        PendingClosed bit NOT NULL DEFAULT(0)
    );
    CREATE UNIQUE INDEX UX_QC_REL_DeliveryKey ON {table}(DeliveryKey) WHERE DeliveryKey IS NOT NULL;
    INSERT INTO {table}(Id, Qty) VALUES (1, 10), (2, 10), (3, 10), (4, 50);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;", connection)
        { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task VerifyIdempotentDeliveryAndInsufficientStockAsync(string cs, string table)
    {
        const string deliveryKey = "QC-DELIVERY-REPLAY-001";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await using var command = new SqlCommand($@"
IF NOT EXISTS (SELECT 1 FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE DeliveryKey=@key)
BEGIN
    UPDATE {table} SET Qty=Qty-6 WHERE Id=1 AND Qty>=6;
    IF @@ROWCOUNT=1
        INSERT INTO {table}(Id, Qty, DeliveryKey, HistoryCount, PendingClosed)
        VALUES (100, 0, @key, 1, 1);
END;", connection, transaction) { CommandTimeout = 5 };
                command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 80) { Value = deliveryKey });
                await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }

        var qty = await ScalarIntAsync(cs, $"SELECT Qty FROM {table} WHERE Id=1");
        var history = await ScalarIntAsync(cs, $"SELECT HistoryCount FROM {table} WHERE DeliveryKey=N'{deliveryKey}'");
        var pending = await ScalarIntAsync(cs, $"SELECT CONVERT(int, PendingClosed) FROM {table} WHERE DeliveryKey=N'{deliveryKey}'");
        Require(qty == 4 && history == 1 && pending == 1,
            $"Replay was not idempotent. Qty={qty}, history={history}, pendingClosed={pending}.");

        // A second delivery asks for more than the remaining balance. The guarded
        // decrement must affect zero rows and must not create a delivery/history row.
        await using (var connection = new SqlConnection(cs))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await using var command = new SqlCommand($@"
UPDATE {table} SET Qty=Qty-5 WHERE Id=1 AND Qty>=5;
IF @@ROWCOUNT=1
    INSERT INTO {table}(Id, Qty, DeliveryKey, HistoryCount, PendingClosed)
    VALUES (101, 0, N'QC-DELIVERY-INSUFFICIENT', 1, 1);", connection, transaction);
            var affected = await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            Require(affected == 0, "Insufficient stock unexpectedly mutated the delivery fixture.");
        }

        Require(await ScalarIntAsync(cs, $"SELECT Qty FROM {table} WHERE Id=1") == 4,
            "Insufficient-stock attempt changed the remaining quantity.");
        Require(await ScalarIntAsync(cs, $"SELECT COUNT(1) FROM {table} WHERE DeliveryKey=N'QC-DELIVERY-INSUFFICIENT'") == 0,
            "Insufficient-stock attempt created a delivery/history record.");
        Console.WriteLine("PASS: delivery replay is idempotent and insufficient stock creates no partial delivery state.");
    }

    private static async Task VerifyAtomicFailureRollbackAsync(string cs, string table)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await using (var writes = new SqlCommand($@"
UPDATE {table} SET Qty=Qty-3 WHERE Id=2 AND Qty>=3;
UPDATE {table} SET HistoryCount=HistoryCount+1, PendingClosed=1 WHERE Id=2;", connection, transaction))
                await writes.ExecuteNonQueryAsync();

            await using var fail = new SqlCommand("THROW 51031, 'Injected failure after delivery and finalization writes.', 1;", connection, transaction);
            await fail.ExecuteNonQueryAsync();
            throw new InvalidOperationException("Injected SQL failure did not occur.");
        }
        catch (SqlException ex) when (ex.Number == 51031)
        {
            await transaction.RollbackAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }

        var row = await ReadStateAsync(cs, table, 2);
        Require(row.Qty == 10 && row.HistoryCount == 0 && !row.PendingClosed,
            $"Atomic rollback failed. Qty={row.Qty}, history={row.HistoryCount}, pending={row.PendingClosed}.");
        Console.WriteLine("PASS: injected post-write failure rolled back quantity, history and pending state together.");
    }

    private static async Task VerifyDeadlockVictimRollbackAsync(string cs, string table)
    {
        using var ready = new CountdownEvent(2);
        using var release = new ManualResetEventSlim(false);

        async Task<bool> Worker(int firstId, int secondId)
        {
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                await UpdateQtyAsync(connection, transaction, table, firstId);
                ready.Signal();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Deadlock workers did not reach the lock barrier.");
                await UpdateQtyAsync(connection, transaction, table, secondId);
                await transaction.CommitAsync();
                return true;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                try { await transaction.RollbackAsync(); } catch { }
                return false;
            }
            catch
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }

        var left = Task.Run(() => Worker(2, 3));
        var right = Task.Run(() => Worker(3, 2));
        if (!ready.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Both SQL deadlock sessions did not acquire their first row lock.");
        release.Set();

        var winners = await Task.WhenAll(left, right);
        Require(winners.Count(x => x) == 1,
            "Expected exactly one deadlock victim and one committed SQL transaction.");
        var qty2 = await ScalarIntAsync(cs, $"SELECT Qty FROM {table} WHERE Id=2");
        var qty3 = await ScalarIntAsync(cs, $"SELECT Qty FROM {table} WHERE Id=3");
        Require(qty2 == 9 && qty3 == 9,
            $"Deadlock victim did not roll back both writes. Row2={qty2}, row3={qty3}.");
        Console.WriteLine("PASS: deterministic two-session deadlock selected one victim; victim transaction was rolled back.");
    }

    private static async Task VerifyCancellationRollbackAsync(string cs, string table)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var cancelled = false;
        try
        {
            await using (var update = new SqlCommand($"UPDATE {table} SET Qty=Qty-5 WHERE Id=4 AND Qty>=5;", connection, transaction))
                await update.ExecuteNonQueryAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using var wait = new SqlCommand("WAITFOR DELAY '00:00:15';", connection, transaction)
            { CommandTimeout = 20 };
            try
            {
                await wait.ExecuteNonQueryAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                cancelled = true;
            }

            if (!cancelled)
                throw new InvalidOperationException("SQL command ignored the cancellation token.");
            await transaction.RollbackAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }

        Require(await ScalarIntAsync(cs, $"SELECT Qty FROM {table} WHERE Id=4") == 50,
            "Cancellation did not roll back the write performed before WAITFOR.");
        Console.WriteLine("PASS: SQL cancellation interrupted a running command and rolled back the active transaction.");
    }

    private static async Task UpdateQtyAsync(SqlConnection connection, SqlTransaction transaction, string table, int id)
    {
        await using var command = new SqlCommand($"UPDATE {table} SET Qty=Qty-1 WHERE Id=@id;", connection, transaction)
        { CommandTimeout = 10 };
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarIntAsync(string cs, string sql)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<(int Qty, int HistoryCount, bool PendingClosed)> ReadStateAsync(string cs, string table, int id)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"SELECT Qty, HistoryCount, PendingClosed FROM {table} WHERE Id=@id;", connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException($"Reliability fixture row {id} is missing.");
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetBoolean(2));
    }

    private static async Task DropAsync(string cs, string table)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"DROP TABLE {table};", connection) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
