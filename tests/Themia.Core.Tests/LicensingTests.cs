using Themia.Core.Geometry;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Microsoft Store licensing rules ([S-1064..S-1069]).</summary>
public sealed class LicensingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static AppState StateWith(int widgets, int views)
    {
        var state = new AppState();
        for (var v = 0; v < views; v++)
        {
            state.Views.Add(ViewState.Create($"View {v + 1}"));
        }

        for (var w = 0; w < widgets; w++)
        {
            state.Views[w % views].Widgets.Add(WidgetInstance.Create(WidgetKind.Clock, new FracRect(0, 0, 0.1, 0.1)));
        }

        state.ActiveViewId = state.Views[0].Id;
        return state;
    }

    [Fact]
    public void S1064_unpackaged_is_unrestricted_whatever_the_store_says()
    {
        foreach (var answer in Enum.GetValues<StoreAnswer>())
        {
            var status = LicenseRules.Decide(packaged: false, answer, lastConfirmedPro: null, Now);
            Assert.Equal(LicenseTier.Unrestricted, status.Tier);
            Assert.Equal(LicenseBasis.DeveloperBuild, status.Basis);
            Assert.False(status.IsLimited);
        }
    }

    [Fact]
    public void S1064_store_answers_decide_the_tier()
    {
        Assert.Equal(new LicenseStatus(LicenseTier.Pro, LicenseBasis.StorePro), LicenseRules.Decide(true, StoreAnswer.Owned, null, Now));
        Assert.Equal(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree), LicenseRules.Decide(true, StoreAnswer.NotOwned, null, Now));
    }

    [Fact]
    public void S1064_not_owned_wins_over_a_recent_confirmation()
    {
        // A refund or a revoked licence: the Store's "no" is current, the cache is history.
        var status = LicenseRules.Decide(true, StoreAnswer.NotOwned, Now.AddHours(-1), Now);
        Assert.Equal(LicenseTier.Free, status.Tier);
    }

    [Fact]
    public void S1064_unknown_without_a_confirmation_is_free()
    {
        Assert.Equal(new LicenseStatus(LicenseTier.Free, LicenseBasis.Checking), LicenseRules.Decide(true, StoreAnswer.NotAsked, null, Now));
        Assert.Equal(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreUnavailable), LicenseRules.Decide(true, StoreAnswer.Unavailable, null, Now));
    }

    [Fact]
    public void S1066_offline_grace_carries_pro_for_seven_days()
    {
        var confirmed = Now.AddDays(-3);
        var status = LicenseRules.Decide(true, StoreAnswer.Unavailable, confirmed, Now);

        Assert.Equal(LicenseTier.Pro, status.Tier);
        Assert.Equal(LicenseBasis.OfflineGrace, status.Basis);
        Assert.Equal(confirmed.AddDays(7), status.GraceUntil);

        // Also while the first answer is still pending, so a Pro user never starts limited.
        Assert.Equal(LicenseTier.Pro, LicenseRules.Decide(true, StoreAnswer.NotAsked, confirmed, Now).Tier);
    }

    [Fact]
    public void S1066_grace_ends_exactly_after_seven_days()
    {
        Assert.NotNull(LicenseRules.GraceUntil(Now - LicenseRules.OfflineGrace, Now));
        Assert.Null(LicenseRules.GraceUntil(Now - LicenseRules.OfflineGrace - TimeSpan.FromSeconds(1), Now));
        Assert.Equal(LicenseTier.Free, LicenseRules.Decide(true, StoreAnswer.Unavailable, Now.AddDays(-8), Now).Tier);
    }

    [Fact]
    public void S1066_a_confirmation_from_the_future_gives_no_grace()
    {
        Assert.Null(LicenseRules.GraceUntil(Now.AddDays(2), Now));
        Assert.NotNull(LicenseRules.GraceUntil(Now.AddMinutes(5), Now)); // within clock skew
    }

    [Fact]
    public void S1065_usage_counts_every_widget_in_every_view()
    {
        var state = StateWith(widgets: 7, views: 3);
        Assert.Equal(new LicenseUsage(7, 3), LicenseUsage.Of(state));
    }

    [Fact]
    public void S1065_free_tier_refuses_the_eleventh_widget_and_third_view()
    {
        var nine = new LicenseUsage(9, 1);
        Assert.False(LicenseRules.WouldExceed(LicenseTier.Free, nine, 1));
        Assert.True(LicenseRules.WouldExceed(LicenseTier.Free, new LicenseUsage(10, 1), 1));

        // A paste of a group is all or nothing.
        Assert.True(LicenseRules.WouldExceed(LicenseTier.Free, new LicenseUsage(8, 1), 3));

        Assert.False(LicenseRules.WouldExceed(LicenseTier.Free, new LicenseUsage(0, 1), 0, 1));
        Assert.True(LicenseRules.WouldExceed(LicenseTier.Free, new LicenseUsage(0, 2), 0, 1));
    }

    [Fact]
    public void S1065_over_the_limit_never_refuses_adding_nothing()
    {
        var over = new LicenseUsage(25, 5);
        Assert.False(LicenseRules.WouldExceed(LicenseTier.Free, over, 0, 0));
    }

    [Fact]
    public void S1065_paste_counts_every_copied_widget_and_a_cut_as_nothing()
    {
        var state = StateWith(widgets: 0, views: 1);
        var view = state.Views[0];
        var first = WidgetInstance.Create(WidgetKind.Clock, new FracRect(0, 0, 0.3, 0.3));
        var second = WidgetInstance.Create(WidgetKind.Clock, new FracRect(0.5, 0, 0.1, 0.1));
        view.Widgets.Add(first);
        view.Widgets.Add(second);

        var screen = new ScreenMetrics(1920, 1080);
        var copyPaste = PlacementPayload.FromView(view, [first.Id, second.Id], PlacementMode.Copy)!.Plan(0.5, 0.5, screen, snapEnabled: false);
        Assert.Equal(2, LicenseUsage.AddedByPaste(state, copyPaste));

        var move = PlacementPayload.FromView(view, [first.Id, second.Id], PlacementMode.Move)!;
        Assert.Equal(0, LicenseUsage.AddedByPaste(state, move.Plan(0.5, 0.5, screen, snapEnabled: false)));

        // The cut original was deleted meanwhile: the paste adds it back, and counts.
        view.Widgets.Clear();
        var orphanCut = move.Plan(0.5, 0.5, screen, snapEnabled: false);
        Assert.Equal(2, LicenseUsage.AddedByPaste(state, orphanCut));
    }

    [Fact]
    public void S1065_pro_and_developer_builds_are_never_limited()
    {
        var huge = new LicenseUsage(500, 50);
        Assert.False(LicenseRules.WouldExceed(LicenseTier.Pro, huge, 10, 10));
        Assert.False(LicenseRules.WouldExceed(LicenseTier.Unrestricted, huge, 10, 10));
    }

    [Fact]
    public void S1067_add_on_ownership_matches_the_product_prefix_of_an_active_sku()
    {
        const string Pro = "9NBLGGH4R2R6";
        Assert.True(LicenseRules.OwnsAddOn([("9NBLGGH4R2R6/0010", true)], Pro));
        Assert.False(LicenseRules.OwnsAddOn([("9NBLGGH4R2R6/0010", false)], Pro));
        Assert.False(LicenseRules.OwnsAddOn([("9NBLGGH4R2R7/0010", true)], Pro));
        Assert.False(LicenseRules.OwnsAddOn([("9NBLGGH4R2R6X/0010", true)], Pro));
        Assert.False(LicenseRules.OwnsAddOn([], Pro));
    }

    [Fact]
    public void S1067_a_placeholder_product_id_is_never_owned()
    {
        Assert.False(LicenseRules.IsStoreId("PRO-ADDON-STORE-ID"));
        Assert.False(LicenseRules.IsStoreId(null));
        Assert.False(LicenseRules.IsStoreId("9nblggh4r2r6"));
        Assert.True(LicenseRules.IsStoreId("9NBLGGH4R2R6"));
        Assert.False(LicenseRules.OwnsAddOn([("PRO-ADDON-STORE-ID/0010", true)], "PRO-ADDON-STORE-ID"));
    }

    [Fact]
    public void S1069_cache_round_trips_and_rejects_anything_else()
    {
        var confirmed = new DateTimeOffset(2026, 9, 30, 8, 15, 0, TimeSpan.Zero);
        Assert.Equal(((LicenseCacheState, DateTimeOffset?))(LicenseCacheState.Valid, confirmed), LicenseCache.Parse(LicenseCache.Format(confirmed)));

        Assert.Equal(LicenseCacheState.Absent, LicenseCache.Parse(null).State);
        Assert.Equal(LicenseCacheState.Absent, LicenseCache.Parse("").State);
        foreach (var bad in new[] { "themia-license:v2:1759220100", "themia-license:v1:", "themia-license:v1:-5", "themia-license:v1:12x", "pro", "themia-license:v1:99999999999999999999" })
        {
            Assert.Equal((LicenseCacheState.Corrupt, (DateTimeOffset?)null), LicenseCache.Parse(bad));
        }
    }

    [Fact]
    public void S1068_every_state_and_outcome_has_words()
    {
        foreach (var basis in Enum.GetValues<LicenseBasis>())
        {
            var status = new LicenseStatus(LicenseTier.Free, basis, Now.AddDays(3));
            Assert.False(string.IsNullOrWhiteSpace(LicenseText.Title(Locale.En, status)));
            Assert.False(string.IsNullOrWhiteSpace(LicenseText.Detail(Locale.En, status)));
        }

        foreach (var outcome in Enum.GetValues<PurchaseOutcome>())
        {
            Assert.False(string.IsNullOrWhiteSpace(LicenseText.Purchase(Locale.En, outcome)));
        }

        Assert.Equal("Developer build", LicenseText.Title(Locale.En, new LicenseStatus(LicenseTier.Unrestricted, LicenseBasis.DeveloperBuild)));
        Assert.Contains("views", LicenseText.Refusal(Locale.En, new LicenseUsage(3, 2), 0, 1), StringComparison.Ordinal);
        Assert.Contains("widgets", LicenseText.Refusal(Locale.En, new LicenseUsage(10, 1), 1, 0), StringComparison.Ordinal);
    }
}
