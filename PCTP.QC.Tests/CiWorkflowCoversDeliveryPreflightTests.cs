using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// G4 preflight (PCTP.QC.SqlIntegration Program.VerifyDeliveryProcedureReadinessAsync) demands six procedures on the
/// disposable database. The CI deploy step once listed only three, so preflight failed with "is missing" and the
/// cleanup step failed after it. These source-contract tests keep the workflow and the preflight list in step.
/// </summary>
public sealed class CiWorkflowCoversDeliveryPreflightTests
{
    private static readonly string[] PreflightProcedures =
    {
        "Usp_Qrcode_Update_Stock_SP", "Usp_Qrcode_Update_Stock2405", "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
        "Usp_Qrcode_Take_Lot2405", "Usp_Qrcode_Take_LotYMVN2405", "sp_GetCurrentStockStatus"
    };

    private static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(path), "Linked source fixture missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void PreflightListInProgram_MatchesTheSixProceduresThisTestKnows()
    {
        var program = Read("SqlIntegrationProgram.source.cs");
        foreach (var name in PreflightProcedures)
            Assert.Contains("\"" + name + "\"", program);
    }

    [Fact]
    public void DeployStep_DeploysEveryPreflightProcedure()
    {
        var workflow = Read("pctp-qc-tests.workflow.yml");
        var start = workflow.IndexOf("Deploy DB57-derived schema to disposable database", StringComparison.Ordinal);
        Assert.True(start >= 0, "Deploy step not found in workflow.");
        var end = workflow.IndexOf("Seed deterministic G4 fixture", start, StringComparison.Ordinal);
        var deployStep = workflow.Substring(start, end - start);
        foreach (var name in PreflightProcedures)
            Assert.True(deployStep.Contains("/" + name + ".sql"), "Deploy step does not deploy " + name + ".sql, but G4 preflight requires it.");
    }

    [Fact]
    public void ProceduresThatSelectDb57_AreDeployedFromUseFreeCopies()
    {
        // Scripts that begin with USE [B7R2_FCC] must never be deployed raw: they would leave PCTP_QC_CI.
        var workflow = Read("pctp-qc-tests.workflow.yml");
        foreach (var name in new[] { "Usp_Qrcode_Take_Lot2405", "Usp_Qrcode_Take_LotYMVN2405" })
        {
            var sql = Read(name + ".source.sql");
            if (sql.Contains("USE [B7R2_FCC]", StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain("SQL/" + name + ".sql", DeployLoop(workflow));
                Assert.Contains("RUNNER_TEMP/deploy/" + name + ".sql", DeployLoop(workflow));
            }
        }
    }

    private static string DeployLoop(string workflow)
    {
        var start = workflow.IndexOf("Deploy DB57-derived schema to disposable database", StringComparison.Ordinal);
        var end = workflow.IndexOf("Seed deterministic G4 fixture", start, StringComparison.Ordinal);
        return workflow.Substring(start, end - start);
    }
}

/// <summary>Source contract for the versioned YMVN LOT-merge procedure (B0-01 / WMS_YMVN_SQL_KEY_INVENTORY.md).</summary>
public sealed class YmvnTakeLotProcedureContractTests
{
    [Fact]
    public void TakeLotYmvn2405_DoesNotChangeSchemaAtRuntime()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Usp_Qrcode_Take_LotYMVN2405.source.sql");
        Assert.True(System.IO.File.Exists(path), "Linked source fixture missing: " + path);
        var sql = System.IO.File.ReadAllText(path);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER COLUMN", sql, StringComparison.OrdinalIgnoreCase);
    }
}
