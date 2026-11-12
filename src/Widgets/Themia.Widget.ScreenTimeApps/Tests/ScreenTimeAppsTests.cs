using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTimeApps.Tests;

/// <summary>[S-3350] [S-3352] The app list's ladder on the family's rows ([S-6711], [S-6712]).</summary>
public sealed class ScreenTimeAppsTests
{
    /// <summary>The card's header, which the content area is the card less.</summary>
    private const double Header = FamilySpace.ContentTop;

    private static WidgetArea Content(WidgetTier tier) => new(tier.Width, tier.Height - Header);

    private static WidgetTier Tier(string name) => WidgetTiers.All.First(t => t.Name == name);

    [Fact]
    public void S3352_rows_grow_with_the_tile_and_never_pass_what_is_wanted()
    {
        Assert.True(Rows("XS") >= 1);
        Assert.True(Rows("Tall") > Rows("S"));
        Assert.True(Rows("XL") > Rows("M"));
        Assert.Equal(3, ScreenTimeAppsLayout.Apps(Content(Tier("XL")), 3, false).Rows);
        Assert.Equal(0, ScreenTimeAppsLayout.Apps(default, 3, false).Rows);

        static int Rows(string tier) => ScreenTimeAppsLayout.Apps(Content(Tier(tier)), 50, false).Rows;
    }

    [Theory]
    [InlineData("XS")]
    [InlineData("S")]
    [InlineData("M")]
    [InlineData("Wide")]
    [InlineData("Tall")]
    [InlineData("L")]
    [InlineData("XL")]
    public void S3352_whole_rows_of_the_familys_pitch_in_the_area_less_the_bottom_inset(string name)
    {
        var area = Content(Tier(name));
        foreach (var figures in new[] { false, true })
        {
            var plan = ScreenTimeAppsLayout.Apps(area, 50, figures);
            Assert.True(plan.Rows * FamilyShape.RowHeight(plan.Lines) <= area.Height - FamilySpace.Inset, name);
            Assert.True((plan.Rows + 1) * FamilyShape.RowHeight(plan.Lines) > area.Height - FamilySpace.Inset, name);
        }
    }

    [Fact]
    public void S3350_the_figures_take_a_second_line_only_where_the_tier_gives_rows_two()
    {
        // One line while compact either way (XS, S, Wide); two from regular both ways, figures on.
        Assert.Equal(1, ScreenTimeAppsLayout.Apps(Content(Tier("Wide")), 5, true).Lines);
        Assert.Equal(2, ScreenTimeAppsLayout.Apps(Content(Tier("M")), 5, true).Lines);
        Assert.Equal(2, ScreenTimeAppsLayout.Apps(Content(Tier("XL")), 5, true).Lines);
        Assert.Equal(1, ScreenTimeAppsLayout.Apps(Content(Tier("XL")), 5, false).Lines);

        // The share whenever it is on; the average only over a window.
        Assert.True(ScreenTimeAppsLayout.Figures(window: false, showAverage: false, showPercent: true));
        Assert.False(ScreenTimeAppsLayout.Figures(window: false, showAverage: true, showPercent: false));
        Assert.True(ScreenTimeAppsLayout.Figures(window: true, showAverage: true, showPercent: false));
    }

    [Fact]
    public void S3350_the_lead_is_the_apps_icon_at_32_from_a_width_of_180()
    {
        Assert.Equal(FamilyShape.Icon, ScreenTimeAppsLayout.Apps(Content(Tier("XS")), 5, false).Lead);
        Assert.Equal(FamilyShape.IconLarge, ScreenTimeAppsLayout.Apps(Content(Tier("S")), 5, false).Lead);
        Assert.Equal(FamilyShape.Icon, ScreenTimeAppsLayout.Apps(new WidgetArea(ScreenTimeAppsLayout.LeadFrom - 1, 200), 5, false).Lead);
        Assert.Equal(FamilyShape.IconLarge, ScreenTimeAppsLayout.Apps(new WidgetArea(ScreenTimeAppsLayout.LeadFrom, 200), 5, false).Lead);
    }

    [Fact]
    public void S3352_a_narrow_row_drops_the_name_and_keeps_the_time()
    {
        // At XS a name squeezed beside the time read as one letter ("O" for Other): the row is the
        // icon and the time, the name in its tooltip; from S on every row carries its name.
        var xs = ScreenTimeAppsLayout.Apps(Content(Tier("XS")), 5, false);
        Assert.True(xs.Rows >= 1);
        Assert.False(xs.Names);

        foreach (var tier in WidgetTiers.All.Where(t => t.Name != "XS"))
        {
            Assert.True(ScreenTimeAppsLayout.Apps(Content(tier), 5, false).Names, tier.Name);
        }

        Assert.True(ScreenTimeAppsLayout.Apps(new WidgetArea(ScreenTimeAppsLayout.NamesFrom, 200), 5, false).Names);
        Assert.False(ScreenTimeAppsLayout.Apps(new WidgetArea(ScreenTimeAppsLayout.NamesFrom - 1, 200), 5, false).Names);
    }
}
