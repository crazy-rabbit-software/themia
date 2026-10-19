using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2849] The weather family's Location name switch only changes the header's title, so it is on
/// the menu's first level, after the Location line, only while the card's header shows and no typed title wins over it; the card the lines
/// are built for comes from the widget (<see cref="QuickSet.Card"/>).
/// </summary>
public sealed class WeatherLocationNameTests
{
    public static TheoryData<string> Weather() => ["weatherNow", "weatherForecast", "airQuality", "rainNextHour"];

    private static WidgetInstance Widget(string kind, HeaderPlacement header, string? label)
    {
        var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(0, 0, 0.2, 0.2));
        widget.Header = header;
        widget.Label = label;
        return widget;
    }

    private static bool OffersName(WidgetInstance widget)
    {
        var menu = new WidgetMenuLines(() => { }, _ => { }) { Card = QuickSet.Card(widget) };
        ModuleRegistry.Widgets.Single(m => m.Descriptor.Id == widget.Kind).MenuLines(menu, widget.Config);
        var location = Assert.Single(menu.Lines, l => l.Text == Str.Shell_Place_Location.Text());

        // Clear location stays either way, in the search: it is the place, not the title.
        Assert.Equal([Str.Shell_Place_Clear.Text()], location.Search!.Fixed.Select(f => f.Text));
        var at = menu.Lines.IndexOf(location);
        var name = menu.Lines.FindIndex(l => l.Text == Str.Weather_LocationNameLabel.Text());
        Assert.True(name < 0 || name == at + 1, "the name switch stands right after the Location line");
        return name >= 0;
    }

    [Theory]
    [MemberData(nameof(Weather))]
    public void S2849_the_name_switch_is_offered_while_the_header_shows_the_widgets_title(string kind)
    {
        Assert.True(OffersName(Widget(kind, HeaderPlacement.Top, null)));
        Assert.True(OffersName(Widget(kind, HeaderPlacement.Bottom, "  ")));
    }

    [Theory]
    [MemberData(nameof(Weather))]
    public void S2849_no_name_switch_without_a_header_or_under_a_typed_title(string kind)
    {
        Assert.False(OffersName(Widget(kind, HeaderPlacement.Hidden, null)));
        Assert.False(OffersName(Widget(kind, HeaderPlacement.Top, "Home")));
    }

    [Fact]
    public void S2849_the_card_reads_the_header_as_drawn_and_a_typed_title()
    {
        Assert.Equal(new MenuCard(true, false), QuickSet.Card(Widget("weatherNow", HeaderPlacement.Top, null)));
        Assert.Equal(new MenuCard(false, true), QuickSet.Card(Widget("weatherNow", HeaderPlacement.Hidden, "Home")));
        Assert.True(MenuCard.Plain.ReadsWidgetTitle);
        Assert.False(new MenuCard(true, true).ReadsWidgetTitle);
    }
}
