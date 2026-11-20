using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Framework.Documents
{
    /// <summary>RBAC vocabulary of PCTP UI Designer. Enforced again inside SQL (migration 051).</summary>
    public static class DocumentDesignerPermissions
    {
        public const string FunctionCode = "ADMIN.DOCUMENT_DESIGNER";
        /// <summary>Open the designer, read metadata and its audit history.</summary>
        public const string View = "VIEW";
        /// <summary>Change presentation of an already registered screen.</summary>
        public const string Edit = "EDIT";
        /// <summary>Create SQL presentation configuration for a C#-registered screen, or complete missing presentation rows.</summary>
        public const string Approve = "APPROVE";
    }

    public interface IDocumentDesignerAuthorizer
    {
        long ActorUserId { get; }
        string ActorCode { get; }
        /// <summary>Throws <see cref="UnauthorizedAccessException"/> unless the signed-in user holds VIEW and <paramref name="actionCode"/>.</summary>
        void Demand(string actionCode);
        /// <summary>Non-throwing probe for enabling buttons. A usability aid, never the security boundary.</summary>
        bool Has(string actionCode);
    }

    public sealed class DocumentDesignSaveResult
    {
        public int ConfigVersion { get; set; }
        public int ChangedCount { get; set; }
    }

    /// <summary>Raised when another designer changed the document after it was loaded (ConfigVersion mismatch).</summary>
    public sealed class DocumentConfigConflictException : InvalidOperationException
    {
        public DocumentConfigConflictException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Writes layer-2 metadata through the SQL procedures that re-check RBAC, concurrency and audit.</summary>
    public interface IDocumentMetadataWriter
    {
        /// <summary>Update-only presentation save. Sends caption/order/width/format/visibility and nothing else.</summary>
        DocumentDesignSaveResult Save(long actorUserId, string actorCode, DocumentMetadata draft, int expectedConfigVersion);
        /// <summary>Creates the definition (or only its missing rows) from a seed built from the trusted code registry.</summary>
        DocumentDesignSaveResult Provision(long actorUserId, string actorCode, DocumentMetadata seed);
        DataTable ListAudit(long actorUserId, string moduleCode, int top);
    }

    public sealed class DocumentDesignerModuleInfo
    {
        public string ModuleCode { get; internal set; }
        public string DisplayName { get; internal set; }
        public bool HasMetadata { get; internal set; }
        public int ConfigVersion { get; internal set; }
        public int MissingFieldCount { get; internal set; }
        public int MissingCommandCount { get; internal set; }
        /// <summary>Null when SQL metadata is consistent with the code registry.</summary>
        public string Problem { get; internal set; }
    }

    /// <summary>One editing session: trusted definition + an editable copy of the SQL metadata.</summary>
    public sealed class DocumentDesignerSession
    {
        public DocumentFormDefinition Trusted { get; internal set; }
        /// <summary>SQL UI metadata exactly as loaded (the baseline Save compares against); null when the screen has not been provisioned yet.</summary>
        public DocumentMetadata Loaded { get; internal set; }
        public int LoadedConfigVersion { get; internal set; }
        public bool HasMetadata { get { return Loaded != null; } }
        /// <summary>A separate copy the UI may edit freely; Save only accepts changes to presentation columns.</summary>
        public DocumentMetadata CreateEditableCopy()
        {
            if (Loaded == null) throw new InvalidOperationException("Màn hình chưa có cấu hình giao diện trong SQL.");
            return DocumentDesignerService.Clone(Loaded);
        }
        public IReadOnlyList<string> MissingFields { get; internal set; }
        public IReadOnlyList<string> MissingCommands { get; internal set; }
    }
}
