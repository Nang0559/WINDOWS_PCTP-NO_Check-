using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Shared.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration
{
internal static class ProductionBulkStockSlotRepositoryScenario
{
    private static readonly string[] FixtureTables = { "Warehouse", "Rack", "Slot", "SlotLot" };

    public static void Run(string connectionString)
    {
        EnsureFixtureNamesAreUnused(connectionString);
        CreateFixture(connectionString);

        try
        {
            int slotId;
            var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = connectionString };
            using (var uow = new UnitOfWork(provider))
            {
                uow.Begin();
                try
                {
                    var repository = new BulkStockSlotRepository(new PhieuSqlExecutor(), uow);
                    slotId = repository.GetOrCreateVirtualSlotId("QC-WH", "QC-RACK", 1000);
                    Require(slotId > 0, "GetOrCreateVirtualSlotId did not create a Slot.");

                    repository.AddLot(slotId, new StockSlotLot
                    {
                        LotNo = "26080700748100851600",
                        ItemCode = "QC-PART",
                        Quantity = 40,
                        TemCode = "QC-TEM-001",
                        RawQr = "QC-QR-001",
                        ImportDate = new DateTime(2026, 10, 20),
                        PhieuStatus = 0
                    });
                    repository.AddLot(slotId, new StockSlotLot
                    {
                        LotNo = "26080700748100851600",
                        ItemCode = "QC-PART",
                        Quantity = 25,
                        TemCode = "QC-TEM-002",
                        RawQr = "QC-QR-002",
                        ImportDate = new DateTime(2026, 10, 21),
                        PhieuStatus = 0
                    });

                    Require(repository.GetLotQuantity(1) == 40, "First inserted SlotLot quantity mismatch.");
                    Require(repository.GetPhieuStatus(1) == 0, "Active SlotLot status mismatch.");
                    Require(repository.GetLotDetails(2)?.TemCode == "QC-TEM-002", "SlotLot metadata lookup mismatch.");
                    Require(repository.GetSlotId(1) == slotId, "SlotLot to Slot relationship mismatch.");

                    var take = repository.TakeLot(slotId, "26080700748100851600", "QC-PART", 50);
                    Require(take.Quantity == 50 && take.ExportLots.Count == 2,
                        "TakeLot did not split consumption across FIFO rows.");
                    Require(take.ExportLots[0].Quantity == 40 && take.ExportLots[1].Quantity == 10,
                        "TakeLot did not consume oldest ImportDate first.");
                    Require(repository.GetLotQuantity(1) == 0 && repository.GetLotQuantity(2) == 15,
                        "TakeLot left unexpected per-row quantities.");

                    var lots = repository.GetLots(slotId);
                    Require(lots.Count == 2 && lots.Sum(x => x.Quantity) == 15 &&
                            lots.Any(x => x.Quantity == 15 && x.TemCode == "QC-TEM-002"),
                        "GetLots did not preserve remaining LOT metadata and quantities.");
                    uow.Commit();
                }
                catch
                {
                    try { uow.Rollback(); } catch { }
                    throw;
                }
            }

            Require(Convert.ToInt32(Scalar(connectionString,
                    "SELECT Quantity FROM Slot WHERE SlotId=@id",
                    new SqlParameter("@id", slotId))) == 15,
                "Slot header quantity was not recalculated from active SlotLot rows.");

            // Cross-repository delivery rollback: a STOCKTP decrement and the actual
            // BulkStockSlotRepository.TakeLot must enlist in the same production UoW.
            RunSharedStockTpAndSlotRollback(connectionString, slotId);

            // Failure injection: repository writes are enlisted in the active production UoW.
            using (var uow = new UnitOfWork(provider))
            {
                uow.Begin();
                try
                {
                    var repository = new BulkStockSlotRepository(new PhieuSqlExecutor(), uow);
                    repository.AddLot(slotId, new StockSlotLot
                    {
                        LotNo = "26080700748100851600",
                        ItemCode = "QC-PART",
                        Quantity = 7,
                        TemCode = "QC-TEM-ROLLBACK",
                        RawQr = "QC-QR-ROLLBACK",
                        ImportDate = new DateTime(2026, 10, 22),
                        PhieuStatus = 0
                    });
                    throw new InvalidOperationException("QC injected failure after production AddLot.");
                }
                catch (InvalidOperationException ex) when (ex.Message == "QC injected failure after production AddLot.")
                {
                    uow.Rollback();
                }
            }

            Require(Convert.ToInt32(Scalar(connectionString,
                    "SELECT COUNT(1) FROM SlotLot WHERE TemCode=@tem",
                    new SqlParameter("@tem", "QC-TEM-ROLLBACK"))) == 0,
                "Production BulkStockSlotRepository AddLot was not rolled back.");
            Require(Convert.ToInt32(Scalar(connectionString,
                    "SELECT Quantity FROM Slot WHERE SlotId=@id",
                    new SqlParameter("@id", slotId))) == 15,
                "Slot header was not rolled back with SlotLot.");
            Console.WriteLine("PASS: production BulkStockSlotRepository add/FIFO-take/header-refresh/rollback executed against disposable fixture tables.");
        }
        finally
        {
            DropFixture(connectionString);
        }
    }

    private static void RunSharedStockTpAndSlotRollback(string cs, int slotId)
    {
        var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = cs };
        using var uow = new UnitOfWork(provider);
        var bulk = new BulkStockSlotRepository(new PhieuSqlExecutor(), uow);
        IStockTpRepository stock = new StockTpRepository(new PhieuSqlExecutor(), uow);

        // A connection-local STOCKTP fixture avoids touching any production table.
        uow.Begin();
        try
        {
            using (var command = uow.Connection.CreateCommand())
            {
                command.Transaction = uow.Transaction;
                command.CommandText = @"
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
                command.ExecuteNonQuery();
            }

            stock.InsertStockTp(new NhapKhoItem
            {
                Lot = "QC-DELIVERY-ATOMIC-LOT",
                Part = "QC-PART",
                Name = "QC delivery atomicity",
                Model = "QC",
                CaSX = 1,
                SlSanXuat = 60,
                SlNhap = 60,
                NgaySX = new DateTime(2026, 10, 22)
            }, 0);
            uow.Commit();
        }
        catch
        {
            try { uow.Rollback(); } catch { }
            throw;
        }

        uow.Begin();
        try
        {
            using (var command = uow.Connection.CreateCommand())
            {
                command.Transaction = uow.Transaction;
                command.CommandText = @"
UPDATE #STOCKTP
SET SLCONLAI = SLCONLAI - 5, SLXUAT = ISNULL(SLXUAT, 0) + 5
WHERE LOT = @lot AND SLCONLAI >= 5;";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@lot";
                parameter.Value = "QC-DELIVERY-ATOMIC-LOT";
                command.Parameters.Add(parameter);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("Could not apply the synthetic STOCKTP delivery decrement.");
            }

            var take = bulk.TakeLot(slotId, "26080700748100851600", "QC-PART", 5);
            Require(take.Quantity == 5, "Production BulkStockSlotRepository did not pick the expected delivery quantity.");

            // Model the remaining delivery-finalization writes in the same UoW.
            // These columns are isolated fixture counters, not production tables;
            // this proves transaction enlistment only, not production repository SQL.
            using (var finalize = uow.Connection.CreateCommand())
            {
                finalize.Transaction = uow.Transaction;
                finalize.CommandText = @"
UPDATE #STOCKTP SET SLCONLAI = SLCONLAI
WHERE LOT = @lot;
UPDATE dbo.Slot SET Quantity = Quantity
WHERE SlotId = @slotId;";
                var lotParameter = finalize.CreateParameter();
                lotParameter.ParameterName = "@lot";
                lotParameter.Value = "QC-DELIVERY-ATOMIC-LOT";
                finalize.Parameters.Add(lotParameter);
                var slotParameter = finalize.CreateParameter();
                slotParameter.ParameterName = "@slotId";
                slotParameter.Value = slotId;
                finalize.Parameters.Add(slotParameter);
                finalize.ExecuteNonQuery();
            }

            throw new InvalidOperationException("QC injected cross-repository delivery failure.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "QC injected cross-repository delivery failure.")
        {
            uow.Rollback();
        }
        catch
        {
            try { uow.Rollback(); } catch { }
            throw;
        }

        var stockAfterRollback = stock.GetByLot("QC-DELIVERY-ATOMIC-LOT");
        Require(stockAfterRollback != null && stockAfterRollback.SlConLai == 60,
            "STOCKTP decrement was not rolled back with the SlotLot movement.");
        Require(bulk.GetLotQuantity(1) == 0 && bulk.GetLotQuantity(2) == 15,
            "SlotLot rows were not restored by the shared delivery rollback.");
        Require(bulk.GetLots(slotId).Sum(x => x.Quantity) == 15,
            "Slot header/active SlotLot quantity changed after cross-repository rollback.");
        Console.WriteLine("PASS: production UnitOfWork rolled back STOCKTP and production BulkStockSlotRepository.TakeLot as one delivery unit.");
    }

    private static void EnsureFixtureNamesAreUnused(string cs)
    {
        using var connection = new SqlConnection(cs);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name IN ('Warehouse','Rack','Slot','SlotLot')";
        using var reader = command.ExecuteReader();
        if (reader.Read())
            throw new InvalidOperationException("Bulk repository fixture requires an empty TEST database: dbo.Warehouse/dbo.Rack/dbo.Slot/dbo.SlotLot already exists. Use a dedicated disposable database; existing tables will never be modified.");
    }

    private static void CreateFixture(string cs)
    {
        using var connection = new SqlConnection(cs);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
BEGIN TRANSACTION;
BEGIN TRY
    CREATE TABLE dbo.Warehouse (WarehouseId int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL);
    CREATE TABLE dbo.Rack (RackId int IDENTITY(1,1) NOT NULL PRIMARY KEY, WarehouseId int NOT NULL, RackName nvarchar(100) NOT NULL);
    CREATE TABLE dbo.Slot (SlotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, RackId int NOT NULL, SlotNumber int NOT NULL, IsOccupied bit NOT NULL DEFAULT(0), Capacity int NOT NULL, Quantity int NOT NULL DEFAULT(0), ItemCode nvarchar(100) NULL, ImportDate datetime NULL);
    CREATE TABLE dbo.SlotLot (SlotLotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, SlotId int NOT NULL, ItemCode nvarchar(100) NULL, LotNo nvarchar(100) NOT NULL, Quantity int NOT NULL, TemCode nvarchar(100) NULL, QrData nvarchar(500) NULL, MaPhieu nvarchar(100) NULL, ImportDate datetime NULL, PhieuStatus tinyint NOT NULL);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;";
        command.ExecuteNonQuery();
    }

    private static void DropFixture(string cs)
    {
        using var connection = new SqlConnection(cs);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
IF OBJECT_ID('dbo.SlotLot','U') IS NOT NULL DROP TABLE dbo.SlotLot;
IF OBJECT_ID('dbo.Slot','U') IS NOT NULL DROP TABLE dbo.Slot;
IF OBJECT_ID('dbo.Rack','U') IS NOT NULL DROP TABLE dbo.Rack;
IF OBJECT_ID('dbo.Warehouse','U') IS NOT NULL DROP TABLE dbo.Warehouse;";
        command.ExecuteNonQuery();
    }

    private static object Scalar(string cs, string sql, params SqlParameter[] parameters)
    {
        using var connection = new SqlConnection(cs);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return command.ExecuteScalar();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

}

namespace PCTP.Modules.KhoVatLy.Kho.Models
{
    public sealed class LotInfo
    {
        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
        public string TemCode { get; set; }
        public string RawQr { get; set; }
        public PCTP.Shared.Models.QRCodeInfo QRInfo { get; set; }
        public DateTime? ImportDate { get; set; }
    }
}

namespace PCTP.Shared.Models
{
    public sealed class QRCodeInfo
    {
        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
        public string MaPhieu { get; set; }
        public DateTime? ImportDate { get; set; }
        public string RawQr { get; set; }
    }
}

namespace PCTP.Shared.Helpers
{
    public static class QRCodeParser
    {
        public static PCTP.Shared.Models.QRCodeInfo ParseQRCode(string value)
            => throw new FormatException("Synthetic fixture QR is intentionally not a production QR payload.");
    }
}

namespace PCTP.VIEWSTOCK.Models { }
