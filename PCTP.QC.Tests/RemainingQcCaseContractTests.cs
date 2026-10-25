using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Production source-contract guards for remaining workflow acceptance gaps.
/// These prove the current source wiring is intentional; they do not claim a
/// live SQL database or WinForms journey was executed.
/// </summary>
public sealed class RemainingQcCaseContractTests
{
    private static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(path), "Linked production source missing: " + path);
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static string Method(string source, string start, string next)
    {
        var begin = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(begin >= 0, "Production method not found: " + start);
        var end = source.IndexOf(next, begin + start.Length, StringComparison.Ordinal);
        Assert.True(end > begin, "Could not isolate production method: " + start);
        return source.Substring(begin, end - begin);
    }

    [Fact]
    public void ReworkMovements_UseDistinctIdempotencyKeysAcrossStagesAndLots()
    {
        var source = Read("ReworkStockService.source.cs");
        var export = Method(source,
            "public ScanResult XuatKhoRework(\n            int phieuXuLyId,\n            int slotLotId,\n            string lotNo,\n            int soLuong,\n            string nguoiXuat,\n            Guid operationId)",
            "/// <summary>null = Slot hợp lệ");
        var ng = Method(source, "public ScanResult NhapLaiHangNG(", "public ScanResult NhapLaiHangOK(");
        var ok = Method(source, "public ScanResult NhapLaiHangOK(", "public ScanResult HoanTraKhoKhiHuy(");
        var cancel = Method(source, "public ScanResult HoanTraKhoKhiHuy(", "private ScanResult FailAndRollback(");

        Assert.Contains("ReferenceType = \"QT_REWORK_EXPORT\"", export);
        Assert.Contains("if (operationId == Guid.Empty)", export);
        Assert.Contains("ReferenceId = operationId.ToString(\"D\")", export);
        Assert.Contains("ReferenceType = \"QT_REWORK_NG_RECEIVE\"", ng);
        Assert.Contains("ReferenceType = \"QT_REWORK_OK_RECEIVE\"", ok);
        Assert.Contains("ReferenceType = \"QT_REWORK_CANCEL_RETURN\"", cancel);
        Assert.Contains("ReferenceId = phieuXuLyId + \":\" + nhom.SlotId + \":\" + lotChuan", cancel);
        Assert.DoesNotContain("ReferenceType = \"PHIEU_XU_LY_BAT_THUONG\"", source);
    }

    [Fact]
    public void QuarantineRelease_LocksThroughSharedSlotRepositoryBeforeRevalidating()
    {
        var service = Read("QuarantineReleaseService.source.cs");
        var release = Method(service, "public QuarantineReleaseResult Release(", "    }\n}");
        var begin = release.IndexOf("_uow.Begin()", StringComparison.Ordinal);
        var lockSlot = release.IndexOf("_slots.LockSlotForUpdate(slotId.Value)", StringComparison.Ordinal);
        var reread = release.IndexOf("var details = _slots.GetLotDetails(request.SlotLotId)", StringComparison.Ordinal);
        var mutation = release.IndexOf("_slots.DecreaseLotQuantity(", StringComparison.Ordinal);
        Assert.True(begin >= 0 && begin < lockSlot && lockSlot < reread && reread < mutation,
            "Release must lock the Slot and re-read quarantine state before changing stock.");

        var adapter = Read("LegacyStockSlotRepositoryAdapter.source.cs");
        var adapterLock = Method(adapter, "public void LockSlotForUpdate(", "public void DecreaseLotQuantity(");
        Assert.Contains("_legacy.LockSlotForUpdate(slotId)", adapterLock);
        Assert.DoesNotContain("NotSupportedException", adapterLock);
    }

    [Fact]
    public void ReworkNgImport_UsesQuarantineMovementAndDoesNotDirectlyCreditStockTp()
    {
        var rework = Read("ReworkStockService.source.cs");
        var ng = Method(rework, "public ScanResult NhapLaiHangNG(", "public ScanResult NhapLaiHangOK(");
        Assert.Contains("MovementReworkNgReceive", ng);
        Assert.Contains("_stockMovement.Receive(new StockMovementRequest", ng);
        Assert.Contains("InsertNhapNG", ng);
        Assert.Contains("REWORK_NG_IMPORT", ng);
        Assert.Contains("_historyRepo.SaveHistory", ng);
        Assert.Contains("_uow.Begin();", ng);
        Assert.Contains("_uow.Commit();", ng);
        Assert.Contains("FailAndRollback", ng);
        Assert.DoesNotContain("_stockTpRepo.Update", ng);
        Assert.DoesNotContain("_stockTpRepo.Insert", ng);
    }

    [Fact]
    public void ReworkNgMovement_CreatesStatusThreeAndNeverIncreasesAvailableBalance()
    {
        var source = Read("StockMovementService.source.cs");
        var add = Method(source, "private StockMovementResult AddToSlot(", "private StockMovementResult RemoveFromSlot(");
        Assert.Contains("StockMovementRequest.Types.ReworkNgReceive", add);
        Assert.Contains("PhieuStatus = isQuarantineReceive ? (byte)3 : (byte)0", add);
        Assert.Contains("!isQuarantineReceive", add);

        var remove = Method(source, "private StockMovementResult RemoveFromSlot(", "private bool TryClaimRequest(");
        Assert.Contains("if (sourceStatus != 0)", remove);
    }

    [Fact]
    public void QrWorkflowAdapter_MapsReceivingAndDeliveryIntoCentralIdempotentMovementCommands()
    {
        var source = Read("QRCodeWorkflowAdapter.source.cs");
        var receive = Method(source, "public static StockMovementRequest BuildReceivingRequest(", "public static StockMovementRequest BuildCustomerDeliveryRequest(");
        var delivery = source.Substring(source.IndexOf("public static StockMovementRequest BuildCustomerDeliveryRequest(", StringComparison.Ordinal));
        Assert.Contains("QRCodeParser.ParseQRCode(rawQr)", receive);
        Assert.Contains("MovementType = StockMovementRequest.Types.Receive", receive);
        Assert.Contains("ReferenceType = \"QR_RECEIVE\"", receive);
        Assert.Contains("QrData = qr.RawQr", receive);
        Assert.Contains("QRCodeParser.ParseQRCode(rawQr)", delivery);
        Assert.Contains("MovementType = StockMovementRequest.Types.Export", delivery);
        Assert.Contains("ReferenceType = \"QR_DELIVERY\"", delivery);
        Assert.Contains("ReferenceId = deliveryReference.Trim()", delivery);
        Assert.Contains("QrData = qr.RawQr", delivery);
    }

    [Fact]
    public void ReworkNgPathKeepsWorkflowAuditAndStockUnderOneUnitOfWork()
    {
        var source = Read("ReworkStockService.source.cs");
        var ng = Method(source, "public ScanResult NhapLaiHangNG(", "public ScanResult NhapLaiHangOK(");
        var receive = ng.IndexOf("_stockMovement.Receive(", StringComparison.Ordinal);
        var workflow = ng.IndexOf("_qtChungRepo.InsertNhapNG(", StringComparison.Ordinal);
        var history = ng.IndexOf("_historyRepo.SaveHistory(", StringComparison.Ordinal);
        var commit = ng.IndexOf("_uow.Commit();", StringComparison.Ordinal);
        Assert.True(receive >= 0 && workflow > receive && history > workflow && commit > history,
            "NG stock, workflow record, audit history and commit must remain ordered inside one UnitOfWork.");
    }
}
