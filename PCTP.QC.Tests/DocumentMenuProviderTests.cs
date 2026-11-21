using System;
using System.Collections.Generic;
using System.IO;
using PCTP.Framework.Documents;
using PCTP.Modules.MesPlanning.Documents;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>The shell may only apply menu rows for code-registered screens the user can VIEW; SQL cannot add screens.</summary>
public sealed class DocumentMenuProviderTests
{
    private sealed class Store : IDocumentMenuRuntimeStore
    {
        public List<DocumentMenuEntry> Rows = new List<DocumentMenuEntry>();
        public IReadOnlyList<DocumentMenuEntry> ListConfigured() => Rows;
    }

    private static DocumentMenuEntry E(string module, string group, string text, int seq, bool active = true) =>
        new DocumentMenuEntry { ModuleCode = module, MenuGroup = group, MenuText = text, SequenceNo = seq, IsActive = active };

    [Fact]
    public void Resolve_drops_unregistered_modules_bad_groups_and_screens_without_view_permission()
    {
        var def = ComponentDemandDefinition.Create();
        var reg = new DocumentRegistry(new[] { def });
        var store = new Store();
        store.Rows.Add(E(def.ModuleCode, "KHO", "Yêu cầu linh kiện", 10));
        store.Rows.Add(E("SQL_ONLY", "KHO", "Giả mạo", 1));
        store.Rows.Add(E(def.ModuleCode, "ADMIN", "Nhóm sai", 2));
        store.Rows.Add(E(def.ModuleCode, "KHO", "  ", 3));

        var allowed = DocumentMenuProvider.Resolve(reg, store, fn => fn == def.ViewFunctionCode);
        Assert.Single(allowed);
        Assert.Equal(def.ModuleCode, allowed[0].ModuleCode);
        Assert.Empty(DocumentMenuProvider.Resolve(reg, store, fn => false));
    }

    [Fact]
    public void Resolve_keeps_inactive_entries_so_the_shell_can_hide_the_screen_and_orders_by_group_then_sequence()
    {
        var def = ComponentDemandDefinition.Create();
        var reg = new DocumentRegistry(new[] { def });
        var store = new Store();
        store.Rows.Add(E(def.ModuleCode, "BAOCAO", "B", 5, active: false));
        var list = DocumentMenuProvider.Resolve(reg, store, fn => true);
        Assert.Single(list);
        Assert.False(list[0].IsActive);
    }

    [Fact]
    public void Migration_055_runtime_list_is_read_only_presentation_and_skips_quarantined_modules()
    {
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "055_MES_UiMenuRuntimeList.sql"));
        int b = sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiScreenMenu_Runtime", StringComparison.Ordinal);
        Assert.True(b >= 0);
        string proc = sql.Substring(b);
        Assert.Contains("FVN_UiScreenQuarantine", proc);
        Assert.Contains("d.IsActive = 1", proc);
        foreach (var forbidden in new[] { "INSERT", "UPDATE", "DELETE", "HeaderView", "DetailView", "WorkflowCode", "sp_executesql", "EXEC(" })
            Assert.DoesNotContain(forbidden, proc, StringComparison.OrdinalIgnoreCase);
    }
}