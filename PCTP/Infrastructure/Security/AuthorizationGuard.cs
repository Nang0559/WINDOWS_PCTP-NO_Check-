using System;
using PCTP.Security;

namespace PCTP.Infrastructure.Security
{
    /// <summary>Central fail-closed authorization guard for application services.</summary>
    public static class AuthorizationGuard
    {
        public static string ActorCode
        {
            get
            {
                var session = UserSession.Current;
                if (session == null) throw new UnauthorizedAccessException("Phiên đăng nhập không tồn tại.");
                return session.ActorCode;
            }
        }

        public static UserSession Demand(string functionCode, string actionCode)
        {
            var session = UserSession.Current;
            if (session == null) throw new UnauthorizedAccessException("Phiên đăng nhập không tồn tại.");
            if (!string.Equals(actionCode, "VIEW", StringComparison.OrdinalIgnoreCase))
                session.DemandPermission(functionCode, "VIEW");
            session.DemandPermission(functionCode, actionCode);
            return session;
        }

        public static UserSession DemandScope(string functionCode, string scopeType, string scopeValue, string siteCode)
        {
            var session = Demand(functionCode, "VIEW");
            if (!session.HasDataScope(functionCode, scopeType, scopeValue, siteCode))
                throw new UnauthorizedAccessException("Ngoài phạm vi dữ liệu được cấp tại chức năng " +
                    functionCode + " (" + scopeType + ": " + scopeValue + ").");
            return session;
        }

        public static UserSession DemandActionAndScope(string functionCode, string actionCode,
            string scopeType, string scopeValue, string siteCode)
        {
            var session = Demand(functionCode, actionCode);
            if (!session.HasDataScope(functionCode, scopeType, scopeValue, siteCode))
                throw new UnauthorizedAccessException("Ngoài phạm vi dữ liệu được cấp tại chức năng " +
                    functionCode + " (" + scopeType + ": " + scopeValue + ").");
            return session;
        }
    }
}