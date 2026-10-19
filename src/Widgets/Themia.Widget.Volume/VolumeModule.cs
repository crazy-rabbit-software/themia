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
using Themia.Widget.Volume;

[assembly: ThemiaWidget("volume", typeof(VolumeModule))]

namespace Themia.Widget.Volume;

/// <summary>[S-2730] An output device's volume: the slider and nothing else, its mute on the speaker. SPEC.md beside this file.</summary>
public sealed class VolumeModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("volume"),
        ConfigType = typeof(VolumeConfig),
        CreateDefault = () => new VolumeConfig(),
        Json = VolumeJsonContext.Default,
        Keywords = ["sound", "audio", "speaker", "mute", "loudness"],
        Label = VolumeStr.Widget_Volume_Label.Local(),
        Description = VolumeStr.WidgetsA_Catalog_Volume_Description.Local(),
        Glyph = Glyphs.Volume,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Media, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => VolumeMenu.Lines(menu, (VolumeConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new VolumeView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(VolumeConfig))]
internal sealed partial class VolumeJsonContext : JsonSerializerContext;
