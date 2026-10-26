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
/// (re-run twice to prove idempotency), then are dropped. TMP/DOCQR/LUU*/stock tables are uniquely
/// named ## tempdb tables; TMP uses the DBA-supplied dbo.TMPPHIEUGIAOHANG column set.
/// Only reachable in --engine-only (disposable PCTP_QC_CI); never against SQL 57/14.
/// Stock is mutated by a SQL-backed IStockMovementService fixture (KhoCore itself is covered elsewhere);
/// what is proven here is atomicity/serialization of stock + ledger + QR + TMP + scan rows.
/// </summary>
internal static class DeliveryCommandSqlScenario
{
    private sealed class Fx { public string Tmp = "", Doc = "", LuuP = "", LuuD = "", Stock = "", Slot = ""; }

    public static async Task RunAsync(string connectionString)
    {
        var legacy = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
        var p = "##PCTP_QC_DCMD_" + Guid.NewGuid().ToString("N") + "_";
        var f = new Fx { Tmp = p + "Tmp", Doc = p + "Doc", LuuP = p + "LuuP", LuuD = p + "LuuD", Stock = p + "Stock", Slot = p + "Slot" };

        using var owner = new SqlConnection(legacy);
        await owner.OpenAsync();
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
            await TwoSessionsSameKey_G5(legacy, owner, f);
            await TwoSessionsSameQr_G5(legacy, owner, f);
            Console.WriteLine("PASS: delivery command SQL scenario (P1/P5/P6/P7/P8/P9/P11/P13, two-session same key and same QR).");
        }
        finally
        {
            try { await Exec(owner, "IF OBJECT_ID(N'dbo.DeliveryCommandQr') IS NOT NULL DROP TABLE dbo.DeliveryCommandQr; IF OBJECT_ID(N'dbo.DeliveryCommandLedger') IS NOT NULL DROP TABLE dbo.DeliveryCommandLedger;"); } catch { }
            foreach (var t in new[] { f.Tmp, f.Doc, f.LuuP, f.LuuD, f.Stock, f.Slot })
                try { await Exec(owner, $"IF OBJECT_ID(N'tempdb..[{t}]') IS NOT NULL DROP TABLE [{t}];"); } catch { }
        }
    }

    // ---- scenarios -------------------------------------------------------------------------

    private static async Task Happy_P1_P5_P11_P13(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        var r = Run(conn, f, Cmd("K-HAPPY", new[] { 1, 2 }, Part("A", new[] { "QR-H1" }, ("LOT-1", 101, 3))));
        Require(r.Success && !r.Duplicate && r.CommittedQuantity == 3, "happy path must commit 3: " + r.Message);
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "STOCKTP 10-3.");
        Require(await Scalar(o, $"SELECT Quantity FROM [{f.Slot}] WHERE Id=101") == 7, "SlotLot 10-3 (STOCKTP == SlotLot).");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Tmp}] WHERE STATUS='OK' AND STT IN (1,2)") == 2, "both STT closed (F-Y4).");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuP}]") == 2, "archive rows for both STT.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Doc}] WHERE KETQUA='DG' AND MAHANGFCC='A'") == 0, "P13: DG rows of the PART consumed, incl. other STT.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuD}]") == 2, "P13: DG rows archived.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Doc}] WHERE KETQUA<>'DG'") == 1, "P13: non-DG rows stay.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-HAPPY'") == 1, "one ledger row.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandQr WHERE QrKey='QR-H1'") == 1, "one QR row.");
    }

    private static async Task Replay_P7(string conn, SqlConnection o, Fx f)
    {
        var r = Run(conn, f, Cmd("K-HAPPY", new[] { 1, 2 }, Part("A", new[] { "QR-H1" }, ("LOT-1", 101, 3))));
        Require(r.Success && r.Duplicate && r.CommittedQuantity == 3, "replay must return prior outcome.");
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "replay must not deduct again.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-HAPPY'") == 1, "replay must not add ledger rows.");
    }

    private static async Task SameQrOtherKey_P8(string conn, SqlConnection o, Fx f)
    {
        var r = Run(conn, f, Cmd("K-QR2", new[] { 3 }, Part("B", new[] { "QR-H1" }, ("LOT-1", 101, 2))));
        Require(!r.Success, "QR already delivered under another key must be rejected.");
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "rejected QR must not move stock.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-QR2'") == 0, "no ledger for rejected command.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Tmp}] WHERE STT=3 AND STATUS='NG'") == 1, "STT 3 stays pending.");
    }

    private static async Task PendingMismatchRollsBack_P6_P9(string conn, SqlConnection o, Fx f)
    {
        var r = Run(conn, f, Cmd("K-ROLL", new[] { 3, 99 }, Part("B", new[] { "QR-R1" }, ("LOT-1", 101, 2))));
        Require(!r.Success, "unknown STT 99 must fail the whole command.");
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 7, "stock restored after rollback.");
        Require(await Scalar(o, $"SELECT Quantity FROM [{f.Slot}] WHERE Id=101") == 7, "SlotLot restored after rollback.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.Tmp}] WHERE STT=3 AND STATUS='NG'") == 1, "closed STT 3 restored (F-Y4).");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-ROLL'") == 0, "ledger rolled back.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandQr WHERE QrKey='QR-R1'") == 0, "QR rolled back.");
        Require(await Scalar(o, $"SELECT COUNT(*) FROM [{f.LuuP}]") == 2, "archive insert for STT 3 rolled back.");
    }

    private static async Task TwoSessionsSameKey_G5(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        var cmd = Cmd("K-RACE", new[] { 1 }, Part("A", new[] { "QR-X1" }, ("LOT-1", 101, 4)));
        var res = await Task.WhenAll(Task.Run(() => Run(conn, f, cmd)), Task.Run(() => Run(conn, f, cmd)));
        Require(res.Count(x => x.Success && !x.Duplicate) == 1, "exactly one session commits the same BusinessKey.");
        Require(res.Count(x => x.Success && x.Duplicate) == 1, "the other session returns the prior outcome: " + string.Join("|", res.Select(x => x.Message)));
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 6, "stock deducted exactly once.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandLedger WHERE BusinessKey='K-RACE'") == 1, "one ledger row.");
    }

    private static async Task TwoSessionsSameQr_G5(string conn, SqlConnection o, Fx f)
    {
        await Reseed(o, f);
        var a = Cmd("K-QA", new[] { 1 }, Part("A", new[] { "QR-SAME" }, ("LOT-1", 101, 2)));
        var b = Cmd("K-QB", new[] { 2 }, Part("A", new[] { "QR-SAME" }, ("LOT-1", 101, 2)));
        var res = await Task.WhenAll(Task.Run(() => Run(conn, f, a)), Task.Run(() => Run(conn, f, b)));
        Require(res.Count(x => x.Success) == 1, "same QR under two keys: exactly one commits: " + string.Join("|", res.Select(x => x.Message)));
        Require(await Scalar(o, $"SELECT SLCONLAI FROM [{f.Stock}] WHERE LOT='LOT-1'") == 8, "stock deducted once.");
        Require(await Scalar(o, "SELECT COUNT(*) FROM dbo.DeliveryCommandQr WHERE QrKey='QR-SAME'") == 1, "one QR row.");
    }

    // ---- plumbing ---------------------------------------------------------------------------

    private static DeliveryStockCommandResult Run(string conn, Fx f, DeliveryStockCommand cmd)
    {
        var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = conn };
        using var uow = new UnitOfWork(provider);
        var db = new PhieuSqlExecutor();
        var handler = new DeliveryStockCommandHandler(
            uow, new FixtureStock(uow, f.Stock, f.Slot),
            new DeliveryCommandLedgerRepository(db, uow),
            new DeliveryTmpPendingGate(db, uow, f.Tmp, f.Doc, hasGear: false, archiveDateFormat: "yyyy-MM-dd",
                luuPhieuTable: f.LuuP, luuDocQrTable: f.LuuD));
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
CREATE TABLE [{f.Tmp}](STT smallint NULL, CUA nvarchar(50) NULL, TRUYEN nvarchar(50) NULL, MAHANG nvarchar(50) NULL, TENHANG nvarchar(50) NULL,
  LOT nvarchar(500) NULL, DV nvarchar(50) NULL, SOLUONG smallint NULL, NGAYGIAO smalldatetime NULL, GIOGIAO nvarchar(50) NULL, STATUS nvarchar(50) NULL,
  TTPHIEU nvarchar(50) NULL, NHAMAY varchar(50) NULL, ADDNM int NULL, HOP int NULL, STATUSDOC nchar(50) NULL, Note nvarchar(250) NULL,
  PO_NO nvarchar(200) NULL, PO_ITEM nvarchar(200) NULL);
CREATE TABLE [{f.LuuP}](STT smallint NULL, CUA nvarchar(50) NULL, TRUYEN nvarchar(50) NULL, MAHANG nvarchar(50) NULL, TENHANG nvarchar(50) NULL,
  LOT nvarchar(500) NULL, DV nvarchar(50) NULL, SOLUONG smallint NULL, NGAYGIAO smalldatetime NULL, GIOGIAO nvarchar(50) NULL, STATUS nvarchar(50) NULL,
  GearYMVN nvarchar(50) NULL, NHAMAY nvarchar(200) NULL, GIOGIAOFCC nvarchar(50) NULL, PO_NO nvarchar(200) NULL, TTPHIEU nvarchar(50) NULL);
CREATE TABLE [{f.Doc}](LOTFCC nvarchar(500) NULL, MAHANGFCC nvarchar(100) NULL, SLTEMFCC int NULL, LOTHVN nvarchar(500) NULL, MAHANGHVN nvarchar(100) NULL,
  SLTEMHVN int NULL, STATUS nvarchar(50) NULL, MAFCC nvarchar(100) NULL, STT int NULL, KETQUA nvarchar(20) NULL);
CREATE TABLE [{f.LuuD}](LOTFCC nvarchar(500) NULL, MAHANGFCC nvarchar(100) NULL, SLTEMFCC int NULL, LOTHVN nvarchar(500) NULL, MAHANGHVN nvarchar(100) NULL,
  SLTEMHVN int NULL, STATUS nvarchar(50) NULL, MAFCC nvarchar(100) NULL, STT int NULL, KETQUA nvarchar(20) NULL,
  NGAYXUAT nvarchar(50) NULL, GIOXUAT nvarchar(50) NULL, NHAMAY nvarchar(200) NULL);
CREATE TABLE [{f.Stock}](LOT nvarchar(500) NOT NULL PRIMARY KEY, SLCONLAI int NOT NULL);
CREATE TABLE [{f.Slot}](Id int NOT NULL PRIMARY KEY, Quantity int NOT NULL);");

    private static async Task Reseed(SqlConnection o, Fx f)
    {
        await Exec(o, $@"
DELETE FROM [{f.Tmp}]; DELETE FROM [{f.LuuP}]; DELETE FROM [{f.Doc}]; DELETE FROM [{f.LuuD}]; DELETE FROM [{f.Stock}]; DELETE FROM [{f.Slot}];
DELETE FROM dbo.DeliveryCommandQr; DELETE FROM dbo.DeliveryCommandLedger;
INSERT [{f.Stock}] VALUES (N'LOT-1', 10); INSERT [{f.Slot}] VALUES (101, 10);
INSERT [{f.Tmp}](STT,MAHANG,LOT,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,NHAMAY,PO_NO) VALUES
 (1,N'A',N'LOT-1',3,'2026-10-26','08:00','NG','N1','PO1'),(2,N'A',N'LOT-1',0,'2026-10-26','08:00','NG','N1','PO1'),(3,N'B',N'LOT-1',2,'2026-10-26','08:00','NG','N1','PO2');
INSERT [{f.Doc}](LOTFCC,MAHANGFCC,STT,KETQUA) VALUES (N'LOT-1',N'A',1,'DG'),(N'LOT-1',N'A',2,'DG'),(N'LOT-1',N'A',3,'OK');");
    }

    private static async Task Exec(SqlConnection c, string sql) { using var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 }; await cmd.ExecuteNonQueryAsync(); }
    private static async Task<int> Scalar(SqlConnection c, string sql) { using var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 }; return Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
    private static void Require(bool ok, string msg) { if (!ok) throw new InvalidOperationException("FAIL: delivery command SQL - " + msg); }
}
