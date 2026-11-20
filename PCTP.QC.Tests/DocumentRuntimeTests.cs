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
/// Contract tests for the three-layer Document Runtime (trusted registry, approved metadata,
/// enforcement boundary). They run without SQL Server or DevExpress.
/// </summary>
public sealed class DocumentRuntimeTests
{
    private sealed class FakeAuthorizer : IDocumentAuthorizer
    {
        public bool Deny;
        public string ActorCode => "tester";
        public void DemandView(DocumentFormDefinition document, string siteCode) { if (Deny) throw new UnauthorizedAccessException(); }
        public void DemandCommand(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode) { if (Deny) throw new UnauthorizedAccessException(); }
        public bool IsCommandAllowed(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode) => !Deny;
        public DocumentDataScope GetDataScope(DocumentFormDefinition document, string siteCode) => DocumentDataScope.AllDepartments;
    }

    private sealed class FakeExecutor : IDocumentCommandExecutor
    {
        public int Calls;
        public List<KeyValuePair<DocumentParameterDefinition, object>> Last;
        public DataTable Execute(DocumentCommandDefinition command, IReadOnlyList<KeyValuePair<DocumentParameterDefinition, object>> values)
        {
            Calls++;
            Last = values.ToList();
            return new DataTable();
        }
    }

    private static DocumentMetadata ValidMetadata(DocumentFormDefinition d)
    {
        var m = new DocumentMetadata
        {
            ModuleCode = d.ModuleCode, HeaderView = "MES.vw_FVN_ComponentDemandHeader",
            DetailView = "MES.vw_FVN_ComponentDemandLine", WorkflowCode = "NONE", DisplayName = "Demand", ConfigVersion = 1
        };
        foreach (var f in d.Fields)
            m.Fields.Add(new DocumentFieldMetadata
            {
                FieldCode = f.FieldCode, RegionCode = DocumentEditorCodes.ToRegionCode(f.Region), Caption = "C_" + f.FieldCode,
                SequenceNo = 1000 - m.Fields.Count, EditorCode = DocumentEditorCodes.ToCode(f.Type), IsVisible = true, IsReadOnly = false
            });
        foreach (var c in d.Commands)
            m.Commands.Add(new DocumentCommandMetadata { CommandCode = c.CommandCode, Caption = "Issue", SequenceNo = 1, IsVisible = true });
        return m;
    }

    private static Dictionary<string, object> IssueInput() => new Dictionary<string, object>
    {
        { "IssueNo", "I-1" }, { "ComponentDemandLineId", 5L }, { "MaterialLotId", 7L },
        { "FromSlotId", 1 }, { "ToSlotId", 2 }, { "Quantity", 3m }
    };

    [Fact]
    public void Definition_AcceptsDottedPermissionCodes_AndBracketsSqlObjects()
    {
        var command = ComponentDemandDefinition.Create().Commands.Single();
        Assert.Equal("MES.COMPONENT_ISSUE", command.PermissionCode);
        Assert.Equal("[MES].[usp_FVN_ComponentIssue_Post]", command.ProcedureName);
    }

    [Theory]
    [InlineData("MES.p;DROP TABLE x")]
    [InlineData("usp_noschema")]
    [InlineData("a.b.c")]
    public void Command_RejectsUnsafeProcedureNames(string procedure)
    {
        Assert.Throws<ArgumentException>(() => new DocumentCommandDefinition("X", "MES.F", "CREATE", procedure, false, null));
    }

    [Fact]
    public void Registry_RejectsDuplicatesAndUnknownModules()
    {
        var d = ComponentDemandDefinition.Create();
        Assert.Throws<InvalidOperationException>(() => new DocumentRegistry(new[] { d, d }));
        Assert.Throws<InvalidOperationException>(() => new DocumentRegistry(new[] { d }).GetRequired("NOPE"));
    }

    [Fact]
    public void Validator_AcceptsMatchingMetadata()
    {
        var d = ComponentDemandDefinition.Create();
        DocumentDefinitionValidator.DemandMatchesRegistry(d, ValidMetadata(d));
    }

    [Fact]
    public void Validator_RejectsUnregisteredFieldViewMismatchMissingCommandAndBadEditor()
    {
        var d = ComponentDemandDefinition.Create();

        var unknownField = ValidMetadata(d);
        unknownField.Fields.Add(new DocumentFieldMetadata { FieldCode = "Secret", RegionCode = "HEADER", EditorCode = "TEXT", IsVisible = true });
        Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesRegistry(d, unknownField));

        var viewMismatch = ValidMetadata(d);
        viewMismatch.HeaderView = "MES.vw_Other";
        Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesRegistry(d, viewMismatch));

        var missingCommand = ValidMetadata(d);
        missingCommand.Commands.Clear();
        Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesRegistry(d, missingCommand));

        var badEditor = ValidMetadata(d);
        badEditor.Fields[0].EditorCode = "MEMO";
        Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesRegistry(d, badEditor));

        var badFormat = ValidMetadata(d);
        badFormat.Fields[0].DisplayFormat = "{0};DROP";
        Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesRegistry(d, badFormat));
    }

    [Fact]
    public void Resolver_OnlyRestricts_AndAppliesCaptionAndOrder()
    {
        var d = ComponentDemandDefinition.Create();
        var resolved = DocumentLayoutResolver.Resolve(d, ValidMetadata(d));
        Assert.All(resolved.HeaderFields.Concat(resolved.DetailFields), f => Assert.True(f.IsReadOnly));
        Assert.StartsWith("C_", resolved.HeaderFields[0].Caption);
        Assert.Equal(11, DocumentLayoutResolver.Resolve(d, null).HeaderFields.Count);
    }

    [Fact]
    public void Dispatcher_FillsSiteActorAndIdempotency_AndRejectsSpoofing()
    {
        var d = ComponentDemandDefinition.Create();
        var executor = new FakeExecutor();
        var dispatcher = new DocumentCommandDispatcher(new DocumentRegistry(new[] { d }), new FakeAuthorizer(), executor);

        dispatcher.Execute("component_demand", "issue_component", " S1 ", IssueInput());
        Assert.Equal(1, executor.Calls);
        Assert.Equal("S1", executor.Last.Single(p => p.Key.Name == "SiteCode").Value);
        Assert.Equal("tester", executor.Last.Single(p => p.Key.Name == "IssuedBy").Value);
        Assert.Equal(32, ((string)executor.Last.Single(p => p.Key.Name == "IdempotencyKey").Value).Length);

        var spoof = IssueInput();
        spoof["IssuedBy"] = "admin";
        Assert.Throws<ArgumentException>(() => dispatcher.Execute("COMPONENT_DEMAND", "ISSUE_COMPONENT", "S1", spoof));
        var missing = IssueInput();
        missing.Remove("Quantity");
        Assert.Throws<ArgumentException>(() => dispatcher.Execute("COMPONENT_DEMAND", "ISSUE_COMPONENT", "S1", missing));
        Assert.Throws<InvalidOperationException>(() => dispatcher.Execute("COMPONENT_DEMAND", "DROP_ALL", "S1", IssueInput()));
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public void Dispatcher_NeverExecutesWhenUnauthorized()
    {
        var d = ComponentDemandDefinition.Create();
        var executor = new FakeExecutor();
        var dispatcher = new DocumentCommandDispatcher(new DocumentRegistry(new[] { d }), new FakeAuthorizer { Deny = true }, executor);
        Assert.Throws<UnauthorizedAccessException>(() => dispatcher.Execute("COMPONENT_DEMAND", "ISSUE_COMPONENT", "S1", IssueInput()));
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public void Dispatcher_FailsClosedWhenWorkflowTransitionRequiredButNoGate()
    {
        var wf = new DocumentFormDefinition("WF_DOC", "MES.vh", "MES.vd", null, "MES.F", "Id", "Site",
            new[]
            {
                new DocumentFieldDefinition("Id", DocumentRegion.Header, DocumentFieldType.Number),
                new DocumentFieldDefinition("Site", DocumentRegion.Header, DocumentFieldType.Text),
                new DocumentFieldDefinition("Id", DocumentRegion.Detail, DocumentFieldType.Number),
                new DocumentFieldDefinition("Site", DocumentRegion.Detail, DocumentFieldType.Text)
            },
            new[] { new DocumentCommandDefinition("GO", "MES.F", "APPROVE", "MES.usp_go", true, null) });
        var executor = new FakeExecutor();
        var dispatcher = new DocumentCommandDispatcher(new DocumentRegistry(new[] { wf }), new FakeAuthorizer(), executor);
        Assert.Throws<InvalidOperationException>(() => dispatcher.Execute("WF_DOC", "GO", "S1", null));
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public void SeedMigration_CoversEveryRegisteredFieldCommandAndView()
    {
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "050_MES_ComponentDemandDocumentViewsAndMetadata.sql"));
        var d = ComponentDemandDefinition.Create();
        foreach (var f in d.Fields)
            Assert.Contains("('" + f.FieldCode + "','" + DocumentEditorCodes.ToRegionCode(f.Region) + "'", sql);
        foreach (var c in d.Commands) Assert.Contains("'" + c.CommandCode + "'", sql);
        Assert.Contains("MES.vw_FVN_ComponentDemandHeader", sql);
        Assert.Contains("MES.vw_FVN_ComponentDemandLine", sql);
    }
}
