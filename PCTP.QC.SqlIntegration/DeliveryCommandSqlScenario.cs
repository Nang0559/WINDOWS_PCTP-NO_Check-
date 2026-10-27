using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// B0-01 delivery command on a real SQL Server (G3/G5 evidence for the production
/// DeliveryStockCommandHandler + DeliveryCommandLedgerRepository + DeliveryTmpPendingGate).
///
/// Fixture fidelity: the ledger tables come from executing the REAL SQL/PCTP_DeliveryCommandLedger.sql
/// (re-run twice to prove idempotency), then are dropped. TMPPHIEUGIAOHANG / DOCQRCODE / LUUPHIEUGIAOHANG
/// (with the real UQ_LUUPHIEU) / LUUDOCQRCODE are the DB57-derived dbo tables deployed from
/// SQL/QC_Schema/01_Tables.sql; fixture rows are identified by MAHANG 'QCD-%' and removed afterwards.
/// Stock/SlotLot and the GEAR-bearing TMP family are uniquely named ## tempdb tables.
/// Only reachable in --engine-only (disposable PCTP_QC_CI); the scenario itself refuses any other
/// database name, so it can never touch SQL 57/14.
/// Stock is mutated by a SQL-backed IStockMovementService fixture (KhoCore itself is covered elsewhere);
/// what is proven here is atomicity/serialization of stock + ledger + QR + TMP + scan rows.
/// </summary>
internal static class DeliveryCommandSqlScenario
{
    private sealed class Fx { public string Tmp = "", TmpG = "", Doc = "", LuuP = "", LuuD = "", Stock = "", Slot = ""; }

    public static async Task RunAsync(string connectionString)
    {
        var legacy = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
        var p = "##PCTP_QC_DCMD_" + Guid.NewGuid().ToString("N") + "_";
        var f = new Fx { Tmp = "TMPPHIEUGIAOHANG", TmpG = p + "TmpG", Doc = "DOCQRCODE", LuuP = "LUUPHIEUGIAOHANG", LuuD = "LUUDOCQRCODE", Stock = p + "Stock", Slot = p + "Slot" };

        using var owner = new SqlConnection(legacy);
        await owner.OpenAsync();
        var dbName = Convert.ToString(await ScalarObject(owner, "SELECT DB_NAME()"));
        if (!string.Equals(dbName, "PCTP_QC_CI", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Delivery command SQL scenario writes dbo fixtures and runs only on PCTP_QC_CI, not " + dbName + ".");
        if (await Scalar(owner, "SELECT COUNT(*) FROM sys.objects WHERE name IN (N'LUUPHIEUGIAOHANG',N'LUUDOCQRCODE',N'TMPPHIEUGIAOHANG',N'DOCQRCODE') AND type='U'") != 4)
            throw new InvalidOperationException("DB57-derived delivery tables are not deployed (SQL/QC_Schema/01_Tables.sql).");
        try
        {
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "PCTP_DeliveryCommandLedger.sql"));
            await Exec(owner, script);
            await Exec(owner, script); // idempotent re-run
            await CreateFixture(owner, f);

            await Happy_P1_P5_P11_P13(legacy, owner, f);
            await Replay_P7(legacy, owner, f);
            await SameQrOtherKey_P8(legacy, owner, f);
            await PendingMismatchRollsBack_P6_P9(legacy, owner, f);
            await DuplicateLuuPhieuKey_UQ(legacy, owner, f);
            await GearIsPreserved_P11(legacy, owner, f);
            await TwoSessionsSameKey_G5(legacy, owner, f);
            await TwoSessionsSameQr_G5(legacy, owner, f);
            Console.WriteLine("PASS: delivery command SQL scenario (P1/P5/P6/P7/P8/P9/P11/P13, UQ_LUUPHIEU, two-session same key and same QR).");
        }
        finally
        {
            try { await DeleteFixtureRows(owner, f); } catch { }
            try { await Exec(owner, "IF OBJECT_ID(N'dbo.DeliveryCommandQr') IS NOT NULL DROP TABLE dbo.DeliveryCommandQr; IF OBJECT_ID(N'dbo.DeliveryCommandLedger') IS NOT NULL DROP TABLE dbo.DeliveryCommandLedger;"); } catch { }
            foreach (var t in new[] { f.TmpG, f.Stock, f.Slot })
                try { await Exec(owner, $"IF OBJECT_ID(N'tempdb..[{t}]') IS NOT NULL DROP TABLE [{t}];"); } catch { }
        }
    }

    // ---- scenarios -------------------------------------------------------------------------

    private static async Task Happy_P1_P5_P11_P13(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        var r = Run(conn, f, Cmd("K-HAPPY", new[] { 1, 2 }, Part("QCD-A", new[] { "QR-H1" }, ("LOT-1", 101, 3))));
        Require(r.Success && !r.Duplicate && r.CommittedQuantity == 3, "happy path must commit 3: " + r.Message);
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "STOCKTP 10-3.");
        Require(await Scalar(o, $"SELECT Quantity FROM [{f.Slot}] WHERE Id=101") == 7, "SlotLot 10-3 (STOCKTP == SlotLot).");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Tmp}] WHERE STATUS='OK' AND STT IN (1,2)") == 2, "both STT closed (F-Y4).");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuP}] WHERE MAHANG LIKE N'QCD-%'") == 2, "archive rows for both STT.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuP}] WHERE STT=1 AND HOP=5 AND PO_ITEM=N'10' AND PO_NO=N'PO1' AND Note=N'n1' AND STATUSDOC=N'OK'") == 1,
            "HOP/PO_NO/PO_ITEM/Note/STATUSDOC must be preserved in LUUPHIEUGIAOHANG.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuD}] WHERE NGAYXUAT = '2026-10-26' AND MAHANGFCC LIKE N'QCD-%'") == 2, "NGAYXUAT is a typed smalldatetime value.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Doc}] WHERE KETQUA='DG' AND MAHANGFCC='QCD-A'") == 0, "P13: DG rows of the PART consumed, incl. other STT.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuD}] WHERE MAHANGFCC LIKE N'QCD-%'") == 2, "P13: DG rows archived.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Doc}] WHERE KETQUA<>'DG' AND MAHANGFCC LIKE 'QCD-%'") == 1, "P13: non-DG rows stay.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-HAPPY'") == 1, "one ledger row.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandQr WHERE QrKey='QR-H1'") == 1, "one QR row.");
    }

    private static async Task Replay_P7(string conn, SqlConnection o, Fx f)
    {
        var r = Run(conn, f, Cmd("K-HAPPY", new[] { 1, 2 }, Part("QCD-A", new[] { "QR-H1" }, ("LOT-1", 101, 3))));
        Require(r.Success && r.Duplicate && r.CommittedQuantity == 3, "replay must return prior outcome.");
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "replay must not deduct again.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-HAPPY'") == 1, "replay must not add ledger rows.");
    }

    private static async Task SameQrOtherKey_P8(string conn, SqlConnection o, Fx f)
    {
        var r = Run(conn, f, Cmd("K-QR2", new[] { 3 }, Part("QCD-B", new[] { "QR-H1" }, ("LOT-1", 101, 2))));
        Require(!r.Success, "QR already delivered under another key must be rejected.");
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "rejected QR must not move stock.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-QR2'") == 0, "no ledger for rejected command.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Tmp}] WHERE STT=3 AND STATUS='NG'") == 1, "STT 3 stays pending.");
    }

    private static async Task PendingMismatchRollsBack_P6_P9(string conn, SqlConnection o, Fx f)
    {
        var r = Run(conn, f, Cmd("K-ROLL", new[] { 3, 99 }, Part("QCD-B", new[] { "QR-R1" }, ("LOT-1", 101, 2))));
        Require(!r.Success, "unknown STT 99 must fail the whole command.");
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "stock restored after rollback.");
        Require(await Scalar(o, $"SELECT Quantity FROM [{f.Slot}] WHERE Id=101") == 7, "SlotLot restored after rollback.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Tmp}] WHERE STT=3 AND STATUS='NG'") == 1, "closed STT 3 restored (F-Y4).");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-ROLL'") == 0, "ledger rolled back.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandQr WHERE QrKey='QR-R1'") == 0, "QR rolled back.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuP}] WHERE MAHANG LIKE N'QCD-%'") == 2, "archive insert for STT 3 rolled back.");
    }

    private static async Task DuplicateLuuPhieuKey_UQ(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        // Same 8 UQ_LUUPHIEU columns as TMP STT 3 (NULL CUA/TRUYEN count as equal in a unique index).
        await Exec(o, $"INSERT [{f.LuuP}](STT,MAHANG,NGAYGIAO,SOLUONG,GIOGIAO,NHAMAY) VALUES (3,N'QCD-B','2026-10-26',2,N'08:00',N'N1');");
        var r = Run(conn, f, Cmd("K-DUP", new[] { 3 }, Part("QCD-B", new[] { "QR-D1" }, ("LOT-1", 101, 2))));
        Require(!r.Success && (r.Message ?? "").Contains("UQ_LUUPHIEU"), "duplicate 8-column key must fail naming UQ_LUUPHIEU: " + r.Message);
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 10, "stock restored after UQ violation.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Tmp}] WHERE STT=3 AND STATUS='NG'") == 1, "TMP stays pending after UQ violation.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-DUP'") == 0, "no ledger after UQ violation.");
        // Same STT but a different key column (SOLUONG) is a different business row and must be allowed.
        await Exec(o, $"DELETE FROM [{f.LuuP}] WHERE MAHANG LIKE N'QCD-%'; INSERT [{f.LuuP}](STT,MAHANG,NGAYGIAO,SOLUONG,GIOGIAO,NHAMAY) VALUES (3,N'QCD-B','2026-10-26',99,N'08:00',N'N1');");
        var ok = Run(conn, f, Cmd("K-DUP2", new[] { 3 }, Part("QCD-B", new[] { "QR-D2" }, ("LOT-1", 101, 2))));
        Require(ok.Success, "STT is not a unique key: differing SOLUONG must be accepted: " + ok.Message);
    }

    private static async Task GearIsPreserved_P11(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        var r = Run(conn, f, Cmd("K-GEAR", new[] { 7 }, Part("QCD-C", new[] { "QR-G1" }, ("LOT-1", 101, 1))), f.TmpG);
        Require(r.Success, "gear command must commit: " + r.Message);
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuP}] WHERE STT=7 AND GearYMVN=N'G1'") == 1, "TMP.GEAR must reach LUUPHIEUGIAOHANG.GearYMVN.");
    }

    private static async Task TwoSessionsSameKey_G5(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        var cmd = Cmd("K-RACE", new[] { 1 }, Part("QCD-A", new[] { "QR-X1" }, ("LOT-1", 101, 4)));
        var res = await Task.WhenAll(Task.Run(() => Run(conn, f, cmd)), Task.Run(() => Run(conn, f, cmd)));
        Require(res.Count(x => x.Success && !x.Duplicate) == 1, "exactly one session commits the same BusinessKey.");
        Require(res.Count(x => x.Success && x.Duplicate) == 1, "the other session returns the prior outcome: " + string.Join("|", res.Select(x => x.Message)));
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 6, "stock deducted exactly once.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-RACE'") == 1, "one ledger row.");
    }

    private static async Task TwoSessionsSameQr_G5(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        var a = Cmd("K-QA", new[] { 1 }, Part("QCD-A", new[] { "QR-SAME" }, ("LOT-1", 101, 2)));
        var b = Cmd("K-QB", new[] { 2 }, Part("QCD-A", new[] { "QR-SAME" }, ("LOT-1", 101, 2)));
        var res = await Task.WhenAll(Task.Run(() => Run(conn, f, a)), Task.Run(() => Run(conn, f, b)));
        Require(res.Count(x => x.Success) == 1, "same QR under two keys: exactly one commits: " + string.Join("|", res.Select(x => x.Message)));
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 8, "stock deducted once.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandQr WHERE QrKey='QR-SAME'") == 1, "one QR row.");
    }

    // ---- plumbing ---------------------------------------------------------------------------

    private static DeliveryStockCommandResult Run(string conn, Fx f, DeliveryStockCommand cmd, string? tmpOverride = null)
    {
        var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = conn };
        using var uow = new UnitOfWork(provider);
        var db = new PhieuSqlExecutor();
        var handler = new DeliveryStockCommandHandler(
            uow, new FixtureStock(uow, f.Stock, f.Slot),
            new DeliveryCommandLedgerRepository(db, uow),
            new DeliveryTmpPendingGate(db, uow, tmpOverride ?? f.Tmp, f.Doc, luuPhieuTable: f.LuuP, luuDocQrTable: f.LuuD));
        return handler.Execute(cmd);
    }

    private sealed class FixtureStock : IStockMovementService
    {
        private readonly IUnitOfWork _uow; private readonly string _stock, _slot;
        public FixtureStock(IUnitOfWork uow, string stock, string slot) { _uow = uow; _stock = stock; _slot = slot; }
        public StockMovementResult Export(StockMovementRequest r)
        {
            int a = Upd($"UPDATE [{_slot}] SET Quantity = Quantity - @q WHERE Id=@id AND Quantity >= @q", r, r.SlotLotId!.Value);
            if (a != 1) return StockMovementResult.Fail("SlotLot không đủ.");
            int b = Upd($"UPDATE [{_stock}] SET SLCONLAI = SLCONLAI - @q WHERE LOT=@lot AND SLCONLAI >= @q", r, 0);
            return b == 1 ? StockMovementResult.Ok() : StockMovementResult.Fail("STOCKTP không đủ.");
        }
        private int Upd(string sql, StockMovementRequest r, int id)
        {
            using var c = new SqlCommand(sql, _uow.Connection, _uow.Transaction);
            c.Parameters.AddWithValue("@q", r.Quantity); c.Parameters.AddWithValue("@id", id); c.Parameters.AddWithValue("@lot", r.LotNo);
            return c.ExecuteNonQuery();
        }
        public StockMovementResult Receive(StockMovementRequest r) => throw new NotSupportedException();
        public StockMovementResult Reserve(StockMovementRequest r) => throw new NotSupportedException();
        public StockMovementResult Pick(StockMovementRequest r) => throw new NotSupportedException();
        public StockMovementResult Move(StockMovementRequest r) => throw new NotSupportedException();
        public StockMovementResult ReturnFromRework(StockMovementRequest r) => throw new NotSupportedException();
        public StockMovementResult Correct(StockMovementRequest r) => throw new NotSupportedException();
        public StockMovementResult RestorePicked(int s, StockSlotLot l) => throw new NotSupportedException();
    }

    private static DeliveryPartAllocation Part(string part, string[] qr, params (string lot, int slotLot, int qty)[] lots) =>
        new(part, "M", "", lots.Select((l, i) => new DeliveryLotAllocation(l.lot, l.lot, l.slotLot, l.qty, i)), qr);
    private static DeliveryStockCommand Cmd(string key, int[] stt, params DeliveryPartAllocation[] parts) =>
        new(key, "HVN", "PGH-1", "qc", new DateTime(2026, 10, 26), parts, stt);

    private static async Task CreateFixture(SqlConnection o, Fx f) => await Exec(o, $@"
-- GEAR-bearing TMP family (YMVN style) derived from the real DB57-derived table + GEAR
SELECT TOP 0 * INTO [{f.TmpG}] FROM dbo.TMPPHIEUGIAOHANG;
ALTER TABLE [{f.TmpG}] ADD GEAR nvarchar(160) NULL;
CREATE TABLE [{f.Stock}](LOT nvarchar(500) NOT NULL PRIMARY KEY, SLCONLAI int NOT NULL);
CREATE TABLE [{f.Slot}](Id int NOT NULL PRIMARY KEY, Quantity int NOT NULL);");

    private static async Task DeleteFixtureRows(SqlConnection o, Fx f) => await Exec(o, $@"
DELETE FROM dbo.LUUDOCQRCODE WHERE MAHANGFCC LIKE N'QCD-%';
DELETE FROM dbo.LUUPHIEUGIAOHANG WHERE MAHANG LIKE N'QCD-%';
DELETE FROM dbo.DOCQRCODE WHERE MAHANGFCC LIKE N'QCD-%';
DELETE FROM dbo.TMPPHIEUGIAOHANG WHERE MAHANG LIKE N'QCD-%';");

    private static async Task Reseed(SqlConnection o, Fx f)
    {
        await DeleteFixtureRows(o, f);
        await Exec(o, $@"
DELETE FROM [{f.TmpG}]; DELETE FROM [{f.Stock}]; DELETE FROM [{f.Slot}];
DELETE FROM dbo.DeliveryCommandQr; DELETE FROM dbo.DeliveryCommandLedger;
INSERT [{f.Stock}] VALUES (N'LOT-1', 10); INSERT [{f.Slot}] VALUES (101, 10);
INSERT [{f.Tmp}](STT,MAHANG,LOT,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,NHAMAY,PO_NO,PO_ITEM,HOP,Note,STATUSDOC) VALUES
 (1,N'QCD-A',N'LOT-1',3,'2026-10-26','08:00','NG','N1','PO1',N'10',5,N'n1',N'OK'),(2,N'QCD-A',N'LOT-1',0,'2026-10-26','08:00','NG','N1','PO1',N'20',5,N'n2',N'OK'),(3,N'QCD-B',N'LOT-1',2,'2026-10-26','08:00','NG','N1','PO2',N'30',1,N'n3',N'OK');
INSERT [{f.TmpG}](STT,MAHANG,LOT,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,NHAMAY,PO_NO,GEAR) VALUES (7,N'QCD-C',N'LOT-1',1,'2026-10-26','09:00','NG','N1','PO7',N'G1');
INSERT [{f.Doc}](LOTFCC,MAHANGFCC,STT,KETQUA) VALUES (N'LOT-1',N'QCD-A',1,'DG'),(N'LOT-1',N'QCD-A',2,'DG'),(N'LOT-1',N'QCD-A',3,'OK');");
    }

    private static async Task Exec(SqlConnection c, string sql) { using var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 }; await cmd.ExecuteNonQueryAsync(); }
    private static async Task<object?> ScalarObject(SqlConnection c, string sql) { using var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 }; return await cmd.ExecuteScalarAsync(); }
    private static async Task<int> Scalar(SqlConnection c, string sql) { using var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 }; return Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
    private static void Require(bool ok, string msg) { if (!ok) throw new InvalidOperationException("FAIL: delivery command SQL - " + msg); }
}
