using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-3350] The screen-time app list: the apps of the period, busiest first, with their time.</summary>
public sealed class ScreenTimeAppsConfig : WidgetConfig, IShownSettings<ScreenTimeSetting>
{
    /// <summary>
    /// [S-6074] The list marks each app against its own limit, never the day's total against the
    /// daily one; an average per day exists over a window (a week or a month), not over one day.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ScreenTimeSetting> ShownSettings => Period == ScreenTimePeriod.Day
        ? [ScreenTimeSetting.AppLimits]
        : [ScreenTimeSetting.AppLimits, ScreenTimeSetting.Average];

    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Day;

    /// <summary>
    /// [S-1211] The most apps listed, 1 to 50, or 0 (the default) for as many as the tile has room
    /// for. A ceiling over what fits, never a fixed length: a bigger tile lists more.
    /// </summary>
    public int MaxApps { get; set; }

    /// <summary>[S-453] In a window, each app's average per day beside its time.</summary>
    public bool ShowAverage { get; set; } = true;

    /// <summary>[S-453] Each app's share of the shown total.</summary>
    public bool ShowPercent { get; set; }

    public override WidgetConfig Clone() => new ScreenTimeAppsConfig
    {
        Period = Period,
        MaxApps = MaxApps,
        ShowAverage = ShowAverage,
        ShowPercent = ShowPercent,
    };
}
