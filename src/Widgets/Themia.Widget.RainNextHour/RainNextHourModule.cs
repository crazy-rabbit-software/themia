using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.RainNextHour;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("rainOutlook", typeof(RainOutlookModule))]
[assembly: ThemiaWidget("rainNextHour", typeof(RainNextHourModule))]
[assembly: ThemiaWidget("rainTotal", typeof(RainTotalModule))]

namespace Themia.Widget.RainNextHour;

/// <summary>
/// [S-14940] The rain family (owner, 2026-11-21: "split up rain next hour into three widgets too."): the rain in the next
/// two hours at a place, a widget a part, each over the same nowcast of its place: the sentence (<c>rainOutlook</c>, a text
/// kind, [S-14942]), the graph (<c>rainNextHour</c>, the old card's id: a placed one is the graph, [S-14941]) and the total
/// (<c>rainTotal</c>, a single metric, [S-14943]). The order here is the family's on the add page. SPEC.md beside this file.
/// </summary>
internal static class RainKinds
{
    /// <summary>[S-14942] The sentence: two lines at most, no lead (it says "rain" itself), the forecast a click opens.</summary>
    public static TextKind Outlook { get; } = new("rainOutlook", "outlook", Glyphs.Rain,
        RainNextHourStr.Online_Rain_Label.Local(), RainNextHourStr.Rain_Outlook_Desc.Local(),
        ["rain", "precipitation", "shower", "umbrella", "nowcast", "drizzle", "weather", "when will it rain"], () => new RainOutlookConfig())
    {
        Lines = 2,
        Lead = false,
        Opens = TextOpens.Weather,
    };

    /// <summary>[S-14943] The total: the millimetres the next two hours hold, the forecast a click opens.</summary>
    public static MetricKind Total { get; } = new("rainTotal", "Total", Glyphs.Rain, MetricHistory.None,
        RainNextHourStr.Rain_Total_Name.Local(), RainNextHourStr.Rain_Total_Label.Local(), RainNextHourStr.Rain_Total_Desc.Local(),
        ["rain", "precipitation", "amount", "millimetres", "total", "weather"], () => new RainTotalConfig())
    {
        Opens = MetricOpens.Weather,
    };

    /// <summary>The family's order on the add page: the sentence, the graph, the total.</summary>
    public const int OutlookRank = 0, GraphRank = 1, TotalRank = 2;
}

/// <summary>
/// [S-5120] [S-14941] The rain graph: a bar per quarter hour of the next two hours at a place, on the one chart. SPEC.md
/// beside this file.
/// </summary>
public sealed class RainNextHourModule : IWidgetModule
{
    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rainNextHour"),
        ConfigType = typeof(RainNextHourConfig),
        CreateDefault = () => new RainNextHourConfig(),
        Json = RainJsonContext.Default,
        Keywords = ["rain", "precipitation", "shower", "umbrella", "radar", "nowcast", "drizzle", "weather", "graph", "bars"],
        Label = RainNextHourStr.Rain_Graph_Label.Local(),
        Description = RainNextHourStr.Rain_Graph_Desc.Local(),
        Glyph = Glyphs.Rain,
        Sizing = _ => RainLayout.Sizing(),
        DescriptionFacts = [("minutes", (long)RainRules.Step.TotalMinutes)],
        Traits = WidgetTraits.None,
        Add = new(AddFolderId.Rain, 0, RainKinds.GraphRank),
    };

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => RainNextHourMenu.Lines(menu, (RainConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new RainNextHourView(host);
}

/// <summary>[S-14942] The rain's sentence as a text kind ([S-14934]), at home in the Rain family under Weather.</summary>
public sealed class RainOutlookModule() : TextModule(RainKinds.Outlook, RainJsonContext.Default, AddFolderId.Rain, RainKinds.OutlookRank)
{
    /// <summary>[S-5127] The place's search and the source's credit.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => RainNextHourMenu.Lines(menu, (RainConfig)config);

    public override FrameworkElement CreateView(IWidgetHost host) => new RainOutlookView(host);
}

/// <summary>[S-14943] The rain's total as a single metric ([S-14604]), at home in the Rain family under Weather.</summary>
public sealed class RainTotalModule() : MetricModule(RainKinds.Total, RainJsonContext.Default, AddFolderId.Rain, RainKinds.TotalRank)
{
    /// <summary>[S-5127] The place's search and the source's credit.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => RainNextHourMenu.Lines(menu, (RainConfig)config);

    public override FrameworkElement CreateView(IWidgetHost host) => new RainTotalView(host);
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RainNextHourConfig))]
[JsonSerializable(typeof(RainOutlookConfig))]
[JsonSerializable(typeof(RainTotalConfig))]
internal sealed partial class RainJsonContext : JsonSerializerContext;
