using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Security;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Lists the Sites a user may open a document for. The scope model stores a site in two places: the SiteCode column, or
    /// ScopeValue when ScopeType = SITE (SiteCode is then usually NULL); an ALL / SITE-* scope without SiteCode means "every
    /// site". Reading only SiteCode (the previous behaviour) rejected exactly those users. Every candidate is confirmed with
    /// <see cref="UserSession.HasDataScope"/>, so USER policies and explicit deny rows still win.
    /// </summary>
    public static class DocumentSiteResolver
    {
        /// <param name="loadActiveSites">Called only when the user has a scope that spans every site.</param>
        public static IReadOnlyList<string> Candidates(UserSession session, string functionCode, Func<IEnumerable<string>> loadActiveSites)
        {
            if (session == null) throw new ArgumentNullException("session");
            if (string.IsNullOrWhiteSpace(functionCode)) throw new ArgumentException("functionCode is required.", "functionCode");

            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool everySite = false;
            foreach (var scope in session.DataScopes)
            {
                if (!scope.IsAllowed) continue;
                if (!string.IsNullOrWhiteSpace(scope.FunctionCode) &&
                    !string.Equals(scope.FunctionCode.Trim(), functionCode.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                bool hasSite = !string.IsNullOrWhiteSpace(scope.SiteCode);
                if (hasSite) named.Add(scope.SiteCode.Trim());

                if (string.Equals(scope.ScopeType, "SITE", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals((scope.ScopeValue ?? string.Empty).Trim(), "*", StringComparison.Ordinal)) everySite |= !hasSite;
                    else if (!string.IsNullOrWhiteSpace(scope.ScopeValue)) named.Add(scope.ScopeValue.Trim());
                }
                else if (string.Equals(scope.ScopeType, "ALL", StringComparison.OrdinalIgnoreCase))
                {
                    everySite |= !hasSite;
                }
            }

            if (everySite && loadActiveSites != null)
                foreach (var site in loadActiveSites() ?? Enumerable.Empty<string>())
                    if (!string.IsNullOrWhiteSpace(site)) named.Add(site.Trim());

            return named
                .Where(site => session.HasDataScope(functionCode, "SITE", site, site))
                .OrderBy(site => site, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
