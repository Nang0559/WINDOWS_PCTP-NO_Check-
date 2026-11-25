using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Framework.Documents;
using PCTP.Security;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// "Tài khoản chưa được gán Site cho chức năng MES.COMPONENT_DEMAND" was raised for users whose scope stores the site in
/// ScopeValue (ScopeType = SITE, SiteCode NULL) or who hold an ALL scope. These tests pin the corrected resolution.
/// </summary>
public sealed class DocumentSiteResolverTests
{
    private const string Function = "MES.COMPONENT_DEMAND";

    private static UserSession Session(params UserDataScope[] scopes) =>
        new UserSession(1, "u", "User", "E1", false, new UserPermission[0], scopes);

    private static UserDataScope Scope(string type, string value, string site = null, string function = null,
        bool allowed = true, string source = "ROLE") =>
        new UserDataScope { ScopeSource = source, FunctionCode = function, ScopeType = type, ScopeValue = value, SiteCode = site, IsAllowed = allowed };

    private static IEnumerable<string> Sites() => new[] { "HVN", "FVN" };

    [Fact]
    public void Site_scope_that_keeps_the_site_in_ScopeValue_is_resolved()
    {
        var sites = DocumentSiteResolver.Candidates(Session(Scope("SITE", "HVN")), Function, Sites);
        Assert.Equal("HVN", Assert.Single(sites));
    }

    [Fact]
    public void Site_scope_that_keeps_the_site_in_SiteCode_still_works()
    {
        var sites = DocumentSiteResolver.Candidates(Session(Scope("SITE", "*", "FVN")), Function, Sites);
        Assert.Equal("FVN", Assert.Single(sites));
    }

    [Fact]
    public void ALL_scope_expands_to_every_active_site()
    {
        var sites = DocumentSiteResolver.Candidates(Session(Scope("ALL", "*")), Function, Sites);
        Assert.Equal(new[] { "FVN", "HVN" }, sites.ToArray());
    }

    [Fact]
    public void ALL_scope_bound_to_one_site_does_not_expand()
    {
        bool loaded = false;
        var sites = DocumentSiteResolver.Candidates(Session(Scope("ALL", "*", "HVN")), Function, () => { loaded = true; return Sites(); });
        Assert.Equal("HVN", Assert.Single(sites));
        Assert.False(loaded);
    }

    [Fact]
    public void A_deny_row_removes_the_site_and_a_scope_of_another_function_is_ignored()
    {
        var session = Session(Scope("ALL", "*"), Scope("SITE", "HVN", allowed: false));
        Assert.Equal("FVN", Assert.Single(DocumentSiteResolver.Candidates(session, Function, Sites)));

        var other = Session(Scope("SITE", "HVN", function: "WAREHOUSE.MOVE"));
        Assert.Empty(DocumentSiteResolver.Candidates(other, Function, Sites));
    }

    [Fact]
    public void A_user_policy_is_the_complete_allow_list()
    {
        var session = Session(Scope("ALL", "*"), Scope("SITE", "FVN", function: Function, source: "USER"));
        Assert.Equal("FVN", Assert.Single(DocumentSiteResolver.Candidates(session, Function, Sites)));
    }

    [Fact]
    public void No_scope_at_all_yields_no_site()
    {
        Assert.Empty(DocumentSiteResolver.Candidates(Session(), Function, Sites));
    }
}
