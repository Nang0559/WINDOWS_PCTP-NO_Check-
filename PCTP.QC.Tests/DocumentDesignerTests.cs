using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using PCTP.Framework.Documents;
using PCTP.Modules.MesPlanning.Documents;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Contract tests for the Document Designer: it may only change layer-2 presentation, needs EDIT/APPROVE, and the SQL
/// write procedures (migration 051) must stay update-only and permission-checked. No SQL Server or DevExpress needed.
/// </summary>
public sealed class DocumentDesignerTests
{
    private sealed class FakeAuthorizer : IDocumentDesignerAuthorizer
    {
        public readonly HashSet<string> Granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public long ActorUserId => 42;
        public string ActorCode => "designer";
        public bool Has(string actionCode) => Granted.Contains(DocumentDesignerPermissions.View) && Granted.Contains(actionCode);
        public void Demand(string actionCode) { if (!Has(actionCode)) throw new UnauthorizedAccessException("denied " + actionCode); }
    }

    private sealed class FakeRepository : IDocumentMetadataRepository
    {
        public DocumentMetadata Current;
        public DocumentMetadata Load(string moduleCode) => Current == null ? null : DocumentDesignerService.Clone(Current);
    }

    private sealed class FakeWriter : IDocumentMetadataWriter
    {
        public int SaveCalls, ProvisionCalls;
        public DocumentMetadata LastSaved, LastSeed;
        public int LastExpectedVersion;
        public DocumentDesignSaveResult Save(long actorUserId, string actorCode, DocumentMetadata draft, int expectedConfigVersion)
        {
            SaveCalls++; LastSaved = draft; LastExpectedVersion = expectedConfigVersion;
            return new DocumentDesignSaveResult { ConfigVersion = expectedConfigVersion + 1, ChangedCount = 1 };
        }
        public DocumentDesignSaveResult Provision(long actorUserId, string actorCode, DocumentMetadata seed)
        {
            ProvisionCalls++; LastSeed = seed;
            return new DocumentDesignSaveResult { ConfigVersion = 1, ChangedCount = seed.Fields.Count };
        }
        public DataTable ListAudit(long actorUserId, string moduleCode, int top) => new DataTable();
    }

    private sealed class Fixture
    {
        public readonly FakeAuthorizer Auth = new FakeAuthorizer();
        public readonly FakeRepository Repo = new FakeRepository();
        public readonly FakeWriter Writer = new FakeWriter();
        public readonly DocumentFormDefinition Trusted = ComponentDemandDefinition.Create();
        public int AfterChangeCalls;
        public DocumentDesignerService Service;

        public Fixture(bool withMetadata = true, params string[] grants)
        {
            foreach (var g in grants) Auth.Granted.Add(g);
            if (withMetadata)
            {
                Repo.Current = DocumentDesignerService.BuildSeed(Trusted, "Demand");
                Repo.Current.ConfigVersion = 3;
                Repo.Current.Layouts.Add(new DocumentLayoutMetadata { LayoutCode = "HEADER", RegionCode = "HEADER", Caption = "H", SequenceNo = 10, IsVisible = true });
                Repo.Current.Layouts.Add(new DocumentLayoutMetadata { LayoutCode = "DETAIL", RegionCode = "DETAIL", Caption = "D", SequenceNo = 20, IsVisible = true });
            }
            Service = new DocumentDesignerService(new DocumentRegistry(new[] { Trusted }), Repo, Writer, Auth, () => AfterChangeCalls++);
        }
    }

    private static readonly string[] Edit = { DocumentDesignerPermissions.View, DocumentDesignerPermissions.Edit };

    [Fact]
    public void Save_requires_edit_permission_and_never_reaches_sql_without_it()
    {
        var f = new Fixture(true, DocumentDesignerPermissions.View);
        var session = f.Service.Open(f.Trusted.ModuleCode);
        var draft = session.CreateEditableCopy();
        Assert.Throws<UnauthorizedAccessException>(() => f.Service.Save(session, draft));
        Assert.Equal(0, f.Writer.SaveCalls);
    }

    [Fact]
    public void Open_requires_view_permission()
    {
        var f = new Fixture(true);
        Assert.Throws<UnauthorizedAccessException>(() => f.Service.Open(f.Trusted.ModuleCode));
        Assert.Throws<UnauthorizedAccessException>(() => f.Service.ListModules());
    }

    [Fact]
    public void Save_sends_presentation_changes_with_the_loaded_config_version()
    {
        var f = new Fixture(true, Edit);
        var session = f.Service.Open(f.Trusted.ModuleCode);
        var draft = session.CreateEditableCopy();
        var field = draft.Fields.First(x => x.RegionCode == "HEADER" && x.FieldCode == "ProductPartNo");
        field.Caption = "Mã SP"; field.Width = 220; field.SequenceNo = 5; field.DisplayFormat = "#,##0.00"; field.IsVisible = true;
        draft.Layouts.First(l => l.LayoutCode == "HEADER").Caption = "Thông tin";
        draft.Commands.First().IsVisible = false;

        var result = f.Service.Save(session, draft);

        Assert.Equal(1, f.Writer.SaveCalls);
        Assert.Equal(3, f.Writer.LastExpectedVersion);
        Assert.Equal(4, result.ConfigVersion);
        Assert.Equal(1, f.AfterChangeCalls);
    }

    [Fact]
    public void Save_rejects_changes_to_editor_readonly_required_or_lookup()
    {
        var f = new Fixture(true, Edit);
        var session = f.Service.Open(f.Trusted.ModuleCode);

        var readOnly = session.CreateEditableCopy();
        readOnly.Fields.First().IsReadOnly = false;
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, readOnly));

        var required = session.CreateEditableCopy();
        required.Fields.First().IsRequired = true;
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, required));

        var editor = session.CreateEditableCopy();
        editor.Fields.First(x => x.FieldCode == "ProductPartNo").EditorCode = "MEMO";
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, editor));

        var status = session.CreateEditableCopy();
        status.Commands.First().RequiredStatusCode = 10;
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, status));
        Assert.Equal(0, f.Writer.SaveCalls);
    }

    [Fact]
    public void Save_rejects_new_fields_commands_layouts_and_binding_changes()
    {
        var f = new Fixture(true, Edit);
        var session = f.Service.Open(f.Trusted.ModuleCode);

        var addField = session.CreateEditableCopy();
        addField.Fields.Add(new DocumentFieldMetadata { FieldCode = "Injected", RegionCode = "HEADER", Caption = "x", EditorCode = "TEXT", IsVisible = true, IsReadOnly = true });
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, addField));

        var addCommand = session.CreateEditableCopy();
        addCommand.Commands.Add(new DocumentCommandMetadata { CommandCode = "DROP_ALL", Caption = "x", IsVisible = true });
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, addCommand));

        var view = session.CreateEditableCopy();
        view.HeaderView = "MES.vw_Other";
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, view));

        var workflow = session.CreateEditableCopy();
        workflow.WorkflowCode = "PRODUCTION";
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, workflow));
        Assert.Equal(0, f.Writer.SaveCalls);
    }

    [Fact]
    public void Save_rejects_unsafe_format_bad_width_and_empty_regions()
    {
        var f = new Fixture(true, Edit);
        var session = f.Service.Open(f.Trusted.ModuleCode);

        var format = session.CreateEditableCopy();
        format.Fields.First(x => x.FieldCode == "PlannedQty").DisplayFormat = "0';DROP TABLE x--";
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, format));

        var width = session.CreateEditableCopy();
        width.Fields.First(x => x.FieldCode == "PlannedQty").Width = 5;
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, width));

        var hidden = session.CreateEditableCopy();
        foreach (var field in hidden.Fields.Where(x => x.RegionCode == "DETAIL")) field.IsVisible = false;
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, hidden));
        Assert.Equal(0, f.Writer.SaveCalls);
    }

    [Fact]
    public void Preview_matches_runtime_resolution_of_the_draft()
    {
        var f = new Fixture(true, Edit);
        var session = f.Service.Open(f.Trusted.ModuleCode);
        var draft = session.CreateEditableCopy();
        draft.Fields.First(x => x.RegionCode == "HEADER" && x.FieldCode == "ProductPartNo").Caption = "Mã SP";
        var resolved = f.Service.Preview(session, draft);
        Assert.Equal("Mã SP", resolved.FindField(DocumentRegion.Header, "ProductPartNo").Caption);
    }

    [Fact]
    public void Save_refuses_a_document_that_is_not_declared_in_sql_yet()
    {
        var f = new Fixture(false, Edit);
        var session = f.Service.Open(f.Trusted.ModuleCode);
        Assert.False(session.HasMetadata);
        Assert.Throws<InvalidOperationException>(() => f.Service.Save(session, DocumentDesignerService.BuildSeed(f.Trusted, "x")));
        Assert.Equal(0, f.Writer.SaveCalls);
    }

    [Fact]
    public void Provision_requires_approve_not_just_edit()
    {
        var f = new Fixture(false, Edit);
        Assert.Throws<UnauthorizedAccessException>(() => f.Service.Provision(f.Trusted.ModuleCode));
        Assert.Equal(0, f.Writer.ProvisionCalls);
    }

    [Fact]
    public void Provision_seed_comes_only_from_the_code_registry_and_is_readonly()
    {
        var f = new Fixture(false, DocumentDesignerPermissions.View, DocumentDesignerPermissions.Approve);
        f.Service.Provision(f.Trusted.ModuleCode);
        var seed = f.Writer.LastSeed;
        Assert.Equal(1, f.Writer.ProvisionCalls);
        Assert.Equal("MES.vw_FVN_ComponentDemandHeader", seed.HeaderView);
        Assert.Equal("MES.vw_FVN_ComponentDemandLine", seed.DetailView);
        Assert.Equal(f.Trusted.Fields.Count, seed.Fields.Count);
        Assert.Equal(f.Trusted.Commands.Count, seed.Commands.Count);
        Assert.All(seed.Fields, x => Assert.True(x.IsReadOnly));
        Assert.All(seed.Commands, c => Assert.True(f.Trusted.AllowsCommand(c.CommandCode)));
        DocumentDefinitionValidator.DemandMatchesRegistry(f.Trusted, seed);
        Assert.Equal(1, f.AfterChangeCalls);
    }

    [Fact]
    public void Open_reports_fields_and_commands_registered_in_code_but_missing_in_sql()
    {
        var f = new Fixture(true, Edit);
        f.Repo.Current.Fields.RemoveAll(x => x.RegionCode == "DETAIL" && x.FieldCode == "OperationNo");
        var session = f.Service.Open(f.Trusted.ModuleCode);
        Assert.Contains("DETAIL:OperationNo", session.MissingFields);
        Assert.Empty(session.MissingCommands);
    }

    [Fact]
    public void Migration_051_save_is_update_only_and_every_procedure_checks_rbac_in_sql()
    {
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "051_MES_DocumentDesignerMetadataWrite.sql"));
        string save = Section(sql, "CREATE PROCEDURE MES.usp_FVN_DocumentMetadata_Save");
        foreach (var forbidden in new[] { "INSERT MES.FVN_DocumentField", "INSERT MES.FVN_DocumentCommand", "INSERT MES.FVN_DocumentLayout",
                     "INSERT MES.FVN_DocumentDefinition", "EditorCode", "IsReadOnly", "IsRequired", "LookupCode", "RequiredStatusCode",
                     "EXEC(", "EXEC (", "sp_executesql" })
            Assert.DoesNotContain(forbidden, save, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"SET\s+(HeaderView|DetailView|WorkflowCode|DisplayName|IsActive)\b", save);
        Assert.Matches(@"UPDATE\s+MES\.FVN_DocumentDefinition\s+SET\s+ConfigVersion", save);
        Assert.Contains("ConfigVersion", save);
        Assert.Contains("UPDLOCK, HOLDLOCK", save);
        Assert.Contains("MES.FVN_DocumentConfigAudit", save);

        foreach (var name in new[] { "usp_FVN_DocumentMetadata_Save", "usp_FVN_DocumentMetadata_Provision", "usp_FVN_DocumentMetadata_ListAudit" })
            Assert.Contains("fn_FVN_DocumentDesigner_HasAction(@ActorUserId", Section(sql, "CREATE PROCEDURE MES." + name));
        Assert.Contains("'APPROVE'", Section(sql, "CREATE PROCEDURE MES.usp_FVN_DocumentMetadata_Provision"));
        Assert.DoesNotContain("sp_executesql", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXEC(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_051_provision_accepts_only_document_views_and_seeds_readonly()
    {
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "051_MES_DocumentDesignerMetadataWrite.sql"));
        string provision = Section(sql, "CREATE PROCEDURE MES.usp_FVN_DocumentMetadata_Provision");
        Assert.Contains("MES.vw[_]FVN[_]%", provision);
        Assert.Contains("IsView", provision);
        Assert.DoesNotContain("ProcedureName", provision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("f.IsVisible, 1", provision);
    }

    private static string Section(string sql, string start)
    {
        int begin = sql.IndexOf(start, StringComparison.Ordinal);
        Assert.True(begin >= 0, "missing " + start);
        int end = sql.IndexOf("\nGO", begin, StringComparison.Ordinal);
        return end < 0 ? sql.Substring(begin) : sql.Substring(begin, end - begin);
    }
}
