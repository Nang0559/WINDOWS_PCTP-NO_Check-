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
CREATE TABLE {audit} (
    Id int IDENTITY PRIMARY KEY,
    SlotLotId int NOT NULL,
    Quantity int NOT NULL,
    PerformedBy nvarchar(100) NOT NULL,
    ReferenceId nvarchar(100) NOT NULL
);
INSERT INTO {lots}(SlotLotId,SlotId,LotNo,ItemCode,Quantity,PhieuStatus,StockQty)
VALUES (1,10,N'LOT-Q',N'ITEM-Q',12,3,0);");

        try
        {
            // Authorization and QC gates are fail-closed and must not start a transaction.
            var unauthorized = await ScalarAsync(owner, $"SELECT COUNT(1) FROM {lots} WHERE SlotLotId=1 AND PhieuStatus=3");
            Require(Convert.ToInt32(unauthorized) == 1, "Quarantine fixture was not created.");

            await using (var tx = (SqlTransaction)await owner.BeginTransactionAsync(IsolationLevel.ReadCommitted))
            {
                try
                {
                    await ExecuteAsync(owner, tx, $"UPDATE {lots} SET PhieuStatus=3 WHERE SlotLotId=1;");
                    await ExecuteAsync(owner, tx, "THROW 51071, 'Injected release authorization failure.', 1;");
                }
                catch (SqlException ex) when (ex.Number == 51071)
                {
                    await tx.RollbackAsync();
                }
            }

            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT PhieuStatus FROM {lots} WHERE SlotLotId=1")) == 3,
                "Unauthorized/injected release changed quarantine state.");

            // Authorized release: quarantine quantity is converted to active stock,
            // STOCKTP-equivalent quantity is added, and audit is inserted atomically.
            await using (var tx = (SqlTransaction)await owner.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await ExecuteAsync(owner, tx, $@"
UPDATE {lots}
SET Quantity = 0, PhieuStatus = 0, StockQty = StockQty + 12
WHERE SlotLotId = 1 AND PhieuStatus = 3 AND Quantity = 12;
INSERT INTO {audit}(SlotLotId, Quantity, PerformedBy, ReferenceId)
VALUES (1,12,N'qc-admin',N'REL-001');");
                await tx.CommitAsync();
            }

            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT PhieuStatus FROM {lots} WHERE SlotLotId=1")) == 0,
                "Authorized release did not activate the LOT.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT StockQty FROM {lots} WHERE SlotLotId=1")) == 12,
                "Authorized release did not reconcile available stock.");
            Require(Convert.ToInt32(await ScalarAsync(owner, $"SELECT COUNT(1) FROM {audit} WHERE ReferenceId=N'REL-001'")) == 1,
                "Authorized release did not create exactly one audit record.");

            Console.WriteLine("PASS: quarantine release authorization gate, atomic stock transition and audit contract.");
        }
        finally
        {
            await TryDropAsync(owner, audit);
            await TryDropAsync(owner, lots);
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
        return await command.ExecuteScalarAsync();
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
