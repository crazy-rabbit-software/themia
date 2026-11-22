using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Series;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13910] The weather forecast (owner, 2026-11-21): the days ahead, each day a composition of widgets, laid on the
/// desktop's grid. Its parts are real widgets (the day facts on the single metric's face, [S-14600], and the design elements,
/// [S-10100]) standing in its day box; every other day draws them again at that day's place. Everything set once is set
/// here: the place, the layout, the spacing, the first day; its parts are added from here ([S-13914]: the host's Add lines)
/// and arranged on the desktop in its edit mode ([S-15526]).
/// </summary>
public sealed class WeatherDaysModule : IWidgetModule
{
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weatherDays"),
        ConfigType = typeof(WeatherDaysConfig),
        CreateDefault = () => new WeatherDaysConfig { UseDeviceLocation = true },
        Json = WeatherFactJsonContext.Default,
        Label = WeatherFactsStr.WeatherFacts_Days_Label.Local(),
        Description = WeatherFactsStr.WeatherFacts_Days_Desc.Local(),
        Glyph = Glyphs.Weather,

        // [S-15503] Its box is whole days of its day box, laid by the host's rules; a new one is room for a week of rows.
        Sizing = _ => WidgetSizing.Range(new(2 * WidgetSizing.Floor, 2 * WidgetSizing.Floor), new(MetricRules.MaxWidth, 7 * MetricRules.RowHeight), new(SizeStretch.Ceiling, SizeStretch.Ceiling)),

        // [S-15501] No body of its own: no card, and never one panel with the parts it holds.
        Traits = WidgetTraits.StartsTransparent | WidgetTraits.NeverJoins,
        Keywords = ["forecast", "days", "week", "rows", "columns", "grid", "weather", "high", "low"],
        Add = new(AddFolderId.Weather, 2, 0, Popular: 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherDaysView();

    /// <summary>
    /// [S-13911] Its place (the weather family's search, the one for all its parts), then its days: the layout, the spacing,
    /// the first day. Its parts' Add lines are the host's (they make widgets, [S-13914]).
    /// </summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (config is not WeatherDaysConfig days)
        {
            return;
        }

        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, days));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.Add(menu.Choice(
            null,
            WeatherFactsStr.WeatherFacts_Days_Layout.Text(),
            [
                (RepeatDirection.Down, WeatherFactsStr.WeatherFacts_Days_Rows.Text()),
                (RepeatDirection.Across, WeatherFactsStr.WeatherFacts_Days_Columns.Text()),
                (RepeatDirection.Wrap, WeatherFactsStr.WeatherFacts_Days_Grid.Text()),
            ],
            days.Direction,
            v => days.Direction = v));

        var grid = days.Direction == RepeatDirection.Wrap;
        menu.Lines.Add(Cells(menu, grid ? WeatherFactsStr.WeatherFacts_Days_RowSpacing.Text() : WeatherFactsStr.WeatherFacts_Days_Spacing.Text(), days.Spacing, v => days.Spacing = v));
        if (grid)
        {
            menu.Lines.Add(Cells(menu, WeatherFactsStr.WeatherFacts_Days_ColumnSpacing.Text(), days.ColumnSpacing, v => days.ColumnSpacing = v));
        }

        menu.Lines.Add(menu.Number(Glyphs.Calendar, WeatherFactsStr.WeatherFacts_Days_Starts.Text(), days.Start, 0, DayRangeSource.Reach - 1, 1, WeatherFactModule.DayName, v =>
        {
            days.Start = v;
            days.Scroll = 0;
        }));

        // [S-13918] How many days: more than its card holds scroll (the wheel over it, a day a notch).
        menu.Lines.Add(menu.Number(null, WeatherFactsStr.WeatherFacts_Days_Count.Text(), days.Days, 1, DayRangeSource.Reach, 1, v => WeatherFactsStr.WeatherFacts_Days_CountDays.Plural(v), v =>
        {
            days.Days = v;
            days.Scroll = 0;
        }));
    }

    /// <summary>[S-13911] A spacing in grid cells, 0 to 20.</summary>
    private static SettingsLine Cells(WidgetMenuLines menu, string text, int value, Action<int> set)
        => menu.Number(null, text, Math.Clamp(value, 0, 20), 0, 20, 1, v => WeatherFactsStr.WeatherFacts_Days_Cells.Plural(v), set);
}
