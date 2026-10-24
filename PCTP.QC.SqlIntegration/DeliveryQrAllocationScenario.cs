using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.SqlIntegration;

internal static class DeliveryQrAllocationScenario
{
    private const string Part = "QC-QR-EXACT-PART";

    public static async Task RunAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await CreateFixtureAsync(connection);
        try
        {
            await DeployProcedureAsync(connection);
            await VerifyExactSubsetAsync(connection);
            await VerifyNoExactSubsetLeavesQrUntouchedAsync(connection);
            await VerifyAlreadyAggregatedQrIsExcludedAsync(connection);
            await VerifyOuterTransactionRollbackAsync(connection);
            await VerifyConcurrentClaimIsSingleWinnerAsync(connectionString);
            Console.WriteLine("PASS: production Usp_Qrcode_Take_Lot2405 exact subset, DG exclusion, rollback and concurrency scenarios.");
        }
        finally
        {
            await ExecuteAsync(connection, @"
IF OBJECT_ID(N'dbo.Usp_Qrcode_Take_Lot2405', N'P') IS NOT NULL DROP PROCEDURE dbo.Usp_Qrcode_Take_Lot2405;
IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANG', N'U') IS NOT NULL DROP TABLE dbo.TMPPHIEUGIAOHANG;
IF OBJECT_ID(N'dbo.DOCQRCODE', N'U') IS NOT NULL DROP TABLE dbo.DOCQRCODE;
IF OBJECT_ID(N'dbo.B20ITEM', N'U') IS NOT NULL DROP TABLE dbo.B20ITEM;");
        }
    }

    private static async Task CreateFixtureAsync(SqlConnection connection)
    {
        await ExecuteAsync(connection, @"
IF OBJECT_ID(N'dbo.Usp_Qrcode_Take_Lot2405', N'P') IS NOT NULL DROP PROCEDURE dbo.Usp_Qrcode_Take_Lot2405;
IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANG', N'U') IS NOT NULL DROP TABLE dbo.TMPPHIEUGIAOHANG;
IF OBJECT_ID(N'dbo.DOCQRCODE', N'U') IS NOT NULL DROP TABLE dbo.DOCQRCODE;
IF OBJECT_ID(N'dbo.B20ITEM', N'U') IS NOT NULL DROP TABLE dbo.B20ITEM;
CREATE TABLE dbo.B20ITEM(Id int NOT NULL, code nvarchar(50) NOT NULL);
CREATE TABLE dbo.DOCQRCODE
(
 STT int NOT NULL PRIMARY KEY,
 LOTFCC nvarchar(56) NOT NULL,
 SLTEMFCC int NOT NULL,
 SLTEMHVN int NOT NULL,
 MAHANGFCC nvarchar(50) NOT NULL,
 KETQUA nvarchar(10) NULL,
 STTBAN int NULL,
 GIO int NULL
);
CREATE TABLE dbo.TMPPHIEUGIAOHANG
(
 STT int NOT NULL PRIMARY KEY,
 MAHANG nvarchar(50) NOT NULL,
 SOLUONG int NOT NULL,
 LOT nvarchar(500) NULL,
 TTPHIEU nvarchar(100) NULL,
 STATUSDOC nvarchar(10) NULL,
 STATUS nvarchar(10) NULL
);
INSERT dbo.B20ITEM(Id,code) VALUES(1,N'QC-QR-EXACT-PART');");
    }

    private static async Task DeployProcedureAsync(SqlConnection connection)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Usp_Qrcode_Take_Lot2405.sql");
        if (!File.Exists(path))
            throw new FileNotFoundException("Production SQL procedure source was not copied to the integration output.", path);

        var script = await File.ReadAllTextAsync(path);
        foreach (var raw in Regex.Split(script, @"(?im)^\s*GO\s*$"))
        {
            var batch = raw.Trim();
            if (batch.Length == 0 || batch.StartsWith("USE ", StringComparison.OrdinalIgnoreCase))
                continue;
            await ExecuteAsync(connection, batch);
        }
    }

    private static async Task VerifyExactSubsetAsync(SqlConnection connection)
    {
        await ResetAsync(connection, (101, 100), new[] { (1, "QR-60", 60), (2, "QR-50", 50), (3, "QR-40", 40) });
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var result = await TakeLotAsync(connection, transaction, 101, 100);
        Require(result.Sum(x => x.Fcc) == 100, "Exact subset result did not total 100.");
        Require(result.Select(x => x.Lot).OrderBy(x => x).SequenceEqual(new[] { "QR-40", "QR-60" }),
            "For QR quantities 60,50,40 and target 100, procedure did not select 60+40.");
        await WriteLotAsync(connection, transaction, 101, 100, string.Join(",", result.Select(x => x.Lot + "-" + x.Fcc)));
        await transaction.CommitAsync();

        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE STT IN(1,3) AND KETQUA=N'DG' AND STTBAN=101;") == 2,
            "The exact selected QR rows were not marked DG and linked to STT 101.");
        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE STT=2 AND ISNULL(KETQUA,N'')<>N'DG' AND STTBAN IS NULL;") == 1,
            "The unused QR row was incorrectly consumed.");
        Require(!string.IsNullOrWhiteSpace(await ScalarAsync<string>(connection, "SELECT LOT FROM dbo.TMPPHIEUGIAOHANG WHERE STT=101;")),
            "Selected TMP row did not persist LOT.");
    }

    private static async Task VerifyNoExactSubsetLeavesQrUntouchedAsync(SqlConnection connection)
    {
        await ResetAsync(connection, (102, 100), new[] { (11, "QR-60", 60), (12, "QR-50", 50) });
        var result = await TakeLotAsync(connection, null, 102, 100);
        Require(result.Count == 0, "Procedure returned LOT despite no exact QR subset.");
        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE ISNULL(KETQUA,N'')=N'DG' OR STTBAN IS NOT NULL;") == 0,
            "No-exact-subset path changed QR DG/STTBAN state.");
        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE STT=102 AND ISNULL(STATUSDOC,N'')=N'' AND ISNULL(LOT,N'')=N'';") == 1,
            "No-exact-subset path changed TMP status or LOT.");
    }

    private static async Task VerifyAlreadyAggregatedQrIsExcludedAsync(SqlConnection connection)
    {
        await ResetAsync(connection, (103, 100), new[] { (21, "QR-DG-60", 60), (22, "QR-60", 60), (23, "QR-40", 40) });
        await ExecuteAsync(connection, "UPDATE dbo.DOCQRCODE SET KETQUA=N'DG',STTBAN=999 WHERE STT=21;");
        var result = await TakeLotAsync(connection, null, 103, 100);
        Require(result.Sum(x => x.Fcc) == 100, "Eligible QR rows did not exactly fill the target after excluding DG.");
        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE STT=21 AND STTBAN=999 AND KETQUA=N'DG';") == 1,
            "Previously aggregated QR was overwritten.");
        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE STT IN(22,23) AND KETQUA=N'DG' AND STTBAN=103;") == 2,
            "The two eligible exact-sum QR rows were not claimed.");
    }

    private static async Task VerifyOuterTransactionRollbackAsync(SqlConnection connection)
    {
        await ResetAsync(connection, (104, 100), new[] { (31, "QR-60", 60), (32, "QR-40", 40) });
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var result = await TakeLotAsync(connection, transaction, 104, 100);
        Require(result.Sum(x => x.Fcc) == 100, "Rollback scenario did not allocate exact quantity.");
        await WriteLotAsync(connection, transaction, 104, 100, string.Join(",", result.Select(x => x.Lot + "-" + x.Fcc)));
        await transaction.RollbackAsync();

        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE ISNULL(KETQUA,N'')=N'DG' OR STTBAN IS NOT NULL;") == 0,
            "Outer rollback did not restore QR status and STTBAN.");
        Require(await ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE STT=104 AND ISNULL(STATUSDOC,N'')=N'' AND ISNULL(LOT,N'')=N'';") == 1,
            "Outer rollback did not restore TMP status and LOT.");
    }

    private static async Task VerifyConcurrentClaimIsSingleWinnerAsync(string connectionString)
    {
        await using (var resetConnection = new SqlConnection(connectionString))
        {
            await resetConnection.OpenAsync();
            await ResetAsync(resetConnection, (105, 100), new[] { (41, "QR-60", 60), (42, "QR-40", 40) });
            await ExecuteAsync(resetConnection, "INSERT dbo.TMPPHIEUGIAOHANG(STT,MAHANG,SOLUONG) VALUES(106,N'QC-QR-EXACT-PART',100);");
        }

        var firstAllocated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () =>
        {
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var result = await TakeLotAsync(c, tx, 105, 100);
            Require(result.Sum(x => x.Fcc) == 100, "First concurrent request did not win the QR claim.");
            await WriteLotAsync(c, tx, 105, 100, "CONCURRENT-LOT");
            firstAllocated.SetResult(true);
            await allowFirstCommit.Task;
            await tx.CommitAsync();
        });

        await firstAllocated.Task;
        var second = Task.Run(async () =>
        {
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var result = await TakeLotAsync(c, tx, 106, 100);
            await tx.CommitAsync();
            return result.Count;
        });

        await Task.Delay(300);
        allowFirstCommit.SetResult(true);
        await first;
        var secondRows = await second;
        Require(secondRows == 0, "Second concurrent request reused QR rows already claimed by the first request.");
        Require(await ScalarOnNewConnectionAsync<int>(connectionString,
            "SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE KETQUA=N'DG' AND STTBAN=105;") == 2,
            "Concurrent test did not leave exactly one STT owning both QR rows.");
    }

    private static async Task<List<(string Lot, int Fcc, int Hvn)>> TakeLotAsync(
        SqlConnection connection, SqlTransaction? transaction, int stt, int quantity)
    {
        await using var command = new SqlCommand("dbo.Usp_Qrcode_Take_Lot2405", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 20
        };
        command.Parameters.Add(new SqlParameter("@_MaFCC", System.Data.SqlDbType.NVarChar, 50) { Value = Part });
        command.Parameters.Add(new SqlParameter("@_STTP", System.Data.SqlDbType.Int) { Value = stt });
        command.Parameters.Add(new SqlParameter("@_DeM", System.Data.SqlDbType.Int) { Value = 2 });
        command.Parameters.Add(new SqlParameter("@_SLGIAO", System.Data.SqlDbType.Int) { Value = quantity });
        command.Parameters.Add(new SqlParameter("@DOCQRTABLE", System.Data.SqlDbType.NVarChar, 128) { Value = "DOCQRCODE" });
        command.Parameters.Add(new SqlParameter("@TMPTABLE", System.Data.SqlDbType.NVarChar, 128) { Value = "TMPPHIEUGIAOHANG" });

        var rows = new List<(string Lot, int Fcc, int Hvn)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add((reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
        return rows;
    }

    private static async Task WriteLotAsync(SqlConnection connection, SqlTransaction transaction, int stt, int quantity, string lot)
    {
        await using var command = new SqlCommand(
            "UPDATE dbo.TMPPHIEUGIAOHANG SET LOT=@lot WHERE STT=@stt AND MAHANG=@ma AND SOLUONG=@qty AND ISNULL(LOT,N'')=N'';",
            connection, transaction);
        command.Parameters.AddWithValue("@lot", lot);
        command.Parameters.AddWithValue("@stt", stt);
        command.Parameters.AddWithValue("@ma", Part);
        command.Parameters.AddWithValue("@qty", quantity);
        Require(await command.ExecuteNonQueryAsync() == 1, "Could not persist LOT to exactly one TMP row.");
    }

    private static async Task ResetAsync(SqlConnection connection, (int Stt, int Qty) tmp, (int Stt, string Lot, int Qty)[] qr)
    {
        await ExecuteAsync(connection, "DELETE dbo.DOCQRCODE; DELETE dbo.TMPPHIEUGIAOHANG;");
        await using (var insertTmp = new SqlCommand(
            "INSERT dbo.TMPPHIEUGIAOHANG(STT,MAHANG,SOLUONG) VALUES(@stt,@ma,@qty);", connection))
        {
            insertTmp.Parameters.AddWithValue("@stt", tmp.Stt);
            insertTmp.Parameters.AddWithValue("@ma", Part);
            insertTmp.Parameters.AddWithValue("@qty", tmp.Qty);
            await insertTmp.ExecuteNonQueryAsync();
        }
        foreach (var item in qr)
        {
            await using var insert = new SqlCommand(
                "INSERT dbo.DOCQRCODE(STT,LOTFCC,SLTEMFCC,SLTEMHVN,MAHANGFCC) VALUES(@stt,@lot,@qty,@qty,@ma);", connection);
            insert.Parameters.AddWithValue("@stt", item.Stt);
            insert.Parameters.AddWithValue("@lot", item.Lot);
            insert.Parameters.AddWithValue("@qty", item.Qty);
            insert.Parameters.AddWithValue("@ma", Part);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 20 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 20 };
        return (T)Convert.ChangeType(await command.ExecuteScalarAsync(), typeof(T));
    }

    private static async Task<T> ScalarOnNewConnectionAsync<T>(string connectionString, string sql)
    {
        await using var c = new SqlConnection(connectionString);
        await c.OpenAsync();
        return await ScalarAsync<T>(c, sql);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
