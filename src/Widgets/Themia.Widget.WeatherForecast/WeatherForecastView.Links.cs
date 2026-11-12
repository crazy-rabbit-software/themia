using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11924] [S-11927] The forecast's place on its view's links: a day's row, column, cell or curve column
/// publishes its day under the pointer (<see cref="LinkTopics.HoveredDate"/>) and takes the item layer's
/// hover fill while that day is pointed at anywhere on the view; the day selected there
/// (<see cref="LinkTopics.SelectedDate"/>, a calendar's click) is marked with the picked look when the
/// forecast shows it. A hover or a selection never changes which days it shows.
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetSurfaceAware
{
    private readonly WidgetLinks _links = new();
    private readonly LinkLights<DateLink> _days;
    private IWidgetSurface? _surface;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    /// <summary>[S-11924] A day's part: it publishes its day and lights for it (hover fill; picked: selected fill and the pill).</summary>
    private void LinkDay(FrameworkElement part, DateOnly day) => _days.Row(part, new DateLink(day));
}
