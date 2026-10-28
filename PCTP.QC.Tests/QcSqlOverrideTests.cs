using System;
using PCTP.ClassSQL;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// The QC override lets CI point the WinForms app at a disposable SQL Server. It must be inert when unset and
/// must refuse anything that could reach DB57 / production, so G5/G6 cannot silently fall back to 57.
/// </summary>
public class QcSqlOverrideTests
{
    private const string Default57 = "Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;User ID=sa;Password=x";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoOverride_ReturnsCompiledDefault(string value)
    {
        Assert.Equal(Default57, QcSqlOverride.Resolve(Default57, value, null));
    }

    [Theory]
    [InlineData("Server=127.0.0.1,1433;Initial Catalog=PCTP_QC_CI_UI;User ID=sa;Password=x")]
    [InlineData("Data Source=localhost\\SQLEXPRESS;Initial Catalog=PCTP_QC_CI_UI;Integrated Security=True")]
    [InlineData("Server=(localdb)\\PCTPQC;Database=PCTP_QC_CI_UI;Integrated Security=True")]
    [InlineData("Server=tcp:localhost,1433;Database=pctp_qc_rework_ci;User ID=sa;Password=x")]
    public void LocalPctpQcFixture_IsAccepted(string conn)
    {
        Assert.Equal(conn, QcSqlOverride.Resolve(Default57, conn, "1"));
    }

    [Theory]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC;User ID=sa;Password=x")]
    [InlineData("Server=192.168.200.57;Database=PCTP_QC_CI_UI;User ID=sa;Password=x")]
    [InlineData("Server=192.168.200.14\\BRAVO;Database=PCTP_QC_CI_UI;User ID=sa;Password=x")]
    [InlineData("Server=127.0.0.1;Database=B7R2_FCC;User ID=sa;Password=x")]
    [InlineData("Server=127.0.0.1;Database=master;User ID=sa;Password=x")]
    [InlineData("Server=sql.example.com;Database=PCTP_QC_CI_UI;User ID=sa;Password=x")]
    [InlineData("Server=localhost;Database=PCTP_QC_CI_UI;AttachDbFilename=C:\\x.mdf")]
    [InlineData("Server=localhost;User ID=sa;Password=x")]
    [InlineData("not a connection string")]
    public void UnsafeTarget_IsRejected(string conn)
    {
        Assert.Throws<InvalidOperationException>(() => QcSqlOverride.Resolve(Default57, conn, "1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("0")]
    public void MissingFixtureConfirmation_IsRejected(string confirm)
    {
        const string ok = "Server=127.0.0.1,1433;Initial Catalog=PCTP_QC_CI_UI;User ID=sa;Password=x";
        Assert.Throws<InvalidOperationException>(() => QcSqlOverride.Resolve(Default57, ok, confirm));
    }
}
