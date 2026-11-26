using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source contract of migration 059: no dynamic SQL, RBAC in every designer procedure, a node can never carry a command.</summary>
public sealed class UiMenuProfileSqlContractTests
{
    private static string Sql => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "059_MES_UiMenuProfiles.sql"));

    [Fact]
    public void Migration_059_uses_no_dynamic_sql()
    {
        Assert.DoesNotContain("sp_executesql", Sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(Regex.IsMatch(Sql, @"\bEXEC(UTE)?\s*\(", RegexOptions.IgnoreCase), "dynamic EXEC(...) found");
    }

    [Fact]
    public void Every_designer_procedure_checks_rbac_and_only_runtime_is_open_to_any_active_user()
    {
        var bodies = Regex.Split(Sql, @"CREATE PROCEDURE\s+", RegexOptions.IgnoreCase).Skip(1).ToList();
        Assert.Equal(6, bodies.Count);
        foreach (var body in bodies)
        {
            string text = body.Split(new[] { "\nGO" }, StringSplitOptions.None)[0];
            if (text.StartsWith("MES.usp_FVN_UiMenuProfile_Runtime", StringComparison.Ordinal))
            {
                Assert.Contains("SEC.UserAccount", text);
                continue;
            }
            Assert.Contains("fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0", text);
        }
    }

    [Fact]
    public void Save_and_create_require_edit_and_save_is_versioned_audited_and_depth_limited()
    {
        string save = Sql.Substring(Sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Save", StringComparison.Ordinal));
        Assert.Contains("HasAction(@ActorUserId, 'EDIT') = 0", save);
        Assert.Contains("59131", save);
        Assert.Contains("FVN_UiScreenAudit", save);
        Assert.Contains("MAXRECURSION", save);
        string create = Sql.Substring(Sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Create", StringComparison.Ordinal));
        Assert.Contains("HasAction(@ActorUserId, 'EDIT') = 0", create.Substring(0, 500));
    }

    [Fact]
    public void A_menu_node_stores_only_a_safe_key_and_a_role_belongs_to_one_profile()
    {
        Assert.Contains("ItemKey NOT LIKE '%[^A-Za-z0-9_]%'", Sql);
        Assert.Contains("CK_MES_FVN_UiMenuNode_Shape", Sql);
        Assert.Contains("UQ_MES_FVN_UiMenuProfileRole_Role", Sql);
        Assert.DoesNotContain("CommandText", Sql);
        Assert.DoesNotContain("ClassName", Sql);
        Assert.DoesNotContain("DllName", Sql);
    }

    private static string Sql060 => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "060_MES_UiMenuProfileOrderDelete.sql"));

    [Fact]
    public void Migration_059_quarantines_a_drifted_menu_table_instead_of_keeping_it_and_deletes_nothing()
    {
        int guard = Sql.IndexOf("schema drift guard", StringComparison.Ordinal);
        int create = Sql.IndexOf("CREATE TABLE MES.FVN_UiMenuProfile\n", StringComparison.Ordinal);
        Assert.True(guard > 0 && guard < create, "the drift guard must run before the tables are created");
        string block = Sql.Substring(guard, create - guard);
        Assert.Contains("COL_LENGTH", block);
        Assert.Contains("sys.sp_rename", block);
        Assert.Contains("_Legacy_", block);
        foreach (string column in new[] { "DisplayName", "NodeId", "ParentNodeId", "ConfigVersion", "ItemKey", "SequenceNo", "RoleCode" })
            Assert.Contains(column, block);
        Assert.DoesNotContain("DROP TABLE", block, StringComparison.OrdinalIgnoreCase);
        Assert.False(Regex.IsMatch(block, @"\bDELETE\b", RegexOptions.IgnoreCase), "the guard must not delete data");
        Assert.False(Regex.IsMatch(block, @"\bEXEC(UTE)?\s*\(", RegexOptions.IgnoreCase), "no dynamic SQL");
    }

    [Fact]
    public void Migration_060_stops_with_a_clear_error_and_skips_later_batches_when_the_structure_is_wrong()
    {
        Assert.Contains("60004", Sql060);
        int check = Sql060.IndexOf("THROW 60004", StringComparison.Ordinal);
        int firstProc = Sql060.IndexOf("CREATE PROCEDURE", StringComparison.Ordinal);
        Assert.True(check > 0 && check < firstProc, "the structure check must precede every procedure");
        foreach (string column in new[] { "DisplayName", "NodeId", "ParentNodeId" })
            Assert.Contains("N'" + column + "'", Sql060.Substring(0, firstProc));
        Assert.Contains("SET NOEXEC ON", Sql060);
        Assert.EndsWith("SET NOEXEC OFF;\nGO", Sql060.Replace("\r\n", "\n").TrimEnd());
    }
}
