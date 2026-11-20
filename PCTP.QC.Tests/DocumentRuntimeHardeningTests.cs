using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PCTP.Framework.Documents;
using PCTP.Modules.MesPlanning.Documents;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Contract tests for the hardening of the Document Runtime: real-schema validation, paged/filtered/department-scoped
/// reads (pure SQL builder), the workflow gate and RequiredStatusCode/LookupCode rules. No SQL Server or DevExpress needed.
/// </summary>
public sealed class DocumentRuntimeHardeningTests
{
    private sealed class Probe : IDocumentSchemaProbe
    {
        public readonly Dictionary<string, List<DocumentSqlColumn>> Views = new();
        public readonly Dictionary<string, List<DocumentSqlColumn>> Procs = new();
        public IReadOnlyList<DocumentSqlColumn> GetViewColumns(string name) => Views.TryGetValue(name, out var l) ? l : null;
        public IReadOnlyList<DocumentSqlColumn> GetProcedureParameters(string name) => Procs.TryGetValue(name, out var l) ? l : null;
    }

    private sealed class Policy : IDocumentTransitionPolicy
    {
        public readonly List<string> Calls = new();
        public bool Deny;
        public void EnsureCanTransition(string workflowCode, int fromStatus, int toStatus)
        {
            Calls.Add(workflowCode + ":" + fromStatus + ">" + toStatus);
            if (Deny) throw new InvalidOperationException("transition denied");
        }
    }

    private sealed class Reader : IDocumentStatusReader
    {
        public int Value = 10;
        public int ReadStatus(DocumentFormDefinition d, DocumentCommandDefinition c, string site, IDictionary<string, object> input) => Value;
    }

    private static string SqlType(DocumentFieldType t) =>
        t == DocumentFieldType.Number ? "decimal" : t == DocumentFieldType.DateTime ? "datetime2" : "nvarchar";

    private static Probe GoodProbe(DocumentFormDefinition d)
    {
        var p = new Probe();
        p.Views[d.HeaderView] = d.FieldsOf(DocumentRegion.Header).Select(f => new DocumentSqlColumn { Name = f.FieldCode, TypeName = SqlType(f.Type) }).ToList();
        p.Views[d.DetailView] = d.FieldsOf(DocumentRegion.Detail).Select(f => new DocumentSqlColumn { Name = f.FieldCode, TypeName = SqlType(f.Type) }).ToList();
        foreach (var c in d.Commands)
            p.Procs[c.ProcedureName] = c.Parameters.Select(x => new DocumentSqlColumn
                { Name = x.Name, TypeName = DocumentDefinitionValidator.SqlTypeName(x.DbType), Precision = x.Precision, Scale = x.Scale }).ToList();
        return p;
    }

    private static DocumentFormDefinition DeptDocument(bool withStatus)
    {
        DocumentFieldDefinition F(string code, DocumentRegion r, DocumentFieldType t) => new DocumentFieldDefinition(code, r, t);
        return new DocumentFormDefinition("DEPT_DOC", "MES.vh", "MES.vd", "WF1", "MES.F", "Id", "Site",
            new[]
            {
                F("Id", DocumentRegion.Header, DocumentFieldType.Number), F("Site", DocumentRegion.Header, DocumentFieldType.Text),
                F("Dept", DocumentRegion.Header, DocumentFieldType.Text), F("Name", DocumentRegion.Header, DocumentFieldType.Text),
                F("Qty", DocumentRegion.Header, DocumentFieldType.Number), F("St", DocumentRegion.Header, DocumentFieldType.Number),
                F("Id", DocumentRegion.Detail, DocumentFieldType.Number), F("Site", DocumentRegion.Detail, DocumentFieldType.Text),
                F("Item", DocumentRegion.Detail, DocumentFieldType.Text)
            },
            new[] { new DocumentCommandDefinition("GO", "MES.F", "APPROVE", "MES.usp_go", true, null, 20) },
            "Dept", withStatus ? "St" : null);
    }

    private static DocumentMetadata Metadata(DocumentFormDefinition d)
    {
        var m = new DocumentMetadata
        {
            ModuleCode = d.ModuleCode, HeaderView = "MES.vw_FVN_ComponentDemandHeader",
            DetailView = "MES.vw_FVN_ComponentDemandLine", WorkflowCode = "NONE", DisplayName = "Demand"
        };
        m.Commands.Add(new DocumentCommandMetadata { CommandCode = "ISSUE_COMPONENT", Caption = "c", SequenceNo = 1, IsVisible = true });
        return m;
    }

    [Fact]
    public void Schema_AcceptsMatchingSqlObjects()
    {
        var d = ComponentDemandDefinition.Create();
        DocumentDefinitionValidator.DemandMatchesSchema(d, GoodProbe(d));
    }

    [Fact]
    public void Schema_RejectsMissingObjectsColumnsTypesAndProcedureDrift()
    {
        var d = ComponentDemandDefinition.Create();
        string proc = d.Commands.Single().ProcedureName;

        var noView = GoodProbe(d); noView.Views.Remove(d.DetailView);
        var noColumn = GoodProbe(d); noColumn.Views[d.HeaderView].RemoveAll(c => c.Name == "PlannedQty");
        var badType = GoodProbe(d); badType.Views[d.HeaderView].First(c => c.Name == "PlannedQty").TypeName = "nvarchar";
        var noProc = GoodProbe(d); noProc.Procs.Clear();
        var extraParam = GoodProbe(d); extraParam.Procs[proc].Add(new DocumentSqlColumn { Name = "Extra", TypeName = "int" });
        var badScale = GoodProbe(d); badScale.Procs[proc].First(c => c.Name == "Quantity").Scale = 2;
        var badParamType = GoodProbe(d); badParamType.Procs[proc].First(c => c.Name == "FromSlotId").TypeName = "bigint";

        foreach (var probe in new[] { noView, noColumn, badType, noProc, extraParam, badScale, badParamType })
            Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesSchema(d, probe));
    }

    [Fact]
    public void Metadata_RequiredStatusCodeNeedsRegisteredStatusField()
    {
        var d = ComponentDemandDefinition.Create();
        var m = Metadata(d);
        m.Commands[0].RequiredStatusCode = 5;
        Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesRegistry(d, m));
    }

    [Fact]
    public void Metadata_LookupCodeOnlyOnLookupEditorWithSafeCode()
    {
        var d = ComponentDemandDefinition.Create();
        var m = Metadata(d);
        m.Fields.Add(new DocumentFieldMetadata { FieldCode = "ProductPartNo", RegionCode = "HEADER", EditorCode = "TEXT", IsVisible = true, LookupCode = "PART_LOOKUP" });
        Assert.Throws<InvalidOperationException>(() => DocumentDefinitionValidator.DemandMatchesRegistry(d, m));
        Assert.False(DocumentDefinitionValidator.IsSafeLookupCode("x;DROP"));
        Assert.True(DocumentDefinitionValidator.IsSafeLookupCode("PART_LOOKUP"));
    }

    [Fact]
    public void ResolvedCommand_StatusRestrictionOnlyDisables()
    {
        var command = new ResolvedCommand { RequiredStatusCode = 20 };
        Assert.True(command.IsAvailableFor(20));
        Assert.True(command.IsAvailableFor("20"));
        Assert.False(command.IsAvailableFor(10));
        Assert.False(command.IsAvailableFor(null));
        Assert.False(command.IsAvailableFor("abc"));
        Assert.True(new ResolvedCommand().IsAvailableFor(null));
    }

    [Fact]
    public void Definition_DepartmentAndStatusFieldsMustBeRegisteredHeaderFields_AndTargetNeedsTransition()
    {
        var d = DeptDocument(true);
        Assert.Equal("Dept", d.DepartmentField);
        Assert.Equal("St", d.StatusField);
        Assert.Throws<ArgumentException>(() => new DocumentFormDefinition("X", "MES.a", "MES.b", null, "MES.F", "Id", "Site",
            new[]
            {
                new DocumentFieldDefinition("Id", DocumentRegion.Header, DocumentFieldType.Number), new DocumentFieldDefinition("Site", DocumentRegion.Header, DocumentFieldType.Text),
                new DocumentFieldDefinition("Id", DocumentRegion.Detail, DocumentFieldType.Number), new DocumentFieldDefinition("Site", DocumentRegion.Detail, DocumentFieldType.Text)
            },
            new[] { new DocumentCommandDefinition("GO", "MES.F", "APPROVE", "MES.p", false, null) }, "Nope"));
        Assert.Throws<ArgumentException>(() => new DocumentCommandDefinition("GO", "MES.F", "APPROVE", "MES.p", false, null, 5));
    }

    [Fact]
    public void SqlBuilder_PagesStablyAndOnlyFiltersByDepartmentWhenScoped()
    {
        var d = DeptDocument(false);
        var all = DocumentSqlBuilder.BuildList(d, new DocumentListQuery { PageIndex = 2, PageSize = 50 }, DocumentDataScope.AllDepartments, false);
        Assert.DoesNotContain("[Dept] IN", all.Sql);
        Assert.Contains("ORDER BY [Id] DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY", all.Sql);
        Assert.Equal(100, (int)all.Parameters.Single(p => p.Name == "Skip").Value);

        var scoped = DocumentSqlBuilder.BuildList(d, new DocumentListQuery(), new DocumentDataScope(false, new[] { "QC", "WH" }), false);
        Assert.Contains("[Dept] IN (@D0,@D1)", scoped.Sql);
        Assert.Throws<UnauthorizedAccessException>(() =>
            DocumentSqlBuilder.BuildList(d, new DocumentListQuery(), new DocumentDataScope(false, null), false));
    }

    [Fact]
    public void SqlBuilder_FiltersAreParametersAndValidatedAgainstRegistry()
    {
        var d = DeptDocument(false);
        var q = new DocumentListQuery();
        q.Filters.Add(new DocumentFilter("Name", DocumentFilterOp.Contains, "a%b_[x];--"));
        q.Filters.Add(new DocumentFilter("Qty", DocumentFilterOp.GreaterOrEqual, "5"));
        var s = DocumentSqlBuilder.BuildList(d, q, DocumentDataScope.AllDepartments, false);
        Assert.Contains("[Name] LIKE @F0 ESCAPE '\\'", s.Sql);
        Assert.Contains("[Qty]>=@F1", s.Sql);
        Assert.DoesNotContain("DROP", s.Sql);
        Assert.Equal("%a\\%b\\_\\[x];--%", (string)s.Parameters.Single(p => p.Name == "F0").Value);

        foreach (var bad in new[]
        {
            new DocumentFilter("Name", DocumentFilterOp.GreaterOrEqual, "x"),
            new DocumentFilter("Secret", DocumentFilterOp.Equals, "x"),
            new DocumentFilter("Qty", DocumentFilterOp.Equals, "abc"),
            new DocumentFilter("Qty", DocumentFilterOp.Equals, null)
        })
        {
            var query = new DocumentListQuery();
            query.Filters.Add(bad);
            Assert.Throws<ArgumentException>(() => DocumentSqlBuilder.BuildList(d, query, DocumentDataScope.AllDepartments, false));
        }
        Assert.Throws<ArgumentException>(() => DocumentSqlBuilder.BuildList(d, new DocumentListQuery { OrderByField = "Secret" }, DocumentDataScope.AllDepartments, false));
        Assert.Throws<ArgumentException>(() => DocumentSqlBuilder.BuildList(d, new DocumentListQuery { PageSize = 5000 }, DocumentDataScope.AllDepartments, false));
    }

    [Fact]
    public void SqlBuilder_CountSharesScope_AndDetailIsScopedThroughHeader()
    {
        var d = DeptDocument(false);
        var count = DocumentSqlBuilder.BuildList(d, new DocumentListQuery(), new DocumentDataScope(false, new[] { "QC" }), true);
        Assert.StartsWith("SELECT COUNT(1)", count.Sql);
        Assert.DoesNotContain("OFFSET", count.Sql);
        Assert.Contains("[Dept] IN (@D0)", count.Sql);

        var detail = DocumentSqlBuilder.BuildDetailByKey(d, 7L, new DocumentDataScope(false, new[] { "QC" }));
        Assert.Contains("FROM [MES].[vd] d WHERE d.[Site]=@Site AND d.[Id]=@Key AND EXISTS (SELECT 1 FROM [MES].[vh] h " +
            "WHERE h.[Id]=d.[Id] AND h.[Site]=d.[Site] AND h.[Dept] IN (@D0))", detail.Sql);
        Assert.DoesNotContain("EXISTS", DocumentSqlBuilder.BuildDetailByKey(d, 7L, DocumentDataScope.AllDepartments).Sql);
        Assert.DoesNotContain("IN (", DocumentSqlBuilder.BuildHeaderByKey(ComponentDemandDefinition.Create(), 7L, new DocumentDataScope(false, null)).Sql);
    }

    [Fact]
    public void WorkflowGate_UsesRegisteredTarget_AndFailsClosedOtherwise()
    {
        var d = DeptDocument(true);
        var policy = new Policy();
        new DocumentWorkflowGate(policy, new Reader { Value = 10 }).Demand(d, d.Commands.Single(), "S1", null);
        Assert.Equal("WF1:10>20", policy.Calls.Single());

        Assert.Throws<InvalidOperationException>(() =>
            new DocumentWorkflowGate(new Policy { Deny = true }, new Reader()).Demand(d, d.Commands.Single(), "S1", null));

        var gate = new DocumentWorkflowGate(new Policy(), new Reader());
        var noStatus = DeptDocument(false);
        var noTarget = new DocumentCommandDefinition("GO", "MES.F", "APPROVE", "MES.usp_go", true, null);
        var noWorkflow = ComponentDemandDefinition.Create();
        Assert.Throws<InvalidOperationException>(() => gate.Demand(noStatus, noStatus.Commands.Single(), "S1", null));
        Assert.Throws<InvalidOperationException>(() => gate.Demand(d, noTarget, "S1", null));
        Assert.Throws<InvalidOperationException>(() => gate.Demand(noWorkflow, noWorkflow.Commands.Single(), "S1", null));
    }

    [Fact]
    public void WorkflowGate_PlugsIntoDispatcher_AndBlocksExecutionOnDeny()
    {
        var d = DeptDocument(true);
        var executor = new CountingExecutor();
        var denied = new DocumentCommandDispatcher(new DocumentRegistry(new[] { d }), new AllowAll(), executor,
            new DocumentWorkflowGate(new Policy { Deny = true }, new Reader()));
        Assert.Throws<InvalidOperationException>(() => denied.Execute("DEPT_DOC", "GO", "S1", null));
        Assert.Equal(0, executor.Calls);

        var allowed = new DocumentCommandDispatcher(new DocumentRegistry(new[] { d }), new AllowAll(), executor,
            new DocumentWorkflowGate(new Policy(), new Reader()));
        allowed.Execute("DEPT_DOC", "GO", "S1", null);
        Assert.Equal(1, executor.Calls);
    }

    private sealed class CountingExecutor : IDocumentCommandExecutor
    {
        public int Calls;
        public DataTable Execute(DocumentCommandDefinition command, IReadOnlyList<KeyValuePair<DocumentParameterDefinition, object>> values)
        {
            Calls++;
            return new DataTable();
        }
    }

    private sealed class AllowAll : IDocumentAuthorizer
    {
        public string ActorCode => "tester";
        public void DemandView(DocumentFormDefinition document, string siteCode) { }
        public void DemandCommand(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode) { }
        public bool IsCommandAllowed(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode) => true;
        public DocumentDataScope GetDataScope(DocumentFormDefinition document, string siteCode) => DocumentDataScope.AllDepartments;
    }
}
