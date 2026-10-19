using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
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

/// <summary>
/// [S-6074] The screen-time rows only some of the three widgets draw with. The limits and the
/// hidden apps are one set for the machine ([S-3322]); a menu offers the part its widget shows.
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
