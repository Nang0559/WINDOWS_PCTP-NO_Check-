using System.Data;
using Microsoft.Data.SqlClient;
using PCTP.QC.Tests;
using PCTP.ClassSQL;
using PCTP.Shared.Common;

internal static class Program
{
    private static async Task<int> Main(string[] args)
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
            var schemaPreflightOnly = args.Any(x => string.Equals(x, "--check-production-schema", StringComparison.OrdinalIgnoreCase));
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = Math.Min(new SqlConnectionStringBuilder(connectionString).ConnectTimeout, 5),
                ApplicationName = "PCTP-QC-SQL-Integration"
            };
            connectionString = builder.ConnectionString;

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            if (schemaPreflightOnly)
            {
                var missing = await FindMissingProductionSchemaAsync(connection);
                if (missing.Count > 0)
                {
                    Console.Error.WriteLine("SCHEMA PREFLIGHT FAILED. Missing production columns:");
                    foreach (var item in missing) Console.Error.WriteLine(" - " + item);
                    return 3;
                }

                Console.WriteLine("PASS: production STOCKTP/SlotLot/Slot/Rack/Warehouse schema preflight (read-only).");
                return 0;
            }

            var table = "dbo.[__PCTP_QC_" + Guid.NewGuid().ToString("N") + "]";
            try
            {
                await CreateFixtureAsync(connection, table);
                await RunProductionUnitOfWorkNestedRollbackAsync(connectionString, table);
                await RunProductionUnitOfWorkNestedCommitAsync(connectionString, table);
                await RunProductionUnitOfWorkDisposeRollbackAsync(connectionString, table);
                await RunProductionSqlRepositoryBaseRollbackAsync(connectionString, table);
                PCTP.QC.SqlIntegration.ProductionStockTpRepositoryScenario.Run(connectionString);
                PCTP.QC.SqlIntegration.ProductionBulkStockSlotRepositoryScenario.Run(connectionString);
                await RunRollbackAtomicityAsync(connection, table);
                await RunConcurrentConditionalDecrementAsync(connectionString, table);
                await RunCommandTimeoutRollbackAsync(connection, table);
                Console.WriteLine("PASS: 10 SQL Server integration scenarios completed (3 production UnitOfWork + 1 production SqlRepositoryBase + 1 production StockTpRepository + 1 production BulkStockSlotRepository + 1 cross-repository delivery rollback + 3 SQL engine contracts).");
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

    private static async Task<List<string>> FindMissingProductionSchemaAsync(SqlConnection connection)
    {
        // Read-only schema inspection. It does not create objects, run migrations,
        // or read business rows. Required columns are derived from the production
        // StockTpRepository, SlotRepository and BulkStockSlotRepository queries.
        var required = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["STOCKTP"] = new[] { "LOT", "PART", "NAME", "MODEL", "SLNHAP", "SLCONLAI", "SLXUAT", "SATUS", "CASX", "NGAYSX", "NGAYNHAP", "SLSX", "NGAYXUAT" },
            ["SlotLot"] = new[] { "SlotLotId", "SlotId", "LotNo", "ItemCode", "Quantity", "TemCode", "QrData", "MaPhieu", "ImportDate", "PhieuStatus" },
            ["Slot"] = new[] { "SlotId", "RackId", "SlotNumber", "Capacity", "Quantity", "IsOccupied", "ItemCode", "ImportDate" },
            ["Rack"] = new[] { "RackId", "WarehouseId", "RackName" },
            ["Warehouse"] = new[] { "WarehouseId", "Name" }
        };

        const string sql = @"
SELECT t.name AS TableName, c.name AS ColumnName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
WHERE s.name = N'dbo'
  AND t.name IN (N'STOCKTP', N'SlotLot', N'Slot', N'Rack', N'Warehouse');";

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand(sql, connection) { CommandTimeout = 5 })
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                found.Add(reader.GetString(0) + "." + reader.GetString(1));
        }

        var missing = new List<string>();
        foreach (var table in required)
        foreach (var column in table.Value)
            if (!found.Contains(table.Key + "." + column))
                missing.Add("dbo." + table.Key + "." + column);
        return missing;
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
INSERT INTO {table}(Id, Lot, StockQty, SlotQty) VALUES (1, 'QC-LOT-ROLLBACK', 100, 100), (2, 'QC-LOT-CONCURRENT', 10, 10), (3, 'QC-LOT-TIMEOUT', 50, 50), (4, 'QC-LOT-UOW', 100, 100);";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("PASS: isolated SQL fixture created.");
    }

    // These scenarios compile and execute the actual production UnitOfWork.cs
    // and IUnitOfWork.cs. Only SQLPROVIDER is replaced by a test-only holder so
    // the production connection-string defaults/credentials are never loaded.
    // Executes the actual production SqlRepositoryBase together with the actual
    // production UnitOfWork. The test-only executor throws if the base class
    // accidentally opens an independent connection while a UoW is active.
    private static async Task RunProductionSqlRepositoryBaseRollbackAsync(string connectionString, string table)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using (var uow = new UnitOfWork(provider))
        {
            var repository = new PCTP.QC.SqlIntegration.SqlRepositoryTransactionProbe(
                new PCTP.Modules.GiaoHangKhach.PhieuSqlExecutor(), uow);

            uow.Begin();
            if (!repository.IsUsingUnitOfWorkTransaction)
                throw new InvalidOperationException("SqlRepositoryBase is not bound to the active UnitOfWork connection/transaction.");

            var affected = repository.ExecuteUpdate($"UPDATE {table} SET StockQty = StockQty - 13 WHERE Id = 4;");
            if (affected != 1)
                throw new InvalidOperationException($"Expected one repository update, got {affected}.");

            uow.Rollback();
        }

        await using var verifyConnection = await OpenAsync(connectionString);
        var row = await ReadRowAsync(verifyConnection, table, 4);
        if (row.StockQty != 90)
            throw new InvalidOperationException($"Production SqlRepositoryBase did not roll back. Expected the pre-scenario balance 90; actual Stock={row.StockQty}.");
        Console.WriteLine("PASS: production SqlRepositoryBase command enlisted in the production UnitOfWork and rolled back.");
    }

    private static async Task RunProductionUnitOfWorkNestedRollbackAsync(string connectionString, string table)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using (var uow = new UnitOfWork(provider))
        {
            uow.Begin();
            ExecuteUnitOfWorkCommand(uow, $"UPDATE {table} SET StockQty = StockQty - 10 WHERE Id = 4;");
            uow.Begin();
            ExecuteUnitOfWorkCommand(uow, $"UPDATE {table} SET SlotQty = SlotQty - 10 WHERE Id = 4;");
            uow.Rollback(); // Nested rollback marks the outer transaction rollback-only.

            var error = AssertRollbackOnlyCommit(uow);
            if (!error)
                throw new InvalidOperationException("Production UnitOfWork did not reject outer commit after nested rollback.");
        }

        await using var verifyConnection = await OpenAsync(connectionString);
        var row = await ReadRowAsync(verifyConnection, table, 4);
        if (row.StockQty != 100 || row.SlotQty != 100)
            throw new InvalidOperationException($"Production UnitOfWork nested rollback failed. Stock={row.StockQty}, Slot={row.SlotQty}.");
        Console.WriteLine("PASS: production UnitOfWork nested rollback-only reverted all writes.");
    }

    private static bool AssertRollbackOnlyCommit(IUnitOfWork uow)
    {
        try
        {
            uow.Commit();
            return false;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("rollback-only", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    private static async Task RunProductionUnitOfWorkNestedCommitAsync(string connectionString, string table)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using (var uow = new UnitOfWork(provider))
        {
            uow.Begin();
            ExecuteUnitOfWorkCommand(uow, $"UPDATE {table} SET StockQty = StockQty - 10 WHERE Id = 4;");
            uow.Begin();
            ExecuteUnitOfWorkCommand(uow, $"UPDATE {table} SET SlotQty = SlotQty - 10 WHERE Id = 4;");
            uow.Commit();
            uow.Commit();
        }

        await using var verifyConnection = await OpenAsync(connectionString);
        var row = await ReadRowAsync(verifyConnection, table, 4);
        if (row.StockQty != 90 || row.SlotQty != 90)
            throw new InvalidOperationException($"Production UnitOfWork nested commit failed. Stock={row.StockQty}, Slot={row.SlotQty}.");
        Console.WriteLine("PASS: production UnitOfWork nested commit persisted the complete unit.");
    }

    private static async Task RunProductionUnitOfWorkDisposeRollbackAsync(string connectionString, string table)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using (var uow = new UnitOfWork(provider))
        {
            uow.Begin();
            ExecuteUnitOfWorkCommand(uow, $"UPDATE {table} SET StockQty = StockQty - 1 WHERE Id = 4;");
            // Dispose without Commit must roll back the active production transaction.
        }

        await using var verifyConnection = await OpenAsync(connectionString);
        var row = await ReadRowAsync(verifyConnection, table, 4);
        if (row.StockQty != 90 || row.SlotQty != 90)
            throw new InvalidOperationException($"Production UnitOfWork dispose rollback failed. Stock={row.StockQty}, Slot={row.SlotQty}.");
        Console.WriteLine("PASS: production UnitOfWork dispose rolled back an uncommitted write.");
    }

    private static void ExecuteUnitOfWorkCommand(IUnitOfWork uow, string sql)
    {
        using var command = new System.Data.SqlClient.SqlCommand(sql, uow.Connection, uow.Transaction)
        {
            CommandTimeout = 5
        };
        command.ExecuteNonQuery();
    }

    private static async Task RunRollbackAtomicityAsync(SqlConnection connection, string table)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var injectedFailureObserved = false;
        try
        {
            await ExecuteAsync(connection, transaction,
                $"UPDATE {table} SET StockQty = StockQty - 10 WHERE Id = 1; UPDATE {table} SET SlotQty = SlotQty - 10 WHERE Id = 1;");
            // Deliberately fail after both writes to verify rollback of the complete unit.
            await ExecuteAsync(connection, transaction, "THROW 51000, 'Injected failure after stock writes for rollback verification.', 1;");
            await transaction.CommitAsync();
            throw new InvalidOperationException("Expected the deliberate SQL failure was not raised.");
        }
        catch (SqlException ex) when (ex.Number == 51000)
        {
            injectedFailureObserved = true;
            await transaction.RollbackAsync();
        }

        if (!injectedFailureObserved)
            throw new InvalidOperationException("The expected post-write failure was not observed.");

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
