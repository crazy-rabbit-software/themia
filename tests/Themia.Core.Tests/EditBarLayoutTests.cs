using Themia.App.Shell;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;
using Xunit;
using B = Themia.Sdk.Kit.ShellMetrics.Bar;
using F = Themia.Sdk.Kit.ShellMetrics.Flyout;

namespace Themia.Core.Tests;

/// <summary>[S-2320]..[S-2326] The edit bar's parts and widths, and where a page's flyout and its beak stand.</summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class EditBarLayoutTests
{
    private static readonly WidgetId Clock = new("clock");

    private static readonly double[] Pages = [52, 60, 48, 40, 76];

    private static EditBarPlan Lay(double? selection = null, bool errors = false, double widest = double.PositiveInfinity)
        => EditBarLayout.Lay(Pages, selection, errors, 80, 20, widest);

    [Fact]
    public void S13701_unplaced_icons_stands_right_beside_add_widget_its_icon_alone()
    {
        // Owner, 2026-11-21: "the selector settings page should be moved next to add widget and given another icon". Its
        // entry is not among the pages' any more: the five pages, then right after Add its icon alone, at every width.
        Assert.DoesNotContain(Settings.SettingsPageId.DesktopItems, Settings.SettingsPages.Entries);
        Assert.Equal(Settings.SettingsPages.Order.Count - 1, Settings.SettingsPages.Entries.Count);
        Assert.Equal(Glyphs.UnplacedIcons, Settings.SettingsPages.Glyph(Settings.SettingsPageId.DesktopItems));
        Assert.NotEqual(Glyphs.GridLayout, Glyphs.UnplacedIcons);
        Assert.NotEqual(Glyphs.Apps, Glyphs.UnplacedIcons);

        foreach (var plan in new[] { Lay(), Lay(selection: 100, errors: true), Lay(selection: 100, widest: Lay(selection: 100).Width - 1) })
        {
            var unplaced = plan.Of(BarPartKind.Unplaced)!.Value;
            var add = plan.Of(BarPartKind.Add)!.Value;
            Assert.Equal((B.IconOnly, true), (unplaced.Width, unplaced.IconOnly));
            Assert.Equal(add.Right + B.EntryGap, unplaced.X);
            Assert.Equal(Pages.Length, plan.Parts.Count(p => p.Kind == BarPartKind.Page));
            Assert.True(plan.Parts.Where(p => p.Kind == BarPartKind.Page).Max(p => p.Right) < plan.SelectionX);
        }
    }

    [Fact]
    public void S2320_the_parts_stand_in_karpos_order_with_karpos_gaps()
    {
        var plan = Lay(selection: 40, errors: true);
        var parts = plan.Parts;

        Assert.Equal(B.Pad, parts[0].X);
        Assert.Equal(B.Lead + B.Icon + B.IconGap + 52 + B.Lead, parts[0].Width);
        Assert.Equal(parts[0].Right + B.EntryGap, parts[1].X);

        var divider = plan.Of(BarPartKind.Divider)!.Value;
        Assert.Equal(plan.Of(BarPartKind.Page, 4)!.Value.Right + B.DividerRoom, divider.X);
        Assert.Equal(divider.Right + B.DividerRoom, plan.SelectionX);
        Assert.Equal(plan.SelectionX, plan.Of(BarPartKind.Selection)!.Value.X);

        // [S-11959] retired: no toggle at the bar's root. After the gutter the errors' room, then [S-14910] the widget
        // gallery's icon button directly before Add, and [S-13701] Unplaced icons right beside Add on its other side.
        Assert.DoesNotContain("Toggle", Enum.GetNames<BarPartKind>());
        var errors = plan.Of(BarPartKind.Errors)!.Value;
        Assert.Equal((plan.SelectionX + B.Selection + B.Gutter, B.IconOnly), (errors.X, errors.Width));
        var gallery = plan.Of(BarPartKind.Gallery)!.Value;
        Assert.Equal((errors.Right + B.EntryGap, B.IconOnly, true), (gallery.X, gallery.Width, gallery.IconOnly));
        var add = plan.Of(BarPartKind.Add)!.Value;
        Assert.Equal(gallery.Right + B.EntryGap, add.X);
        var unplaced = plan.Of(BarPartKind.Unplaced)!.Value;
        Assert.Equal((add.Right + B.EntryGap, B.IconOnly, true), (unplaced.X, unplaced.Width, unplaced.IconOnly));
        var done = plan.Of(BarPartKind.Done)!.Value;
        Assert.Equal(unplaced.Right + B.Pad, done.X);
        Assert.Equal(B.DoneLeast, done.Width);
        Assert.Equal(done.Right + B.Pad, plan.Width);
        Assert.Equal(48, B.Height);
    }

    [Fact]
    public void S2320_one_width_whatever_is_selected_and_whether_there_are_errors()
    {
        var bare = Lay();

        Assert.Equal(bare.Width, Lay(selection: 300, errors: true).Width);
        Assert.Equal(bare.Of(BarPartKind.Add), Lay(selection: 40, errors: true).Of(BarPartKind.Add));
        Assert.Equal(bare.Of(BarPartKind.Gallery), Lay(selection: 300, errors: true).Of(BarPartKind.Gallery));
        Assert.Null(bare.Of(BarPartKind.Divider));
        Assert.Null(bare.Of(BarPartKind.Selection));
        Assert.Null(bare.Of(BarPartKind.Errors));

        // [S-13701] Unplaced icons stands whatever is selected.
        Assert.Equal(bare.Of(BarPartKind.Unplaced), Lay(selection: 300, errors: true).Of(BarPartKind.Unplaced));
    }

    [Fact]
    public void S2322_a_narrow_display_takes_the_pages_words_first_then_the_selections_room()
    {
        var full = Lay(selection: 100);
        var compact = Lay(selection: 100, widest: full.Width - 1);

        Assert.True(compact.Compact);
        Assert.All(compact.Parts.Where(p => p.Kind == BarPartKind.Page), p => Assert.Equal((B.IconOnly, true), (p.Width, p.IconOnly)));
        Assert.Equal(B.Selection, compact.SelectionRoom);

        var narrower = Lay(selection: 100, widest: compact.Width - 50);
        Assert.Equal(B.Selection - 50, narrower.SelectionRoom);
        Assert.Equal(compact.Width - 50, narrower.Width);
    }

    [Fact]
    public void S2324_the_selections_entry_is_cut_short_then_its_icon_alone()
    {
        Assert.Equal((EditBarLayout.EntryWidth(100), false), EditBarLayout.Fit(100, 256));
        Assert.Equal((200, false), EditBarLayout.Fit(300, 200));
        Assert.Equal((EditBarLayout.CutLeast, false), EditBarLayout.Fit(300, EditBarLayout.CutLeast));
        Assert.Equal((B.IconOnly, true), EditBarLayout.Fit(300, EditBarLayout.CutLeast - 1));

        // A short name fits a short room whole.
        Assert.Equal((EditBarLayout.EntryWidth(10), false), EditBarLayout.Fit(10, EditBarLayout.EntryWidth(10)));
    }

    [Fact]
    public void S2325_a_flyout_is_centred_on_its_entry_twelve_under_the_bar_its_beak_at_the_entry()
    {
        var area = new PixelRect(0, 0, 1920, 1040);
        var bar = new PixelRect(400, 8, 1000, 48);
        var entry = new PixelRect(800, 16, 80, 32);

        var spot = FlyoutPlacement.Place(entry, bar, area, 300);

        Assert.Equal(F.Width, spot.Width);
        Assert.Equal(840 - (F.Width / 2), spot.X);
        Assert.Equal(8 + 48 + F.Gap, spot.Y);
        Assert.Equal(300, spot.Height);
        Assert.Equal(F.Width / 2, spot.Tip);
    }

    [Fact]
    public void S2325_a_tall_page_stops_at_seventy_percent_of_the_work_area_and_scrolls()
    {
        var area = new PixelRect(0, 0, 1920, 1000);
        var spot = FlyoutPlacement.Place(new PixelRect(800, 16, 80, 32), new PixelRect(400, 8, 1000, 48), area, 5000);

        Assert.Equal(700, spot.Height);
    }

    [Fact]
    public void S2350_hung_again_after_a_card_closes_the_flyout_is_as_tall_as_its_page_again()
    {
        // The Views page (420 tall), Work's card opened (640), closed again (420): each hanging is the page's height,
        // never the tallest it has been; past 70 % of the work area it stops there and scrolls, and shrinks back from it.
        var area = new PixelRect(0, 0, 1920, 1000);
        var (entry, bar) = (new PixelRect(800, 16, 80, 32), new PixelRect(400, 8, 1000, 48));

        var heights = new[] { 420.0, 640, 420, 900, 420 }.Select(page => FlyoutPlacement.Place(entry, bar, area, page).Height).ToList();

        Assert.Equal([420.0, 640, 420, 700, 420], heights);
    }

    [Fact]
    public void S2326_pushed_in_at_a_screen_edge_the_beak_stays_over_its_entry()
    {
        var area = new PixelRect(0, 0, 900, 1000);
        var entry = new PixelRect(20, 16, 40, 32);

        var spot = FlyoutPlacement.Place(entry, new PixelRect(10, 8, 880, 48), area, 200);

        Assert.Equal(F.EdgeMargin, spot.X);
        Assert.Equal(40 - F.EdgeMargin, spot.Tip);

        // At the very edge the tip stays on the top's straight: the corner and half the beak in.
        var corner = FlyoutPlacement.Place(new PixelRect(0, 16, 10, 32), new PixelRect(0, 8, 880, 48), area, 200);
        Assert.Equal(Themia.Sdk.Kit.ShellMetrics.OverlayRadius + (F.BeakWidth / 2), corner.Tip);
        var right = FlyoutPlacement.Place(new PixelRect(850, 16, 40, 32), new PixelRect(10, 8, 880, 48), area, 200);
        Assert.Equal(900 - F.EdgeMargin - F.Width, right.X);
        Assert.Equal(870 - right.X, right.Tip);
    }

    // ---- [S-2344] a selected widget under its page is seen through it -------------------------------

    private static readonly FlyoutSpot Panel = new(400, 68, 580, 600, 290);

    [Fact]
    public void S2344_a_widget_under_the_page_is_a_window_in_the_panels_own_dips()
    {
        var windows = FlyoutPlacement.ShineThrough(Panel, [new PixelRect(500, 200, 200, 100)]);

        var window = Assert.Single(windows);
        Assert.Equal(new PixelRect(100, 132, 200, 100), window.Box);
        Assert.False(window.TopLeft || window.TopRight || window.BottomRight || window.BottomLeft);
    }

    [Fact]
    public void S2344_a_widget_beside_the_page_or_only_touching_it_has_no_window()
    {
        Assert.Empty(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(100, 200, 200, 100)]));
        Assert.Empty(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(200, 200, 200, 100)]));
        Assert.Empty(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(500, 668, 200, 100)]));

        // Above the panel (under the bar and the beak): not the panel's.
        Assert.Empty(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(600, 8, 200, 60)]));
        Assert.Empty(FlyoutPlacement.ShineThrough(Panel, []));
    }

    [Fact]
    public void S2344_a_widget_past_the_panels_edge_is_cut_to_it_and_takes_its_round_corner()
    {
        // Over the panel's top-left corner, past it on both sides.
        var corner = Assert.Single(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(300, 20, 200, 100)]));
        Assert.Equal(new PixelRect(0, 0, 100, 52), corner.Box);
        Assert.Equal((true, false, false, false), (corner.TopLeft, corner.TopRight, corner.BottomRight, corner.BottomLeft));

        // Wider and taller than the panel: the whole of it, every corner round.
        var all = Assert.Single(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(0, 0, 2000, 2000)]));
        Assert.Equal(new PixelRect(0, 0, 580, 600), all.Box);
        Assert.True(all.TopLeft && all.TopRight && all.BottomRight && all.BottomLeft);

        // Along the foot only: its two foot corners.
        var foot = Assert.Single(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(300, 600, 800, 200)]));
        Assert.Equal((false, false, true, true), (foot.TopLeft, foot.TopRight, foot.BottomRight, foot.BottomLeft));
    }

    [Fact]
    public void S2344_joined_widgets_are_one_window_so_no_seam_shows_where_they_meet()
    {
        // A group of two side by side and two stacked: one box each time.
        var side = Assert.Single(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(500, 200, 100, 80), new PixelRect(600, 200, 120, 80)]));
        Assert.Equal(new PixelRect(100, 132, 220, 80), side.Box);

        var stacked = Assert.Single(FlyoutPlacement.ShineThrough(Panel, [new PixelRect(500, 300, 100, 80), new PixelRect(500, 200, 100, 100)]));
        Assert.Equal(new PixelRect(100, 132, 100, 180), stacked.Box);
    }

    [Fact]
    public void S2344_widgets_that_overlap_never_thin_the_same_spot_twice()
    {
        PixelRect[] widgets = [new PixelRect(500, 200, 200, 200), new PixelRect(600, 300, 200, 200), new PixelRect(550, 250, 50, 50)];

        var windows = FlyoutPlacement.ShineThrough(Panel, widgets);

        // No two windows overlap, and together they cover exactly the union: 200² + 200² - 100².
        for (var i = 0; i < windows.Count; i++)
        {
            for (var j = i + 1; j < windows.Count; j++)
            {
                Assert.Null(GridGeometry.Intersect(windows[i].Box, windows[j].Box));
            }
        }

        Assert.Equal((200 * 200) + (200 * 200) - (100 * 100), windows.Sum(w => w.Box.Width * w.Box.Height), 6);
    }

    [Fact]
    public void S2341_the_selections_entry_names_the_kind_the_count_of_one_kind_or_the_widgets()
    {
        static string Name(WidgetId kind) => kind == Clock ? "Clock" : "Other";

        Assert.Null(SelectionPage.Words(QuickSetOffer.None, Name));
        Assert.Equal("Clock", SelectionPage.Words(new QuickSetOffer(1, null, "w", Clock, "Kitchen") { CommonKind = Clock }, Name));
        Assert.Equal("2 × Clock", SelectionPage.Words(new QuickSetOffer(2, null, null, null, null) { CommonKind = Clock }, Name));
        Assert.Equal("3 widgets", SelectionPage.Words(new QuickSetOffer(3, null, null, null, null), Name));
    }

    [Fact]
    public void S2342_the_selection_page_shows_what_the_selection_shares()
    {
        var one = new SelectionModel(SelectionKind.One, new QuickSetOffer(1, null, "w", Clock, null), []);
        var many = new SelectionModel(SelectionKind.Several, new QuickSetOffer(3, null, null, null, null), []);

        Assert.Equal([SelectionSection.Shows, SelectionSection.Appearance], SelectionPage.Sections(one, ownLines: true, styleLines: false));
        Assert.Equal([SelectionSection.Appearance], SelectionPage.Sections(one, ownLines: false, styleLines: false));
        Assert.DoesNotContain("Header", Enum.GetNames<SelectionSection>());
        Assert.Equal([SelectionSection.Appearance], SelectionPage.Sections(many, ownLines: true, styleLines: true));
    }

    [Fact]
    public void S11755_several_widgets_page_is_what_they_share_and_their_look_never_a_widget_alone()
    {
        SharedRow[] place = [new(SharedSettings.Place, ["a", "b"], Mixed: false)];
        var offer = new QuickSetOffer(2, null, null, null, null);
        var several = new SelectionModel(SelectionKind.Several, offer, place);
        var bare = new SelectionModel(SelectionKind.Several, offer, []);

        Assert.Equal([SelectionSection.Shared, SelectionSection.Appearance], SelectionPage.Sections(several, ownLines: true, styleLines: true));
        Assert.Equal([SelectionSection.Appearance], SelectionPage.Sections(bare, ownLines: true, styleLines: true));
        Assert.DoesNotContain("Members", Enum.GetNames<SelectionSection>());

        // There are no groups: no group or member page, no link back to one.
        Assert.Equal(["One", "Several"], Enum.GetNames<SelectionKind>());
        Assert.DoesNotContain("InGroup", Enum.GetNames<SelectionSection>());
    }

    [Fact]
    public void S2345_the_selection_page_has_a_settings_tab_and_an_appearance_tab()
    {
        SharedRow[] place = [new(SharedSettings.Place, ["a", "b"], Mixed: false)];
        var one = new SelectionModel(SelectionKind.One, new QuickSetOffer(1, null, "w", Clock, null), []);
        var group = new SelectionModel(SelectionKind.Several, new QuickSetOffer(2, null, null, null, null), place);
        var bare = new SelectionModel(SelectionKind.Several, new QuickSetOffer(2, null, null, null, null), []);

        // One widget: what it shows on Settings, its look on Appearance.
        var tabs = SelectionPage.Tabs(SelectionPage.Sections(one, ownLines: true, styleLines: false));
        Assert.Equal([SelectionTab.Settings, SelectionTab.Appearance], tabs.Select(t => t.Tab));
        Assert.Equal([SelectionSection.Shows], tabs[0].Sections);
        Assert.Equal([SelectionSection.Appearance], tabs[1].Sections);

        // Several: For all of them on Settings, the look on Appearance.
        var grouped = SelectionPage.Tabs(SelectionPage.Sections(group, ownLines: true, styleLines: true));
        Assert.Equal([SelectionSection.Shared], grouped[0].Sections);
        Assert.Equal(SelectionTab.Appearance, grouped[1].Tab);

        // Nothing to set but the look: one tab, which the page draws as no strip.
        Assert.Equal([SelectionTab.Appearance], SelectionPage.Tabs(SelectionPage.Sections(bare, ownLines: true, styleLines: true)).Select(t => t.Tab));
        Assert.All(Enum.GetValues<SelectionSection>(), s => Assert.Equal(s == SelectionSection.Appearance ? SelectionTab.Appearance : SelectionTab.Settings, SelectionPage.TabOf(s)));
    }

    [Fact]
    public void S2341_the_entry_says_the_kind_or_how_many()
    {
        var two = new SelectionModel(SelectionKind.Several, new QuickSetOffer(4, null, null, null, null), []);
        Assert.Equal(Glyphs.Apps, SelectionPage.Glyph(two));
        Assert.Null(SelectionPage.Words((SelectionModel?)null, _ => "?"));
    }
}
