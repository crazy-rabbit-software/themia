using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-3321] What a screen-time widget covers: one day, or a rolling window ending today.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScreenTimePeriod>))]
public enum ScreenTimePeriod
{
    Day = 0,

    /// <summary>The last 7 days, never the calendar week.</summary>
    Week = 1,

    /// <summary>The last 30 days, never the calendar month.</summary>
    Month = 2,
}

/// <summary>[S-3330] How the chart draws its days.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScreenTimeChartStyle>))]
public enum ScreenTimeChartStyle
{
    /// <summary>One bar per day, stacked by app.</summary>
    Bars = 0,

    /// <summary>A calendar of the window's days, shaded by use.</summary>
    Heatmap = 1,
}

/// <summary>[S-457] A daily limit on one app, as the settings page edits it: its key and its seconds.</summary>
public sealed class ScreenTimeAppLimit
{
    public string App { get; set; } = string.Empty;

    /// <summary>Seconds per day. Zero or less means no limit.</summary>
    public int Seconds { get; set; }
}

/// <summary>
/// [S-6074] The screen-time rows only some of the three widgets draw with. The limits and the
/// hidden apps are one set for the machine ([S-3322]); a page offers the part its widget shows.
/// </summary>
public enum ScreenTimeSetting
{
    /// <summary>The daily limit: the chart's and the donut's summary.</summary>
    DailyLimit,

    /// <summary>The per-app limits: the list's times turn red past them.</summary>
    AppLimits,

    /// <summary>Each app's average per day: the list, over a week or a month.</summary>
    Average,
}

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

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ScreenTimeChart;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeChart"),
        Kind = WidgetKind.ScreenTimeChart,
        ConfigType = typeof(ScreenTimeChartConfig),
        CreateDefault = () => new ScreenTimeChartConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "history", "week", "chart", "statistics"],
        LabelId = Str.WidgetsC_ScreenTime_Chart_Label,
        DescriptionId = Str.WidgetsC_ScreenTime_Chart_Desc,
        Glyph = Glyphs.ScreenTimeChart,
        DefaultWidth = 320,
        DefaultHeight = 220,
        Traits = WidgetTraits.CanHaveHeader,
    };

    /// <summary>Week or Month; a day is the donut's and the list's ([S-3330]).</summary>
    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Week;

    public ScreenTimeChartStyle Style { get; set; } = ScreenTimeChartStyle.Bars;

    public override WidgetConfig Clone() => new ScreenTimeChartConfig { Period = Period, Style = Style };
}

/// <summary>[S-3340] The screen-time donut: each app's share of the period, as a ring with the total in its hole.</summary>
public sealed class ScreenTimeDonutConfig : WidgetConfig, IShownSettings<ScreenTimeSetting>
{
    /// <summary>[S-6074] The ring's total reads against the daily limit; no app's limit shows on it.</summary>
    [JsonIgnore]
    public IReadOnlyList<ScreenTimeSetting> ShownSettings => [ScreenTimeSetting.DailyLimit];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ScreenTimeDonut;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeDonut"),
        Kind = WidgetKind.ScreenTimeDonut,
        ConfigType = typeof(ScreenTimeDonutConfig),
        CreateDefault = () => new ScreenTimeDonutConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "share", "pie", "ring", "statistics"],
        LabelId = Str.WidgetsC_ScreenTime_Donut_Label,
        DescriptionId = Str.WidgetsC_ScreenTime_Donut_Desc,
        Glyph = Glyphs.ScreenTimeDonut,
        DefaultWidth = 240,
        DefaultHeight = 260,
        Traits = WidgetTraits.CanHaveHeader,
    };

    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Day;

    public override WidgetConfig Clone() => new ScreenTimeDonutConfig { Period = Period };
}

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

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ScreenTimeApps;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeApps"),
        Kind = WidgetKind.ScreenTimeApps,
        ConfigType = typeof(ScreenTimeAppsConfig),
        CreateDefault = () => new ScreenTimeAppsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "app usage", "list", "limits"],
        LabelId = Str.WidgetsC_ScreenTime_Apps_Label,
        DescriptionId = Str.WidgetsC_ScreenTime_Apps_Desc,
        Glyph = Glyphs.ScreenTime,
        DefaultWidth = 280,
        DefaultHeight = 300,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
    };

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
