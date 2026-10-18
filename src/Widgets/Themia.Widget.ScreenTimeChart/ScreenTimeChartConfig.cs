using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3330] The screen-time chart: the last week or month, a day at a time.
/// <para>
/// None of the screen-time configs holds usage, hidden apps or limits: the history and the
/// preferences every screen-time widget shares live in their own file on this machine ([S-459],
/// [S-3322]), so copying a widget or exporting a layout never carries what the user did with their day.
/// </para>
/// </summary>
public sealed class ScreenTimeChartConfig : WidgetConfig, IShownSettings<ScreenTimeSetting>
{
    /// <summary>[S-6074] The chart sums the day against the daily limit; no app's limit shows on it.</summary>
    [JsonIgnore]
    public IReadOnlyList<ScreenTimeSetting> ShownSettings => [ScreenTimeSetting.DailyLimit];

    /// <summary>Week or Month; a day is the donut's and the list's ([S-3330]).</summary>
    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Week;

    public ScreenTimeChartStyle Style { get; set; } = ScreenTimeChartStyle.Bars;

    public override WidgetConfig Clone() => new ScreenTimeChartConfig { Period = Period, Style = Style };
}
