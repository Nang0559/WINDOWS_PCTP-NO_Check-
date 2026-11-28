using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 080 (đối chiếu Work Center ↔ mã bộ phận 4 ký tự, bảo trì tay cả hai bảng đối chiếu).</summary>
public sealed class DepartmentCodeCrossRefContractTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
    private static string Migration() => Read("080_MES_DepartmentCodeCrossRefAndMaintenance.sql");

    [Fact]
    public void Both_department_codes_are_unique_per_site_and_stay_four_characters()
    {
        var sql = Migration();
        Assert.Contains("CONSTRAINT PK_FVN_DepartmentCodeCrossRef PRIMARY KEY (SiteCode, WorkCenterNo)", sql);
        Assert.Contains("CONSTRAINT UQ_FVN_DepartmentCodeCrossRef_Dept UNIQUE (SiteCode, DepartmentCode)", sql);
        Assert.Contains("CHECK (LEN(DepartmentCode) = 4)", sql);
        Assert.Contains("MigrationVersion = '079'", sql);
        Assert.Contains("VALUES ('080'", sql);
    }

    [Fact]
    public void Migration_runs_without_create_or_alter()
    {
        Assert.DoesNotContain("CREATE OR ALTER", Migration(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generator_handles_five_character_work_centers_and_previews_without_writing()
    {
        var sql = Migration();
        Assert.Contains("RIGHT('0000' + CONVERT(varchar(4), @n), 4)", sql);
        Assert.Contains("LEN(@n) = 5", sql);
        Assert.Contains("IF @Preview = 0", sql);
        Assert.Contains("THROW 57784", sql);
    }

    [Fact]
    public void Manual_maintenance_exists_for_line_and_department_and_protects_defined_codes()
    {
        var sql = Migration();
        foreach (var name in new[]
        {
            "MES.usp_FVN_LineCodeCrossRef_Save", "MES.usp_FVN_LineCodeCrossRef_Delete",
            "MES.usp_FVN_DepartmentCodeCrossRef_Save", "MES.usp_FVN_DepartmentCodeCrossRef_Delete",
        })
            Assert.Matches(@"ALTER PROCEDURE " + Regex.Escape(name), sql);
        Assert.Contains("THROW 57811", sql); // line: defined 3-char code cannot be changed
        Assert.Contains("THROW 57813", sql); // line: defined 3-char code cannot be deleted
        Assert.Contains("THROW 57796", sql); // department: linked work center cannot change code
        Assert.Contains("THROW 57798", sql); // department: defined code cannot be deleted
    }

    [Fact]
    public void Link_consults_the_work_center_cross_reference_before_padding()
    {
        var sql = Migration();
        var link = sql.Substring(sql.IndexOf("ALTER PROCEDURE MES.usp_FVN_SiteDepartmentLine_Link", StringComparison.Ordinal));
        var xref = link.IndexOf("FROM MES.FVN_DepartmentCodeCrossRef", StringComparison.Ordinal);
        var pad = link.IndexOf("MES.fn_FVN_DepartmentCodeFromWorkCenter", StringComparison.Ordinal);
        Assert.True(xref > 0 && pad > xref, "Cross reference must be consulted before padding the work center.");
        Assert.Contains("THROW 57785", link);
        Assert.Contains("INSERT MES.FVN_DepartmentCodeCrossRef", link);
    }

    [Fact]
    public void Repository_lets_sql_resolve_the_department_code_when_blank()
    {
        var src = Read("SiteDepartmentLineRepository.source.cs");
        Assert.Contains("MES.usp_FVN_DepartmentCodeCrossRef_Generate", src);
        Assert.Contains("MES.usp_FVN_LineCodeCrossRef_Save", src);
        Assert.DoesNotContain("if (dept.Length == 0) dept = DepartmentCodeFromWorkCenter(workCenterNo)", src);
    }
}
