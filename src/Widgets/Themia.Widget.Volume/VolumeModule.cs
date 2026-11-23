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
using Themia.Sdk.Kit;

[assembly: ThemiaWidget("volume", typeof(VolumeModule))]

namespace Themia.Widget.Volume;

/// <summary>[S-2730] [S-17225] An output device's volume: one row of the speaker (its mute), the level and the slider, Brightness's row. SPEC.md beside this file.</summary>
public sealed class VolumeModule : IWidgetModule
{
    /// <summary>[S-2025] What a click on the card opens ([S-2000]).</summary>
    internal static CardOpen? Opens => CardOpen.Settings("sound");

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
        Sizing = _ => LevelRow.Sizing(),
        Traits = WidgetTraits.None,
        Add = new(AddFolderId.Media, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => VolumeMenu.Lines(menu, (VolumeConfig)config, AudioDeviceLine.Outputs(menu));

    public FrameworkElement CreateView(IWidgetHost host) => new VolumeView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(VolumeConfig))]
internal sealed partial class VolumeJsonContext : JsonSerializerContext;
