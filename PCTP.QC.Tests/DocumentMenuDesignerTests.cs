using System;
using System.Collections.Generic;
using System.IO;
using PCTP.Framework.Documents;
using PCTP.Modules.MesPlanning.Documents;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Menu designer: registry-only, EDIT required, ConfigVersion carried to SQL, migration 054 stays presentation-only.</summary>
public sealed class DocumentMenuDesignerTests
{
    private sealed class Auth : IDocumentDesignerAuthorizer
    {
        public readonly HashSet<string> Granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public long ActorUserId => 7;
        public string ActorCode => "menu-admin";
        public bool Has(string a) => Granted.Contains("VIEW") && Granted.Contains(a);
        public void Demand(string a) { if (!Has(a)) throw new UnauthorizedAccessException("denied " + a); }
    }

    private sealed class Store : IDocumentMenuStore
    {
        public int SaveCalls; public int LastExpected; public DocumentMenuEntry LastEntry; public bool Conflict;
        public List<DocumentMenuEntry> Rows = new List<DocumentMenuEntry>();
        public IReadOnlyList<DocumentMenuEntry> List(long u) => Rows;
        public DocumentDesignSaveResult Save(long u, string code, DocumentMenuEntry e, int expected)
        {
            SaveCalls++; LastExpected = expected; LastEntry = e;
            if (Conflict) throw new DocumentConfigConflictException("conflict", null);
            return new DocumentDesignSaveResult { ConfigVersion = expected + 1, ChangedCount = 1 };
        }
    }

    private static (DocumentMenuDesignerService svc, Store store, Auth auth, string module) Make(params string[] grants)
    {
        var trusted = ComponentDemandDefinition.Create();
        var auth = new Auth(); foreach (var g in grants) auth.Granted.Add(g);
        var store = new Store();
        return (new DocumentMenuDesignerService(new DocumentRegistry(new[] { trusted }), store, auth), store, auth, trusted.ModuleCode);
    }

    private static DocumentMenuEntry Entry(string module, int version = 3) => new DocumentMenuEntry
    { ModuleCode = module, MenuGroup = "KHO", MenuText = "Yêu cầu linh kiện", SequenceNo = 20, IsActive = true, ConfigVersion = version };

    [Fact]
    public void Save_requires_edit_and_never_reaches_sql_without_it()
    {
        var m = Make("VIEW");
        Assert.Throws<UnauthorizedAccessException>(() => m.svc.Save(Entry(m.module)));
        Assert.Equal(0, m.store.SaveCalls);
    }

    [Fact]
    public void Save_sends_the_loaded_config_version_to_sql()
    {
        var m = Make("VIEW", "EDIT");
        var r = m.svc.Save(Entry(m.module, 5));
        Assert.Equal(5, m.store.LastExpected);
        Assert.Equal(6, r.ConfigVersion);
    }

    [Fact]
    public void Save_rejects_unregistered_module_bad_group_text_and_sequence()
    {
        var m = Make("VIEW", "EDIT");
        Assert.ThrowsAny<Exception>(() => m.svc.Save(Entry("NOT_IN_REGISTRY")));
        var bad = Entry(m.module); bad.MenuGroup = "ADMIN";
        Assert.Throws<InvalidOperationException>(() => m.svc.Save(bad));
        bad = Entry(m.module); bad.MenuText = "  ";
        Assert.Throws<InvalidOperationException>(() => m.svc.Save(bad));
        bad = Entry(m.module); bad.MenuText = "a\nb";
        Assert.Throws<InvalidOperationException>(() => m.svc.Save(bad));
        bad = Entry(m.module); bad.SequenceNo = 10001;
        Assert.Throws<InvalidOperationException>(() => m.svc.Save(bad));
        Assert.Equal(0, m.store.SaveCalls);
    }

    [Fact]
    public void Conflict_from_store_surfaces_as_DocumentConfigConflictException()
    {
        var m = Make("VIEW", "EDIT"); m.store.Conflict = true;
        Assert.Throws<DocumentConfigConflictException>(() => m.svc.Save(Entry(m.module)));
    }

    [Fact]
    public void List_hides_modules_that_are_not_in_the_code_registry()
    {
        var m = Make("VIEW");
        m.store.Rows.Add(Entry(m.module)); m.store.Rows.Add(Entry("SQL_ONLY_SCREEN"));
        var list = m.svc.List();
        Assert.Single(list);
        Assert.Equal(m.module, list[0].ModuleCode);
    }

    [Fact]
    public void Migration_054_menu_save_has_concurrency_rbac_audit_and_no_catalogue_writes()
    {
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "054_MES_UiDesignerMenuConcurrency.sql"));
        int b = sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiScreenMenu_Save", StringComparison.Ordinal);
        Assert.True(b >= 0);
        string save = sql.Substring(b);
        Assert.Contains("@ExpectedConfigVersion", save);
        Assert.Contains("@MenuExists", save);
        Assert.Contains("IF @MenuExists = 1", save);
        Assert.Contains("UPDLOCK, HOLDLOCK", save);
        Assert.Contains("55131", save);
        Assert.Contains("fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT')", save);
        Assert.Contains("MES.FVN_DocumentConfigAudit", save);
        Assert.Matches(@"UPDATE\s+MES\.FVN_DocumentDefinition\s+SET\s+ConfigVersion", save);
        foreach (var forbidden in new[] { "INSERT MES.FVN_DocumentDefinition", "INSERT MES.FVN_DocumentField",
                     "INSERT MES.FVN_DocumentCommand", "HeaderView", "DetailView", "WorkflowCode", "sp_executesql", "EXEC(" })
            Assert.DoesNotContain(forbidden, save, StringComparison.OrdinalIgnoreCase);
    }
}