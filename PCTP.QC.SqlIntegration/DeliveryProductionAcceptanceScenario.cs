using System.Data;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Runtime acceptance for the remaining delivery gates. All writes are made
/// inside an outer transaction and rolled back; the fixed QC database is never
/// left with a workflow mutation.
/// </summary>
internal static class DeliveryProductionAcceptanceScenario
{
    private sealed record Candidate(int Stt, string LotExpression, string ItemCode, int Quantity, string Factory, string DeliveryTime);
    private sealed record State(string TempStatus, int LedgerCount, int StockRemaining);

    public static async Task RunAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        // Never wait on locks held by real users of the shared test DB.
        await using (var lockTimeout = new SqlCommand("SET LOCK_TIMEOUT 5000;", connection))
            await lockTimeout.ExecuteNonQueryAsync();
        int? pinnedStt = int.TryParse(Environment.GetEnvironmentVariable("PCTP_QC_DEL_ACCEPTANCE_STT"), out var parsedStt) ? parsedStt : null;
        var candidate = await FindCandidateAsync(connection, pinnedStt);
        if (candidate == null)
            throw new InvalidOperationException(pinnedStt.HasValue
                ? $"DEL runtime acceptance: TMPPHIEUGIAOHANG STT={pinnedStt} is not an eligible pending row (needs unique stocked LOT, enough SLCONLAI, no existing delivery ledger row)."
                : "DEL runtime acceptance could not find a safe pending delivery row. Seed one dedicated pending row and set PCTP_QC_DEL_ACCEPTANCE_STT to its STT.");

        Console.WriteLine($"INFO: DEL runtime candidate STT={candidate.Stt}, LOT={candidate.LotExpression}, quantity={candidate.Quantity}, item={candidate.ItemCode}.");
        await VerifySuccessAndCallerRollbackAsync(connection, candidate);
        await VerifyShortageAndRollbackAsync(connection, candidate);
        await VerifyReplayIsIdempotentAsync(connection, candidate);
        Console.WriteLine("PASS: DEL-003/DEL-004/DEL-007 production procedure acceptance completed with rollback isolation.");
    }

    private static async Task<Candidate?> FindCandidateAsync(SqlConnection connection, int? pinnedStt)
    {
        const string sql = @"
SELECT TOP (1)
    t.STT, RTRIM(t.LOT), RTRIM(t.MAHANG), t.SOLUONG,
    RTRIM(ISNULL(t.NHAMAY,N'')), RTRIM(ISNULL(t.GIOGIAO,N''))
FROM dbo.TMPPHIEUGIAOHANG t
WHERE (@pinned IS NULL OR t.STT = @pinned)
  AND ISNULL(t.LOT,N'') <> N''
  AND ISNULL(t.STATUS,N'') <> N'OK'
  AND ISNULL(t.SOLUONG,0) > 0
  AND CHARINDEX(N',',t.LOT) = 0
  AND CHARINDEX(N'-',t.LOT) > 1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.LUUPHIEUGIAOHANG l
      WHERE RTRIM(ISNULL(l.MAHANG,N'')) = RTRIM(ISNULL(t.MAHANG,N''))
        AND CAST(l.NGAYGIAO AS date) = CAST(t.NGAYGIAO AS date)
        AND RTRIM(ISNULL(l.CUA,N'')) = RTRIM(ISNULL(t.CUA,N''))
        AND RTRIM(ISNULL(l.TRUYEN,N'')) = RTRIM(ISNULL(t.TRUYEN,N''))
        AND l.SOLUONG = t.SOLUONG
        AND RTRIM(ISNULL(l.GIOGIAO,N'')) = RTRIM(ISNULL(t.GIOGIAO,N''))
        AND RTRIM(ISNULL(l.NHAMAY,N'')) = RTRIM(LEFT(ISNULL(t.NHAMAY,N''),50)) )
  -- (no 'l.STT = t.STT' check: TMP.STT restarts every batch and is copied into the ledger, so it is not a unique key;
  --  the 7-field delivery key above is the same identity the procedure uses for its duplicate check.)
  AND EXISTS (
      SELECT 1
      FROM dbo.STOCKTP s
      CROSS APPLY (SELECT LEFT(RTRIM(t.LOT),CHARINDEX(N'-',t.LOT)-1) LotInput) x
      WHERE ((LEN(RTRIM(x.LotInput)) > 13 AND RTRIM(s.LOT)=RTRIM(x.LotInput))
          OR (LEN(RTRIM(x.LotInput)) <= 13
              AND LEFT(RTRIM(s.LOT),13)=LEFT(RTRIM(x.LotInput),13)
              AND (SELECT COUNT(DISTINCT RTRIM(s2.LOT)) FROM dbo.STOCKTP s2
                   WHERE LEFT(RTRIM(s2.LOT),13)=LEFT(RTRIM(x.LotInput),13))=1))
        AND ISNULL(s.SLCONLAI,0) >= t.SOLUONG )
ORDER BY t.NGAYGIAO,t.STT;";

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        command.Parameters.Add(new SqlParameter("@pinned", SqlDbType.Int) { Value = (object?)pinnedStt ?? DBNull.Value });
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new Candidate(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3),
            reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5));
    }

    private static async Task VerifySuccessAndCallerRollbackAsync(SqlConnection connection, Candidate c)
    {
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var before = await ReadStateAsync(connection, tx, c.Stt);
        var result = await ExecuteProcedureAsync(connection, tx, c, c.Quantity);
        Require(result.SuccessCount > 0 && result.ErrorCount == 0, "DEL-003 success candidate did not produce an OK result from the production procedure.");
        var after = await ReadStateAsync(connection, tx, c.Stt);
        Require(after.TempStatus.Equals("OK", StringComparison.OrdinalIgnoreCase), "Successful procedure did not mark TMPPHIEUGIAOHANG OK inside the caller transaction.");
        Require(after.LedgerCount == before.LedgerCount + 1, "Successful procedure did not create exactly one delivery ledger row.");
        Require(after.StockRemaining == before.StockRemaining - c.Quantity, "Successful procedure did not reduce STOCKTP by the requested quantity.");
        await tx.RollbackAsync();

        var restored = await ReadStateAsync(connection, null, c.Stt);
        Require(restored.TempStatus == before.TempStatus && restored.LedgerCount == before.LedgerCount && restored.StockRemaining == before.StockRemaining,
            "Caller rollback did not restore TMP status, delivery ledger and STOCKTP.");
        Console.WriteLine("PASS: DEL-003 production procedure success path and caller-owned rollback are atomic across STOCKTP, LUUPHIEUGIAOHANG and TMPPHIEUGIAOHANG.");
    }

    private static async Task VerifyShortageAndRollbackAsync(SqlConnection connection, Candidate c)
    {
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var before = await ReadStateAsync(connection, tx, c.Stt);
        var shortage = checked(before.StockRemaining + c.Quantity + 1);
        await ExecuteAsync(connection, tx, "UPDATE dbo.TMPPHIEUGIAOHANG SET SOLUONG=@q WHERE STT=@stt", new SqlParameter("@q", shortage), new SqlParameter("@stt", c.Stt));
        var result = await ExecuteProcedureAsync(connection, tx, c, shortage);
        Require(result.SuccessCount == 0 && result.ErrorCount > 0, "DEL-007 shortage did not return an explicit business error.");
        var after = await ReadStateAsync(connection, tx, c.Stt);
        Require(after.StockRemaining == before.StockRemaining && after.LedgerCount == before.LedgerCount && after.TempStatus == before.TempStatus,
            "DEL-007 shortage produced a partial business mutation.");
        await tx.RollbackAsync();
        Console.WriteLine("PASS: DEL-007 insufficient-stock path returned a business error with no partial STOCKTP, delivery-ledger or pending-state mutation.");
    }

    private static async Task VerifyReplayIsIdempotentAsync(SqlConnection connection, Candidate c)
    {
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var before = await ReadStateAsync(connection, tx, c.Stt);
        var first = await ExecuteProcedureAsync(connection, tx, c, c.Quantity);
        Require(first.SuccessCount > 0 && first.ErrorCount == 0, "DEL-004 first confirmation did not succeed.");
        await ExecuteAsync(connection, tx, "UPDATE dbo.TMPPHIEUGIAOHANG SET STATUS=N'PENDING' WHERE STT=@stt", new SqlParameter("@stt", c.Stt));
        var replay = await ExecuteProcedureAsync(connection, tx, c, c.Quantity);
        Require(replay.SuccessCount == 0 && replay.ErrorCount > 0, "DEL-004 replay unexpectedly deducted stock twice.");
        var after = await ReadStateAsync(connection, tx, c.Stt);
        Require(after.LedgerCount == before.LedgerCount + 1, "DEL-004 replay created more than one delivery ledger row.");
        await tx.RollbackAsync();
        var restored = await ReadStateAsync(connection, null, c.Stt);
        Require(restored.StockRemaining == before.StockRemaining && restored.LedgerCount == before.LedgerCount,
            "DEL-004 replay rollback did not restore the original state.");
        Console.WriteLine("PASS: DEL-004 production procedure replay is idempotent; transaction-owned delivery identity prevents a second deduction.");
    }

    private static async Task<(int SuccessCount,int ErrorCount)> ExecuteProcedureAsync(SqlConnection connection, SqlTransaction tx, Candidate c, int quantity)
    {
        // The procedure loads EVERY TMP row whose STATUS is not OK, not only the candidate. Other pending rows
        // (e.g. leftover NG rows with insufficient stock) would add errors and block the stock update, so mask them
        // as OK for this call. This runs inside the caller transaction that every scenario rolls back.
        await ExecuteAsync(connection, tx, "UPDATE dbo.TMPPHIEUGIAOHANG SET STATUS=N'OK' WHERE ISNULL(STT,-1)<>@stt AND (STATUS IS NULL OR STATUS<>N'OK')", new SqlParameter("@stt", c.Stt));
        await ExecuteAsync(connection, tx, "UPDATE dbo.TMPPHIEUGIAOHANG SET SOLUONG=@q WHERE STT=@stt", new SqlParameter("@q", quantity), new SqlParameter("@stt", c.Stt));
        await using var command = new SqlCommand("dbo.Usp_Qrcode_Update_Stock2405", connection, tx)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 30
        };
        command.Parameters.Add(new SqlParameter("@GIOGIAOFCC", c.DeliveryTime));
        command.Parameters.Add(new SqlParameter("@NHAMAY", c.Factory));
        command.Parameters.Add(new SqlParameter("@TMPTABLE", "TMPPHIEUGIAOHANG"));
        command.Parameters.Add(new SqlParameter("@DOCQRTABLE", "DOCQRCODE"));

        var success = 0;
        var errors = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) success++;
        if (await reader.NextResultAsync()) while (await reader.ReadAsync()) errors++;
        return (success, errors);
    }

    private static async Task<State> ReadStateAsync(SqlConnection connection, SqlTransaction? tx, int stt)
    {
        var lotExpression = await ScalarStringAsync(connection, tx, "SELECT RTRIM(LOT) FROM dbo.TMPPHIEUGIAOHANG WHERE STT=@stt", new SqlParameter("@stt", stt)) ?? string.Empty;
        var dash = lotExpression.IndexOf('-');
        var lot = (dash > 0 ? lotExpression[..dash] : lotExpression).Trim();
        var status = await ScalarStringAsync(connection, tx, "SELECT ISNULL(STATUS,N'') FROM dbo.TMPPHIEUGIAOHANG WHERE STT=@stt", new SqlParameter("@stt", stt)) ?? string.Empty;
        var stock = await ScalarIntAsync(connection, tx, @"SELECT ISNULL(SUM(s.SLCONLAI),0)
FROM dbo.STOCKTP s
WHERE RTRIM(s.LOT)=RTRIM(@lot)
   OR (LEN(RTRIM(@lot)) <= 13 AND LEFT(RTRIM(s.LOT),13)=LEFT(RTRIM(@lot),13)
       AND (SELECT COUNT(DISTINCT RTRIM(s2.LOT)) FROM dbo.STOCKTP s2 WHERE LEFT(RTRIM(s2.LOT),13)=LEFT(RTRIM(@lot),13))=1)", new SqlParameter("@lot", lot));
        var ledger = await ScalarIntAsync(connection, tx, @"SELECT COUNT(*) FROM dbo.LUUPHIEUGIAOHANG l JOIN dbo.TMPPHIEUGIAOHANG t ON t.STT=@stt
WHERE RTRIM(l.MAHANG)=RTRIM(t.MAHANG) AND CAST(l.NGAYGIAO AS date)=CAST(t.NGAYGIAO AS date)
  AND RTRIM(ISNULL(l.CUA,N''))=RTRIM(ISNULL(t.CUA,N'')) AND RTRIM(ISNULL(l.TRUYEN,N''))=RTRIM(ISNULL(t.TRUYEN,N''))
  AND l.SOLUONG=t.SOLUONG AND RTRIM(ISNULL(l.GIOGIAO,N''))=RTRIM(ISNULL(t.GIOGIAO,N''))
  AND RTRIM(ISNULL(l.NHAMAY,N''))=RTRIM(LEFT(ISNULL(t.NHAMAY,N''),50))", new SqlParameter("@stt", stt));
        return new State(status, ledger, stock);
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction tx, string sql, params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, tx) { CommandTimeout = 10 };
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(SqlConnection connection, SqlTransaction? tx, string sql, params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, tx) { CommandTimeout = 10 };
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, SqlTransaction? tx, string sql, params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, tx) { CommandTimeout = 10 };
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
