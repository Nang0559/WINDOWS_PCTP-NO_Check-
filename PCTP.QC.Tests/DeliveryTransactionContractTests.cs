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
        Assert.Contains("ELSE IF XACT_STATE() = -1", sql);
        Assert.Contains("THROW; -- the caller must roll back the doomed outer transaction", sql);
    }

    [Fact]
    public void StockProcedure_SerializesDuplicateConfirmationBeforeCheckingExistingDelivery()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock2405.sql");

        var appLock = sql.IndexOf("sys.sp_getapplock", StringComparison.OrdinalIgnoreCase);
        var duplicateLookup = sql.IndexOf("-- 6.1. KIỂM TRA PHIẾU ĐÃ LƯU", StringComparison.OrdinalIgnoreCase);

        Assert.Contains("PCTP_DELIVERY:", sql);
        Assert.Contains("@LockOwner = N'Transaction'", sql);
        Assert.Contains("@LockMode = N'Exclusive'", sql);
        Assert.True(appLock >= 0 && duplicateLookup > appLock,
            "A transaction-owned application lock must be acquired before the existing-delivery lookup.");
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
