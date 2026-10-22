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
        var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        uow.Begin();

        try
        {
            using (var create = uow.Connection.CreateCommand())
            {
                create.Transaction = uow.Transaction;
                create.CommandText = @"
CREATE TABLE #STOCKTP
(
    LOT nvarchar(50) NOT NULL PRIMARY KEY,
    PART nvarchar(50) NULL,
    NAME nvarchar(100) NULL,
    MODEL nvarchar(30) NULL,
    SLNHAP int NULL,
    SLCONLAI int NULL,
    SLXUAT int NULL,
    SATUS smallint NULL,
    CASX smallint NULL,
    NGAYSX datetime NULL,
    NGAYNHAP datetime NULL,
    SLSX int NULL,
    NGAYXUAT datetime NULL
);";
                create.ExecuteNonQuery();
            }

            IStockTpRepository repository = new StockTpRepository(
                new PhieuSqlExecutor(), uow);

            repository.InsertStockTp(new NhapKhoItem
            {
                Lot = "QC-REPO-LOT-0001",
                Part = "PART-QC",
                Name = "QC integration",
                Model = "MODEL-QC",
                CaSX = 2,
                SlSanXuat = 120,
                SlNhap = 40,
                NgaySX = new DateTime(2026, 10, 20)
            }, 0);

            var inserted = repository.GetByLot("QC-REPO-LOT-0001");
            Require(inserted != null, "InsertStockTp then GetByLot returned no row.");
            Require(inserted.SlNhap == 40 && inserted.SlConLai == 40,
                $"InsertStockTp mapped wrong quantities: received={inserted.SlNhap}, remaining={inserted.SlConLai}.");
            Require(repository.ExistsStockTp("QC-REPO-LOT-0001"),
                "ExistsStockTp did not find the inserted LOT.");
            Require(repository.GetSlDaNhap("QC-REPO-LOT-0001") == 40,
                "GetSlDaNhap returned an unexpected quantity.");

            repository.UpdateStockTp("QC-REPO-LOT-0001", 15, 1);
            Require(repository.GetSlConLai("QC-REPO-LOT-0001") == 55,
                "UpdateStockTp did not increment SLCONLAI by the received delta.");

            var batch = repository.GetSlConLaiBatch(new[]
            {
                "QC-REPO-LOT-0001", "QC-REPO-MISSING", "QC-REPO-LOT-0001"
            });
            Require(batch.Count == 1 && batch["QC-REPO-LOT-0001"] == 55,
                "GetSlConLaiBatch returned unexpected LOT values.");

            var current = repository.GetTonKhoHienTai();
            Require(current.Rows.Count == 1 &&
                    Convert.ToInt32(current.Rows[0]["SLCONLAI"]) == 55,
                "GetTonKhoHienTai did not return the active fixture row.");

            uow.Commit();
            Console.WriteLine("PASS: production StockTpRepository CRUD/read/batch methods executed against isolated #STOCKTP.");
        }
        catch
        {
            try { uow.Rollback(); } catch { }
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
        public string Lot { get; set; }
        public string Part { get; set; }
        public string Name { get; set; }
        public string Model { get; set; }
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

