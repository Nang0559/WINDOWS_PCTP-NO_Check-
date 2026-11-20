using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Framework.Documents
{
    /// <summary>Delegates to the existing AuthorizationGuard (RBAC action + SITE data scope). No parallel permission engine.</summary>
    public sealed class SessionDocumentAuthorizer : IDocumentAuthorizer
    {
        public string ActorCode { get { return AuthorizationGuard.ActorCode; } }

        public void DemandView(DocumentFormDefinition document, string siteCode)
        {
            if (document == null) throw new ArgumentNullException("document");
            try
            {
                AuthorizationGuard.DemandScope(document.ViewFunctionCode, "SITE", siteCode, siteCode);
            }
            catch (UnauthorizedAccessException)
            {
                // A user limited to departments has no SITE scope; accept only when the document is
                // department-scoped and at least one DEPARTMENT grant applies at this site.
                if (document.DepartmentField == null) throw;
                AuthorizationGuard.Demand(document.ViewFunctionCode, "VIEW");
                if (GetDataScope(document, siteCode).Departments.Count == 0) throw;
            }
        }

        public DocumentDataScope GetDataScope(DocumentFormDefinition document, string siteCode)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (document.DepartmentField == null) return DocumentDataScope.AllDepartments;

            var session = UserSession.Current;
            if (session == null) throw new UnauthorizedAccessException("Phiên đăng nhập không tồn tại.");
            string function = document.ViewFunctionCode;

            // Same rule as HasDataScope: once a USER policy exists for the function it is the complete allow-list.
            bool hasUserPolicy = session.DataScopes.Any(s =>
                string.Equals(s.ScopeSource, "USER", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(s.FunctionCode) ||
                 string.Equals(s.FunctionCode, function, StringComparison.OrdinalIgnoreCase)));
            var applicable = session.DataScopes.Where(s =>
                (string.IsNullOrWhiteSpace(s.FunctionCode) ||
                 string.Equals(s.FunctionCode, function, StringComparison.OrdinalIgnoreCase)) &&
                (!hasUserPolicy || string.Equals(s.ScopeSource, "USER", StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(s.SiteCode) ||
                 string.Equals(s.SiteCode, siteCode, StringComparison.OrdinalIgnoreCase))).ToList();

            bool siteWide =
                (applicable.Any(s => s.IsAllowed &&
                    (string.Equals(s.ScopeType, "ALL", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(s.ScopeType, "SITE", StringComparison.OrdinalIgnoreCase))) &&
                 session.HasDataScope(function, "SITE", siteCode, siteCode)) ||
                (applicable.Any(s => s.IsAllowed &&
                    string.Equals(s.ScopeType, "DEPARTMENT", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.ScopeValue, "*", StringComparison.Ordinal)) &&
                 session.HasDataScope(function, "DEPARTMENT", "*", siteCode));
            if (siteWide) return DocumentDataScope.AllDepartments;

            var departments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in applicable.Where(s => s.IsAllowed &&
                string.Equals(s.ScopeType, "DEPARTMENT", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(s.ScopeValue) && !string.Equals(s.ScopeValue, "*", StringComparison.Ordinal)))
            {
                if (session.HasDataScope(function, "DEPARTMENT", item.ScopeValue.Trim(), siteCode))
                    departments.Add(item.ScopeValue.Trim());
            }
            return new DocumentDataScope(false, departments);
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
