using System;
using System.Data;
using System.Data.SqlClient;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Executes the two production XuLyHangLoi repositories against disposable dbo fixtures.
/// The runner invokes this only in --engine-only mode; shared FCC business databases are never
/// eligible. Production repository code, SqlRepositoryBase, UnitOfWork and SQL Server are real.
/// The test executor deliberately throws if a repository bypasses the active UnitOfWork.
/// </summary>
internal static class ProductionReworkRepositorySqlScenario
{
    private const string TicketTable = "dbo.FVN_PhieuXuLyBatThuong";
    private const string ReturnLineTable = "dbo.FVN_PhieuTraHangCT";

    public static Task RunAsync(string connectionString)
    {
        Run(connectionString);
        return Task.CompletedTask;
    }

    private static void Run(string connectionString)
    {
        var legacyConnectionString = SqlTestConfiguration.BuildLegacySqlClientConnectionString(connectionString);
        EnsureFixtureNamesAreFree(legacyConnectionString);
        CreateFixtures(legacyConnectionString);

        try
        {
            RunAbnormalTicketRepository(legacyConnectionString);
            RunReturnWorkflowRepository(legacyConnectionString);
            Console.WriteLine("PASS: production TraHangQTChungRepository + PhieuXuLyBatThuongRepository SQL integration against disposable dbo fixtures; rollback and UnitOfWork participation verified.");
        }
        finally
        {
            DropFixtures(legacyConnectionString);
        }
    }

    private static void RunAbnormalTicketRepository(string connectionString)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        var repository = new PhieuXuLyBatThuongRepository(new PhieuSqlExecutor(), uow);

        const int returnLineId = 910001;
        Execute(connectionString, $@"
INSERT INTO {ReturnLineTable}(Id, PhieuXuLyBatThuongId) VALUES ({returnLineId}, NULL);");

        var first = NewTicket("QC-SQL-TICKET-1", returnLineId);
        var id = repository.Insert(returnLineId, first);
        Require(id > 0, "PhieuXuLyBatThuongRepository.Insert returned an invalid identity.");
        Require(!uow.IsInTransaction, "Insert must commit its own transaction when no caller transaction exists.");

        uow.Begin();
        try
        {
            var loaded = repository.GetById(id);
            Require(loaded != null && loaded.SoPhieu == first.SoPhieu, "GetById did not map the inserted ticket.");
            Require(loaded.Nguon == NguonXuLyBatThuong.TraNoiBo &&
                    loaded.SlotIdNguon == 41 && loaded.LotNguon == "LOT-QC-TICKET",
                "GetById lost source/LOT metadata.");
            Require(repository.GetStatus(id) == QTChungStatus.Moi, "New ticket must start at Moi.");
            Require(repository.GetByPhieuTraHangId(first.PhieuTraHangId.Value)?.Id == id,
                "GetByPhieuTraHangId did not find the ticket.");
            Require(repository.GetAllByPhieuTraHangId(first.PhieuTraHangId.Value).Count == 1,
                "GetAllByPhieuTraHangId returned an unexpected count.");
            Require(repository.GetByNguon(NguonXuLyBatThuong.TraNoiBo).Count == 1,
                "GetByNguon returned an unexpected count.");
            uow.Rollback();
        }
        catch
        {
            if (uow.IsInTransaction) uow.Rollback();
            throw;
        }

        uow.Begin();
        try
        {
            repository.UpdateDinhHuong(id, HuongXuLyBatThuong.CanRework, "QC-SQL");
            repository.UpdateStatus(id, QTChungStatus.DaDinhHuong, "QC-SQL");
            Require(repository.UpdateStatusIfCurrentIs(id, QTChungStatus.DaDinhHuong,
                QTChungStatus.DaXuatKhoRework, "KHO-SQL"), "Compare-and-set status update should succeed.");
            Require(!repository.UpdateStatusIfCurrentIs(id, QTChungStatus.DaDinhHuong,
                QTChungStatus.DaGiaoSanXuat, "STALE-SQL"), "Stale compare-and-set must not update.");
            Require(repository.CountByStatus(QTChungStatus.DaXuatKhoRework) == 1,
                "CountByStatus returned an unexpected count.");
            Require(repository.GetByStatus(QTChungStatus.DaXuatKhoRework).Count == 1,
                "GetByStatus returned an unexpected count.");
            Require(repository.UpdateLyDoHuy(id, QTChungStatus.DaXuatKhoRework,
                "SQL rollback contract", "QC-SQL"), "Conditional cancellation update should succeed.");
            uow.Commit();
        }
        catch
        {
            if (uow.IsInTransaction) uow.Rollback();
            throw;
        }

        uow.Begin();
        try
        {
            var cancelled = repository.GetById(id);
            Require(cancelled.Status == QTChungStatus.Huy &&
                    cancelled.LyDoHuy == "SQL rollback contract" &&
                    cancelled.NguoiHuy == "QC-SQL",
                "Conditional cancellation fields were not persisted.");
            uow.Rollback();
        }
        catch
        {
            if (uow.IsInTransaction) uow.Rollback();
            throw;
        }

        // A failed child-link update must roll back the newly inserted parent row.
        bool duplicateLinkRejected = false;
        try
        {
            repository.Insert(returnLineId, NewTicket("QC-SQL-TICKET-DUPLICATE", returnLineId));
        }
        catch (InvalidOperationException)
        {
            duplicateLinkRejected = true;
        }
        Require(duplicateLinkRejected, "A return line already linked to another ticket must be rejected.");

        // A caller-owned transaction must contain the repository's nested Insert and remain rollbackable.
        const int rollbackLineId = 910002;
        Execute(connectionString, $@"
INSERT INTO {ReturnLineTable}(Id, PhieuXuLyBatThuongId) VALUES ({rollbackLineId}, NULL);");
        uow.Begin();
        try
        {
            var pendingId = repository.Insert(rollbackLineId, NewTicket("QC-SQL-TICKET-ROLLBACK", rollbackLineId));
            Require(pendingId > 0 && uow.IsInTransaction, "Insert must join the caller-owned UnitOfWork.");
            uow.Rollback();
        }
        catch
        {
            if (uow.IsInTransaction) uow.Rollback();
            throw;
        }

        var postconditions = ScalarPair(connectionString, $@"
SELECT
    (SELECT COUNT(1) FROM {TicketTable} WHERE SoPhieu IN
        (N'QC-SQL-TICKET-1', N'QC-SQL-TICKET-DUPLICATE', N'QC-SQL-TICKET-ROLLBACK')),
    (SELECT COUNT(1) FROM {ReturnLineTable} WHERE Id={rollbackLineId} AND PhieuXuLyBatThuongId IS NOT NULL);");
        Require(postconditions.Item1 == 1 && postconditions.Item2 == 0,
            $"Ticket atomicity postcondition failed: ticketRows={postconditions.Item1}, rolledBackLinks={postconditions.Item2}.");
        Console.WriteLine("PASS: production PhieuXuLyBatThuongRepository insert/map/status/CAS/cancel/link-conflict/caller-rollback contracts.");
    }

    private static void RunReturnWorkflowRepository(string connectionString)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        var repository = new TraHangQTChungRepository(new PhieuSqlExecutor(), uow);
        const int ticketId = 920001;
        var operationId = Guid.NewGuid();

        uow.Begin();
        try
        {
            var exportId = repository.InsertXuat(new TraHangQTChungXuat
            {
                OperationId = operationId,
                PhieuXuLyBatThuongId = ticketId,
                SlotIdNguon = 12,
                LotXuat = "LOT-RW-SQL",
                LoaiXuat = "Rework",
                MaHang = "PART-RW",
                SoLuongXuat = 8,
                TonTruoc = 20,
                TonSau = 12,
                NgayXuat = new DateTime(2026, 10, 26, 8, 0, 0),
                NguoiXuat = "KHO-SQL",
                LyDo = "SQL repository integration",
                Note = "export"
            });
            Require(exportId > 0, "InsertXuat returned an invalid identity.");

            var deliveryId = repository.InsertGiao(new TraHangQTChungGiao
            {
                PhieuXuLyBatThuongId = ticketId,
                LotGiao = "LOT-RW-SQL",
                MaHang = "PART-RW",
                SoLuongGiao = 5,
                ThoiGian = new DateTime(2026, 10, 26, 9, 0, 0),
                NgayGiao = "2026-10-26",
                NguoiNhan = "SX-SQL",
                BoPhanNhan = "REWORK",
                SoPhieuGiaoNhan = "GIAO-SQL-01",
                Note = "delivery"
            });
            Require(deliveryId > 0, "InsertGiao returned an invalid identity.");

            var qcId = repository.InsertQC(new TraHangQTChungQC
            {
                PhieuXuLyBatThuongId = ticketId,
                SoLuongDaRework = 5,
                SoLuongOK = 4,
                SoLuongNG = 1,
                DaKiemTraTem = false,
                ThoiGian = new DateTime(2026, 10, 26, 10, 0, 0),
                NguoiQC = "QC-SQL",
                KetLuan = "OK=4;NG=1",
                Note = "qc"
            });
            Require(qcId > 0, "InsertQC returned an invalid identity.");

            var ngId = repository.InsertNhapNG(new TraHangQTChungNhapNG
            {
                PhieuXuLyBatThuongId = ticketId,
                SlotIdOK = 12,
                SlotIdNG = 13,
                LotNhapLai = "LOT-RW-SQL-NG",
                MaHang = "PART-RW",
                SoLuongNG = 1,
                SlotIdNhap = 13,
                NgayNhap = new DateTime(2026, 10, 26, 11, 0, 0),
                NguoiNhap = "KHO-SQL",
                LyDo = "NG after rework",
                Note = "receive NG"
            });
            Require(ngId > 0, "InsertNhapNG returned an invalid identity.");

            var exports = repository.GetXuat(ticketId);
            var exportByOperation = repository.GetXuatByOperationId(operationId);
            var exportById = repository.GetXuatById(exportId);
            var deliveries = repository.GetGiao(ticketId);
            var deliveryById = repository.GetGiaoById(deliveryId);
            var qc = repository.GetQC(ticketId);
            var ngRows = repository.GetNhapNG(ticketId);
            var ngById = repository.GetNhapNGById(ngId);

            Require(exports.Count == 1 && exports[0].SoLuongXuat == 8, "GetXuat returned incorrect export rows.");
            Require(exportByOperation?.Id == exportId && exportByOperation.OperationId == operationId,
                "GetXuatByOperationId failed to resolve the idempotency key.");
            Require(exportById?.LotXuat == "LOT-RW-SQL", "GetXuatById failed to map the export row.");
            Require(deliveries.Count == 1 && deliveries[0].SoLuongGiao == 5 &&
                    deliveryById?.SoPhieuGiaoNhan == "GIAO-SQL-01",
                "GetGiao/GetGiaoById returned incorrect delivery data.");
            Require(qc?.SoLuongOK == 4 && qc.SoLuongNG == 1, "GetQC returned incorrect QC values.");
            Require(ngRows.Count == 1 && ngById?.SoLuongNG == 1, "GetNhapNG/GetNhapNGById returned incorrect NG rows.");

            repository.UpdateLatestGiaoNote(ticketId, "hoàn thành rework", "SX-SQL");
            repository.UpdateDaKiemTraTem(qcId, true);
            Require(repository.GetQC(ticketId).DaKiemTraTem, "UpdateDaKiemTraTem was not persisted.");
            Require(repository.GetTongSoLuongDaXuat(ticketId) == 8, "Export total is incorrect.");
            Require(repository.GetTongSoLuongDaGiao(ticketId) == 5, "Delivery total is incorrect.");
            Require(repository.GetTongSoLuongOK(ticketId) == 4, "QC OK total is incorrect.");
            Require(repository.GetTongSoLuongNG(ticketId) == 1, "QC NG total is incorrect.");
            Require(repository.GetTongSoLuongDaNhapNG(ticketId) == 1, "NG receipt total is incorrect.");
            Require(repository.DaXuatKho(ticketId) && repository.DaGiaoSanXuat(ticketId) &&
                    repository.DaQCXacNhan(ticketId) && repository.DaNhapNG(ticketId),
                "Workflow existence projections returned false.");
            var timeline = repository.GetTimeline(ticketId);
            Require(timeline.Count == 4, $"Timeline should contain XUAT/GIAO/QC/NHAP_NG, got {timeline.Count}.");
            Require(timeline.Exists(x => x.Buoc == "XUAT") &&
                    timeline.Exists(x => x.Buoc == "GIAO") &&
                    timeline.Exists(x => x.Buoc == "QC") &&
                    timeline.Exists(x => x.Buoc == "NHAP_NG"),
                "Timeline is missing one or more workflow event types.");

            // Duplicate operation IDs must be rejected by the real unique index.
            bool duplicateOperationRejected = false;
            try
            {
                repository.InsertXuat(new TraHangQTChungXuat
                {
                    OperationId = operationId,
                    PhieuXuLyBatThuongId = ticketId,
                    SlotIdNguon = 12,
                    LotXuat = "LOT-RW-SQL",
                    LoaiXuat = "Rework",
                    MaHang = "PART-RW",
                    SoLuongXuat = 1,
                    TonTruoc = 12,
                    TonSau = 11,
                    NgayXuat = DateTime.Now,
                    NguoiXuat = "KHO-SQL"
                });
            }
            catch (SqlException)
            {
                duplicateOperationRejected = true;
            }
            Require(duplicateOperationRejected, "Duplicate export OperationId must be rejected by SQL.");

            uow.Rollback();
        }
        catch
        {
            if (uow.IsInTransaction) uow.Rollback();
            throw;
        }

        var remaining = Convert.ToInt32(Scalar(connectionString, $@"
SELECT
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungXuat WHERE PhieuXuLyBatThuongId={ticketId}) +
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungGiao WHERE PhieuXuLyBatThuongId={ticketId}) +
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungQC WHERE PhieuXuLyBatThuongId={ticketId}) +
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungNhapNG WHERE PhieuXuLyBatThuongId={ticketId});"));
        Require(remaining == 0, $"Repository caller rollback left {remaining} rework ledger rows behind.");
        Console.WriteLine("PASS: production TraHangQTChungRepository CRUD/map/ledger totals/timeline/OperationId uniqueness/caller rollback.");
    }

    private static PhieuXuLyBatThuong NewTicket(string number, int returnLineId) => new()
    {
        SoPhieu = number,
        Nguon = NguonXuLyBatThuong.TraNoiBo,
        PhieuTraHangId = 77,
        PhieuTraHangCTId = returnLineId,
        SoPhieuTraHangGoc = "RETURN-SQL",
        SlotIdNguon = 41,
        LotNguon = "LOT-QC-TICKET",
        Model = "MODEL-SQL",
        MaSanPham = "PART-SQL",
        SoLo = "LOT-SQL",
        SoLoLoi = "LOT-SQL-NG",
        SoLuongLoi = 4,
        NoiDungBatThuong = "integration",
        PhanLoaiXuLy = "QC",
        BoPhanPhatHanh = "QA",
        HuongXuLy = HuongXuLyBatThuong.ChuaXacDinh,
        CreatedBy = "QC-SQL"
    };

    private static void EnsureFixtureNamesAreFree(string connectionString)
    {
        var exists = Convert.ToInt32(Scalar(connectionString, @"
SELECT COUNT(1)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=N'dbo' AND t.name IN
(
 N'FVN_PhieuXuLyBatThuong', N'FVN_PhieuTraHangCT',
 N'FVN_TraHangQTChungXuat', N'FVN_TraHangQTChungGiao',
 N'FVN_TraHangQTChungQC', N'FVN_TraHangQTChungNhapNG'
);"));
        Require(exists == 0,
            "Refusing to create repository fixtures because one or more dbo business table names already exist. Run only against the disposable --engine-only database.");
    }

    private static void CreateFixtures(string connectionString)
    {
        Execute(connectionString, @"
CREATE TABLE dbo.FVN_PhieuTraHangCT
(
    Id int NOT NULL PRIMARY KEY,
    PhieuXuLyBatThuongId int NULL
);
CREATE TABLE dbo.FVN_PhieuXuLyBatThuong
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SoPhieu nvarchar(50) NOT NULL,
    Nguon int NOT NULL,
    PhieuTraHangCTId int NOT NULL,
    PhieuTraHangId int NULL,
    SoPhieuTraHangGoc nvarchar(100) NULL,
    PhieuKhachTraId int NULL,
    SlotIdNguon int NULL,
    LotNguon nvarchar(100) NULL,
    Model nvarchar(100) NULL,
    MaSanPham nvarchar(100) NULL,
    SoLo nvarchar(100) NULL,
    SoLoLoi nvarchar(100) NULL,
    SoLuongLoi int NOT NULL,
    NoiDungBatThuong nvarchar(500) NULL,
    PhanLoaiXuLy nvarchar(100) NULL,
    BoPhanPhatHanh nvarchar(100) NULL,
    Status int NOT NULL,
    HuongXuLy int NOT NULL,
    NgayDinhHuong datetime NULL,
    NguoiDinhHuong nvarchar(100) NULL,
    LyDoHuy nvarchar(500) NULL,
    NgayHuy datetime NULL,
    NguoiHuy nvarchar(100) NULL,
    CreatedAt datetime NOT NULL,
    CreatedBy nvarchar(100) NULL,
    UpdatedAt datetime NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE TABLE dbo.FVN_TraHangQTChungXuat
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    OperationId uniqueidentifier NULL,
    PhieuXuLyBatThuongId int NOT NULL,
    SlotIdNguon int NOT NULL,
    LotXuat nvarchar(100) NULL,
    LoaiXuat nvarchar(50) NULL,
    MaHang nvarchar(100) NULL,
    SoLuongXuat int NOT NULL,
    TonTruoc int NOT NULL,
    TonSau int NOT NULL,
    NgayXuat datetime NOT NULL,
    NguoiXuat nvarchar(100) NULL,
    LyDo nvarchar(500) NULL,
    Note nvarchar(1000) NULL
);
CREATE UNIQUE INDEX UX_QC_ReworkExport_OperationId
ON dbo.FVN_TraHangQTChungXuat(OperationId)
WHERE OperationId IS NOT NULL;

CREATE TABLE dbo.FVN_TraHangQTChungGiao
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PhieuXuLyBatThuongId int NOT NULL,
    LotGiao nvarchar(100) NULL,
    MaHang nvarchar(100) NULL,
    SoLuongGiao int NOT NULL,
    ThoiGian datetime NOT NULL,
    NgayGiao nvarchar(30) NULL,
    NguoiNhan nvarchar(100) NULL,
    BoPhanNhan nvarchar(100) NULL,
    SoPhieuGiaoNhan nvarchar(100) NULL,
    Note nvarchar(1000) NULL
);
CREATE TABLE dbo.FVN_TraHangQTChungQC
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PhieuXuLyBatThuongId int NOT NULL,
    SoLuongDaRework int NOT NULL,
    SoLuongOK int NOT NULL,
    SoLuongNG int NOT NULL,
    DaKiemTraTem bit NOT NULL,
    ThoiGian datetime NOT NULL,
    NguoiQC nvarchar(100) NULL,
    KetLuan nvarchar(500) NULL,
    Note nvarchar(1000) NULL
);
CREATE TABLE dbo.FVN_TraHangQTChungNhapNG
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PhieuXuLyBatThuongId int NOT NULL,
    SlotIdOK int NULL,
    SlotIdNG int NULL,
    LotNhapLai nvarchar(100) NULL,
    MaHang nvarchar(100) NULL,
    SoLuongNG int NOT NULL,
    SlotIdNhap int NULL,
    NgayNhap datetime NOT NULL,
    NguoiNhap nvarchar(100) NULL,
    LyDo nvarchar(500) NULL,
    Note nvarchar(1000) NULL
);");
        Console.WriteLine("PASS: disposable XuLyHangLoi repository fixture schema created (fixed dbo names confirmed absent).");
    }

    private static void DropFixtures(string connectionString)
    {
        Execute(connectionString, @"
DROP TABLE IF EXISTS dbo.FVN_TraHangQTChungNhapNG;
DROP TABLE IF EXISTS dbo.FVN_TraHangQTChungQC;
DROP TABLE IF EXISTS dbo.FVN_TraHangQTChungGiao;
DROP TABLE IF EXISTS dbo.FVN_TraHangQTChungXuat;
DROP TABLE IF EXISTS dbo.FVN_PhieuXuLyBatThuong;
DROP TABLE IF EXISTS dbo.FVN_PhieuTraHangCT;");
    }

    private static object Scalar(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        connection.Open();
        return command.ExecuteScalar();
    }

    private static (int, int) ScalarPair(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        connection.Open();
        using var reader = command.ExecuteReader();
        Require(reader.Read(), "Expected scalar postcondition row.");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        connection.Open();
        command.ExecuteNonQuery();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
