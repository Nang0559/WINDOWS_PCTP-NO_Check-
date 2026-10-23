using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Models;

namespace PCTP.QC.SqlIntegration
{
internal static class ProductionStockTpRepositoryScenario
{
    public static void Run(string connectionString)
    {
        connectionString = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
        var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        var fixtureLot = "QC-REPO-" + Guid.NewGuid().ToString("N");
        uow.Begin();

        try
        {
            // Session-local fixture on the exact UnitOfWork connection. Never write to
            // shared dbo business tables during the repository integration test.
            using (var createFixture = new SqlCommand(@"
CREATE TABLE #STOCKTP
(
    LOT nvarchar(100) NOT NULL PRIMARY KEY,
    MODEL nvarchar(100) NULL,
    PART nvarchar(100) NULL,
    NAME nvarchar(200) NULL,
    CASX smallint NULL,
    NGAYSX datetime NULL,
    SLSX int NULL,
    NGAYNHAP datetime NULL,
    SLNHAP int NULL,
    NGAYXUAT datetime NULL,
    SLXUAT int NULL,
    SLCONLAI int NULL,
    SATUS smallint NULL
);", uow.Connection, uow.Transaction))
            {
                createFixture.CommandTimeout = 5;
                createFixture.ExecuteNonQuery();
            }

            IStockTpRepository repository = new StockTpRepository(
                new PhieuSqlExecutor(), uow, "#STOCKTP");

            repository.InsertStockTp(new NhapKhoItem
            {
                Lot = fixtureLot,
                Part = "PART-QC",
                Name = "QC integration",
                Model = "MODEL-QC",
                CaSX = 2,
                SlSanXuat = 120,
                SlNhap = 40,
                NgaySX = new DateTime(2026, 10, 20)
            }, 0);

            var inserted = repository.GetByLot(fixtureLot);
            if (inserted is null)
                throw new InvalidOperationException("InsertStockTp then GetByLot returned no row from session-local #STOCKTP.");
            Require(inserted.SlNhap == 40 && inserted.SlConLai == 40,
                $"InsertStockTp mapped wrong quantities: received={inserted.SlNhap}, remaining={inserted.SlConLai}.");
            Require(repository.ExistsStockTp(fixtureLot),
                "ExistsStockTp did not find the inserted test LOT in session-local #STOCKTP.");
            Require(repository.GetSlDaNhap(fixtureLot) == 40,
                "GetSlDaNhap returned an unexpected quantity.");
            Console.WriteLine("PASS: session-local #STOCKTP insert / GetByLot / ExistsStockTp / GetSlDaNhap (uncommitted transaction).");

            repository.UpdateStockTp(fixtureLot, 15, 1);
            Require(repository.GetSlConLai(fixtureLot) == 55,
                "UpdateStockTp did not increment SLCONLAI by the received delta.");

            var batch = repository.GetSlConLaiBatch(new[] { fixtureLot, "QC-REPO-MISSING", fixtureLot });
            Require(batch.Count == 1 && batch[fixtureLot] == 55,
                "GetSlConLaiBatch returned unexpected LOT values.");
            Console.WriteLine("PASS: real dbo.STOCKTP update delta and batch lookup.");

            var current = repository.GetTonKhoHienTai();
            var currentFixtureRows = current.AsEnumerable()
                .Where(row => string.Equals(Convert.ToString(row["LOT"]), fixtureLot,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            Require(currentFixtureRows.Count == 1 &&
                    Convert.ToInt32(currentFixtureRows[0]["SLCONLAI"]) == 55,
                "GetTonKhoHienTai did not return exactly one active test LOT with SLCONLAI=55. " +
                $"Rows={currentFixtureRows.Count}; quantities=" +
                string.Join(",", currentFixtureRows.Select(row => Convert.ToString(row["SLCONLAI"]))));

            var remainingLots = repository.GetDanhSachLotConTon();
            var remainingFixtureLots = remainingLots
                .Where(item => string.Equals(item.Lot, fixtureLot, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Require(remainingFixtureLots.Count == 1 && remainingFixtureLots[0].SlConLai == 55,
                "GetDanhSachLotConTon did not return exactly one test LOT with SLCONLAI=55. " +
                "Rows=" + remainingFixtureLots.Count +
                "; quantities=" + string.Join(",", remainingFixtureLots.Select(item => item.SlConLai)));
            Console.WriteLine("PASS: real dbo.STOCKTP current-stock and remaining-LOT list projections.");

            var selectedLots = repository.GetTonKhoTheoLot(new List<string> { fixtureLot });
            Require(selectedLots.Rows.Count == 1, "GetTonKhoTheoLot did not return the requested test LOT.");
            Require(repository.GetTonKhoTheoLot(null!).Rows.Count == 0,
                "GetTonKhoTheoLot(null) should return an empty table.");
            Console.WriteLine("PASS: real dbo.STOCKTP selected-LOT query and null-input contract.");

            bool missingLotRejected = false;
            try { repository.UpdateStockTp("QC-REPO-MISSING-" + Guid.NewGuid().ToString("N"), 1, 0); }
            catch (InvalidOperationException) { missingLotRejected = true; }
            Require(missingLotRejected, "UpdateStockTp must reject a LOT that does not exist.");
            Console.WriteLine("PASS: update of a missing LOT is rejected.");

            // Deliberately rollback rather than commit: test real constraints and SQL,
            // but leave the shared SQL 57 business rows unchanged.
            uow.Rollback();
            Console.WriteLine("PASS: session-local #STOCKTP checks rolled back; no shared business row was written.");

            using (var verifyConnection = new SqlConnection(connectionString))
            using (var verify = new SqlCommand("SELECT COUNT(1) FROM dbo.STOCKTP WHERE LOT = @lot;", verifyConnection))
            {
                verify.Parameters.Add(new SqlParameter("@lot", fixtureLot));
                verify.CommandTimeout = 5;
                verifyConnection.Open();
                var count = Convert.ToInt32(verify.ExecuteScalar());
                Require(count == 0, "Safety verification failed: test LOT still exists in dbo.STOCKTP after rollback.");
            }
            Console.WriteLine("PASS: rollback verification confirmed no test LOT remains in dbo.STOCKTP.");
        }
        catch
        {
            try { if (uow.IsInTransaction) uow.Rollback(); } catch { }
            throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

}

// Minimal compile-time DTOs isolate the production repository from unrelated
// desktop/UI model dependencies. SQL behavior and repository implementation are production code.
namespace PCTP.Shared.Models
{
    public sealed class StockItem
    {
        public string Lot { get; set; } = string.Empty;
        public string Part { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int? SlNhap { get; set; }
        public int? SlConLai { get; set; }
        public int? SlXuat { get; set; }
        public short? Satus { get; set; }
        public short? CaSX { get; set; }
        public DateTime? NgaySX { get; set; }
        public DateTime? NgayNhap { get; set; }
    }

    public sealed class NhapKhoItem
    {
        public string Lot { get; set; }
        public string Model { get; set; }
        public string Part { get; set; }
        public string Name { get; set; }
        public short CaSX { get; set; }
        public int SlSanXuat { get; set; }
        public int SlNhap { get; set; }
        public DateTime? NgaySX { get; set; }
    }
}

