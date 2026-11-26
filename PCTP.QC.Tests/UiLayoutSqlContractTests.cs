using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source contract of migrations 056-058: no dynamic SQL, RBAC in every procedure, layouts never carry SQL text.</summary>
public sealed class UiLayoutSqlContractTests
{
    private static string Sql(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));
    private static string Layout056 => Sql("056_MES_UiLayoutDesigner.sql");

    [Fact]
    public void Migrations_056_to_058_use_no_dynamic_sql()
    {
        foreach (var sql in new[] { Layout056, Sql("057_MES_UiScreenCatalog.sql"), Sql("058_MES_UiScreenAuditList.sql") })
        {
            Assert.DoesNotContain("sp_executesql", sql, StringComparison.OrdinalIgnoreCase);
            Assert.False(Regex.IsMatch(sql, @"\bEXEC(UTE)?\s*\(", RegexOptions.IgnoreCase), "dynamic EXEC(...) found");
        }
    }

    [Fact]
    public void Every_056_procedure_checks_rbac_inside_sql()
    {
        string sql = Layout056;
        var bodies = Regex.Split(sql, @"CREATE PROCEDURE\s+", RegexOptions.IgnoreCase).Skip(1).ToList();
        Assert.True(bodies.Count >= 7);
        foreach (var body in bodies)
        {
            string text = body.Split(new[] { "\nGO" }, StringSplitOptions.None)[0];
            Assert.True(Regex.IsMatch(text, @"fn_FVN_(DocumentDesigner_HasAction|UiHasFunctionAction)"),
                "procedure without RBAC check: " + text.Substring(0, Math.Min(60, text.Length)));
        }
    }

    [Fact]
    public void Save_and_create_require_edit_or_approve_and_save_has_version_conflict_and_audit()
    {
        string sql = Layout056;
        string save = sql.Substring(sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiScreen_Save", StringComparison.Ordinal));
        Assert.Contains("HasAction(@ActorUserId, 'EDIT') = 0", save);
        Assert.Contains("56131", save);
        Assert.Contains("FVN_UiScreenAudit", save);
        string create = sql.Substring(sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiScreen_Create", StringComparison.Ordinal));
        Assert.Contains("HasAction(@ActorUserId, 'APPROVE') = 0", create.Substring(0, 700));
        string approve = sql.Substring(sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiDataSource_Approve", StringComparison.Ordinal));
        Assert.Contains("HasAction(@ActorUserId, 'APPROVE') = 0", approve.Substring(0, 700));
    }

    [Fact]
    public void Data_source_is_limited_to_fvn_views_with_a_mandatory_site_column()
    {
        string sql = Layout056;
        Assert.Contains("MES.vw[_]FVN[_]%", sql);
        Assert.True(Regex.IsMatch(sql, @"SiteColumn\s+sysname\s+NOT NULL"));
        Assert.Contains("Cột Site phải là cột chữ có thật của view", sql);
    }

    [Fact]
    public void Layout_tables_have_no_column_that_can_hold_sql_text_or_code_names()
    {
        string sql = Layout056;
        string tables = sql.Substring(sql.IndexOf("CREATE TABLE MES.FVN_UiScreenColumn", StringComparison.Ordinal),
            sql.IndexOf("CREATE TABLE MES.FVN_UiScreenAudit", StringComparison.Ordinal) - sql.IndexOf("CREATE TABLE MES.FVN_UiScreenColumn", StringComparison.Ordinal));
        foreach (var forbidden in new[] { "FilterKey", "SqlText", "Expression", "ProcedureName", "DllName", "ClassName", "CtorArg" })
            Assert.DoesNotContain(forbidden, tables, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CK_MES_FVN_UiScreenFilter_Token", tables);          // closed token list
        Assert.Contains("CK_MES_FVN_UiScreenColumn_Format", tables);         // closed format list
    }

    [Fact]
    public void Audit_list_is_read_only_and_requires_designer_view()
    {
        string sql = Sql("058_MES_UiScreenAuditList.sql");
        Assert.Contains("HasAction(@ActorUserId, 'VIEW') = 0", sql);
        string proc = sql.Substring(sql.IndexOf("CREATE PROCEDURE", StringComparison.Ordinal));
        foreach (var write in new[] { "INSERT ", "UPDATE ", "DELETE ", "MERGE " })
            Assert.DoesNotContain(write, proc.Split(new[] { "\nGO" }, StringSplitOptions.None)[0], StringComparison.OrdinalIgnoreCase);
    }
}
