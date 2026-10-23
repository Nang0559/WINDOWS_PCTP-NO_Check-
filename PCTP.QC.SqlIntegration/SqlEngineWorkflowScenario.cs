using System.Data;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// SQL Server engine-level workflow checks using session-local temp tables and a
/// uniquely named tempdb global table for two-session concurrency. This does not
/// claim to execute the production WinForms orchestration or business repositories.
/// </summary>
internal static class SqlEngineWorkflowScenario
{
    public static async Task RunAsync(string connectionString)
    {
        await VerifyPhysicalLocationQuarantineAndLedgerAsync(connectionString);
        await VerifyConcurrentLotReservationAsync(connectionString);
        Console.WriteLine("PASS: isolated SQL workflow checks (location lookup, quarantine/release, ledger equation, idempotent reference, concurrent LOT reservation).");
    }

    private static async Task VerifyPhysicalLocationQuarantineAndLedgerAsync(string cs)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var setup = new SqlCommand(@"
CREATE TABLE #Warehouse(WarehouseId int NOT NULL PRIMARY KEY, Name nvarchar(50) NOT NULL UNIQUE);
CREATE TABLE #Rack(RackId int NOT NULL PRIMARY KEY, WarehouseId int NOT NULL, RackName nvarchar(50) NOT NULL, UNIQUE(WarehouseId, RackName));
CREATE TABLE #Slot(SlotId int NOT NULL PRIMARY KEY, RackId int NOT NULL, SlotNumber int NOT NULL, UNIQUE(RackId, SlotNumber));
CREATE TABLE #SlotLot(SlotLotId int IDENTITY PRIMARY KEY, SlotId int NOT NULL, LotNo nvarchar(50) NOT NULL, Qty int NOT NULL CHECK(Qty >= 0), Status tinyint NOT NULL);
CREATE TABLE #Movement(Id int IDENTITY PRIMARY KEY, LotNo nvarchar(50) NOT NULL, QtyIn int NOT NULL DEFAULT(0), QtyOut int NOT NULL DEFAULT(0), ReferenceKey nvarchar(80) NULL UNIQUE);
CREATE TABLE #Delivery(Id int IDENTITY PRIMARY KEY, ReferenceKey nvarchar(80) NOT NULL UNIQUE);
INSERT INTO #Warehouse VALUES(1, N'QC-WH');
INSERT INTO #Rack VALUES(1, 1, N'QC-RACK');
INSERT INTO #Slot VALUES(1, 1, 7);
INSERT INTO #SlotLot(SlotId, LotNo, Qty, Status) VALUES(1, N'QC-LOT-001', 15, 3);
INSERT INTO #Movement(LotNo, QtyIn, QtyOut) VALUES(N'QC-LOT-001', 12, 5);", connection)
        { CommandTimeout = 5 };
        await setup.ExecuteNonQueryAsync();

        var located = await ScalarIntAsync(connection, @"
SELECT COUNT(1) FROM #SlotLot sl
JOIN #Slot s ON s.SlotId=sl.SlotId
JOIN #Rack r ON r.RackId=s.RackId
JOIN #Warehouse w ON w.WarehouseId=r.WarehouseId
WHERE w.Name=N'QC-WH' AND r.RackName=N'QC-RACK' AND s.SlotNumber=7 AND sl.LotNo=N'QC-LOT-001';");
        Require(located == 1, "LOT physical location lookup did not return the expected warehouse/rack/slot.");

        var quarantinedExport = await ScalarIntAsync(connection, @"
UPDATE #SlotLot SET Qty=Qty-1 WHERE LotNo=N'QC-LOT-001' AND Status=0 AND Qty>=1;
SELECT @@ROWCOUNT;");
        Require(quarantinedExport == 0, "Quarantined LOT was unexpectedly eligible for export.");

        await ExecuteAsync(connection, "UPDATE #SlotLot SET Status=0 WHERE LotNo=N'QC-LOT-001' AND Status=3;");
        var releasedStatus = await ScalarIntAsync(connection, "SELECT Status FROM #SlotLot WHERE LotNo=N'QC-LOT-001';");
        Require(releasedStatus == 0, "Authorized release did not transition quarantine status to active.");

        // Same reference can create a single delivery record; a replay must not
        // create another ledger event.
        await ExecuteAsync(connection, @"
IF NOT EXISTS(SELECT 1 FROM #Delivery WHERE ReferenceKey=N'QC-REF-001')
BEGIN
  INSERT INTO #Delivery(ReferenceKey) VALUES(N'QC-REF-001');
  INSERT INTO #Movement(LotNo, QtyOut, ReferenceKey) VALUES(N'QC-LOT-001', 2, N'QC-REF-001');
END;
IF NOT EXISTS(SELECT 1 FROM #Delivery WHERE ReferenceKey=N'QC-REF-001')
BEGIN
  INSERT INTO #Delivery(ReferenceKey) VALUES(N'QC-REF-001');
  INSERT INTO #Movement(LotNo, QtyOut, ReferenceKey) VALUES(N'QC-LOT-001', 2, N'QC-REF-001');
END;");
        Require(await ScalarIntAsync(connection, "SELECT COUNT(1) FROM #Delivery WHERE ReferenceKey=N'QC-REF-001';") == 1,
            "Delivery reference replay created duplicate delivery state.");
        Require(await ScalarIntAsync(connection, "SELECT COUNT(1) FROM #Movement WHERE ReferenceKey=N'QC-REF-001';") == 1,
            "Delivery reference replay duplicated the stock movement.");

        var opening = 10;
        var received = await ScalarIntAsync(connection, "SELECT SUM(QtyIn) FROM #Movement WHERE LotNo=N'QC-LOT-001';");
        var issued = await ScalarIntAsync(connection, "SELECT SUM(QtyOut) FROM #Movement WHERE LotNo=N'QC-LOT-001';");
        var closing = await ScalarIntAsync(connection, "SELECT SUM(Qty) FROM #SlotLot WHERE LotNo=N'QC-LOT-001';");
        Require(opening + received - issued == closing,
            $"Ledger does not reconcile: opening {opening} + in {received} - out {issued} != closing {closing}.");

        // A failure after multiple writes must roll back all changes in the unit.
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await ExecuteAsync(connection, transaction, "UPDATE #SlotLot SET Qty=Qty-2 WHERE LotNo=N'QC-LOT-001'; UPDATE #Movement SET QtyOut=QtyOut+2 WHERE LotNo=N'QC-LOT-001';");
            try
            {
                await ExecuteAsync(connection, transaction, "THROW 51092, 'QC workflow rollback injection', 1;");
                throw new InvalidOperationException("Injected SQL failure was not raised.");
            }
            catch (SqlException ex) when (ex.Number == 51092)
            {
                await transaction.RollbackAsync();
            }
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }

        Require(await ScalarIntAsync(connection, "SELECT SUM(Qty) FROM #SlotLot WHERE LotNo=N'QC-LOT-001';") == closing,
            "Multi-write rollback did not restore SlotLot quantity.");
        Require(await ScalarIntAsync(connection, "SELECT SUM(QtyOut) FROM #Movement WHERE LotNo=N'QC-LOT-001';") == issued,
            "Multi-write rollback did not restore movement history.");
        Console.WriteLine("PASS: isolated location, quarantine/release, duplicate reference, ledger and multi-write rollback checks.");
    }

    private static async Task VerifyConcurrentLotReservationAsync(string cs)
    {
        // A unique tempdb global temp table lets two independent SQL sessions
        // contend for one LOT without creating or mutating any dbo business table.
        var table = "##PCTP_QC_LOT_" + Guid.NewGuid().ToString("N");
        await using var setupConnection = new SqlConnection(cs);
        await setupConnection.OpenAsync();
        await using (var setup = new SqlCommand($"CREATE TABLE {table}(Id int NOT NULL PRIMARY KEY, Qty int NOT NULL); INSERT INTO {table}(Id,Qty) VALUES(1,15);", setupConnection)
        { CommandTimeout = 5 })
        {
            await setup.ExecuteNonQueryAsync();
        }

        try
        {
            async Task<int> ReserveAsync()
            {
                await using var connection = new SqlConnection(cs);
                await connection.OpenAsync();
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                try
                {
                    await using var command = new SqlCommand($@"
UPDATE {table} WITH (UPDLOCK, ROWLOCK)
SET Qty=Qty-10
WHERE Id=1 AND Qty>=10;
SELECT @@ROWCOUNT;", connection, transaction) { CommandTimeout = 10 };
                    var affected = Convert.ToInt32(await command.ExecuteScalarAsync());
                    await transaction.CommitAsync();
                    return affected;
                }
                catch
                {
                    try { await transaction.RollbackAsync(); } catch { }
                    throw;
                }
            }

            var results = await Task.WhenAll(Task.Run(ReserveAsync), Task.Run(ReserveAsync));
            Require(results.Sum() == 1, $"Expected exactly one 10-unit reservation from 15 units; affected rows were {string.Join(",", results)}.");
            await using var verifyConnection = new SqlConnection(cs);
            await verifyConnection.OpenAsync();
            var remaining = await ScalarIntAsync(verifyConnection, $"SELECT Qty FROM {table} WHERE Id=1;");
            Require(remaining == 5, $"Concurrent reservation oversold or lost stock; expected 5 remaining, got {remaining}.");
            Console.WriteLine("PASS: two SQL sessions concurrently reserved 10 from 15; one succeeded and 5 remained.");
        }
        finally
        {
            await using var cleanup = new SqlCommand($"IF OBJECT_ID('tempdb..{table}') IS NOT NULL DROP TABLE {table};", setupConnection)
            { CommandTimeout = 5 };
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
