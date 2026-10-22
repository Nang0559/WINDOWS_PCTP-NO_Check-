using System.Data;
using Microsoft.Data.SqlClient;
using PCTP.QC.Tests;

internal static class Program
{
    private static async Task<int> Main()
    {
        var connectionString = Environment.GetEnvironmentVariable("PCTP_QC_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("BLOCKED: Set PCTP_QC_SQL_CONNECTION to an explicitly local TEST_* database.");
            return 2;
        }

        try
        {
            TestEnvironmentPolicy.EnsureSafeSqlTestTarget(connectionString);
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = Math.Min(new SqlConnectionStringBuilder(connectionString).ConnectTimeout, 5),
                ApplicationName = "PCTP-QC-SQL-Integration"
            };
            connectionString = builder.ConnectionString;

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var table = "dbo.[__PCTP_QC_" + Guid.NewGuid().ToString("N") + "]";
            await CreateFixtureAsync(connection, table);

            try
            {
                await RunRollbackAtomicityAsync(connection, table);
                await RunConcurrentConditionalDecrementAsync(connectionString, table);
                await RunCommandTimeoutRollbackAsync(connection, table);
                Console.WriteLine("PASS: 3 SQL Server integration scenarios completed.");
                return 0;
            }
            finally
            {
                await DropFixtureAsync(connection, table);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static async Task CreateFixtureAsync(SqlConnection connection, string table)
    {
        var sql = $@"CREATE TABLE {table}
(
    Id int NOT NULL PRIMARY KEY,
    Lot varchar(40) NOT NULL,
    StockQty int NOT NULL,
    SlotQty int NOT NULL
);
INSERT INTO {table}(Id, Lot, StockQty, SlotQty) VALUES (1, 'QC-LOT-ROLLBACK', 100, 100), (2, 'QC-LOT-CONCURRENT', 10, 10), (3, 'QC-LOT-TIMEOUT', 50, 50);";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS: isolated SQL fixture created.");
    }

    private static async Task RunRollbackAtomicityAsync(SqlConnection connection, string table)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await ExecuteAsync(connection, transaction,
                $"UPDATE {table} SET StockQty = StockQty - 10 WHERE Id = 1; UPDATE {table} SET SlotQty = SlotQty - 10 WHERE Id = 1;");
            // Deliberately fail after both writes to verify rollback of the complete unit.
            await ExecuteAsync(connection, transaction, "THROW 51000, 'Injected failure after stock writes for rollback verification.', 1;");
            await transaction.CommitAsync();
            throw new InvalidOperationException("Expected the deliberate SQL failure was not raised.");
        }
        catch (SqlException)
        {
            await transaction.RollbackAsync();
        }

        var row = await ReadRowAsync(connection, table, 1);
        if (row.StockQty != 100 || row.SlotQty != 100)
            throw new InvalidOperationException($"Rollback was not atomic. Stock={row.StockQty}, Slot={row.SlotQty}.");
        Console.WriteLine("PASS: transaction rollback reverted both stock and slot mutations.");
    }

    private static async Task RunConcurrentConditionalDecrementAsync(string connectionString, string table)
    {
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        async Task<int> TryDecreaseAsync()
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            ready.Signal();
            start.Wait(TimeSpan.FromSeconds(10));
            try
            {
                await using var command = new SqlCommand(
                    $"UPDATE {table} SET StockQty = StockQty - 7 WHERE Id = 2 AND StockQty >= 7;",
                    connection, transaction) { CommandTimeout = 5 };
                var changed = await command.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
                return changed;
            }
            catch
            {
                try { await transaction.RollbackAsync(); } catch { }
                throw;
            }
        }

        var first = Task.Run(TryDecreaseAsync);
        var second = Task.Run(TryDecreaseAsync);
        if (!ready.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Both SQL sessions did not reach the concurrency barrier.");
        start.Set();
        var affected = await Task.WhenAll(first, second);
        await using var verifyConnection = await OpenAsync(connectionString);
        var row = await ReadRowAsync(verifyConnection, table, 2);
        if (affected.Sum() != 1 || row.StockQty != 3)
            throw new InvalidOperationException(
                $"Conditional decrement was not serialized. Rows={string.Join(",", affected)}, final stock={row.StockQty}.");
        Console.WriteLine("PASS: two concurrent conditional decrements allowed exactly one winner.");
    }

    private static async Task RunCommandTimeoutRollbackAsync(SqlConnection connection, string table)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await ExecuteAsync(connection, transaction, $"UPDATE {table} SET StockQty = StockQty - 5 WHERE Id = 3;");
            await using var command = new SqlCommand("WAITFOR DELAY '00:00:04';", connection, transaction)
            {
                CommandTimeout = 1
            };
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            throw new InvalidOperationException("Expected SQL command timeout did not occur.");
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            await transaction.RollbackAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }

        var row = await ReadRowAsync(connection, table, 3);
        if (row.StockQty != 50)
            throw new InvalidOperationException($"Timeout rollback failed. Final stock={row.StockQty}.");
        Console.WriteLine("PASS: command timeout was bounded and its transaction rolled back.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(int StockQty, int SlotQty)> ReadRowAsync(SqlConnection connection, string table, int id)
    {
        await using var command = new SqlCommand($"SELECT StockQty, SlotQty FROM {table} WHERE Id = @id;", connection);
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException($"Fixture row {id} is missing.");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<SqlConnection> OpenAsync(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task DropFixtureAsync(SqlConnection connection, string table)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = new SqlCommand($"IF OBJECT_ID(N'{table.Replace("[", "").Replace("]", "")}', N'U') IS NOT NULL DROP TABLE {table};", connection)
        {
            CommandTimeout = 5
        };
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Fixture table dropped.");
    }
}
