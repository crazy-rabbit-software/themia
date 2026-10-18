using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
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


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeChart"),
        ConfigType = typeof(ScreenTimeChartConfig),
        CreateDefault = () => new ScreenTimeChartConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "history", "week", "chart", "statistics"],
        Label = Str.WidgetsC_ScreenTime_Chart_Label.Local(),
        Description = Str.WidgetsC_ScreenTime_Chart_Desc.Local(),
        Glyph = Glyphs.ScreenTimeChart,
        DefaultWidth = 320,
        DefaultHeight = 220,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.ScreenTime, 0, 0),
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


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeDonut"),
        ConfigType = typeof(ScreenTimeDonutConfig),
        CreateDefault = () => new ScreenTimeDonutConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "share", "pie", "ring", "statistics"],
        Label = Str.WidgetsC_ScreenTime_Donut_Label.Local(),
        Description = Str.WidgetsC_ScreenTime_Donut_Desc.Local(),
        Glyph = Glyphs.ScreenTimeDonut,
        DefaultWidth = 240,
        DefaultHeight = 260,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.ScreenTime, 0, 1),
    };

    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Day;

    public override WidgetConfig Clone() => new ScreenTimeDonutConfig { Period = Period };
}
