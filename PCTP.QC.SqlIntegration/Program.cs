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
                Console.Error.WriteLine("BLOCKED: SQL Test requires --user/--password or environment variables PCTP_QC_SQL_USER and PCTP_QC_SQL_PASSWORD.");
                Console.Error.WriteLine($"Target is fixed to {SqlTestConfiguration.Server}/{SqlTestConfiguration.Database}.");
                return 2;
            }

            var connectionString = SqlTestConfiguration.BuildConnectionString(userId, password);
            TestEnvironmentPolicy.EnsureSafeSqlTestTarget(connectionString);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine($"PASS: SQL connection established to {SqlTestConfiguration.Server}/{SqlTestConfiguration.Database}.");

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

            if (args.Any(arg => string.Equals(arg, "--delivery-preflight", StringComparison.OrdinalIgnoreCase)))
            {
                await VerifyDeliveryProcedureReadinessAsync(connection);
                Console.WriteLine("PASS: delivery procedure deployment preflight completed; metadata only, no procedure execution or business writes.");
                return 0;
            }

            if (args.Any(arg => string.Equals(arg, "--delivery-acceptance", StringComparison.OrdinalIgnoreCase)))
            {
                // Explicit opt-in only. Executes dbo.Usp_Qrcode_Update_Stock2405 inside rollback-only
                // transactions, so it is never reachable from --preflight / --delivery-preflight or
                // from an ambient environment variable.
                await VerifyDeliveryProcedureReadinessAsync(connection);
                await DeliveryProductionAcceptanceScenario.RunAsync(connectionString);
                return 0;
            }

            if (args.Any(arg => string.Equals(arg, "--quarantine-release-preflight", StringComparison.OrdinalIgnoreCase)))
            {
                await QuarantineReleaseScenario.RunAsync(connectionString);
                Console.WriteLine("PASS: quarantine release SQL-engine contract completed using isolated tempdb fixtures.");
                return 0;
            }

            Console.WriteLine("INFO: running production StockTpRepository checks against a session-local #STOCKTP fixture (dbo.STOCKTP is not touched)...");
            ProductionStockTpRepositoryScenario.Run(connectionString);
            Console.WriteLine("PASS: production StockTpRepository integration checks completed against session-local #STOCKTP.");

            Console.WriteLine("INFO: running production BulkStockSlotRepository against session-local #Warehouse/#Rack/#Slot/#SlotLot fixtures...");
            ProductionBulkStockSlotRepositoryScenario.Run(connectionString);
            Console.WriteLine("PASS: production BulkStockSlotRepository FIFO, slot-header refresh and cross-repository rollback checks completed with isolated fixtures.");

            Console.WriteLine("INFO: running concurrent production BulkStockSlotRepository.TakeLot checks against uniquely named tempdb fixtures...");
            await ProductionBulkStockSlotConcurrencyScenario.RunAsync(connectionString);

            Console.WriteLine("INFO: running SqlRepositoryBase.InTransaction commit/rollback/join-caller checks against a uniquely named tempdb fixture...");
            await InTransactionScenario.RunAsync(connectionString);

            Console.WriteLine("INFO: running isolated SQL transaction rollback/commit smoke checks...");
            await RunIsolatedEngineReliabilityAsync(connection);
            Console.WriteLine("PASS: SQL transaction rollback/commit smoke checks completed using a session-local temporary table.");

            Console.WriteLine("INFO: verifying delivery savepoint behavior against a session-local fixture...");
            await DeliveryTransactionBoundaryScenario.RunAsync(connectionString);

            Console.WriteLine("INFO: running delivery reliability tests in uniquely named tempdb fixtures...");
            await DeliveryReliabilityScenario.RunAsync(connectionString);

            Console.WriteLine("INFO: running exact order-table delivery marking against a rollback-isolated SQL fixture...");
            OrderTableDeliveryMarkingScenario.Run(connectionString);
            Console.WriteLine("PASS: order-table delivery marking verified for PO + part + date + hour:minute; replay and neighboring delivery slots checked.");

            Console.WriteLine("INFO: running isolated SQL workflow checks using temporary fixtures only...");
            await SqlEngineWorkflowScenario.RunAsync(connectionString);
            Console.WriteLine("PASS: isolated SQL workflow and concurrent LOT reservation checks completed.");

            Console.WriteLine("INFO: running quarantine release authorization and atomicity against isolated tempdb fixtures...");
            await QuarantineReleaseScenario.RunAsync(connectionString);

            Console.WriteLine("INFO: remaining runtime acceptance gates: exact Usp_Qrcode_Update_Stock2405 execution against a dedicated isolated copy of the business schema; full PhieuKhoRepository orchestration; and Windows end-to-end UI business-journey execution. No shared production workflow rows were mutated.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: FULL SQL TEST");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task VerifyDeliveryProcedureReadinessAsync(SqlConnection connection)
    {
        const string sql = @"
SELECT
    OBJECT_ID(N'dbo.Usp_Qrcode_Update_Stock2405', N'P') AS ProcedureId,
    OBJECT_ID(N'dbo.LUUPHIEUGIAOHANG', N'U') AS DeliveryLedgerId,
    OBJECT_ID(N'dbo.LUUDOCQRCODE', N'U') AS DeliveryQrLedgerId,
    OBJECT_ID(N'dbo.STOCKTP', N'U') AS StockId,
    m.definition
FROM (VALUES (1)) AS seed(n)
LEFT JOIN sys.sql_modules m
    ON m.object_id = OBJECT_ID(N'dbo.Usp_Qrcode_Update_Stock2405', N'P');";

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Delivery procedure metadata query returned no row.");

        var procedureId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
        var ledgerId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
        var qrLedgerId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
        var stockId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
        var definition = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
        await reader.CloseAsync();

        var missing = new List<string>();
        if (procedureId == null) missing.Add("dbo.Usp_Qrcode_Update_Stock2405 is missing");
        if (ledgerId == null) missing.Add("dbo.LUUPHIEUGIAOHANG is missing");
        if (qrLedgerId == null) missing.Add("dbo.LUUDOCQRCODE is missing");
        if (stockId == null) missing.Add("dbo.STOCKTP is missing");

        if (procedureId != null)
        {
            if (!definition.Contains("PCTP_DELIVERY:", StringComparison.OrdinalIgnoreCase))
                missing.Add("deployed procedure does not contain the PCTP_DELIVERY idempotency lock marker");
            if (!definition.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase))
                missing.Add("deployed procedure does not contain sp_getapplock");
            if (!definition.Contains("SAVE TRANSACTION UspQrcodeUpdateStock2405", StringComparison.OrdinalIgnoreCase))
                missing.Add("deployed procedure does not preserve the caller transaction with the expected savepoint");
            if (!definition.Contains("@LockOwner = N'Transaction'", StringComparison.OrdinalIgnoreCase))
                missing.Add("deployed procedure does not use a transaction-owned application lock");
        }

        if (missing.Count > 0)
        {
            foreach (var item in missing)
                Console.Error.WriteLine("FAIL: DELIVERY PREFLIGHT: " + item);
            Console.Error.WriteLine("ACTION: compare the deployed dbo.Usp_Qrcode_Update_Stock2405 definition with SQL/Usp_Qrcode_Update_Stock2405.sql and review required ledger tables. This check is read-only.");
            throw new InvalidOperationException("Delivery procedure deployment preflight failed with " + missing.Count + " issue(s).");
        }

        Console.WriteLine("PASS: dbo.Usp_Qrcode_Update_Stock2405 exists.");
        Console.WriteLine("PASS: delivery and QR ledger tables plus dbo.STOCKTP exist.");
        Console.WriteLine("PASS: deployed procedure contains savepoint and transaction-owned idempotency-lock markers.");
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        var environmentVariable = name switch
        {
            "--user" => "PCTP_QC_SQL_USER",
            "--password" => "PCTP_QC_SQL_PASSWORD",
            _ => null
        };
        return environmentVariable == null ? null : Environment.GetEnvironmentVariable(environmentVariable);
    }

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
