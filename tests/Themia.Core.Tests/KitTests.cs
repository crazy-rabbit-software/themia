using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-7000]..[S-7050] The widget kit's pure core: items, actions and menus, keys, the ladder, screens, freshness, setup.</summary>
public sealed class KitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 15, 0, 0, TimeSpan.FromHours(2));

    private static KitItem Item(string id, string title = "A title", string? link = "https://news.example/a")
        => new(id, title, Link: link is null ? null : new Uri(link));

    // ---- [S-7000]..[S-7003] items --------------------------------------------------------------

    [Fact]
    public void S7002_an_item_without_an_id_is_dropped_and_ids_are_unique()
    {
        var clean = KitItems.Clean([Item("a", "first"), Item(" "), Item("b"), Item("a", "second")]);

        Assert.Equal(["a", "b"], clean.Select(i => i.Id));
        Assert.Equal("first", clean[0].Title);
    }

    [Fact]
    public void S7002_strings_are_cut_to_their_bounds_without_splitting_a_character()
    {
        var emoji = new string('x', KitBounds.Title - 1) + "\U0001F600";
        var clean = KitItems.Clean([new KitItem("a", emoji, Subtitle: new string('s', 500), Badges: [new("b"), new(" "), new("c"), new("d"), new("e"), new("f")])]);

        Assert.Equal(KitBounds.Title - 1, clean[0].Title.Length);
        Assert.Equal(KitBounds.Subtitle, clean[0].Subtitle!.Length);
        Assert.Equal(["b", "c", "d", "e"], clean[0].Badges!.Select(b => b.Text));
    }

    [Theory]
    [InlineData("https://news.example/a", true)]
    [InlineData("http://news.example/a", true)]
    [InlineData("ftp://news.example/a", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("https://user:pw@news.example/a", false)]
    public void S7002_a_feed_link_must_be_a_plain_web_address(string link, bool kept)
    {
        var clean = KitItems.Clean([Item("a", link: link)]);

        Assert.Single(clean);
        Assert.Equal(kept, clean[0].Link is not null);
    }

    [Theory]
    [InlineData("https://github.example/pr/1", true)]
    [InlineData("http://github.example/pr/1", false)]
    [InlineData("https://elsewhere.example/pr/1", false)]
    public void S7002_a_service_link_must_be_https_on_its_hosts(string link, bool kept)
        => Assert.Equal(kept, KitItems.Clean([Item("a", link: link)], ["github.example"])[0].Link is not null);

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(-2.0, 0.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(double.NaN, null)]
    [InlineData(double.PositiveInfinity, null)]
    public void S7002_a_summary_fraction_is_clamped_or_dropped(double fraction, double? expected)
        => Assert.Equal(expected, KitItems.Clean(new KitSummary("Cycle 4", Fraction: fraction))!.Fraction);

    [Fact]
    public void S7001_a_when_is_an_instant_or_a_day_never_both()
    {
        var instant = KitWhen.Instant(Now);
        var day = KitWhen.OnDay(new DateOnly(2026, 10, 15));

        Assert.Equal(Now, instant.At);
        Assert.Null(instant.Day);
        Assert.Null(day.At);
        Assert.Equal(new DateOnly(2026, 10, 15), day.Day);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1000, "1k")]
    [InlineData(1234, "1.2k")]
    [InlineData(1999, "1.9k")]
    [InlineData(12_345, "12k")]
    [InlineData(999_999, "999k")]
    [InlineData(3_400_000, "3.4M")]
    [InlineData(12_000_000, "12M")]
    [InlineData(-5, "0")]
    public void S7003_counts_are_written_short_and_never_rounded_up(long count, string expected)
        => Assert.Equal(expected, KitText.Count(Locale.En, count));

    [Fact]
    public void S7003_counts_use_the_languages_digits()
        => Assert.Equal(LocaleFormat.Integer(Locale.Ar, 12) + "k", KitText.Count(Locale.Ar, 12_345));

    [Fact]
    public void S7003_an_age_is_relative_for_a_week_then_a_date()
    {
        Assert.Equal(FetchState.Freshness(Locale.En, Now.AddMinutes(-5), Now), KitText.When(Locale.En, KitWhen.Instant(Now.AddMinutes(-5)), KitTime.Age, Now));
        Assert.Equal("Oct 4", KitText.When(Locale.En, KitWhen.Instant(Now.AddDays(-10)), KitTime.Age, Now));
        Assert.Equal("Oct 15", KitText.When(Locale.En, KitWhen.OnDay(new DateOnly(2026, 10, 15)), KitTime.Age, Now));
        Assert.Equal(string.Empty, KitText.When(Locale.En, null, KitTime.Age, Now));
        Assert.Equal(string.Empty, KitText.When(Locale.En, KitWhen.Instant(Now), KitTime.None, Now));
    }

    // ---- [S-7010] [S-7011] actions and menus -----------------------------------------------------

    private static readonly KitAction Open = new(KitActionKind.Open, "Open", Target: new Uri("https://news.example/a"));
    private static readonly KitAction CopyLink = new(KitActionKind.CopyLink, "Copy link", Text: "https://news.example/a");
    private static readonly KitAction Refresh = new(KitActionKind.Refresh, "Refresh");

    [Fact]
    public void S7011_a_feed_items_menu_is_open_and_copy_then_refresh()
    {
        var lines = KitMenu.Lines([Refresh, CopyLink, Open]);

        Assert.Equal(["Open", "Copy link", "-", "Refresh"], lines.Select(l => l.IsSeparator ? "-" : l.Action!.Label));
        Assert.All(lines.Where(l => !l.IsSeparator), l => Assert.False(l.Action!.Disabled));
    }

    [Fact]
    public void S7011_groups_come_in_one_order_and_empty_ones_leave_no_separator()
    {
        var lines = KitMenu.Lines(
        [
            new(KitActionKind.Delete, "Delete"),
            new(KitActionKind.OpenTo, "Review on the site", Target: new Uri("https://news.example/r")),
            new(KitActionKind.CopyText, "Copy number", Text: "#12"),
            new(KitActionKind.Rename, "Rename"),
            Open,
            Refresh,
            new(KitActionKind.OpenTo, "Merge on the site", Target: new Uri("https://news.example/m")),
        ]);

        Assert.Equal(
            ["Open", "Review on the site", "Merge on the site", "Copy number", "-", "Rename", "-", "Delete", "-", "Refresh"],
            lines.Select(l => l.IsSeparator ? "-" : l.Action!.Label));
    }

    [Fact]
    public void S7011_the_first_copy_rename_and_delete_are_the_icon_rows()
    {
        var lines = KitMenu.Lines([Open, CopyLink, new(KitActionKind.CopyText, "Copy title", Text: "t"), new(KitActionKind.Rename, "Rename"), new(KitActionKind.Delete, "Delete")]);
        var quick = lines.Where(l => !l.IsSeparator).ToDictionary(l => l.Action!.Label, l => l.Quick);

        Assert.Null(quick["Open"]);
        Assert.Equal(QuickCommand.Copy, quick["Copy link"]);
        Assert.Null(quick["Copy title"]);
        Assert.Equal(QuickCommand.Rename, quick["Rename"]);
        Assert.Equal(QuickCommand.Delete, quick["Delete"]);
    }

    /// <summary>[S-7011] [S-1753] Each line that has a key says it, as Explorer's menu does: Enter, Ctrl+C for the first copy, F2, Delete.</summary>
    [Fact]
    public void S7011_a_line_with_a_key_shows_it()
    {
        var lines = KitMenu.Lines([Open, CopyLink, new(KitActionKind.CopyText, "Copy title", Text: "t"), new(KitActionKind.Rename, "Rename"), new(KitActionKind.Delete, "Delete"), Refresh]);
        var keys = lines.Where(l => !l.IsSeparator).ToDictionary(l => l.Action!.Label, l => l.Shortcut);

        Assert.Equal(KeyChord.Plain(ChordKey.Enter), keys["Open"]);
        Assert.Equal(KeyChord.Ctrl(ChordKey.C), keys["Copy link"]);
        Assert.Null(keys["Copy title"]);
        Assert.Equal(KeyChord.Plain(ChordKey.F2), keys["Rename"]);
        Assert.Equal(KeyChord.Plain(ChordKey.Delete), keys["Delete"]);
        Assert.Null(keys["Refresh"]);
    }

    [Fact]
    public void S7011_an_item_without_a_link_shows_its_open_and_copy_disabled_and_has_no_primary()
    {
        KitAction[] actions = [Open with { Target = null }, CopyLink with { Text = string.Empty }, Refresh];
        var lines = KitMenu.Lines(actions);

        Assert.True(lines[0].Action!.Disabled);
        Assert.True(lines[1].Action!.Disabled);
        Assert.False(lines[3].Action!.Disabled);
        Assert.Null(KitMenu.Primary(actions));
        Assert.Same(Open, KitMenu.Primary([Open, Refresh]));
    }

    [Fact]
    public void S7011_a_single_kind_given_twice_is_one_line()
        => Assert.Equal(2, KitMenu.Lines([Open, Open with { Label = "Again" }, CopyLink]).Count);

    [Fact]
    public void S7010_no_action_kind_changes_how_a_widget_looks()
        => Assert.DoesNotContain(Enum.GetNames<KitActionKind>(), n => n is "View" or "Sort" or "Column" or "Show" or "Style" or "Filter");

    [Fact]
    public void S7012_every_kit_icon_and_action_kind_has_its_glyph()
    {
        Assert.All(Enum.GetValues<KitIcon>(), icon => Assert.False(string.IsNullOrEmpty(KitIcons.Glyph(icon))));
        Assert.All(Enum.GetValues<KitActionKind>(), kind => Assert.False(string.IsNullOrEmpty(KitIcons.Glyph(new KitAction(kind, "x")))));
    }

    /// <summary>
    /// [S-7012] Fixed: a feed's Open line drew Explorer's "Show more options" picture, because its
    /// glyph (open in a new window, U+E8A7) is the code point Explorer's art is matched to.
    /// </summary>
    [Fact]
    public void S7012_open_is_the_open_glyph_not_explorers_show_more_options()
    {
        var glyph = KitIcons.Glyph(Open);

        Assert.Equal(Themia.App.Shell.Glyphs.Open, glyph);
        Assert.NotEqual(MenuArt.ShowMoreOptions, MenuArtRules.ForGlyph(char.ConvertToUtf32(glyph, 0)));
    }

    // ---- [S-7020] keys ------------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKey.Down, -1, KitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Up, -1, KitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Left, -1, KitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Right, -1, KitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.PageUp, -1, KitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.End, -1, KitKeyCommand.Focus, 29)]
    [InlineData(WidgetKey.Enter, -1, KitKeyCommand.None, -1)]
    [InlineData(WidgetKey.Down, 3, KitKeyCommand.Focus, 4)]
    [InlineData(WidgetKey.Right, 3, KitKeyCommand.Focus, 4)]
    [InlineData(WidgetKey.Left, 3, KitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.Up, 0, KitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Down, 29, KitKeyCommand.Focus, 29)]
    [InlineData(WidgetKey.PageDown, 25, KitKeyCommand.Focus, 29)]
    [InlineData(WidgetKey.PageUp, 15, KitKeyCommand.Focus, 5)]
    [InlineData(WidgetKey.Home, 15, KitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Enter, 7, KitKeyCommand.Open, 7)]
    [InlineData(WidgetKey.Space, 7, KitKeyCommand.Toggle, 7)]
    [InlineData(WidgetKey.Rename, 7, KitKeyCommand.Rename, 7)]
    [InlineData(WidgetKey.Delete, 7, KitKeyCommand.Delete, 7)]
    [InlineData(WidgetKey.Menu, 7, KitKeyCommand.Menu, 7)]
    [InlineData(WidgetKey.Escape, 7, KitKeyCommand.Release, -1)]
    [InlineData(WidgetKey.Copy, 7, KitKeyCommand.Copy, 7)]
    [InlineData(WidgetKey.Copy, -1, KitKeyCommand.None, -1)]
    [InlineData(WidgetKey.Tab, 7, KitKeyCommand.None, 7)]
    [InlineData(WidgetKey.Enter, 40, KitKeyCommand.Open, 29)]
    public void S7020_one_key_map_for_every_kit_list(WidgetKey key, int index, KitKeyCommand command, int at)
        => Assert.Equal(new KitKeyResult(command, at), KitKeys.Decide(key, index, 30));

    [Fact]
    public void S7020_an_empty_list_takes_no_key_but_escape()
    {
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.Down, -1, 0).Command);
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.Space, -1, 0).Command);
        Assert.Equal(KitKeyCommand.Release, KitKeys.Decide(WidgetKey.Escape, -1, 0).Command);
    }

    // ---- [S-7030] [S-7031] the ladder -----------------------------------------------------------

    private static readonly KitSpec Rich = new(KitRowLook.Rich, KitShow.All, () => "(untitled)", () => "No articles", MaxColumns: 3);

    public static TheoryData<string, double, double> Tiers => new()
    {
        { "XS", 120, 76 },
        { "S", 200, 106 },
        { "M", 320, 196 },
        { "Wide", 480, 116 },
        { "Tall", 200, 356 },
        { "L", 560, 376 },
        { "XL", 800, 516 },
    };

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S7031_one_whole_row_fits_at_every_tier(string tier, double width, double height)
    {
        var plan = KitLadder.Plan(new WidgetArea(width, height), Rich, KitShow.All);

        Assert.True(KitLadder.RowEstimate(plan) <= height - KitLadder.ListInset + Disclosure.Tolerance, tier);
        Assert.True(plan.TitleLines >= 1, tier);
    }

    [Fact]
    public void S7031_the_columns_follow_the_width_and_the_specs_cap()
    {
        Assert.Equal(3, KitLadder.Plan(new WidgetArea(800, 516), Rich, KitShow.All).Columns);
        Assert.Equal(2, KitLadder.Plan(new WidgetArea(480, 116), Rich, KitShow.All).Columns);
        Assert.Equal(1, KitLadder.Plan(new WidgetArea(320, 196), Rich, KitShow.All).Columns);
        Assert.Equal(1, KitLadder.Plan(new WidgetArea(800, 516), Rich with { MaxColumns = 1 }, KitShow.All).Columns);
    }

    [Fact]
    public void S7030_a_field_the_spec_does_not_offer_or_the_user_turned_off_stays_off()
    {
        var off = KitLadder.Plan(new WidgetArea(560, 376), Rich, KitShow.None);
        var unoffered = KitLadder.Plan(new WidgetArea(560, 376), Rich with { Optional = KitShow.When }, KitShow.All);

        Assert.False(off.Lead || off.Subtitle || off.When || off.PreviewLines > 0);
        Assert.True(unoffered.When);
        Assert.False(unoffered.Lead || unoffered.Subtitle || unoffered.PreviewLines > 0);
    }

    [Fact]
    public void S7031_more_titles_beat_previews_on_a_medium_tile_and_a_tall_column()
    {
        Assert.Equal(0, KitLadder.Plan(new WidgetArea(320, 196), Rich, KitShow.All).PreviewLines);
        var tall = KitLadder.Plan(new WidgetArea(200, 356), Rich, KitShow.All);
        Assert.Equal(0, tall.PreviewLines);
        Assert.True(tall.Lead);
        Assert.False(tall.Subtitle);
        Assert.True(KitLadder.Plan(new WidgetArea(800, 516), Rich, KitShow.All).PreviewLines > 0);
    }

    [Fact]
    public void S7031_whole_rows_only_at_least_one()
    {
        Assert.Equal(2, KitLadder.WholeRows([60, 60, 60], 150));
        Assert.Equal(1, KitLadder.WholeRows([200], 100));
        Assert.Equal(0, KitLadder.WholeRows([], 100));
    }

    // ---- [S-7040] [S-7041] screens ------------------------------------------------------------

    private static KitScreen Decide(IReadOnlyList<KitOutcome> outcomes, int items, IReadOnlyList<KitAccount>? accounts = null, IReadOnlyList<string>? chosen = null, string? noMatch = null)
        => KitScreens.Decide(Locale.En, accounts, chosen ?? [], outcomes, items, "No articles", noMatch);

    [Fact]
    public void S7040_items_are_drawn_with_a_line_per_failed_source_for_the_header()
    {
        var screen = Decide([new("World News", FetchStatus.Ok), new("Field notes", FetchStatus.BadResponse, "HTTP 404 Not Found"), new("Tech", FetchStatus.Offline)], 4);

        Assert.Equal(KitScreenKind.Items, screen.Kind);
        Assert.Equal(["Field notes: HTTP 404 Not Found", "Tech: " + FetchState.Message(Locale.En, FetchStatus.Offline)], screen.HeaderProblems);
    }

    [Fact]
    public void S7040_a_source_not_answered_yet_is_loading()
    {
        var screen = Decide([new("A", FetchStatus.BadResponse), new("B", FetchStatus.Loading)], 0);

        Assert.Equal(KitScreenKind.Loading, screen.Kind);
        Assert.Equal(FetchState.Message(Locale.En, FetchStatus.Loading), screen.Sentence);
        Assert.Equal(KitScreenKind.Loading, Decide([], 0).Kind);
    }

    [Fact]
    public void S7040_every_source_answering_with_nothing_is_the_specs_empty_sentence()
    {
        Assert.Equal(new KitScreen(KitScreenKind.Empty, "No articles", false, []) with { HeaderProblems = [] }, Decide([new("A", FetchStatus.Empty)], 0) with { HeaderProblems = [] });
        Assert.Equal("No articles", Decide([new("A", FetchStatus.Empty), new("B", FetchStatus.Empty)], 0).Sentence);
    }

    [Fact]
    public void S7040_a_filter_that_leaves_nothing_says_so()
    {
        var screen = Decide([new("A", FetchStatus.Ok)], 0, noMatch: "Nothing matches");

        Assert.Equal(KitScreenKind.NoMatch, screen.Kind);
        Assert.Equal("Nothing matches", screen.Sentence);
    }

    [Fact]
    public void S7040_every_source_failing_says_every_reason_in_order()
    {
        var screen = Decide([new("A", FetchStatus.Unreachable), new("B", FetchStatus.BadResponse, "HTTP 404 Not Found"), new("C", FetchStatus.Empty, "the feed has no items")], 0);

        Assert.Equal(KitScreenKind.Problem, screen.Kind);
        Assert.Equal($"A: {FetchState.Message(Locale.En, FetchStatus.Unreachable)}\nB: HTTP 404 Not Found\nC: the feed has no items", screen.Sentence);
        Assert.False(screen.OffersAccounts);
    }

    [Fact]
    public void S7040_a_source_of_its_own_has_no_owner_in_its_sentence()
        => Assert.Equal(FetchState.Message(Locale.En, FetchStatus.Offline), Decide([new(null, FetchStatus.Offline)], 0).Sentence);

    [Fact]
    public void S7041_accounts_decide_before_anything_is_asked()
    {
        KitAccount work = new("w", "Work");
        KitAccount home = new("h", "Home");

        Assert.Equal(KitScreenKind.NoAccounts, Decide([], 0, accounts: []).Kind);

        var gone = Decide([], 0, accounts: [work], chosen: ["old"]);
        Assert.Equal(KitScreenKind.AccountsGone, gone.Kind);
        Assert.True(gone.OffersAccounts);

        var one = Decide([], 0, accounts: [work with { State = KitAccountState.SignInRequired }, home], chosen: ["w"]);
        Assert.Equal(KitScreenKind.SignInRequired, one.Kind);
        Assert.Contains("Work", one.Sentence, StringComparison.Ordinal);

        Assert.Equal(KitScreenKind.Forbidden, Decide([], 0, accounts: [work with { State = KitAccountState.Forbidden }]).Kind);
        Assert.Equal(KitScreenKind.Loading, Decide([new("Work", FetchStatus.Loading)], 0, accounts: [work, home with { State = KitAccountState.SignInRequired }]).Kind);
        Assert.Equal([work, home], KitScreens.Targets([work, home], []));
    }

    // ---- [S-7042] freshness -------------------------------------------------------------------

    [Fact]
    public void S7042_one_tooltip_line_per_source_and_problem_stamped_with_the_oldest_copy()
    {
        var lines = KitFreshness.Lines(Locale.En,
        [
            new("Work", FetchStatus.Offline, HeldAt: Now.AddHours(-1)),
            new("Work", FetchStatus.Offline, HeldAt: Now.AddHours(-3)),
            new("Home", FetchStatus.Ok, HeldAt: Now.AddHours(-9)),
            new("News", FetchStatus.Empty, HeldAt: Now),
            new("Tech", FetchStatus.RateLimited),
        ], Now);

        var offline = FetchState.Message(Locale.En, FetchStatus.Offline);
        Assert.Equal(
        [
            $"Work: {offline} · updated {LocaleFormat.Time(Locale.En, Now.AddHours(-3).DateTime)}",
            $"Tech: {FetchState.Message(Locale.En, FetchStatus.RateLimited)}",
        ], lines);
    }

    [Fact]
    public void S7042_an_older_copy_is_stamped_with_its_weekday_then_its_date()
    {
        Assert.StartsWith(LocaleFormat.WeekdayName(Locale.En, Now.AddDays(-2).DayOfWeek, NameWidth.Short), KitFreshness.When(Locale.En, Now.AddDays(-2), Now), StringComparison.Ordinal);
        Assert.Equal("Oct 1", KitFreshness.When(Locale.En, Now.AddDays(-13), Now));
    }

    [Fact]
    public void S7042_the_refresh_line_says_how_old_the_oldest_copy_is()
    {
        Assert.Equal("Refresh", KitFreshness.RefreshLabel(Locale.En, [new("A", FetchStatus.Loading)], Now));
        Assert.Equal(
            "Refresh · updated " + LocaleFormat.Relative(Locale.En, TimeSpan.FromMinutes(-20), RelativeStyle.Short),
            KitFreshness.RefreshLabel(Locale.En, [new("A", FetchStatus.Ok, HeldAt: Now.AddMinutes(-5)), new("B", FetchStatus.Offline, HeldAt: Now.AddMinutes(-20))], Now));
    }

    // ---- [S-7064] the header ------------------------------------------------------------------

    [Fact]
    public void S7064_a_header_with_nothing_to_show_takes_no_room()
    {
        // Regression: an empty header stayed visible at zero width, so the panel it shares with the
        // exchange rates' day still spent its spacing on it (the day stood one gap in from the edge).
        Assert.False(KitHeaderParts.Of(problems: 0, busy: false, count: null).Any);
        Assert.False(KitHeaderParts.Of(problems: 0, busy: false, count: 0).Any);
    }

    [Fact]
    public void S7064_each_part_shows_on_its_own_and_shows_the_header()
    {
        Assert.Equal(new KitHeaderParts(Problem: true, Ring: false, Badge: false), KitHeaderParts.Of(2, busy: false, count: null));
        Assert.Equal(new KitHeaderParts(Problem: false, Ring: true, Badge: false), KitHeaderParts.Of(0, busy: true, count: -1));
        Assert.Equal(new KitHeaderParts(Problem: false, Ring: false, Badge: true), KitHeaderParts.Of(0, busy: false, count: 3));
        Assert.True(KitHeaderParts.Of(0, busy: false, count: 3).Any);
    }

    // ---- [S-7050] setup -----------------------------------------------------------------------

    private sealed class Sample(bool configured, FeatureId? needs) : IKitConfig
    {
        public FeatureId? Needs => needs;

        public bool Configured => configured;

        public IReadOnlyList<string> AccountIds => [];

        public KitShow Shows => KitShow.All;
    }

    [Fact]
    public void S7050_a_kit_widget_waits_for_setup_until_it_has_something_to_read_and_an_account_for_it()
    {
        ISetupRule web = new Sample(true, null);
        ISetupRule unset = new Sample(false, null);
        ISetupRule mail = new Sample(true, HostFeatures.Mail);

        Assert.False(web.NeedsSetup(FeatureSet.None));
        Assert.True(unset.NeedsSetup(FeatureSet.Of(HostFeatures.Mail)));
        Assert.True(mail.NeedsSetup(FeatureSet.Of(HostFeatures.Calendar)));
        Assert.False(mail.NeedsSetup(FeatureSet.Of(HostFeatures.Mail)));
    }
}
