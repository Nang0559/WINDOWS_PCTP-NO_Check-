using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class ReturnDeliveryConsistencySourceContractTests
{
    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));

    [Fact]
    public void CustomerReturn_DeliveryNoteAndWorkflowStatusShareOneUnitOfWork()
    {
        var caller = ReadSource("KhachTraHangService.source.cs");
        var qt = ReadSource("QTChungService.source.cs");

        var callerStart = caller.IndexOf("public void DanhDauPhieuGiaoGocChoGiaoBu(", StringComparison.Ordinal);
        var callerEnd = caller.IndexOf("\n        }", callerStart, StringComparison.Ordinal);
        Assert.True(callerStart >= 0 && callerEnd > callerStart);
        var callerMethod = caller.Substring(callerStart, callerEnd - callerStart);

        Assert.Contains("_qtChungService.DanhDauChoGiaoBu(", callerMethod, StringComparison.Ordinal);
        Assert.Contains("() => _phieuGiaoRepo.CapNhatNotePhieuGiao(dinhDanhPhieuGiao, note)", callerMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("Uow.Begin()", callerMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("Uow.Commit()", callerMethod, StringComparison.Ordinal);

        var qtStart = qt.IndexOf("public ScanResult DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien, Action beforeStatusUpdate)", StringComparison.Ordinal);
        var qtEnd = qt.IndexOf("\n        public ScanResult NhapLaiHangNG(", qtStart, StringComparison.Ordinal);
        Assert.True(qtStart >= 0 && qtEnd > qtStart);
        var qtMethod = qt.Substring(qtStart, qtEnd - qtStart);

        var begin = qtMethod.IndexOf("_uow.Begin()", StringComparison.Ordinal);
        var callback = qtMethod.IndexOf("beforeStatusUpdate?.Invoke()", StringComparison.Ordinal);
        var status = qtMethod.IndexOf("UpdateStatusWithConcurrencyCheck(", StringComparison.Ordinal);
        var commit = qtMethod.IndexOf("_uow.Commit()", StringComparison.Ordinal);
        var rollback = qtMethod.IndexOf("SafeRollback()", StringComparison.Ordinal);

        Assert.True(begin >= 0 && begin < callback);
        Assert.True(callback < status && status < commit);
        Assert.True(rollback > commit);
        Assert.Contains("bool alreadyWaitingForCompensation", qtMethod, StringComparison.Ordinal);
        Assert.Contains("beforeStatusUpdate?.Invoke()", qtMethod, StringComparison.Ordinal);
        Assert.Contains("if (!alreadyWaitingForCompensation)", qtMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("if (phieu.Status == QTChungStatus.ChoGiaoBu)\n                return ScanResult.OK", qtMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_ExposesAtomicCallbackWithoutRemovingExistingCallContract()
    {
        var contract = ReadSource("IQTChungService.source.cs");
        Assert.Contains("DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien)", contract, StringComparison.Ordinal);
        Assert.Contains("DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien, Action beforeStatusUpdate)", contract, StringComparison.Ordinal);
    }
}
