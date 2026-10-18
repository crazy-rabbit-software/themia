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
using Themia.Widget.Brightness;

[assembly: ThemiaWidget("brightness", typeof(BrightnessModule))]

namespace Themia.Widget.Brightness;

/// <summary>[S-4440] The display brightness: one slider and the sun, every display together or the one it is on. SPEC.md beside this file.</summary>
public sealed class BrightnessModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("brightness"),
        ConfigType = typeof(BrightnessConfig),
        CreateDefault = () => new BrightnessConfig(),
        Json = BrightnessJsonContext.Default,
        Keywords = ["display", "screen", "dim", "monitor", "backlight", "ddc"],
        Label = BrightnessStr.Panels_Brightness_Label.Local(),
        Description = BrightnessStr.Panels_Brightness_Desc.Local(),
        Glyph = Glyphs.Brightness,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Devices, 1, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => BrightnessSettings.Rows(rows.Rows, (BrightnessConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new BrightnessView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BrightnessConfig))]
internal sealed partial class BrightnessJsonContext : JsonSerializerContext;
