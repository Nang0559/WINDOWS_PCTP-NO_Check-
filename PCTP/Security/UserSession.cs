using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Security
{
    public sealed class UserPermission
    {
        public string FunctionCode { get; set; }
        public string ActionCode { get; set; }
        public bool IsAllowed { get; set; }
    }

    public sealed class UserDataScope
    {
        public string ScopeSource { get; set; }
        public string FunctionCode { get; set; }
        public string ScopeType { get; set; }
        public string ScopeValue { get; set; }
        public string SiteCode { get; set; }
        public bool IsAllowed { get; set; }
    }

    public sealed class UserSession
    {
        private readonly HashSet<string> _permissions;
        public long UserId { get; private set; }
        public string UserName { get; private set; }
        public string DisplayName { get; private set; }
        public string EmployeeCode { get; private set; }
        public string ActorCode => string.IsNullOrWhiteSpace(EmployeeCode) ? UserName : EmployeeCode;
        public bool MustChangePassword { get; private set; }
        public IReadOnlyList<UserPermission> Permissions { get; private set; }
        public IReadOnlyList<UserDataScope> DataScopes { get; private set; }

        public static UserSession Current { get; private set; }

        public UserSession(long userId, string userName, string displayName, string employeeCode,
            bool mustChangePassword, IEnumerable<UserPermission> permissions, IEnumerable<UserDataScope> scopes)
        {
            UserId = userId;
            UserName = userName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            EmployeeCode = employeeCode;
            MustChangePassword = mustChangePassword;
            Permissions = (permissions ?? Enumerable.Empty<UserPermission>()).ToList().AsReadOnly();
            DataScopes = (scopes ?? Enumerable.Empty<UserDataScope>()).ToList().AsReadOnly();
            _permissions = new HashSet<string>(Permissions.Where(p => p.IsAllowed)
                .Select(p => Key(p.FunctionCode, p.ActionCode)), StringComparer.OrdinalIgnoreCase);
        }

        public static void SignIn(UserSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            Current = session;
        }

        public static void SignOut() { Current = null; }

        public bool HasPermission(string functionCode, string actionCode)
        {
            return _permissions.Contains(Key(functionCode, actionCode));
        }

        public void DemandPermission(string functionCode, string actionCode)
        {
            if (!HasPermission(functionCode, actionCode))
                throw new UnauthorizedAccessException("Tài khoản không có quyền " + actionCode +
                    " tại chức năng " + functionCode + ".");
        }

        public bool HasDataScope(string functionCode, string scopeType, string scopeValue, string siteCode)
        {
            Func<UserDataScope, bool> matches = s =>
                (string.IsNullOrWhiteSpace(s.FunctionCode) ||
                 string.Equals(s.FunctionCode, functionCode, StringComparison.OrdinalIgnoreCase)) &&
                (string.Equals(s.ScopeType, "ALL", StringComparison.OrdinalIgnoreCase) ||
                 (string.Equals(s.ScopeType, scopeType, StringComparison.OrdinalIgnoreCase) &&
                  (s.ScopeValue == "*" || string.Equals(s.ScopeValue, scopeValue, StringComparison.OrdinalIgnoreCase)))) &&
                (string.IsNullOrWhiteSpace(s.SiteCode) ||
                 string.Equals(s.SiteCode, siteCode, StringComparison.OrdinalIgnoreCase));

            // Explicit account scopes take precedence over role scopes for the same data key.
            var userScopes = DataScopes.Where(s =>
                string.Equals(s.ScopeSource, "USER", StringComparison.OrdinalIgnoreCase) && matches(s)).ToList();
            var effective = userScopes.Count > 0
                ? userScopes
                : DataScopes.Where(s =>
                    !string.Equals(s.ScopeSource, "USER", StringComparison.OrdinalIgnoreCase) && matches(s)).ToList();

            if (effective.Count == 0) return false;
            if (effective.Any(s => !s.IsAllowed)) return false;
            return effective.Any(s => s.IsAllowed);
        }

        private static string Key(string functionCode, string actionCode)
        {
            return (functionCode ?? string.Empty).Trim().ToUpperInvariant() + "|" +
                   (actionCode ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}