using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Runs two independent production UnitOfWork + BulkStockSlotRepository consumers
/// against uniquely named tempdb global fixtures. No dbo business tables are touched.
/// </summary>
internal static class ProductionBulkStockSlotConcurrencyScenario
{
    public static async Task RunAsync(string connectionString)
    {
        var legacyConnectionString = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
        var prefix = "##PCTP_QC_" + Guid.NewGuid().ToString("N") + "_";
        var warehouse = prefix + "Warehouse";
        var rack = prefix + "Rack";
        var slot = prefix + "Slot";
        var slotLot = prefix + "SlotLot";

        using var owner = new SqlConnection(legacyConnectionString);
        await owner.OpenAsync();

        try
        {
            using (var setup = owner.CreateCommand())
            {
                setup.CommandTimeout = 5;
                setup.CommandText = $@"
CREATE TABLE [{warehouse}](WarehouseId int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL UNIQUE);
CREATE TABLE [{rack}](RackId int IDENTITY(1,1) NOT NULL PRIMARY KEY, WarehouseId int NOT NULL, RackName nvarchar(100) NOT NULL, CONSTRAINT [UQ_{prefix.TrimStart('#') }Rack] UNIQUE(WarehouseId,RackName));
CREATE TABLE [{slot}](SlotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, RackId int NOT NULL, SlotNumber int NOT NULL, IsOccupied bit NOT NULL DEFAULT(0), Capacity int NOT NULL, Quantity int NOT NULL DEFAULT(0), ItemCode nvarchar(100) NULL, ImportDate datetime NULL);
CREATE TABLE [{slotLot}](SlotLotId int IDENTITY(1,1) NOT NULL PRIMARY KEY, SlotId int NOT NULL, ItemCode nvarchar(100) NULL, LotNo nvarchar(100) NOT NULL, Quantity int NOT NULL CHECK(Quantity >= 0), TemCode nvarchar(100) NULL, QrData nvarchar(500) NULL, MaPhieu nvarchar(100) NULL, ImportDate datetime NULL, PhieuStatus tinyint NOT NULL);
INSERT INTO [{warehouse}](Name) VALUES(N'QC-CONC-WH');
INSERT INTO [{rack}](WarehouseId,RackName) VALUES(1,N'QC-CONC-RACK');
INSERT INTO [{slot}](RackId,SlotNumber,IsOccupied,Capacity,Quantity,ItemCode) VALUES(1,1,1,100,15,N'QC-PART');
INSERT INTO [{slotLot}](SlotId,ItemCode,LotNo,Quantity,TemCode,QrData,ImportDate,PhieuStatus)
VALUES(1,N'QC-PART',N'QC-CONCURRENT-LOT',15,N'QC-TEM-CONC',N'QC-QR-CONC',GETDATE(),0);";
                await setup.ExecuteNonQueryAsync();
            }

            async Task<bool> TryConsumeTenAsync()
            {
                var provider = new PCTP.ClassSQL.SQLPROVIDER { B7R2_FCCdb = legacyConnectionString };
                using var uow = new UnitOfWork(provider);
                uow.Begin();
                try
                {
                    var repository = new BulkStockSlotRepository(
                        new PhieuSqlExecutor(),
                        uow,
                        useQcTempTables: true,
                        qcTablePrefix: prefix);
                    repository.TakeLot(1, "QC-CONCURRENT-LOT", "QC-PART", 10);
                    uow.Commit();
                    return true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ", StringComparison.OrdinalIgnoreCase))
                {
                    if (uow.IsInTransaction) uow.Rollback();
                    return false;
                }
                catch
                {
                    if (uow.IsInTransaction) uow.Rollback();
                    throw;
                }
            }

            var results = await Task.WhenAll(Task.Run(TryConsumeTenAsync), Task.Run(TryConsumeTenAsync));
            Require(results.Count(x => x) == 1,
                $"Expected one production repository TakeLot to succeed from 15 units; results were {string.Join(",", results)}.");

            using (var verify = owner.CreateCommand())
            {
                verify.CommandTimeout = 5;
                verify.CommandText = $@"
SELECT (SELECT SUM(Quantity) FROM [{slotLot}] WHERE LotNo=N'QC-CONCURRENT-LOT' AND PhieuStatus=0),
       (SELECT Quantity FROM [{slot}] WHERE SlotId=1);";
                using var reader = await verify.ExecuteReaderAsync();
                Require(await reader.ReadAsync(), "Could not read concurrent fixture quantities.");
                var lotQuantity = Convert.ToInt32(reader.GetValue(0));
                var slotQuantity = Convert.ToInt32(reader.GetValue(1));
                Require(lotQuantity == 5 && slotQuantity == 5,
                    $"Production repository concurrency oversold or left a stale slot header: LOT={lotQuantity}, Slot={slotQuantity}.");
            }

            Console.WriteLine("PASS: two independent production UnitOfWork + BulkStockSlotRepository.TakeLot calls competed for 10 from 15; one succeeded, one rejected shortage, and LOT/Slot both reconciled to 5.");
        }
        finally
        {
            foreach (var table in new[] { slotLot, slot, rack, warehouse })
            {
                using var cleanup = owner.CreateCommand();
                cleanup.CommandTimeout = 5;
                cleanup.CommandText = $"IF OBJECT_ID(N'tempdb..{table}') IS NOT NULL DROP TABLE [{table}];";
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
