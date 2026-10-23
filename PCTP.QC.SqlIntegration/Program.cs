using System.Data;
using Microsoft.Data.SqlClient;
using PCTP.QC.Tests;

namespace PCTP.QC.SqlIntegration;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var userId = GetOption(args, "--user");
            var password = GetOption(args, "--password");
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("BLOCKED: SQL Test requires --user <sql-user> and --password <sql-password>.");
                Console.Error.WriteLine($"Target is fixed to {SqlTestConfiguration.Server}/{SqlTestConfiguration.Database}.");
                return 2;
            }

            var connectionString = SqlTestConfiguration.BuildConnectionString(userId, password);
            TestEnvironmentPolicy.EnsureSafeSqlTestTarget(connectionString);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine($"PASS: SQL connection established to {SqlTestConfiguration.Server}/{SqlTestConfiguration.Database}.");

            // Safe-by-default mode: inspect the fixed test schema, then exercise production repositories
            // against isolated # fixtures and use a rollback-only smoke test against dbo.STOCKTP.
            // No shared workflow tables are created, dropped, or committed.
            var missing = await FindMissingProductionSchemaAsync(connection);
            if (missing.Count > 0)
            {
                Console.Error.WriteLine("FAIL: schema diagnostics found issues. No data or schema was changed.");
                foreach (var item in missing) Console.Error.WriteLine(" - " + item);
                Console.Error.WriteLine("ACTION: review the listed table/column definitions on 192.168.200.57; this tester will not auto-create or alter them.");
                return 3;
            }

            Console.WriteLine("PASS: existing dbo schema is compatible with the currently declared QC repository contract.");

            if (args.Any(arg => string.Equals(arg, "--preflight", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("INFO: schema-only preflight completed; no database objects or business rows were changed.");
                return 0;
            }

            // Production StockTpRepository scenario uses a session-local #STOCKTP fixture and rollback.
            // Bulk Slot/Rack/Warehouse operations below also use only session-local # tables.
            Console.WriteLine("INFO: running production StockTpRepository checks against real dbo.STOCKTP in a rollback transaction...");
            ProductionStockTpRepositoryScenario.Run(connectionString);
            Console.WriteLine("PASS: production StockTpRepository integration checks completed against dbo.STOCKTP with rollback isolation.");

            Console.WriteLine("INFO: running production BulkStockSlotRepository against session-local #Warehouse/#Rack/#Slot/#SlotLot fixtures...");
            ProductionBulkStockSlotRepositoryScenario.Run(connectionString);
            Console.WriteLine("PASS: production BulkStockSlotRepository FIFO, slot-header refresh and cross-repository rollback checks completed with isolated fixtures.");

            Console.WriteLine("INFO: running concurrent production BulkStockSlotRepository.TakeLot checks against uniquely named tempdb fixtures...");
            await ProductionBulkStockSlotConcurrencyScenario.RunAsync(connectionString);
            
            Console.WriteLine("INFO: running isolated SQL transaction rollback/commit smoke checks...");
            await RunIsolatedEngineReliabilityAsync(connection);
            Console.WriteLine("PASS: SQL transaction rollback/commit smoke checks completed using a session-local temporary table.");

            Console.WriteLine("INFO: running exact order-table delivery marking against a rollback-isolated SQL fixture...");
            OrderTableDeliveryMarkingScenario.Run(connectionString);
            Console.WriteLine("PASS: order-table delivery marking verified for PO + part + date + hour:minute; replay and neighboring delivery slots checked.");

            Console.WriteLine("INFO: running isolated SQL workflow checks using temporary fixtures only...");
            await SqlEngineWorkflowScenario.RunAsync(connectionString);
            Console.WriteLine("PASS: isolated SQL workflow and concurrent LOT reservation checks completed.");

            Console.WriteLine("INFO: remaining unverified: full production delivery orchestration, WinForms UI automation, and concurrency across production repositories. No shared business workflow rows were mutated.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: FULL SQL TEST");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    // Optional SQL-engine smoke test retained for future opt-in use. Its fixture is
    // session-local; do not change this to dbo or enable shared-table fixtures here.
    private static async Task RunIsolatedEngineReliabilityAsync(SqlConnection connection)
    {
        var table = "[#__PCTP_QC_ENGINE_" + Guid.NewGuid().ToString("N") + "]";
        await using var create = new SqlCommand($@"
CREATE TABLE {table}
(
    Id int NOT NULL PRIMARY KEY,
    Qty int NOT NULL
);
INSERT INTO {table}(Id, Qty) VALUES (1, 50), (2, 10);", connection) { CommandTimeout = 5 };

        try
        {
            await create.ExecuteNonQueryAsync();

            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                try
                {
                    await ExecuteAsync(connection, transaction, $"UPDATE {table} SET Qty = Qty - 10 WHERE Id = 1;");
                    await ExecuteAsync(connection, transaction, "THROW 51090, 'QC injected failure.', 1;");
                }
                catch (SqlException ex) when (ex.Number == 51090)
                {
                    await transaction.RollbackAsync();
                }
            }

            var rollbackQty = await ScalarAsync(connection, $"SELECT Qty FROM {table} WHERE Id = 1;");
            Require(Convert.ToInt32(rollbackQty) == 50, "Injected rollback did not restore the engine fixture.");
            Console.WriteLine("PASS: SQL injected failure rollback.");

            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                await ExecuteAsync(connection, transaction, $"UPDATE {table} SET Qty = Qty - 5 WHERE Id = 1;");
                await transaction.CommitAsync();
            }

            var committedQty = await ScalarAsync(connection, $"SELECT Qty FROM {table} WHERE Id = 1;");
            Require(Convert.ToInt32(committedQty) == 45, "Committed SQL transaction did not persist the expected quantity.");
            Console.WriteLine("PASS: SQL commit contract.");
        }
        finally
        {
            await using var drop = new SqlCommand($"DROP TABLE {table};", connection) { CommandTimeout = 5 };
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<string>> FindMissingProductionSchemaAsync(SqlConnection connection)
    {
        var required = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["STOCKTP"] = new[] { "LOT", "PART", "NAME", "MODEL", "SLNHAP", "SLCONLAI", "SLXUAT", "SATUS", "CASX", "NGAYSX", "NGAYNHAP", "SLSX", "NGAYXUAT" },
            ["SlotLot"] = new[] { "SlotLotId", "SlotId", "LotNo", "ItemCode", "Quantity", "TemCode", "QrData", "MaPhieu", "ImportDate", "PhieuStatus" },
            ["Slot"] = new[] { "SlotId", "RackId", "SlotNumber", "Capacity", "Quantity", "IsOccupied", "ItemCode", "ImportDate" },
            ["Rack"] = new[] { "RackId", "WarehouseId", "RackName" },
            ["Warehouse"] = new[] { "WarehouseId", "Name" }
        };

        const string sql = @"
SELECT t.name, c.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
WHERE s.name = N'dbo'
  AND t.name IN (N'STOCKTP', N'SlotLot', N'Slot', N'Rack', N'Warehouse');";

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            found.Add(reader.GetString(0) + "." + reader.GetString(1));

        var missing = new List<string>();
        foreach (var table in required)
        foreach (var column in table.Value)
            if (!found.Contains(table.Key + "." + column))
                missing.Add("dbo." + table.Key + "." + column);
        return missing;
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        return await command.ExecuteScalarAsync();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
