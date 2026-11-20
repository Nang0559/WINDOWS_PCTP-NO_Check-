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

    public interface IDocumentDataProvider
    {
        DataTable ListHeaders(string moduleCode, string siteCode, int top);
        DataTable GetHeader(string moduleCode, string siteCode, object key);
        DataTable GetDetail(string moduleCode, string siteCode, object key);
    }
}
