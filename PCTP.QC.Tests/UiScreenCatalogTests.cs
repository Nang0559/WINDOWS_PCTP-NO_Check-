using System;
using System.Collections.Generic;
using PCTP.Framework.UiLayout;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>"Open UI screen by code" opens only codes SQL lists for the user; malformed codes never reach the store.</summary>
public sealed class UiScreenCatalogTests
{
    private sealed class Store : IUiScreenCatalogStore
    {
        public List<UiScreenInfo> Rows = new List<UiScreenInfo>();
        public long LastActor;
        public int Calls;
        public IList<UiScreenInfo> ListScreens(long actorUserId) { LastActor = actorUserId; Calls++; return Rows; }
    }

    private static UiScreenInfo S(string code, string title = "T", string fn = "WAREHOUSE.COMPONENT_PO") =>
        new UiScreenInfo { ScreenCode = code, Title = title, SourceCode = "SRC", ViewFunctionCode = fn };

    [Fact]
    public void Find_returns_a_listed_screen_for_the_current_actor()
    {
        var store = new Store();
        store.Rows.Add(S("COMPONENT_PO", "Phiếu đặt hàng linh kiện"));
        var catalog = new UiScreenCatalog(store, () => 42);

        var found = catalog.Find(" COMPONENT_PO ");

        Assert.Equal("COMPONENT_PO", found.ScreenCode);
        Assert.Equal(42, store.LastActor);
    }

    [Fact]
    public void Find_rejects_a_code_that_is_not_listed_for_the_user()
    {
        var store = new Store();
        store.Rows.Add(S("COMPONENT_PO"));
        var catalog = new UiScreenCatalog(store, () => 1);

        Assert.Throws<UnauthorizedAccessException>(() => catalog.Find("OTHER_SCREEN"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("component_po")]
    [InlineData("A'; DROP TABLE X;--")]
    [InlineData("A B")]
    [InlineData("MES.vw_FVN_UiComponentPo")]
    public void Find_rejects_malformed_codes_before_asking_the_store(string code)
    {
        var store = new Store();
        store.Rows.Add(S("COMPONENT_PO"));
        var catalog = new UiScreenCatalog(store, () => 1);

        Assert.Throws<ArgumentException>(() => catalog.Find(code));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public void List_drops_malformed_rows_and_duplicates_and_orders_by_title()
    {
        var store = new Store();
        store.Rows.Add(S("B_SCREEN", "Bravo"));
        store.Rows.Add(S("A_SCREEN", "Alpha"));
        store.Rows.Add(S("A_SCREEN", "Alpha again"));
        store.Rows.Add(S("bad code", "Hỏng"));
        store.Rows.Add(S("NO_TITLE", "  "));
        store.Rows.Add(S("NO_FUNCTION", "Thiếu quyền", " "));
        store.Rows.Add(null);

        var list = new UiScreenCatalog(store, () => 1).List();

        Assert.Equal(new[] { "A_SCREEN", "B_SCREEN" }, new[] { list[0].ScreenCode, list[1].ScreenCode });
        Assert.Equal(2, list.Count);
        Assert.Equal("Alpha", list[0].Title);
    }
}
