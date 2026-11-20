using System.Collections.Generic;
using System.Data;

namespace PCTP.Framework.Documents
{
    /// <summary>Reads layer-2 presentation metadata. Returns null when a module has no active metadata.</summary>
    public interface IDocumentMetadataRepository
    {
        DocumentMetadata Load(string moduleCode);
    }

    /// <summary>Bridges the document runtime to the existing SEC/RBAC and data-scope checks.</summary>
    public interface IDocumentAuthorizer
    {
        string ActorCode { get; }
        void DemandView(DocumentFormDefinition document, string siteCode);
        void DemandCommand(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode);
        /// <summary>Non-throwing probe for enabling UI. It is a usability aid, never a security boundary.</summary>
        bool IsCommandAllowed(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode);
        /// <summary>
        /// Department data scope for reading <paramref name="document"/>. Called after <see cref="DemandView"/>;
        /// documents without a DepartmentField always get <see cref="DocumentDataScope.AllDepartments"/>.
        /// </summary>
        DocumentDataScope GetDataScope(DocumentFormDefinition document, string siteCode);
    }

    /// <summary>Shared workflow-transition check. Commands that require it fail closed without a gate.</summary>
    public interface IDocumentWorkflowGate
    {
        void Demand(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode,
            IDictionary<string, object> input);
    }

    /// <summary>Executes one registered procedure with already-validated, declared parameters only.</summary>
    public interface IDocumentCommandExecutor
    {
        DataTable Execute(DocumentCommandDefinition command,
            IReadOnlyList<KeyValuePair<DocumentParameterDefinition, object>> values);
    }

    /// <summary>Policy that decides whether a workflow transition is legal (adapter over the shared workflow engine).</summary>
    public interface IDocumentTransitionPolicy
    {
        /// <summary>Throws when the transition is not allowed.</summary>
        void EnsureCanTransition(string workflowCode, int fromStatus, int toStatus);
    }

    /// <summary>Reads the current numeric workflow status of the document a command targets.</summary>
    public interface IDocumentStatusReader
    {
        int ReadStatus(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode,
            IDictionary<string, object> input);
    }

    /// <summary>Column or parameter as it really exists in SQL Server (sys.columns / sys.parameters).</summary>
    public sealed class DocumentSqlColumn
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
    }

    /// <summary>Reads the real SQL schema so the trusted registry can be checked against it (null = object missing).</summary>
    public interface IDocumentSchemaProbe
    {
        IReadOnlyList<DocumentSqlColumn> GetViewColumns(string bracketedViewName);
        IReadOnlyList<DocumentSqlColumn> GetProcedureParameters(string bracketedProcedureName);
    }

    public interface IDocumentDataProvider
    {
        DataTable ListHeaders(string moduleCode, string siteCode, int top);
        DocumentPage ListHeaders(string moduleCode, string siteCode, DocumentListQuery query);
        DataTable GetHeader(string moduleCode, string siteCode, object key);
        DataTable GetDetail(string moduleCode, string siteCode, object key);
    }
}
