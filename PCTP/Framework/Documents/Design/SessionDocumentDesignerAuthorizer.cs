using System;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Framework.Documents
{
    /// <summary>Delegates to the existing AuthorizationGuard (RBAC). The same rule is enforced again inside SQL.</summary>
    public sealed class SessionDocumentDesignerAuthorizer : IDocumentDesignerAuthorizer
    {
        public long ActorUserId
        {
            get
            {
                var session = UserSession.Current;
                if (session == null) throw new UnauthorizedAccessException("Phiên đăng nhập không tồn tại.");
                return session.UserId;
            }
        }

        public string ActorCode { get { return AuthorizationGuard.ActorCode; } }

        public void Demand(string actionCode)
        {
            AuthorizationGuard.Demand(DocumentDesignerPermissions.FunctionCode, actionCode);
        }

        public bool Has(string actionCode)
        {
            try
            {
                Demand(actionCode);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
