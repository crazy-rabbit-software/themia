using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>[S-3350] The screen-time app list: the apps of the period, busiest first, with their time.</summary>
public sealed class ScreenTimeAppsConfig : WidgetConfig, IShownSettings<ScreenTimeSetting>, IKitRows
{
    /// <summary>[S-11510] A new list's rows.</summary>
    public const int DefaultRows = 5;

    /// <summary>
    /// [S-6074] The list marks each app against its own limit, never the day's total against the
    /// daily one; an average per day exists over a window (a week or a month), not over one day.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ScreenTimeSetting> ShownSettings => Period == ScreenTimePeriod.Day
        ? [ScreenTimeSetting.AppLimits]
        : [ScreenTimeSetting.AppLimits, ScreenTimeSetting.Average];

    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Day;

    /// <summary>[S-11510] The rows the list shows (1..20): the busiest apps, the last one Other when apps are left over.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11510] One line a row, or two: the second the share and a window's average, as far as they are on.</summary>
    public int Lines { get; set; } = 1;

    /// <summary>[S-453] In a window, each app's average per day on its second line.</summary>
    public bool ShowAverage { get; set; } = true;

    /// <summary>[S-453] Each app's share of the shown total on its second line.</summary>
    public bool ShowPercent { get; set; }

    /// <summary>[S-11510] Whether a row has anything for a second line: the share when shown, the average over a window when shown.</summary>
    [JsonIgnore]
    public bool HasFigures => ShowPercent || (ScreenTimeRules.ClampPeriod(Period) != ScreenTimePeriod.Day && ShowAverage);

    /// <summary>[S-11510] The rows as drawn: a second line only where a row has figures for it.</summary>
    [JsonIgnore]
    public KitRowsChoice RowsShown => new(KitSizing.ClampRows(Rows), HasFigures ? KitSizing.ClampLines(Lines) : 1);

    public override WidgetConfig Clone() => new ScreenTimeAppsConfig
    {
        Period = Period,
        Rows = Rows,
        Lines = Lines,
        ShowAverage = ShowAverage,
        ShowPercent = ShowPercent,
    };

    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        if (Rows != KitSizing.ClampRows(Rows))
        {
            repairs.Add($"screen-time rows {Rows} are out of range; clamped to {KitSizing.ClampRows(Rows)}");
            Rows = KitSizing.ClampRows(Rows);
        }

        if (Lines != KitSizing.ClampLines(Lines))
        {
            repairs.Add($"screen-time lines {Lines} are out of range; clamped to {KitSizing.ClampLines(Lines)}");
            Lines = KitSizing.ClampLines(Lines);
        }

        return repairs;
    }
}
