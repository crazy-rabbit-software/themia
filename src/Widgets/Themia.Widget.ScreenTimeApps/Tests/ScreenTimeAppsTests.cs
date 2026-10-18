using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.ScreenTimeApps.Tests;

/// <summary>[S-3352] The app list's ladder. Moved with the widget from Core's ScreenTimeLayoutTests, unchanged in what they assert (the ladder is ScreenTimeAppsLayout's now).</summary>
public sealed class ScreenTimeAppsTests
{
    /// <summary>The card's header, which the content area is the card less.</summary>
    private const double Header = 44;

    /// <summary>A compact list row (Fluent's 32).</summary>
    private const double Row = 32;

    private static WidgetArea Content(WidgetTier tier) => new(tier.Width, tier.Height - Header);

    private static WidgetTier Tier(string name) => WidgetTiers.All.First(t => t.Name == name);

    [Fact]
    public void S3352_rows_grow_with_the_tile_and_never_pass_what_is_wanted()
    {
        var rows = WidgetTiers.All.Select(t => ScreenTimeAppsLayout.Apps(Content(t), Row, 50, false, true, true).Rows).ToList();

        Assert.True(rows[0] >= 1);
        Assert.True(Rows("Tall") > Rows("S"));
        Assert.True(Rows("XL") > Rows("M"));
        Assert.Equal(3, ScreenTimeAppsLayout.Apps(Content(Tier("XL")), Row, 3, false, true, true).Rows);
        Assert.Equal(0, ScreenTimeAppsLayout.Apps(default, Row, 3, false, true, true).Rows);

        static int Rows(string tier) => ScreenTimeAppsLayout.Apps(Content(Tier(tier)), Row, 50, false, true, true).Rows;
    }

    [Fact]
    public void S3352_columns_come_with_width_and_the_average_only_in_a_window()
    {
        var narrow = ScreenTimeAppsLayout.Apps(Content(Tier("XS")), Row, 5, true, true, true);
        var medium = ScreenTimeAppsLayout.Apps(Content(Tier("M")), Row, 5, true, true, true);
        var day = ScreenTimeAppsLayout.Apps(Content(Tier("M")), Row, 5, false, true, true);
        var off = ScreenTimeAppsLayout.Apps(Content(Tier("XL")), Row, 5, true, false, false);

        Assert.False(narrow.Percent || narrow.Average);
        Assert.True(medium.Percent && medium.Average);
        Assert.False(day.Average);
        Assert.False(off.Percent || off.Average);
    }

    [Fact]
    public void S3352_a_narrow_row_drops_the_name_and_keeps_the_time()
    {
        // At XS a name squeezed beside the time read as one letter ("O" for Other): the row is the
        // icon and the time, the name in its tooltip; from S on every row carries its name.
        var xs = ScreenTimeAppsLayout.Apps(Content(Tier("XS")), Row, 5, false, true, true);
        Assert.True(xs.Rows >= 1);
        Assert.False(xs.Names);

        foreach (var tier in WidgetTiers.All.Where(t => t.Name != "XS"))
        {
            Assert.True(ScreenTimeAppsLayout.Apps(Content(tier), Row, 5, false, true, true).Names, tier.Name);
        }

        Assert.True(ScreenTimeAppsLayout.Apps(new WidgetArea(ScreenTimeAppsLayout.NamesFrom, 200), Row, 5, false, true, true).Names);
        Assert.False(ScreenTimeAppsLayout.Apps(new WidgetArea(ScreenTimeAppsLayout.NamesFrom - 1, 200), Row, 5, false, true, true).Names);
    }
}
