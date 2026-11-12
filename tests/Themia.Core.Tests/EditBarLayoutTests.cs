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

        var errors = plan.Of(BarPartKind.Errors)!.Value;
        Assert.Equal(plan.SelectionX + B.Selection + B.Gutter, errors.X);
        Assert.Equal(B.IconOnly, errors.Width);
        var add = plan.Of(BarPartKind.Add)!.Value;
        Assert.Equal(errors.Right + B.EntryGap, add.X);
        var done = plan.Of(BarPartKind.Done)!.Value;
        Assert.Equal(add.Right + B.Pad, done.X);
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
        Assert.Null(bare.Of(BarPartKind.Divider));
        Assert.Null(bare.Of(BarPartKind.Selection));
        Assert.Null(bare.Of(BarPartKind.Errors));
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

    [Fact]
    public void S2341_the_selections_entry_names_the_kind_the_count_of_one_kind_or_the_widgets()
    {
        static string Name(WidgetId kind) => kind == Clock ? "Clock" : "Other";

        Assert.Null(SelectionPage.Words(QuickSetOffer.None, Name));
        Assert.Equal("Clock", SelectionPage.Words(new QuickSetOffer(1, null, null, "w", Clock, "Kitchen") { CommonKind = Clock }, Name));
        Assert.Equal("2 × Clock", SelectionPage.Words(new QuickSetOffer(2, null, null, null, null, null) { CommonKind = Clock }, Name));
        Assert.Equal("3 widgets", SelectionPage.Words(new QuickSetOffer(3, null, null, null, null, null), Name));
    }

    [Fact]
    public void S2342_the_selection_page_shows_what_the_selection_shares()
    {
        var one = new SelectionModel(SelectionKind.One, new QuickSetOffer(1, HeaderPlacement.Top, null, "w", Clock, null, HasHeader: true), [], []);
        var many = new SelectionModel(SelectionKind.Several, new QuickSetOffer(3, null, null, null, null, null, HasHeader: false), [], []);

        Assert.Equal([SelectionSection.Shows, SelectionSection.Header, SelectionSection.Appearance], SelectionPage.Sections(one, ownLines: true, styleLines: false));
        Assert.Equal([SelectionSection.Header, SelectionSection.Appearance], SelectionPage.Sections(one, ownLines: false, styleLines: false));
        Assert.Equal([SelectionSection.Appearance], SelectionPage.Sections(many, ownLines: true, styleLines: true));
    }

    [Fact]
    public void S11754_a_groups_page_is_what_its_widgets_share_its_widgets_and_their_look()
    {
        SharedRow[] place = [new(SharedSettings.Place, ["a", "b"], Mixed: false)];
        var offer = new QuickSetOffer(2, HeaderPlacement.Top, null, null, null, null, HasHeader: true);
        var group = new SelectionModel(SelectionKind.Group, offer, [], place) { GroupId = "g" };
        var several = new SelectionModel(SelectionKind.Several, offer, [], place);
        var bare = new SelectionModel(SelectionKind.Group, offer, [], []) { GroupId = "g" };
        var member = new SelectionModel(SelectionKind.Member, new QuickSetOffer(1, HeaderPlacement.Top, null, "a", Clock, null), [], []) { GroupId = "g", GroupName = "Station" };

        // A group: what they share, its widgets, their look; never a title or the header's place, which are each widget's.
        Assert.Equal([SelectionSection.Shared, SelectionSection.Members, SelectionSection.Appearance], SelectionPage.Sections(group, ownLines: true, styleLines: true));
        Assert.Equal([SelectionSection.Members, SelectionSection.Appearance], SelectionPage.Sections(bare, ownLines: true, styleLines: true));

        // Several widgets that are not one group share the same section, and keep the header's place.
        Assert.Equal([SelectionSection.Shared, SelectionSection.Header, SelectionSection.Appearance], SelectionPage.Sections(several, ownLines: true, styleLines: true));

        // An entered member is its own page, under the link back to its group.
        Assert.Equal([SelectionSection.InGroup, SelectionSection.Shows, SelectionSection.Header, SelectionSection.Appearance], SelectionPage.Sections(member, ownLines: true, styleLines: false));
        Assert.Equal("Station", member.GroupTitle);
    }

    [Fact]
    public void S11754_a_group_is_called_by_its_name_or_group_and_a_typed_name_is_trimmed()
    {
        Assert.Equal("Group", SelectionPage.GroupTitle(null));
        Assert.Equal("Group", SelectionPage.GroupTitle("  "));
        Assert.Equal("Station", SelectionPage.GroupTitle("Station"));

        Assert.Null(SelectionPage.GroupName(""));
        Assert.Null(SelectionPage.GroupName("   "));
        Assert.Null(SelectionPage.GroupName(" Group "));
        Assert.Equal("Roof", SelectionPage.GroupName("  Roof "));

        var group = new SelectionModel(SelectionKind.Group, new QuickSetOffer(4, null, null, null, null, null), [], []) { GroupId = "g", GroupName = "Weather station" };
        Assert.Equal("Weather station", SelectionPage.Words(group, _ => "?"));
        Assert.Equal(ReadyMadeGroups.WeatherStation.Glyph, SelectionPage.Glyph(group));
        Assert.Equal(Glyphs.Group, SelectionPage.Glyph(group with { GroupName = "Roof" }));
        Assert.Null(SelectionPage.Words((SelectionModel?)null, _ => "?"));
    }
}
