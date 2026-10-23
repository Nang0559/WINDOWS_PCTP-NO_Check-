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
    /// <summary>
    /// Executes the production BulkStockSlotRepository and StockTpRepository against
    /// session-local # fixtures only. No dbo business tables are created or mutated.
    /// </summary>
    public static void Run(string connectionString)
    {
        connectionString = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
        var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        uow.Begin();

        try
        {
            using (var setup = uow.Connection.CreateCommand())
            {
                setup.Transaction = uow.Transaction;
                setup.CommandTimeout = 5;
                setup.CommandText = @"
CREATE TABLE #Warehouse(WarehouseId int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL UNIQUE);
CREATE TABLE #Rack(RackId int IDENTITY(1,1) NOT NULL PRIMARY KEY, WarehouseId int NOT NULL, RackName nvarchar(100) NOT NULL, CONSTRAINT UQ_QC_Rack UNIQUE(WarehouseId,RackName));
CREATE TABLE #Slot(SlotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, RackId int NOT NULL, SlotNumber int NOT NULL, IsOccupied bit NOT NULL DEFAULT(0), Capacity int NOT NULL, Quantity int NOT NULL DEFAULT(0), ItemCode nvarchar(100) NULL, ImportDate datetime NULL);
CREATE TABLE #SlotLot(SlotLotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, SlotId int NOT NULL, ItemCode nvarchar(100) NULL, LotNo nvarchar(100) NOT NULL, Quantity int NOT NULL CHECK(Quantity >= 0), TemCode nvarchar(100) NULL, QrData nvarchar(500) NULL, MaPhieu nvarchar(100) NULL, ImportDate datetime NULL, PhieuStatus tinyint NOT NULL);
CREATE TABLE #STOCKTP(LOT nvarchar(100) NOT NULL PRIMARY KEY, MODEL nvarchar(100) NULL, PART nvarchar(100) NULL, NAME nvarchar(200) NULL, CASX smallint NULL, NGAYSX datetime NULL, SLSX int NULL, NGAYNHAP datetime NULL, SLNHAP int NULL, NGAYXUAT datetime NULL, SLXUAT int NULL, SLCONLAI int NULL, SATUS smallint NULL);
CREATE TABLE #DeliveryState(Id int NOT NULL PRIMARY KEY, HistoryCount int NOT NULL, PendingClosed bit NOT NULL);
INSERT INTO #DeliveryState(Id,HistoryCount,PendingClosed) VALUES(1,0,0);";
                setup.ExecuteNonQuery();
            }

            var bulk = new BulkStockSlotRepository(new PhieuSqlExecutor(), uow, useQcTempTables: true);
            IStockTpRepository stock = new StockTpRepository(new PhieuSqlExecutor(), uow, "#STOCKTP");
            int slotId = bulk.GetOrCreateVirtualSlotId("QC-WH-" + Guid.NewGuid().ToString("N"), "QC-RACK", 1000);
            const string lot = "26080700748100851600";

            bulk.AddLot(slotId, new StockSlotLot
            {
                LotNo = lot, ItemCode = "QC-PART", Quantity = 40,
                TemCode = "QC-TEM-001", RawQr = "QC-QR-001",
                ImportDate = new DateTime(2026, 10, 20), PhieuStatus = 0
            });
            bulk.AddLot(slotId, new StockSlotLot
            {
                LotNo = lot, ItemCode = "QC-PART", Quantity = 25,
                TemCode = "QC-TEM-002", RawQr = "QC-QR-002",
                ImportDate = new DateTime(2026, 10, 21), PhieuStatus = 0
            });
            stock.InsertStockTp(new NhapKhoItem
            {
                Lot = lot, Part = "QC-PART", Name = "QC isolated delivery",
                Model = "QC", CaSX = 1, SlSanXuat = 65, SlNhap = 65,
                NgaySX = new DateTime(2026, 10, 20)
            }, 0);

            Require(bulk.GetLotQuantity(1) == 40 && bulk.GetLotQuantity(2) == 25,
                "Production BulkStockSlotRepository did not insert the two fixture rows.");
            Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId)) == 65,
                "Slot header did not match the sum of active SlotLot quantities.");

            // Exercise the production repository's quarantine guard, not only a synthetic SQL predicate.
            using (var quarantine = uow.Connection.CreateCommand())
            {
                quarantine.Transaction = uow.Transaction;
                quarantine.CommandTimeout = 5;
                quarantine.CommandText = @"
INSERT INTO #SlotLot(SlotId,ItemCode,LotNo,Quantity,TemCode,QrData,ImportDate,PhieuStatus)
VALUES(@slot,N'QC-PART',N'QC-QUARANTINE-LOT',5,N'QC-QUARANTINE-TEM',N'QC-QUARANTINE-QR',GETDATE(),3);";
                quarantine.Parameters.Add(new SqlParameter("@slot", slotId));
                quarantine.ExecuteNonQuery();
            }

            bool quarantineRejected = false;
            try
            {
                bulk.TakeLot(slotId, "QC-QUARANTINE-LOT", "QC-PART", 1);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ", StringComparison.OrdinalIgnoreCase))
            {
                quarantineRejected = true;
            }
            Require(quarantineRejected, "Production TakeLot allowed a quarantined LOT to be exported.");
            Require(Convert.ToInt32(ScalarInTransaction(uow,
                "SELECT Quantity FROM #SlotLot WHERE LotNo=@lot AND PhieuStatus=3", "QC-QUARANTINE-LOT")) == 5,
                "Quarantined LOT quantity changed after rejected export.");
            Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId)) == 65,
                "Rejected quarantined export changed the active Slot header.");
            Console.WriteLine("PASS: production BulkStockSlotRepository.TakeLot rejects quarantined LOT without changing its quantity or active Slot header.");

            // Over-request must be rejected, and the caller's transaction/savepoint
            // must restore rows that TakeLot tentatively decremented before detecting shortage.
            uow.Transaction.Save("QC_BEFORE_SHORTAGE");
            bool shortageRejected = false;
            try
            {
                bulk.TakeLot(slotId, lot, "QC-PART", 66);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ", StringComparison.OrdinalIgnoreCase))
            {
                shortageRejected = true;
                uow.Transaction.Rollback("QC_BEFORE_SHORTAGE");
            }
            Require(shortageRejected, "Production TakeLot accepted an over-request of 66 from 65 units.");
            Require(bulk.GetLotQuantity(1) == 40 && bulk.GetLotQuantity(2) == 25,
                "Caller rollback did not restore both SlotLot rows after an insufficient-stock request.");
            Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId)) == 65,
                "Slot header changed after a rejected insufficient-stock request.");
            Console.WriteLine("PASS: production TakeLot rejects an over-request and savepoint rollback restores all partially touched LOT rows.");

            uow.Transaction.Save("QC_BEFORE_DELIVERY");
            try
            {
                var take = bulk.TakeLot(slotId, lot, "QC-PART", 50);
                Require(take.Quantity == 50 && take.ExportLots.Count == 2,
                    "Production TakeLot did not consume the requested quantity across FIFO rows.");
                Require(take.ExportLots[0].Quantity == 40 && take.ExportLots[1].Quantity == 10,
                    "Production TakeLot did not consume oldest ImportDate first.");
                Require(bulk.GetLotQuantity(1) == 0 && bulk.GetLotQuantity(2) == 15,
                    "Production TakeLot left unexpected per-row quantities.");

                using (var updateStock = uow.Connection.CreateCommand())
                {
                    updateStock.Transaction = uow.Transaction;
                    updateStock.CommandTimeout = 5;
                    updateStock.CommandText = "UPDATE #STOCKTP SET SLCONLAI=SLCONLAI-@q, SLXUAT=ISNULL(SLXUAT,0)+@q WHERE LOT=@lot AND SLCONLAI>=@q;";
                    updateStock.Parameters.Add(new SqlParameter("@q", 50));
                    updateStock.Parameters.Add(new SqlParameter("@lot", lot));
                    Require(updateStock.ExecuteNonQuery() == 1, "Isolated STOCKTP fixture decrement failed.");
                }
                using (var finalize = uow.Connection.CreateCommand())
                {
                    finalize.Transaction = uow.Transaction;
                    finalize.CommandText = "UPDATE #DeliveryState SET HistoryCount=HistoryCount+1, PendingClosed=1 WHERE Id=1;";
                    Require(finalize.ExecuteNonQuery() == 1, "Isolated delivery finalization fixture update failed.");
                }

                Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT SLCONLAI FROM #STOCKTP WHERE LOT=@lot", lot)) == 15,
                    "STOCKTP remaining quantity did not reconcile after simulated delivery.");
                Require(Convert.ToInt32(ScalarInTransaction(uow,
                    "SELECT CASE WHEN ISNULL(SLNHAP,0)-ISNULL(SLXUAT,0)=ISNULL(SLCONLAI,0) THEN 1 ELSE 0 END FROM #STOCKTP WHERE LOT=@lot", lot)) == 1,
                    "Production STOCKTP report equation SLNHAP - SLXUAT = SLCONLAI failed.");
                Require(Convert.ToInt32(ScalarInTransaction(uow,
                    "SELECT COUNT(1) FROM #SlotLot WHERE LotNo=@lot AND PhieuStatus=0", lot)) == 2,
                    "LOT traceability did not return both active SlotLot rows for the stock ledger LOT.");
                Require(Convert.ToInt32(ScalarInTransaction(uow,
                    "SELECT CASE WHEN (SELECT SUM(Quantity) FROM #SlotLot WHERE LotNo=@lot AND PhieuStatus=0) = (SELECT SLCONLAI FROM #STOCKTP WHERE LOT=@lot) THEN 1 ELSE 0 END", lot)) == 1,
                    "Production STOCKTP current stock does not reconcile with active SlotLot quantities.");
                Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId)) == 15,
                    "Slot header did not refresh after production TakeLot.");
                Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT HistoryCount FROM #DeliveryState WHERE Id=1")) == 1,
                    "Delivery history fixture did not record the successful simulated finalization.");

                // Roll back every repository/ledger/finalization write to the same savepoint.
                uow.Transaction.Rollback("QC_BEFORE_DELIVERY");
            }
            catch
            {
                try { uow.Transaction.Rollback("QC_BEFORE_DELIVERY"); } catch { }
                throw;
            }

            Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT SLCONLAI FROM #STOCKTP WHERE LOT=@lot", lot)) == 65,
                "STOCKTP quantity was not restored by cross-repository rollback.");
            Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT SUM(Quantity) FROM #SlotLot WHERE SlotId=@id AND PhieuStatus=0", slotId)) == 65,
                "SlotLot quantities were not restored by cross-repository rollback.");
            Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId)) == 65,
                "Slot header quantity was not restored by cross-repository rollback.");
            Require(Convert.ToInt32(ScalarInTransaction(uow, "SELECT HistoryCount FROM #DeliveryState WHERE Id=1")) == 0 &&
                    Convert.ToInt32(ScalarInTransaction(uow, "SELECT PendingClosed FROM #DeliveryState WHERE Id=1")) == 0,
                "Delivery history/pending fixture writes were not rolled back.");

            uow.Rollback();
            Console.WriteLine("PASS: production BulkStockSlotRepository FIFO/header refresh and STOCKTP + SlotLot + delivery-state rollback using session-local # fixtures only.");
        }
        catch
        {
            try { if (uow.IsInTransaction) uow.Rollback(); } catch { }
            throw;
        }
    }

    private static object ScalarInTransaction(UnitOfWork uow, string sql, object parameter = null)
    {
        using var command = uow.Connection.CreateCommand();
        command.Transaction = uow.Transaction;
        command.CommandTimeout = 5;
        command.CommandText = sql;
        if (sql.Contains("@id", StringComparison.OrdinalIgnoreCase))
            command.Parameters.Add(new SqlParameter("@id", parameter));
        else if (sql.Contains("@lot", StringComparison.OrdinalIgnoreCase))
            command.Parameters.Add(new SqlParameter("@lot", parameter));
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
        public string LotNo { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string TemCode { get; set; } = string.Empty;
        public string RawQr { get; set; } = string.Empty;
        public PCTP.Shared.Models.QRCodeInfo QRInfo { get; set; } = new PCTP.Shared.Models.QRCodeInfo();
        public DateTime? ImportDate { get; set; }
    }
}

namespace PCTP.Shared.Models
{
    public sealed class QRCodeInfo
    {
        public string LotNo { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string MaPhieu { get; set; } = string.Empty;
        public DateTime? ImportDate { get; set; }
        public string RawQr { get; set; } = string.Empty;
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
