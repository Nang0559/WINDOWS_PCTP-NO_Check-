using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Static regression guards for the transaction contract between the delivery repository
/// and Usp_Qrcode_Update_Stock2405. These tests do not replace SQL Server end-to-end tests.
/// </summary>
public sealed class DeliveryTransactionContractTests
{
    private static string ReadSource(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        Assert.True(File.Exists(path), "Linked source fixture missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void StockProcedure_PreservesCallerOwnedTransaction()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock2405.sql");

        Assert.Contains("DECLARE @StartedTransaction BIT = 0", sql);
        Assert.Contains("IF @@TRANCOUNT = 0", sql);
        Assert.Contains("SAVE TRANSACTION UspQrcodeUpdateStock2405", sql);
        Assert.Contains("IF @StartedTransaction = 1", sql);
        Assert.Contains("COMMIT TRANSACTION;", sql);
        Assert.Contains("ROLLBACK TRANSACTION UspQrcodeUpdateStock2405", sql);
        Assert.Contains("DECLARE @ErrorSnapshot TABLE", sql);
        Assert.Contains("FROM @ErrorSnapshot", sql);
        Assert.Contains("ELSE IF XACT_STATE() = -1", sql);
        Assert.Contains("THROW; -- the caller must roll back the doomed outer transaction", sql);
    }

    [Fact]
    public void StockProcedure_SerializesDuplicateConfirmationBeforeCheckingExistingDelivery()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock2405.sql");

        var transactionStart = sql.IndexOf("IF @@TRANCOUNT = 0", StringComparison.OrdinalIgnoreCase);
        var appLock = sql.IndexOf("sys.sp_getapplock", StringComparison.OrdinalIgnoreCase);
        var duplicateLookup = sql.IndexOf("-- 6.1. KIỂM TRA PHIẾU ĐÃ LƯU", StringComparison.OrdinalIgnoreCase);

        Assert.Contains("PCTP_DELIVERY:", sql);
        Assert.Contains("@LockOwner = N'Transaction'", sql);
        Assert.Contains("@LockMode = N'Exclusive'", sql);
        Assert.True(transactionStart >= 0 && appLock > transactionStart && duplicateLookup > appLock,
            "The transaction must start first, then acquire a transaction-owned application lock before the existing-delivery lookup.");
    }

    [Fact]
    public void StockProcedure_LocksPhysicalLotAndGuardsFinalDecrement()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock2405.sql");

        Assert.Contains("FROM STOCKTP WITH (UPDLOCK, HOLDLOCK, ROWLOCK)", sql);
        Assert.Contains("AND ISNULL(SLCONLAI, 0) >= @_SLUD", sql);
        Assert.Contains("SET @StockRowsUpdated = @@ROWCOUNT", sql);
        Assert.Contains("IF @StockRowsUpdated <> 1", sql);
        Assert.Contains("Tồn kho LOT đã thay đổi trong lúc xác nhận giao", sql);
    }

    [Fact]
    public void StockProcedureSp_SerializesFifoAndGuardsStockDecrement()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock_SP.sql");

        var transactionStart = sql.IndexOf("IF @@TRANCOUNT = 0", StringComparison.OrdinalIgnoreCase);
        var appLock = sql.IndexOf("sys.sp_getapplock", StringComparison.OrdinalIgnoreCase);
        var fifoCursor = sql.IndexOf("DECLARE cur CURSOR", StringComparison.OrdinalIgnoreCase);
        var stockUpdate = sql.IndexOf("UPDATE STOCKTP", StringComparison.OrdinalIgnoreCase);

        Assert.Contains("PCTP_DELIVERY_FIFO_SP", sql);
        Assert.Contains("@LockOwner = N'Transaction'", sql);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", sql);
        Assert.Contains("AND ISNULL(SLCONLAI, 0) >= @slCan", sql);
        Assert.Contains("SET @StockUpdated = CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END", sql);
        Assert.Contains("IF @StockUpdated <> 1", sql);
        Assert.True(transactionStart >= 0 && appLock > transactionStart && fifoCursor > appLock && stockUpdate > fifoCursor,
            "The FIFO path must start/join its transaction and acquire the transaction-owned lock before reading delivery rows or decrementing stock.");
    }

    [Fact]
    public void YmvnFifoAllocation_UsesStableTieBreakerForEqualReceiptDates()
    {
        var source = ReadSource("PhieuKhoRepository.source.cs");
        Assert.Contains("ORDER BY NGAYNHAP ASC, LOT ASC", source);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", source);
        Assert.Contains("WHERE LOT = @lot AND SLCONLAI >= @sl", source);
    }

    [Fact]
    public void Receiving_SerializesSameLotBeforeReadingCumulativeReceivedQuantity()
    {
        var service = ReadSource("NhapTpReceivingService.source.cs");
        var guard = ReadSource("StockTpImportGuardRepository.source.cs");

        var qrLock = service.IndexOf("_importGuard.AcquireImportLock(qr.RawQr)", StringComparison.Ordinal);
        var lotLock = service.IndexOf("_importGuard.AcquireLotImportLock(lotNo)", StringComparison.Ordinal);
        var duplicateCheck = service.IndexOf("if (DaNhapTruocDo(qr))", qrLock, StringComparison.Ordinal);
        var receivedQuantityRead = service.IndexOf("_stockTpRepo.GetSlDaNhap(lotNo)", lotLock, StringComparison.Ordinal);

        Assert.True(qrLock >= 0 && lotLock > qrLock && duplicateCheck > lotLock,
            "The QR lock and then LOT lock must be acquired before the transactional duplicate check.");
        Assert.True(receivedQuantityRead > lotLock,
            "Cumulative STOCKTP received quantity must be read only after same-LOT receipts are serialized.");
        Assert.Contains("NHAPTP_LOT_", guard);
        Assert.Contains("@LockOwner   = 'Transaction'", guard);
        Assert.Contains("AcquireLotImportLock phải được gọi bên trong transaction", guard);
    }

    [Fact]
    public void Receiving_SlotLotMetadataIsPersistedByKhoCoreWithinMovement()
    {
        var receiving = ReadSource("NhapTpReceivingService.source.cs");
        var movement = ReadSource("StockMovementService.source.cs");
        var slotRepository = ReadSource("BulkStockSlotRepository.source.cs");

        Assert.Contains("ReceivingMaPhieu = PhieuNoHelper.NewMaPhieuNhap(lotNo)", receiving);
        Assert.Contains("TemCode = qr.MaPhieu", receiving);
        Assert.Contains("QrData = qr.RawQr", receiving);
        Assert.DoesNotContain("_phieuRepo.InsertPhieuMoi(", receiving);
        Assert.DoesNotContain("SlotLotWrittenByCaller = true", receiving);

        Assert.Contains("MaPhieu = request.ReceivingMaPhieu", movement);
        Assert.Contains("SoPhieuTong = request.ReceivingSoPhieuTong", movement);
        Assert.Contains("ParentSoPhieu = request.ReceivingParentSoPhieu", movement);
        Assert.Contains("ImportDate = isNormalReceive", movement);
        Assert.Contains("INSERT INTO SlotLot", slotRepository);
        Assert.Contains("AddLot phải chạy trong transaction", slotRepository);
        Assert.Contains("NgaySX, SoPhieuTong, MaPhieu, ParentSoPhieu, PhieuStatus", slotRepository);
        Assert.Contains("UpdateSlotHeaderBySlotId(slotId)", slotRepository);

        var addToSlotStart = movement.IndexOf("private StockMovementResult AddToSlot", StringComparison.Ordinal);
        Assert.True(addToSlotStart >= 0, "StockMovementService.AddToSlot must exist.");
        var receivingWrite = movement.IndexOf("_receiving.Update(request.LotNo", addToSlotStart, StringComparison.Ordinal);
        var slotLotWrite = movement.IndexOf("_slots.AddLot(request.TargetSlotId.Value", addToSlotStart, StringComparison.Ordinal);
        Assert.True(receivingWrite >= 0 && slotLotWrite > receivingWrite,
            "STOCKTP receiving persistence and SlotLot detail must both be invoked by AddToSlot before workflow commit.");

    }

    [Fact]
    public void Receiving_UsesKhoCoreOwnedStockTpPersistenceInAllModuleFactories()
    {
        var repository = ReadSource("StockReceivingRepository.source.cs");
        var receivingFactory = ReadSource("NhapTpModuleFactory.source.cs");
        var stockFactory = ReadSource("MainStockModuleFactory.source.cs");

        Assert.Contains("IStockReceivingRepository", repository);
        Assert.Contains(": this(db, uow, \"dbo.STOCKTP\")", repository);
        Assert.Contains("\"#STOCKTP\"", repository);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", repository);
        Assert.Contains("SLNHAP = ISNULL(SLNHAP, 0) + @sl", repository);
        Assert.Contains("SLCONLAI = ISNULL(SLCONLAI, 0) + @sl", repository);
        Assert.Contains("NGAYNHAP = GETDATE()", repository);
        Assert.Contains("SATUS = @status", repository);
        Assert.Contains("affected != 1", repository);
        Assert.Contains("EnsureTransaction()", repository);
        Assert.Contains("StockReceivingRepository(dbExecutor, uow)", receivingFactory);
        Assert.Contains("StockReceivingRepository(dbExecutor, uow)", stockFactory);
        Assert.DoesNotContain("StockReceivingRepositoryAdapter", receivingFactory);
        Assert.DoesNotContain("StockReceivingRepositoryAdapter", stockFactory);
    }

    [Fact]
    public void Receiving_And_MainStock_FactoriesUseTransactionalMovementGuardRepository()
    {
        var receivingFactory = ReadSource("NhapTpModuleFactory.source.cs");
        var stockFactory = ReadSource("MainStockModuleFactory.source.cs");

        // NHAP_TP / export requests carry a ReferenceType; the legacy adapter's TryRegisterMovement
        // throws NotSupportedException, which surfaced as "Lỗi nhập tồn kho: Stock movement idempotency requires ...".
        Assert.Contains("new BulkStockSlotRepository(dbExecutor, uow)", receivingFactory);
        Assert.Contains("new BulkStockSlotRepository(dbExecutor, uow)", stockFactory);
        Assert.DoesNotContain("new LegacyStockSlotRepositoryAdapter(", receivingFactory);
        Assert.DoesNotContain("new LegacyStockSlotRepositoryAdapter(", stockFactory);
    }

    [Fact]
    public void ReworkFactory_WiresTransactionalMovementGuardIntoLegacySlotAdapter()
    {
        var factory = ReadSource("XuLyHangLoiModuleFactory.source.cs");
        var adapter = ReadSource("LegacyStockSlotRepositoryAdapter.source.cs");

        Assert.Contains("new ReworkStockSlotAdapter(", factory);
        Assert.Contains("new BulkStockSlotRepository(dbExecutor, uow)", factory);
        Assert.Contains("_movementGuard.TryRegisterMovement(", adapter);
        // Without a guard the adapter must still fail closed (never an in-memory idempotency claim).
        Assert.Contains("NotSupportedException", adapter);
    }

    [Fact]
    public void DeliveryRepository_DoesNotConsumeRolledBackSuccessRowsWhenProcedureReturnsErrors()
    {
        var source = ReadSource("PhieuKhoRepository.source.cs");
        const string guard = "if (errors.Rows.Count > 0)";
        const string stockProcessing = "coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);";

        var firstGuard = source.IndexOf(guard, StringComparison.OrdinalIgnoreCase);
        var secondGuard = source.IndexOf(guard, firstGuard + guard.Length, StringComparison.OrdinalIgnoreCase);
        var firstStock = source.IndexOf(stockProcessing, StringComparison.Ordinal);
        var secondStock = source.IndexOf(stockProcessing, firstStock + stockProcessing.Length, StringComparison.Ordinal);

        Assert.True(firstGuard >= 0 && secondGuard > firstGuard,
            "Both production stored-procedure delivery routes must abort processing when the error resultset is non-empty.");
        Assert.True(firstStock > firstGuard && secondStock > secondGuard,
            "Each error guard must appear before processing the procedure's success resultset.");
        Assert.Contains("Uow.Rollback();", source.Substring(firstGuard, firstStock - firstGuard));
        Assert.Contains("Uow.Rollback();", source.Substring(secondGuard, secondStock - secondGuard));
    }

    [Fact]
    public void DeliveryRepository_UsesExplicitTransactionAroundStockAndFinalization()
    {
        var source = ReadSource("PhieuKhoRepository.source.cs");

        Assert.Contains("Uow.Begin();", source);
        Assert.Contains("HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);", source);
        Assert.Contains("Uow.Commit();", source);
        Assert.Contains("Uow.Rollback();", source);
        Assert.Contains("if (!Uow.IsInTransaction)", source);
    }
}
