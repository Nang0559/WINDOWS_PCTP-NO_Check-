using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Contract test cho khoá đọc QR theo (CustomerNo, ScanMode = MP/SP) và việc nhả khoá khi kết thúc phiên.
/// Đọc source/SQL thật được link trong csproj, không cần SQL Server.
/// </summary>
public sealed class QrReaderLockMpSpContractTests
{
    private static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(path), "Missing production source fixture: " + path);
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static string Proc(string sql, string procName)
    {
        int start = sql.IndexOf("PROCEDURE dbo." + procName, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing procedure " + procName);
        var proc = sql.Substring(start);
        int end = proc.IndexOf("\nGO", StringComparison.Ordinal);
        return end >= 0 ? proc.Substring(0, end) : proc;
    }

    [Fact]
    public void Migration_LockTable_MustBeKeyedByCustomerAndScanMode()
    {
        var sql = Read("PCTP_QrReaderLock_MP_SP_Migration.sql");
        Assert.Contains("PRIMARY KEY (CustomerNo, ScanMode)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_RegistrationTable_MustBeKeyedByCustomerIpAndScanMode()
    {
        var sql = Read("PCTP_QrReaderLock_MP_SP_Migration.sql");
        Assert.Contains("PRIMARY KEY (CustomerNo, ClientIp, ScanMode)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Usp_QrReaderLock_Acquire")]
    [InlineData("Usp_QrReaderLock_Heartbeat")]
    [InlineData("Usp_QrReaderLock_Release")]
    [InlineData("Usp_QrReaderLock_ForceRelease")]
    [InlineData("Usp_QrReaderMachine_Upsert")]
    [InlineData("Usp_QrReaderMachine_Remove")]
    public void Migration_Procedures_MustValidateScanModeAsMpOrSp(string procName)
    {
        var proc = Proc(Read("PCTP_QrReaderLock_MP_SP_Migration.sql"), procName);
        Assert.Contains("@ScanMode", proc, StringComparison.Ordinal);
        Assert.Contains("IN (N'MP', N'SP') THROW", proc, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Usp_QrReaderLock_Acquire")]
    [InlineData("Usp_QrReaderLock_Heartbeat")]
    [InlineData("Usp_QrReaderLock_Release")]
    [InlineData("Usp_QrReaderLock_List")]
    [InlineData("Usp_QrReaderLock_ForceRelease")]
    public void Migration_LockProcedures_MustNotAcceptClientIpParameter(string procName)
    {
        // Định danh máy vẫn do server đọc: không đưa @ClientIp vào tham số của proc khoá.
        var proc = Proc(Read("PCTP_QrReaderLock_MP_SP_Migration.sql"), procName);
        var header = proc.Substring(0, proc.IndexOf("\nAS", StringComparison.Ordinal));
        Assert.DoesNotContain("@ClientIp", header, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Usp_QrReaderLock_Heartbeat")]
    [InlineData("Usp_QrReaderLock_Release")]
    [InlineData("Usp_QrReaderLock_ForceRelease")]
    public void Migration_Procedures_MustScopeDeleteAndUpdateByScanMode(string procName)
    {
        // Nhả/heartbeat khoá MP không được đụng vào khoá SP của cùng khách (và ngược lại).
        var proc = Proc(Read("PCTP_QrReaderLock_MP_SP_Migration.sql"), procName);
        Assert.Contains("ScanMode = @mode", proc, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_Acquire_MustSerializeAndRequireRegistrationForThatMode()
    {
        var acquire = Proc(Read("PCTP_QrReaderLock_MP_SP_Migration.sql"), "Usp_QrReaderLock_Acquire");
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", acquire, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ScanMode = @mode", acquire, StringComparison.Ordinal);
        Assert.Contains("IF @registered = 0", acquire, StringComparison.Ordinal);
        Assert.Contains("@Force = 1 OR @hb < DATEADD(SECOND, -@TtlSeconds, @now)", acquire, StringComparison.Ordinal);
    }

    [Fact]
    public void LockService_InvalidScanMode_MustNotThrowOutOfTryAcquire()
    {
        // TryAcquire cam kết fail-closed: NormalizeMode (có thể throw) phải nằm TRONG try.
        var source = Read("CustomerQrLockService.source.cs");
        var acquire = source.Substring(source.IndexOf("public QrLockInfo TryAcquire(string customerNo, string scanMode", StringComparison.Ordinal));
        acquire = acquire.Substring(0, acquire.IndexOf("public bool Heartbeat(string customerNo) ", StringComparison.Ordinal));
        int tryPos = acquire.IndexOf("try", StringComparison.Ordinal);
        int normalizePos = acquire.IndexOf("NormalizeMode(", StringComparison.Ordinal);
        Assert.True(tryPos >= 0 && normalizePos > tryPos, "NormalizeMode must be called inside the try block.");
    }

    [Fact]
    public void ScanMode_ForNewQrSession_MustUseSameSpFlagAsRestoreAndPhieuState()
    {
        // Mở mới và khôi phục phiên phải cùng một tiêu chí MP/SP (CoConfigSP), nếu không khoá sẽ sai chế độ.
        var source = Read("DocQrPresenter.qrlock.source.cs");
        Assert.DoesNotContain("CoLoaiSP && _c.PhieuView.IsLoaiSP", source, StringComparison.Ordinal);
        Assert.True(Regex.Matches(source, @"CoConfigSP && _c\.PhieuView\.IsLoaiSP").Count >= 2,
            "OnDocQRCode and TryRestoreExistingQrSession must both use CoConfigSP.");
    }

    [Fact]
    public void PermissionService_ReleaseScanLock_MustOnlyReleaseWhenHoldingLock()
    {
        var source = Read("MachinePermissionService.source.cs");
        var release = source.Substring(source.IndexOf("public void ReleaseScanLock()", StringComparison.Ordinal));
        release = release.Substring(0, release.IndexOf("public bool IsRegistered", StringComparison.Ordinal));
        Assert.Contains("if (!_holdsLock) return;", release, StringComparison.Ordinal);
        Assert.Contains("_locks.Release(_customerNo, mode)", release, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionService_ViewOnlyRegisteredMachine_MustRetryAcquireAfterOwnerReleases()
    {
        // Máy đã đăng ký nhưng khoá đang do máy khác giữ không được cache ChiXem vĩnh viễn.
        var source = Read("MachinePermissionService.source.cs");
        Assert.Contains("if (info.IsOwnedByMe || (!info.Registered && info.Error == null))", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PresenterContext_ClearDeliverySession_MustReleaseQrLock()
    {
        var source = Read("HVNPresenterContext.qrlock.source.cs");
        var clear = source.Substring(source.IndexOf("internal void ClearDeliverySession()", StringComparison.Ordinal));
        clear = clear.Substring(0, clear.IndexOf("internal bool IsCurrentDeliverySessionValid", StringComparison.Ordinal));
        Assert.Contains("MachinePermission?.ReleaseScanLock()", clear, StringComparison.Ordinal);
    }
}
