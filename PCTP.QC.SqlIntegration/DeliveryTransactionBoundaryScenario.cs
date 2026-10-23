using System;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Verifies the SQL Server savepoint contract used by Usp_Qrcode_Update_Stock2405
/// without executing it against shared production tables.
/// </summary>
internal static class DeliveryTransactionBoundaryScenario
{
    public static async Task RunAsync(string connectionString)
    {
        var table = "#PCTP_QC_DeliveryTxn_" + Guid.NewGuid().ToString("N");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var create = new SqlCommand($@"
CREATE TABLE {table}(Id int NOT NULL PRIMARY KEY, Qty int NOT NULL);
INSERT INTO {table}(Id, Qty) VALUES(1, 15);", connection) { CommandTimeout = 5 })
        {
            await create.ExecuteNonQueryAsync();
        }

        // Success path: a procedure savepoint must not commit the caller's transaction.
        await using (var outer = (SqlTransaction)await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, outer, $"SAVE TRANSACTION PctpDeliverySavepoint; UPDATE {table} SET Qty=Qty-10 WHERE Id=1;");
            await ExecuteAsync(connection, outer, $"UPDATE {table} SET Qty=Qty+2 WHERE Id=1;");
            await outer.RollbackAsync();
        }

        var afterCallerRollback = await ScalarAsync(connection, $"SELECT Qty FROM {table} WHERE Id=1;");
        Require(afterCallerRollback == 15,
            $"Outer rollback did not restore the savepoint test fixture; expected 15, got {afterCallerRollback}.");

        // Business-error path: roll back to savepoint, report the error, and leave
        // the outer transaction usable so the repository can explicitly roll it back.
        await using (var outer = (SqlTransaction)await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, outer, $"SAVE TRANSACTION PctpDeliverySavepoint; UPDATE {table} SET Qty=Qty-10 WHERE Id=1;");
            await ExecuteAsync(connection, outer, $"ROLLBACK TRANSACTION PctpDeliverySavepoint;");
            await ExecuteAsync(connection, outer, $"UPDATE {table} SET Qty=Qty-1 WHERE Id=1;");
            var stillActive = await ScalarAsync(connection, "SELECT XACT_STATE();", outer);
            Require(stillActive == 1, $"Outer transaction became unusable after rollback to savepoint; XACT_STATE={stillActive}.");
            await outer.RollbackAsync();
        }

        var finalQuantity = await ScalarAsync(connection, $"SELECT Qty FROM {table} WHERE Id=1;");
        Require(finalQuantity == 15,
            $"Delivery savepoint scenario left unexpected fixture quantity {finalQuantity}.");

        await using (var drop = new SqlCommand($"DROP TABLE {table};", connection) { CommandTimeout = 5 })
        {
            await drop.ExecuteNonQueryAsync();
        }

        Console.WriteLine("PASS: delivery savepoint contract preserves caller transaction; caller rollback restores all fixture writes.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 5 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql, SqlTransaction? transaction = null)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 5 };
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
