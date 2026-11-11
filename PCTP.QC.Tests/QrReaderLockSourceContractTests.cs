using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class QrReaderLockSourceContractTests
{
    private static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(path), "Missing production source fixture: " + path);
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    [Fact]
    public void LockTable_MustAllowOnlyOneOwnerPerCustomer()
    {
        var sql = Read("PCTP_QrReaderLock.sql");
        Assert.Contains("CONSTRAINT PK_FVN_QrReaderLock PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomerNo  NVARCHAR(50)  NOT NULL CONSTRAINT PK_FVN_QrReaderLock", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Acquire_MustSerializeConcurrentCallersAndRequireRegistration()
    {
        var sql = Read("PCTP_QrReaderLock.sql");
        var acquire = sql.Substring(sql.IndexOf("PROCEDURE dbo.Usp_QrReaderLock_Acquire", StringComparison.Ordinal));
        acquire = acquire.Substring(0, acquire.IndexOf("\nGO", StringComparison.Ordinal));
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", acquire, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FVN_QrReaderMachine", acquire, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @registered = 0", acquire, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SYSDATETIME()", acquire, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@Force = 1 OR @hb < DATEADD(SECOND, -@TtlSeconds, @now)", acquire, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Heartbeat_MustDropOwnershipWhenRegistrationIsRemoved()
    {
        var sql = Read("PCTP_QrReaderLock.sql");
        var hb = sql.Substring(sql.IndexOf("PROCEDURE dbo.Usp_QrReaderLock_Heartbeat", StringComparison.Ordinal));
        hb = hb.Substring(0, hb.IndexOf("\nGO", StringComparison.Ordinal));
        Assert.Contains("NOT EXISTS (SELECT 1 FROM dbo.FVN_QrReaderMachine", hb, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DELETE dbo.FVN_QrReaderLock", hb, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Release_MustOnlyDeleteTheCallersOwnLock()
    {
        var sql = Read("PCTP_QrReaderLock.sql");
        var rel = sql.Substring(sql.IndexOf("PROCEDURE dbo.Usp_QrReaderLock_Release", StringComparison.Ordinal));
        rel = rel.Substring(0, rel.IndexOf("\nGO", StringComparison.Ordinal));
        Assert.Contains("WHERE CustomerNo = @CustomerNo AND ClientIp = @ip", rel, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Usp_QrReaderLock_Acquire")]
    [InlineData("Usp_QrReaderLock_Heartbeat")]
    [InlineData("Usp_QrReaderLock_Release")]
    [InlineData("Usp_QrReaderLock_List")]
    public void LockProcedures_MustIdentifyMachineByServerObservedIp_NotByClientParameter(string procName)
    {
        // Client không được tự khai IP/định danh: SP phải tự đọc CONNECTIONPROPERTY('client_net_address').
        var sql = Read("PCTP_QrReaderLock.sql");
        var proc = sql.Substring(sql.IndexOf("PROCEDURE dbo." + procName, StringComparison.Ordinal));
        proc = proc.Substring(0, proc.IndexOf("\nGO", StringComparison.Ordinal));
        var header = proc.Substring(0, proc.IndexOf("\nAS", StringComparison.Ordinal));

        Assert.Contains("CONNECTIONPROPERTY('client_net_address')", proc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@ClientIp", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@Ip ", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegistrationTable_MustBeKeyedByCustomerAndClientIp()
    {
        var sql = Read("PCTP_QrReaderLock.sql");
        Assert.Contains("CONSTRAINT PK_FVN_QrReaderMachine PRIMARY KEY (CustomerNo, ClientIp)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CustomerQrLockService.source.cs")]
    [InlineData("MachinePermissionService.source.cs")]
    [InlineData("QrReaderMachineService.source.cs")]
    [InlineData("QrLockSummary.source.cs")]
    public void QrLockCode_MustNotHardCodeCustomerNumbers(string file)
    {
        // Thêm khách hàng mới vào CustomerTableConfig phải tự nhận, không sửa code khoá.
        var source = Read(file);
        Assert.False(Regex.IsMatch(source, "\"1000\\d\\d\""), file + " must not hard-code customer numbers.");
    }

    [Fact]
    public void LockService_MustFailClosedWhenAcquireThrows()
    {
        var source = Read("CustomerQrLockService.source.cs");
        var acquire = source.Substring(source.IndexOf("public QrLockInfo TryAcquire", StringComparison.Ordinal));
        acquire = acquire.Substring(0, acquire.IndexOf("public bool Heartbeat", StringComparison.Ordinal));
        Assert.Contains("catch (Exception ex)", acquire);
        Assert.Contains("IsOwnedByMe = false", acquire);
    }

    [Fact]
    public void ScanHandlers_MustRecheckLockOwnershipBeforeWriting()
    {
        var source = Read("DocQrPresenter.qrlock.source.cs");
        Assert.Contains("EnsureQrLockOwnership()", source);
        var submit = source.IndexOf("private void OnQRCodeSubmitted", StringComparison.Ordinal);
        var guard = source.IndexOf("if (!EnsureQrLockOwnership())", submit, StringComparison.Ordinal);
        Assert.True(submit >= 0 && guard > submit, "OnQRCodeSubmitted must guard with EnsureQrLockOwnership.");
    }
}
