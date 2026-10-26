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

        await VerifyApplicationLockSerializesDuplicateCheckAsync(connectionString);
        Console.WriteLine("PASS: delivery savepoint contract preserves caller transaction; caller rollback restores all fixture writes.");

    }

    private static async Task VerifyApplicationLockSerializesDuplicateCheckAsync(string connectionString)
    {
        var table = "##PCTP_QC_DELIVERY_LOCK_" + Guid.NewGuid().ToString("N");
        var resource = "PCTP_QC_DELIVERY_LOCK_" + Guid.NewGuid().ToString("N");
        await using var owner = new SqlConnection(connectionString);
        await owner.OpenAsync();
        await using (var create = new SqlCommand($"CREATE TABLE {table}(DeliveryKey nvarchar(100) NOT NULL PRIMARY KEY);", owner) { CommandTimeout = 5 })
        {
            await create.ExecuteNonQueryAsync();
        }

        await using var first = new SqlConnection(connectionString);
        await first.OpenAsync();
        await using var firstTx = (SqlTransaction)await first.BeginTransactionAsync();
        await AcquireAppLockAsync(first, firstTx, resource);
        await ExecuteAsync(first, firstTx, $"INSERT INTO {table}(DeliveryKey) VALUES(N'QC-DELIVERY-KEY');");

        using var secondStarted = new System.Threading.ManualResetEventSlim(false);
        var secondRequest = Task.Run(async () =>
        {
            await using var second = new SqlConnection(connectionString);
            await second.OpenAsync();
            await using var secondTx = (SqlTransaction)await second.BeginTransactionAsync();
            secondStarted.Set();
            await AcquireAppLockAsync(second, secondTx, resource);
            var existing = await ScalarAsync(second, $"SELECT COUNT(1) FROM {table} WHERE DeliveryKey=N'QC-DELIVERY-KEY';", secondTx);
            await secondTx.CommitAsync();
            return existing;
        });

        if (!secondStarted.Wait(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("Second SQL session did not start the delivery lock request.");
        await Task.Delay(250);
        await firstTx.CommitAsync();

        var existingAfterLock = await secondRequest;
        Require(existingAfterLock == 1,
            $"Second session did not observe the first committed delivery after acquiring the transaction-owned app lock; count={existingAfterLock}.");

        var total = await ScalarAsync(owner, $"SELECT COUNT(1) FROM {table} WHERE DeliveryKey=N'QC-DELIVERY-KEY';");
        Require(total == 1, $"Concurrent duplicate confirmation created {total} rows instead of exactly one.");

        await using var drop = new SqlCommand($"DROP TABLE {table};", owner) { CommandTimeout = 5 };
        await drop.ExecuteNonQueryAsync();
        Console.WriteLine("PASS: two SQL sessions serialize a duplicate delivery key with transaction-owned sp_getapplock; exactly one delivery row remains.");
    }

    private static async Task AcquireAppLockAsync(SqlConnection connection, SqlTransaction transaction, string resource)
    {
        await using var command = new SqlCommand(@"
DECLARE @result int;
EXEC @result = sys.sp_getapplock
    @Resource=@resource,
    @LockMode=N'Exclusive',
    @LockOwner=N'Transaction',
    @LockTimeout=10000;
SELECT @result;", connection, transaction) { CommandTimeout = 12 };
        command.Parameters.AddWithValue("@resource", resource);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync());
        Require(result >= 0, $"SQL application lock failed with result {result}.");
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
