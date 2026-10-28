using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// B0-01 specification tests for the YMVN customer-delivery stock path
/// (PhieuYmvnService.CapNhapKho -> PhieuKhoRepository.CapNhapKhoYMVN -> TruStockTpFifo).
///
/// These are SOURCE-CONTRACT characterization tests: they freeze the order of operations and the
/// statements of the legacy writer so that the delivery-command migration cannot change behaviour
/// silently. Runtime behaviour of the STOCKTP statements is covered on SQL Server by
/// PCTP.QC.SqlIntegration/YmvnDeliverySpecScenario (job sql-engine-integration).
///
/// They do NOT prove B0-01. When B0-01 is implemented, tests marked "B0_01_StillOpen" must be flipped
/// together with WMS_RELEASE_GATE.md; every other test is a parity requirement for the new command.
/// </summary>
public sealed class YmvnDeliverySpecContractTests
{
    private static string Read(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        Assert.True(File.Exists(path), "Linked source fixture missing: " + path);
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static string Repository() => Read("PhieuKhoRepository.source.cs");

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Marker not found: " + startMarker);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, "End marker not found after: " + startMarker);
        return source.Substring(start, end - start);
    }

    private static string YmvnMethod() => Slice(Repository(), "#region CapNhapKhoYMVN", "#endregion");

    private static string FifoMethod() => Slice(Repository(), "private bool TruStockTpFifo", "private void HoanTatSauKhiTruKho");

    private static int IndexOf(string body, string needle, int from = 0)
    {
        var index = body.IndexOf(needle, from, StringComparison.Ordinal);
        Assert.True(index >= 0, "Expected text not found (after position " + from + "): " + needle);
        return index;
    }

    private static int Count(string body, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = body.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    [Fact]
    public void Ymvn_ValidatesLotQuantityInputBeforeOpeningTheTransaction()
    {
        var body = YmvnMethod();
        var begin = IndexOf(body, "Uow.Begin();");

        Assert.True(IndexOf(body, "lotSl.Split(',')") < begin, "Parsing must finish before the UoW is opened.");
        Assert.True(IndexOf(body, "Định dạng LOT-số lượng không hợp lệ") < begin);
        Assert.True(IndexOf(body, "LOT không hợp lệ") < begin);
        Assert.True(IndexOf(body, "Không có LOT hợp lệ để xuất kho") < begin);
        Assert.Contains("if (string.IsNullOrWhiteSpace(lotSl)) return false;", body);
    }

    [Fact]
    public void Ymvn_ParsesLotQuantityWithTheLastSeparatorAndRejectsNonPositiveQuantities()
    {
        var body = YmvnMethod();

        Assert.Contains("part.LastIndexOf('-')", body);
        Assert.Contains("separator <= 0 || separator >= part.Length - 1", body);
        Assert.Contains("int.TryParse(part.Substring(separator + 1).Trim(), out int sl) || sl <= 0", body);
        Assert.Contains("LotCodeHelper.TrimTo(part.Substring(0, separator).Trim(), LotCodeHelper.LEN_HEAD_FIXED)", body);
    }

    [Fact]
    public void Ymvn_StepOrderIsFrozen()
    {
        var body = YmvnMethod();
        var markers = new[]
        {
            "Uow.Begin();",
            "FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE {matchCondition}",
            "Không đủ tồn kho",
            "TruStockTpFifo(matchCondition, sl)",
            "bulkService.TruKhoAoTheoLot(lotKey, sl, manageTransaction: false)",
            "INSERT INTO LUUDOCQRCODE",
            "INSERT INTO LUUPHIEUGIAOHANG",
            "UPDATE [{tmpTable}] SET STATUS = 'OK' WHERE STT = @stt AND STATUS = 'NG'",
            "updatedTmp != 1",
            "DELETE FROM [{docQRTable}] WHERE MAHANGFCC = @maHang AND KETQUA = 'DG'",
            "HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY)",
            "DanhDauDaGiao(key, _cfg)",
            "Uow.Commit();"
        };

        var previous = -1;
        foreach (var marker in markers)
        {
            var index = IndexOf(body, marker, previous + 1);
            Assert.True(index > previous, "Step order changed around: " + marker);
            previous = index;
        }
    }

    [Fact]
    public void Ymvn_ShortageRollsBackTheWholeUnitBeforeReturningFalse()
    {
        var body = YmvnMethod();
        var shortage = IndexOf(body, "Không đủ tồn kho");
        var rollback = IndexOf(body, "Uow.Rollback();", shortage);
        var returnFalse = IndexOf(body, "return false;", shortage);

        Assert.True(rollback < returnFalse, "A shortage must roll back before returning the error to the caller.");
        Assert.Contains("SLC = sl, SLTK = slConlai, SLT = sl - slConlai", body);
    }

    [Fact]
    public void Ymvn_AnyExceptionRollsBackAndRethrows()
    {
        var body = YmvnMethod();
        Assert.Matches(new Regex(@"catch\s*\{\s*Uow\.Rollback\(\);\s*throw;\s*\}"), body);
        Assert.Contains("Không thể xác nhận phiếu giao STT={stt}", body);
        Assert.Contains("đã thay đổi trong lúc xử lý", body);
    }

    [Fact]
    public void Ymvn_SlotLotIsDeductedInTheSameUnitOfWorkAndFailsClosed()
    {
        var body = YmvnMethod();
        Assert.Contains("CreateBulkService()", body);
        Assert.Contains("manageTransaction: false", body);
        Assert.DoesNotContain("manageTransaction: true", body);

        var bulk = Read("BulkStockAdjustService.source.cs");
        Assert.Matches(new Regex(@"if \(!manageTransaction\)\s+throw new InvalidOperationException\(msg\);"), bulk);
        Assert.Contains("Chưa cấu hình IStockMovementService cho BulkStockAdjustService.", bulk);
    }

    [Fact]
    public void TruStockTpFifo_FreezesOrderGuardAndExactCoverage()
    {
        var body = FifoMethod();

        Assert.Contains("FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE {matchCondition} AND SLCONLAI > 0 ORDER BY NGAYNHAP ASC, LOT ASC", body);
        Assert.Contains("Math.Min(slDongNay, conLaiCanTru)", body);
        Assert.Contains("SLXUAT = ISNULL(SLXUAT,0) + @sl, SLCONLAI = SLCONLAI - @sl", body);
        Assert.Contains("WHERE LOT = @lot AND SLCONLAI >= @sl", body);
        Assert.Contains("if (affected != 1) return false;", body);
        Assert.Contains("return conLaiCanTru == 0;", body);
    }

    [Fact]
    public void Ymvn_MatchesLotsThroughTheSharedLotMatchPredicate()
    {
        var body = YmvnMethod();
        Assert.Contains("LotCodeHelper.BuildLotMatchSql(\"LOT\"", body);
    }

    [Fact]
    public void Ymvn_ScanRowsAreArchivedAndDeletedByPartNotBySttCurrentBehaviour()
    {
        // FINDING F-Y1 (needs product-owner confirmation): the DG scan rows of EVERY STT of the same part
        // are archived and deleted when one STT is delivered. Frozen as current behaviour.
        var body = YmvnMethod();
        Assert.Contains("WHERE MAHANGFCC = @maHang AND KETQUA = 'DG'", body);
        Assert.DoesNotContain("MAHANGFCC = @maHang AND KETQUA = 'DG' AND STT", body);
    }

    [Fact]
    public void StockTpWriters_AreLimitedToTheThreeKnownLegacyPaths()
    {
        var source = Repository();

        Assert.Equal(1, Count(source, "Usp_Qrcode_Update_Stock2405"));
        Assert.Equal(1, Count(source, "Usp_Qrcode_Update_Stock_SP"));
        Assert.Equal(1, Count(source, "UPDATE STOCKTP"));
        Assert.DoesNotContain("INSERT INTO STOCKTP", source);
        Assert.DoesNotContain("DELETE FROM STOCKTP", source);
        Assert.DoesNotContain("UPDATE [STOCKTP]", source);
    }

    [Fact]
    public void B0_01_StillOpen_MovementServiceIsOptionalInTheRepositoryConstructor()
    {
        // If this fails, gate item 4 of WMS_RELEASE_GATE.md (movement service required) may have been implemented.
        // Update the gate document and this test together; do not just delete the assertion.
        Assert.Contains("IStockMovementService stockMovement = null", Repository());
    }

    [Fact]
    public void YmvnService_ChecksFifoBeforeAnyStockMutationAndSkipsReleasedOrDeliveredRows()
    {
        var service = Read("PhieuYmvnService.source.cs");
        var gate = IndexOf(service, "EvaluateFifoViolations(tmpTable, docQrTable)");
        var release = IndexOf(service, "ReleaseFifoViolations(tmpTable, docQrTable, fifoViolations)");
        var recheck = IndexOf(service, "EvaluateFifoViolations(tmpTable, docQrTable)", gate + 1);
        var stock = IndexOf(service, "_phieuRepo.CapNhapKhoYMVN(");

        Assert.True(gate < release && release < recheck && recheck < stock,
            "FIFO gate -> release -> re-check must all happen before the first YMVN stock mutation.");
        Assert.Contains("if (!confirmation.IsCompleted || !confirmation.WaitForDecision())", service);
        Assert.Contains("if (releasedStt.Contains(stt))", service);
        Assert.Contains("if (lot == \"\" || status == \"OK\")", service);
    }

    [Fact]
    public void YmvnService_CommitsPerSttNotPerBatchCurrentBehaviour()
    {
        // FINDING F-Y4: every STT is its own transaction inside CapNhapKhoYMVN, so a batch of several STT is not atomic:
        // a business error is collected and the loop continues; an exception stops the loop with earlier STT already committed.
        var service = Read("PhieuYmvnService.source.cs");
        var loop = IndexOf(service, "foreach (DataRow row in donHang.Rows)");
        var call = IndexOf(service, "_phieuRepo.CapNhapKhoYMVN(", loop);

        Assert.Matches(new Regex(@"else if \(err != null\)\s+errors\.Add\(err\);"), service);
        Assert.Equal(1, Count(service.Substring(loop), "_phieuRepo.CapNhapKhoYMVN("));
        Assert.True(call > loop);
        Assert.DoesNotContain("BeginTransaction", service);
        Assert.DoesNotContain("Uow.Begin", service);
    }
}
