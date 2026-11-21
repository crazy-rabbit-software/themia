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
using Themia.Widget.WeatherNow;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("weatherSky", typeof(WeatherSkyModule))]
[assembly: ThemiaWidget("weatherCondition", typeof(WeatherConditionModule))]

namespace Themia.Widget.WeatherNow;

/// <summary>
/// [S-15020] The weather summary's family (owner, 2026-11-21: "weather summary should also be split into three widgets"):
/// the weather now where the user says they are, a widget a part, each over the same reading of its place: the sky as one
/// icon (<c>weatherSky</c>, an icon kind, [S-15021]) and the condition in words (<c>weatherCondition</c>, a text kind,
/// [S-15023]); the third part, the temperature, is the weather facts' single metric (<c>weatherTemperature</c>). A placed
/// summary (<c>weatherNow</c>, gone) becomes the three in its spot ([S-15025], <see cref="WeatherSummary"/>). The order here
/// is the family's on the add page. SPEC.md beside this file.
/// </summary>
internal static class WeatherNowKinds
{
    /// <summary>[S-15021] The sky: the condition's glyph, the forecast a click opens.</summary>
    public static IconKind Sky { get; } = new("weatherSky", Glyphs.Weather,
        WeatherNowStr.WeatherNow_Sky_Label.Local(), WeatherNowStr.WeatherNow_Sky_Desc.Local(),
        ["weather", "sky", "conditions", "sunny", "cloudy", "rain", "snow", "icon", "now"], () => new WeatherSkyConfig())
    {
        Opens = IconOpens.Weather,
    };

    /// <summary>[S-15023] The condition: one line, no lead (the sky is its own widget), a detail's tone, the forecast a click opens.</summary>
    public static TextKind Condition { get; } = new("weatherCondition", "condition", Glyphs.Weather,
        WeatherNowStr.WeatherNow_Condition_Label.Local(), WeatherNowStr.WeatherNow_Condition_Desc.Local(),
        ["weather", "conditions", "sky", "overcast", "sunny", "now", "description"], () => new WeatherConditionConfig())
    {
        Lead = false,
        Tone = TextTone.Secondary,
        Opens = TextOpens.Weather,
    };

    /// <summary>The family's order on the add page: the sky, the condition.</summary>
    public const int SkyRank = 0, ConditionRank = 1;
}

/// <summary>[S-15021] The sky now as an icon kind ([S-15003]), at home in the Weather summary family under Weather.</summary>
public sealed class WeatherSkyModule() : IconModule(WeatherNowKinds.Sky, WeatherNowJsonContext.Default, AddFolderId.WeatherSummary,
    WeatherNowKinds.SkyRank, popular: 1, merges: WeatherSummary.Sky)
{
    /// <summary>[S-6174] The place's search.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => WeatherNowMenu.Lines(menu, (WeatherNowPartConfig)config);

    public override FrameworkElement CreateView(IWidgetHost host) => new WeatherSkyView(host);
}

/// <summary>[S-15023] The condition now as a text kind ([S-14934]), at home in the Weather summary family under Weather.</summary>
public sealed class WeatherConditionModule() : TextModule(WeatherNowKinds.Condition, WeatherNowJsonContext.Default, AddFolderId.WeatherSummary,
    WeatherNowKinds.ConditionRank, merges: WeatherSummary.Condition)
{
    /// <summary>[S-6174] The place's search.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => WeatherNowMenu.Lines(menu, (WeatherNowPartConfig)config);

    public override FrameworkElement CreateView(IWidgetHost host) => new WeatherConditionView(host);
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherSkyConfig))]
[JsonSerializable(typeof(WeatherConditionConfig))]
internal sealed partial class WeatherNowJsonContext : JsonSerializerContext;
