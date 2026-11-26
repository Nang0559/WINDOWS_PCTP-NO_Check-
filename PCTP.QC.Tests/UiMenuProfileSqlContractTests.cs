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
}
