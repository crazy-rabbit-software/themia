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
using Themia.Widget.AirQuality;

[assembly: ThemiaWidget("airQuality", typeof(AirQualityModule))]

namespace Themia.Widget.AirQuality;

/// <summary>[S-11130] The air quality index at a place and its band, as a number or on a gauge. SPEC.md beside this file.</summary>
public sealed class AirQualityModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+EC0A, a leaf: air quality and pollen; checked on a rendered sheet of SegoeIcons.ttf).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xEC0A);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("airQuality"),
        ConfigType = typeof(AirQualityConfig),
        CreateDefault = () => new AirQualityConfig(),
        Json = AirQualityJsonContext.Default,
        Keywords = ["aqi", "air quality index", "pollution", "smog", "eaqi"],
        Label = AirQualityStr.Online_Air_Label.Local(),
        Description = AirQualityStr.Online_Air_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => AirLayout.Sizing((AirQualityConfig)c),
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 0, 4),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => AirQualityMenu.Lines(menu, (AirQualityConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new AirQualityView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AirQualityConfig))]
internal sealed partial class AirQualityJsonContext : JsonSerializerContext;
