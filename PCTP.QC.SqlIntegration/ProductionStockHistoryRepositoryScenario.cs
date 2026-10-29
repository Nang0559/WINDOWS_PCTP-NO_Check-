using System;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration
{
    /// <summary>
    /// Exercises the production StockHistoryRepository INSERT path against a
    /// session-local temp table. It deliberately begins UnitOfWork so the
    /// repository must use the active SQL connection/transaction; the test
    /// executor throws if the repository falls back to an independent connection.
    /// </summary>
    internal static class ProductionStockHistoryRepositoryScenario
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
CREATE TABLE #StockHistory
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ActionType nvarchar(20) NULL,
    ItemCode nvarchar(50) NULL,
    TemCode nvarchar(200) NULL,
    LotNo nvarchar(200) NULL,
    Quantity int NOT NULL,
    Date datetime NOT NULL,
    FromSlotId int NULL,
    ToSlotId int NULL,
    QrData nvarchar(max) NULL,
    MaPhieu nvarchar(100) NULL,
    PerformedBy nvarchar(50) NULL
);");

                var repository = new StockHistoryRepository(new PhieuSqlExecutor(), uow);
                var lot = new LotInfo
                {
                    TemCode = "QC-TEM-HISTORY",
                    LotNo = "QC-LOT-HISTORY",
                    Quantity = 17,
                    RawQr = "QC-RAW-QR",
                    MaPhieuKho = "QC-PHIEU-HISTORY"
                };

                repository.SaveHistory("MOVE", "QC-ITEM", lot, 11, 22, "QC-OPERATOR");

                Require(Convert.ToInt32(Scalar(uow, "SELECT COUNT(*) FROM #StockHistory")) == 1,
                    "Production repository did not insert exactly one history row.");
                Require(Convert.ToInt32(Scalar(uow, "SELECT Quantity FROM #StockHistory")) == 17,
                    "Production repository did not persist the LOT quantity.");
                Require(Scalar(uow, "SELECT ItemCode FROM #StockHistory") == "QC-ITEM",
                    "Production repository did not persist ItemCode.");
                Require(Scalar(uow, "SELECT TemCode FROM #StockHistory") == "QC-TEM-HISTORY",
                    "Production repository did not persist TemCode.");
                Require(Scalar(uow, "SELECT LotNo FROM #StockHistory") == "QC-LOT-HISTORY",
                    "Production repository did not persist LotNo.");
                Require(Scalar(uow, "SELECT QrData FROM #StockHistory") == "QC-RAW-QR",
                    "Production repository did not preserve the raw QR.");
                Require(Scalar(uow, "SELECT MaPhieu FROM #StockHistory") == "QC-PHIEU-HISTORY",
                    "Production repository did not persist MaPhieu.");
                Require(Scalar(uow, "SELECT PerformedBy FROM #StockHistory") == "QC-OPERATOR",
                    "Production repository did not persist the operator.");
                Require(Convert.ToInt32(Scalar(uow, "SELECT FromSlotId FROM #StockHistory")) == 11,
                    "Production repository did not persist FromSlotId.");
                Require(Convert.ToInt32(Scalar(uow, "SELECT ToSlotId FROM #StockHistory")) == 22,
                    "Production repository did not persist ToSlotId.");

                uow.Rollback();
                Console.WriteLine("PASS: production StockHistoryRepository parameter mapping and transaction rollback against session-local #StockHistory.");
            }
            catch
            {
                try { uow.Rollback(); } catch { }
                throw;
            }
        }

        private static void Exec(IUnitOfWork uow, string sql)
        {
            using var command = new SqlCommand(sql, uow.Connection, uow.Transaction) { CommandTimeout = 5 };
            command.ExecuteNonQuery();
        }

        private static object Scalar(IUnitOfWork uow, string sql)
        {
            using var command = new SqlCommand(sql, uow.Connection, uow.Transaction) { CommandTimeout = 5 };
            return command.ExecuteScalar();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
