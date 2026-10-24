using System;
using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Models;

namespace PCTP.QC.SqlIntegration
{
    internal static class ProductionBulkStockSlotRepositoryScenario
    {
        public static void Run(string connectionString)
        {
            connectionString = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
            var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = connectionString };
            using var uow = new UnitOfWork(provider);
            uow.Begin();

            try
            {
                Exec(uow, @"
CREATE TABLE #Warehouse(WarehouseId int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL UNIQUE);
CREATE TABLE #Rack(RackId int IDENTITY(1,1) NOT NULL PRIMARY KEY, WarehouseId int NOT NULL, RackName nvarchar(100) NOT NULL, CONSTRAINT UQ_QC_Rack UNIQUE(WarehouseId,RackName));
CREATE TABLE #Slot(SlotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, RackId int NOT NULL, SlotNumber int NOT NULL, IsOccupied bit NOT NULL DEFAULT(0), Capacity int NOT NULL, Quantity int NOT NULL DEFAULT(0), ItemCode nvarchar(100) NULL, ImportDate datetime NULL);
CREATE TABLE #SlotLot(SlotLotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, SlotId int NOT NULL, ItemCode nvarchar(100) NULL, LotNo nvarchar(100) NOT NULL, Quantity int NOT NULL CHECK(Quantity >= 0), TemCode nvarchar(100) NULL, QrData nvarchar(500) NULL, MaPhieu nvarchar(100) NULL, ImportDate datetime NULL, PhieuStatus tinyint NOT NULL);
CREATE TABLE #STOCKTP(LOT nvarchar(100) NOT NULL PRIMARY KEY, MODEL nvarchar(100) NULL, PART nvarchar(100) NULL, NAME nvarchar(200) NULL, CASX smallint NULL, NGAYSX datetime NULL, SLSX int NULL, NGAYNHAP datetime NULL, SLNHAP int NULL, NGAYXUAT datetime NULL, SLXUAT int NULL, SLCONLAI int NULL, SATUS smallint NULL);
CREATE TABLE #DeliveryState(Id int NOT NULL PRIMARY KEY, HistoryCount int NOT NULL, PendingClosed bit NOT NULL);
INSERT INTO #DeliveryState VALUES(1,0,0);");

                var bulk = new BulkStockSlotRepository(new PhieuSqlExecutor(), uow, useQcTempTables: true);
                IStockTpRepository stock = new StockTpRepository(new PhieuSqlExecutor(), uow, "#STOCKTP");
                string warehouse = "QC-WH-" + Guid.NewGuid().ToString("N");
                int slotId = bulk.GetOrCreateVirtualSlotId(warehouse, "QC-RACK-01", 1000);
                int secondSlotId = bulk.GetOrCreateVirtualSlotId(warehouse, "QC-RACK-02", 1000);
                const string lot = "26080700748100851600";

                Add(bulk, slotId, lot, 40, "QC-TEM-001", "QC-QR-001", 0, new DateTime(2026, 10, 20));
                Add(bulk, slotId, lot, 25, "QC-TEM-002", "QC-QR-002", 0, new DateTime(2026, 10, 21));
                Add(bulk, secondSlotId, lot, 25, "QC-TEM-003", "QC-QR-003", 0, new DateTime(2026, 10, 22));
                stock.InsertStockTp(new NhapKhoItem { Lot = lot, Part = "QC-PART", Name = "QC isolated delivery", Model = "QC", CaSX = 1, SlSanXuat = 90, SlNhap = 90, NgaySX = new DateTime(2026, 10, 20) }, 0);

                Require(bulk.GetLotQuantity(1) == 40 && bulk.GetLotQuantity(2) == 25 && bulk.GetLotQuantity(3) == 25, "Initial SlotLot fixtures were not inserted across both Slots.");
                Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId) == 65, "First Slot header must equal its active SlotLot quantity.");
                Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", secondSlotId) == 25, "Second Slot header must equal its active SlotLot quantity.");
                var locations = bulk.FindSlotLotsByLot(lot, 0);
                Require(locations.Count == 2 && locations.Sum(x => x.Quantity) == 90, "One LOT must be discoverable in multiple Slots while STOCKTP remains a single aggregate row.");
                Require(locations.Any(x => x.SlotId == slotId && x.Quantity == 65) && locations.Any(x => x.SlotId == secondSlotId && x.Quantity == 25), "LOT quantities were not grouped by physical Slot.");

                // AddLot rejects Quantity <= 0, so historical states use positive trace quantities.
                // They must still be excluded from current stock because only PhieuStatus=0 counts.
                Add(bulk, slotId, "QC-CLOSED-LOT", 1, "QC-CLOSED-TEM", "QC-CLOSED-QR", 1, DateTime.Today);
                Add(bulk, slotId, "QC-CANCELLED-LOT", 1, "QC-CANCELLED-TEM", "QC-CANCELLED-QR", 2, DateTime.Today);
                Add(bulk, slotId, "QC-QUARANTINE-LOT", 5, "QC-QUARANTINE-TEM", "QC-QUARANTINE-QR", 3, DateTime.Today);
                Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId) == 65, "Slot header incorrectly counted PhieuStatus 1/2/3.");
                Require(Scalar(uow, "SELECT ISNULL(SUM(Quantity),0) FROM #SlotLot WHERE SlotId=@id AND PhieuStatus=0", slotId) == 65, "Active SlotLot sum is inconsistent.");
                Require(Scalar(uow, "SELECT ISNULL(SUM(Quantity),0) FROM #SlotLot WHERE SlotId=@id AND PhieuStatus IN (1,2,3)", slotId) == 7, "Historical/quarantine fixture rows are inconsistent.");

                bool quarantineRejected = false;
                try { bulk.TakeLot(slotId, "QC-QUARANTINE-LOT", "QC-PART", 1); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ", StringComparison.OrdinalIgnoreCase)) { quarantineRejected = true; }
                Require(quarantineRejected, "TakeLot allowed a quarantined LOT to be exported.");
                Require(Scalar(uow, "SELECT Quantity FROM #SlotLot WHERE LotNo=@lot AND PhieuStatus=3", "QC-QUARANTINE-LOT") == 5, "Quarantine quantity changed after rejected export.");
                Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId) == 65, "Rejected quarantine export changed Slot header.");
                Console.WriteLine("PASS: production BulkStockSlotRepository rejects quarantined LOT without changing current stock.");

                uow.Transaction.Save("QC_BEFORE_SHORTAGE");
                bool shortageRejected = false;
                try { bulk.TakeLot(slotId, lot, "QC-PART", 66); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ", StringComparison.OrdinalIgnoreCase)) { shortageRejected = true; uow.Transaction.Rollback("QC_BEFORE_SHORTAGE"); }
                Require(shortageRejected, "TakeLot accepted an over-request.");
                Require(bulk.GetLotQuantity(1) == 40 && bulk.GetLotQuantity(2) == 25, "Savepoint rollback did not restore both LOT rows.");
                Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId) == 65, "Savepoint rollback did not restore Slot header.");
                Console.WriteLine("PASS: production TakeLot shortage rejection and savepoint rollback.");

                uow.Transaction.Save("QC_BEFORE_DELIVERY");
                try
                {
                    var take = bulk.TakeLot(slotId, lot, "QC-PART", 50);
                    Require(take.Quantity == 50 && take.ExportLots.Count == 2, "TakeLot did not consume two FIFO rows.");
                    Require(take.ExportLots[0].Quantity == 40 && take.ExportLots[1].Quantity == 10, "TakeLot did not consume FIFO order.");
                    Require(bulk.GetLotQuantity(1) == 0 && bulk.GetLotQuantity(2) == 15, "Unexpected per-row quantities after TakeLot.");

                    Exec(uow, "UPDATE #STOCKTP SET SLCONLAI=SLCONLAI-@q, SLXUAT=ISNULL(SLXUAT,0)+@q WHERE LOT=@lot AND SLCONLAI>=@q;", 1, new SqlParameter("@q", 50), new SqlParameter("@lot", lot));
                    Exec(uow, "UPDATE #DeliveryState SET HistoryCount=HistoryCount+1, PendingClosed=1 WHERE Id=1;", 1);

                    Require(Scalar(uow, "SELECT SLCONLAI FROM #STOCKTP WHERE LOT=@lot", lot) == 15, "STOCKTP remaining quantity did not reconcile.");
                    Require(Scalar(uow, "SELECT CASE WHEN ISNULL(SLNHAP,0)-ISNULL(SLXUAT,0)=ISNULL(SLCONLAI,0) THEN 1 ELSE 0 END FROM #STOCKTP WHERE LOT=@lot", lot) == 1, "STOCKTP equation failed.");
                    Require(Scalar(uow, "SELECT CASE WHEN (SELECT SUM(Quantity) FROM #SlotLot WHERE LotNo=@lot AND PhieuStatus=0)=(SELECT SLCONLAI FROM #STOCKTP WHERE LOT=@lot) THEN 1 ELSE 0 END", lot) == 1, "STOCKTP does not reconcile with active SlotLot quantity across multiple Slots.");
                    Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId) == 15, "First Slot header does not reconcile after export.");
                    Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", secondSlotId) == 25, "Second Slot header must remain unchanged after export from the first Slot.");
                    Require(Scalar(uow, "SELECT HistoryCount FROM #DeliveryState WHERE Id=1") == 1, "Delivery finalization fixture did not update.");
                    uow.Transaction.Rollback("QC_BEFORE_DELIVERY");
                }
                catch
                {
                    try { uow.Transaction.Rollback("QC_BEFORE_DELIVERY"); } catch { }
                    throw;
                }

                Require(Scalar(uow, "SELECT SLCONLAI FROM #STOCKTP WHERE LOT=@lot", lot) == 90, "STOCKTP was not restored by rollback.");
                Require(Scalar(uow, "SELECT SUM(Quantity) FROM #SlotLot WHERE SlotId=@id AND PhieuStatus=0", slotId) == 90, "Active SlotLot rows were not restored by rollback.");
                Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", slotId) == 65, "First Slot header was not restored by rollback.");
                Require(Scalar(uow, "SELECT Quantity FROM #Slot WHERE SlotId=@id", secondSlotId) == 25, "Second Slot header was not restored by rollback.");
                Require(Scalar(uow, "SELECT HistoryCount FROM #DeliveryState WHERE Id=1") == 0 && Scalar(uow, "SELECT PendingClosed FROM #DeliveryState WHERE Id=1") == 0, "Delivery-state writes were not rolled back.");

                uow.Rollback();
                Console.WriteLine("PASS: production BulkStockSlotRepository FIFO, PhieuStatus current-stock filtering, STOCKTP reconciliation and cross-ledger rollback using session-local # fixtures.");
            }
            catch
            {
                try { if (uow.IsInTransaction) uow.Rollback(); } catch { }
                throw;
            }
        }

        private static void Add(BulkStockSlotRepository bulk, int slotId, string lot, int quantity, string tem, string qr, byte status, DateTime importDate)
        {
            bulk.AddLot(slotId, new StockSlotLot { LotNo = lot, ItemCode = "QC-PART", Quantity = quantity, TemCode = tem, RawQr = qr, ImportDate = importDate, PhieuStatus = status });
        }

        private static int Scalar(UnitOfWork uow, string sql, object? parameter = null)
        {
            using var command = uow.Connection.CreateCommand();
            command.Transaction = uow.Transaction;
            command.CommandTimeout = 5;
            command.CommandText = sql;
            if (sql.Contains("@id", StringComparison.OrdinalIgnoreCase))
                command.Parameters.Add(new SqlParameter("@id", parameter ?? DBNull.Value));
            else if (sql.Contains("@lot", StringComparison.OrdinalIgnoreCase))
                command.Parameters.Add(new SqlParameter("@lot", parameter ?? DBNull.Value));
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void Exec(UnitOfWork uow, string sql, params SqlParameter[] parameters)
        {
            using var command = uow.Connection.CreateCommand();
            command.Transaction = uow.Transaction;
            command.CommandTimeout = 5;
            command.CommandText = sql;
            if (parameters != null) command.Parameters.AddRange(parameters);
            command.ExecuteNonQuery();
        }

        private static void Exec(UnitOfWork uow, string sql, int expectedRows, params SqlParameter[] parameters)
        {
            using var command = uow.Connection.CreateCommand();
            command.Transaction = uow.Transaction;
            command.CommandTimeout = 5;
            command.CommandText = sql;
            if (parameters != null) command.Parameters.AddRange(parameters);
            int rows = command.ExecuteNonQuery();
            if (rows != expectedRows) throw new InvalidOperationException("Unexpected affected-row count: " + rows);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
