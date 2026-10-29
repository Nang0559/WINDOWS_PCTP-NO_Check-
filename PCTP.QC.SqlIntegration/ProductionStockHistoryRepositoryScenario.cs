using System;
using System.Data;
using System.Data.SqlClient;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration
{
    /// <summary>
    /// Runs the actual write-side StockHistoryRepository against a fresh,
    /// disposable database. The repository uses the unqualified dbo.StockHistory
    /// name, so a session-local #table would not exercise its real SQL.
    /// This scenario is permitted only in the ephemeral SQL-engine CI path.
    /// </summary>
    internal static class ProductionStockHistoryRepositoryScenario
    {
        public static void Run(string connectionString)
        {
            var masterBuilder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master",
                ConnectTimeout = 5
            };
            var databaseName = "PCTP_QC_HISTORY_" + Guid.NewGuid().ToString("N");
            if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, "^PCTP_QC_HISTORY_[a-f0-9]{32}$"))
                throw new InvalidOperationException("Generated fixture database name failed safety validation.");

            var fixtureBuilder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = databaseName,
                ConnectTimeout = 5,
                ApplicationName = "PCTP-QC-StockHistory-Fixture"
            };

            try
            {
                using (var master = new SqlConnection(masterBuilder.ConnectionString))
                {
                    master.Open();
                    using var create = master.CreateCommand();
                    create.CommandText = "CREATE DATABASE [" + databaseName + "]";
                    create.CommandTimeout = 15;
                    create.ExecuteNonQuery();
                }

                using (var setup = new SqlConnection(fixtureBuilder.ConnectionString))
                {
                    setup.Open();
                    using var command = setup.CreateCommand();
                    command.CommandTimeout = 10;
                    command.CommandText = @"
CREATE TABLE dbo.StockHistory
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
);";
                    command.ExecuteNonQuery();
                }

                var provider = new SQLPROVIDER { B7R2_FCCdb = fixtureBuilder.ConnectionString };
                using (var uow = new UnitOfWork(provider))
                {
                    uow.Begin();
                    try
                    {
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

                        Require(Convert.ToInt32(Scalar(uow, "SELECT COUNT(*) FROM dbo.StockHistory")) == 1,
                            "Production repository did not insert exactly one history row.");
                        Require(Convert.ToInt32(Scalar(uow, "SELECT Quantity FROM dbo.StockHistory")) == 17,
                            "Production repository did not persist Quantity.");
                        Require(Convert.ToString(Scalar(uow, "SELECT ItemCode FROM dbo.StockHistory")) == "QC-ITEM",
                            "Production repository did not persist ItemCode.");
                        Require(Convert.ToString(Scalar(uow, "SELECT TemCode FROM dbo.StockHistory")) == "QC-TEM-HISTORY",
                            "Production repository did not persist TemCode.");
                        Require(Convert.ToString(Scalar(uow, "SELECT LotNo FROM dbo.StockHistory")) == "QC-LOT-HISTORY",
                            "Production repository did not persist LotNo.");
                        Require(Convert.ToString(Scalar(uow, "SELECT QrData FROM dbo.StockHistory")) == "QC-RAW-QR",
                            "Production repository did not preserve RawQr.");
                        Require(Convert.ToString(Scalar(uow, "SELECT MaPhieu FROM dbo.StockHistory")) == "QC-PHIEU-HISTORY",
                            "Production repository did not persist MaPhieu.");
                        Require(Convert.ToString(Scalar(uow, "SELECT PerformedBy FROM dbo.StockHistory")) == "QC-OPERATOR",
                            "Production repository did not persist PerformedBy.");
                        Require(Convert.ToInt32(Scalar(uow, "SELECT FromSlotId FROM dbo.StockHistory")) == 11,
                            "Production repository did not persist FromSlotId.");
                        Require(Convert.ToInt32(Scalar(uow, "SELECT ToSlotId FROM dbo.StockHistory")) == 22,
                            "Production repository did not persist ToSlotId.");

                        uow.Rollback();
                        Console.WriteLine("PASS: production StockHistoryRepository wrote the expected values and caller rollback removed its row.");
                    }
                    catch
                    {
                        try { uow.Rollback(); } catch { }
                        throw;
                    }
                }
            }
            finally
            {
                using var master = new SqlConnection(masterBuilder.ConnectionString);
                master.Open();
                using var drop = master.CreateCommand();
                drop.CommandTimeout = 15;
                drop.CommandText = "IF DB_ID(@db) IS NOT NULL BEGIN ALTER DATABASE [" + databaseName + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + databaseName + "]; END";
                drop.Parameters.Add("@db", SqlDbType.NVarChar, 128).Value = databaseName;
                drop.ExecuteNonQuery();
            }
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
