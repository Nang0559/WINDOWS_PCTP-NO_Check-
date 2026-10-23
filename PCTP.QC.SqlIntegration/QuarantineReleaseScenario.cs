using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// SQL-engine proof of the quarantine-release transaction contract using uniquely
/// named tempdb fixtures. No shared business rows are changed.
/// </summary>
internal static class QuarantineReleaseScenario
{
    public static async Task RunAsync(string connectionString)
    {
        var prefix = "##PCTP_QC_RELEASE_" + Guid.NewGuid().ToString("N");
        var lots = prefix + "_LOTS";
        var audit = prefix + "_AUDIT";
        var slot = prefix + "_SLOT";
        await using var owner = new SqlConnection(connectionString);
        await owner.OpenAsync();
        await ExecuteAsync(owner, $@"
CREATE TABLE {lots} (
    SlotLotId int NOT NULL PRIMARY KEY,
    SlotId int NOT NULL,
    LotNo nvarchar(100) NOT NULL,
    ItemCode nvarchar(100) NOT NULL,
    Quantity int NOT NULL,
    PhieuStatus tinyint NOT NULL,
    StockQty int NOT NULL
);
CREATE TABLE {slot} (SlotId int NOT NULL PRIMARY KEY, Quantity int NOT NULL);
CREATE TABLE {audit} (
    Id int IDENTITY PRIMARY KEY,
    SlotLotId int NOT NULL,
    Quantity int NOT NULL,
    PerformedBy nvarchar(100) NOT NULL,
    ReferenceId nvarchar(100) NOT NULL
);
INSERT INTO {lots}(SlotLotId,SlotId,LotNo,ItemCode,Quantity,PhieuStatus,StockQty)
VALUES (1,10,N'LOT-Q',N'ITEM-Q',12,3,0);
INSERT INTO {slot}(SlotId,Quantity) VALUES (10,0);");

        try
        {
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT COUNT(1) FROM {lots} WHERE SlotLotId=1 AND PhieuStatus=3")) == 1,
                "Quarantine fixture was not created.");

            // Inject a failure after all simulated persistence steps; transaction rollback must restore every ledger.
            await using (var tx = (SqlTransaction)await owner.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                try
                {
                    await ExecuteAsync(owner, tx, $@"
UPDATE {lots} SET Quantity=0 WHERE SlotLotId=1 AND PhieuStatus=3 AND Quantity=12;
INSERT INTO {lots}(SlotLotId,SlotId,LotNo,ItemCode,Quantity,PhieuStatus,StockQty)
SELECT 2,SlotId,LotNo,ItemCode,12,0,0 FROM {lots} WHERE SlotLotId=1;
UPDATE {lots} SET StockQty=12 WHERE SlotLotId=1;
UPDATE {slot} SET Quantity=12 WHERE SlotId=10;
INSERT INTO {audit}(SlotLotId,Quantity,PerformedBy,ReferenceId) VALUES (1,12,N'qc-admin',N'REL-FAIL');
THROW 51071, 'Injected release persistence failure.', 1;");
                }
                catch (SqlException ex) when (ex.Number == 51071)
                {
                    await tx.RollbackAsync();
                }
            }

            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT Quantity FROM {lots} WHERE SlotLotId=1")) == 12,
                "Injected release failure did not restore quarantined quantity.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT COUNT(1) FROM {lots} WHERE SlotId=10 AND PhieuStatus=0 AND Quantity>0")) == 0,
                "Injected release failure left an active SlotLot behind.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT StockQty FROM {lots} WHERE SlotLotId=1")) == 0,
                "Injected release failure left STOCKTP-equivalent quantity behind.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT Quantity FROM {slot} WHERE SlotId=10")) == 0,
                "Injected release failure left a changed Slot header.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT COUNT(1) FROM {audit} WHERE ReferenceId=N'REL-FAIL'")) == 0,
                "Injected release failure left an audit row.");

            // Successful release preserves the quarantined source as historical status 3,
            // creates a distinct active SlotLot, updates the stock ledger and Slot header, and audits once.
            await using (var tx = (SqlTransaction)await owner.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await ExecuteAsync(owner, tx, $@"
UPDATE {lots} SET Quantity=0 WHERE SlotLotId=1 AND PhieuStatus=3 AND Quantity=12;
IF @@ROWCOUNT <> 1 THROW 51072, 'Quarantine source row changed before release.', 1;
INSERT INTO {lots}(SlotLotId,SlotId,LotNo,ItemCode,Quantity,PhieuStatus,StockQty)
SELECT 2,SlotId,LotNo,ItemCode,12,0,0 FROM {lots} WHERE SlotLotId=1;
UPDATE {lots} SET StockQty=12 WHERE SlotLotId=1;
UPDATE {slot} SET Quantity=12 WHERE SlotId=10;
INSERT INTO {audit}(SlotLotId,Quantity,PerformedBy,ReferenceId) VALUES (1,12,N'qc-admin',N'REL-001');");
                await tx.CommitAsync();
            }

            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT PhieuStatus FROM {lots} WHERE SlotLotId=1")) == 3 &&
                    Convert.ToInt32(await ScalarAsync(owner, $"SELECT Quantity FROM {lots} WHERE SlotLotId=1")) == 0,
                "Release must retain the quarantined source row as zero-quantity history.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT COUNT(1) FROM {lots} WHERE SlotId=10 AND PhieuStatus=0 AND Quantity=12")) == 1,
                "Authorized release did not create exactly one active SlotLot row.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT StockQty FROM {lots} WHERE SlotLotId=1")) == 12,
                "Authorized release did not reconcile available stock.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT Quantity FROM {slot} WHERE SlotId=10")) == 12,
                "Authorized release did not reconcile the Slot header to active SlotLot quantity.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT SUM(Quantity) FROM {lots} WHERE SlotId=10 AND PhieuStatus=0")) == 12,
                "Slot header/current SlotLot invariant failed after release.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT COUNT(1) FROM {audit} WHERE ReferenceId=N'REL-001'")) == 1,
                "Authorized release did not create exactly one audit record.");

            Console.WriteLine("PASS: quarantine release rollback, active-stock conversion, Slot header reconciliation and audit contract.");
        }
        finally
        {
            await TryDropAsync(owner, audit);
            await TryDropAsync(owner, lots);
            await TryDropAsync(owner, slot);
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction tx, string sql)
    {
        await using var command = new SqlCommand(sql, connection, tx) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        return await command.ExecuteScalarAsync() ?? DBNull.Value;
    }

    private static async Task TryDropAsync(SqlConnection connection, string table)
    {
        try { await ExecuteAsync(connection, $"DROP TABLE {table};"); } catch { }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
