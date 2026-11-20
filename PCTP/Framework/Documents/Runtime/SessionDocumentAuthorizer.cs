using System;
using PCTP.Infrastructure.Security;

namespace PCTP.Framework.Documents
{
    /// <summary>Delegates to the existing AuthorizationGuard (RBAC action + SITE data scope). No parallel permission engine.</summary>
    public sealed class SessionDocumentAuthorizer : IDocumentAuthorizer
    {
        public string ActorCode { get { return AuthorizationGuard.ActorCode; } }

        public void DemandView(DocumentFormDefinition document, string siteCode)
        {
            if (document == null) throw new ArgumentNullException("document");
            AuthorizationGuard.DemandScope(document.ViewFunctionCode, "SITE", siteCode, siteCode);
        }

        public void DemandCommand(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (command == null) throw new ArgumentNullException("command");
            AuthorizationGuard.DemandActionAndScope(command.PermissionCode, command.ActionCode, "SITE", siteCode, siteCode);
        }

        public bool IsCommandAllowed(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode)
        {
            try
            {
                DemandCommand(document, command, siteCode);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
