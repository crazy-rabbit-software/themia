using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.ScreenTimeChart;

namespace Themia.Core.Model;

/// <summary>
/// [S-3330] [S-3336] The chart's styles ([S-6000]): how it draws its days. The first is the default;
/// an unknown stored name is repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<ScreenTimeChartStyle>))]
public enum ScreenTimeChartStyle
{
    /// <summary>One bar per day, stacked by app.</summary>
    Bars = 0,

    /// <summary>A calendar of the window's days, shaded by use ([S-456]).</summary>
    Calendar = 1,
}

/// <summary>
/// [S-3330] The screen-time chart: the last week or month, a day at a time.
/// <para>
/// None of the screen-time configs holds usage, hidden apps or limits: the history and the
/// preferences every screen-time widget shares live in their own file on this machine ([S-459],
/// [S-3322]), so copying a widget or exporting a layout never carries what the user did with their day.
/// </para>
/// </summary>
public sealed class ScreenTimeChartConfig : WidgetConfig, IStyled, IShownSettings<ScreenTimeSetting>
{
    private static readonly StyleOption[] ChartStyles =
    [
        StyleOption.Of(ScreenTimeChartStyle.Bars, ScreenTimeChartStr.Styles_ScreenTimeChart_Bars.Local(), 0xE9D2),
        StyleOption.Of(ScreenTimeChartStyle.Calendar, ScreenTimeChartStr.Styles_ScreenTimeChart_Calendar.Local(), 0xE787),
    ];

    /// <summary>[S-6074] The chart sums the day against the daily limit; no app's limit shows on it.</summary>
    [JsonIgnore]
    public IReadOnlyList<ScreenTimeSetting> ShownSettings => [ScreenTimeSetting.DailyLimit];

    /// <summary>Week or Month; a day is the donut's and the list's ([S-3330]).</summary>
    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Week;

    /// <summary>[S-3336] The chart's look: what was its Style choice.</summary>
    public ScreenTimeChartStyle Style { get; set; } = ScreenTimeChartStyle.Bars;

    IReadOnlyList<StyleOption> IStyled.Styles => ChartStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ScreenTimeChartStyle)value;
    }

    public override WidgetConfig Clone() => new ScreenTimeChartConfig { Period = Period, Style = Style };
}
