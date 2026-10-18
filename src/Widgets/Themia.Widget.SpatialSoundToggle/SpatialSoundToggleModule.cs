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
using Themia.Widget.SpatialSoundToggle;

[assembly: ThemiaWidget("spatialSoundToggle", typeof(SpatialSoundToggleModule))]

namespace Themia.Widget.SpatialSoundToggle;

/// <summary>[S-9100] The Spatial sound quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class SpatialSoundToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("spatialSoundToggle"),
        ConfigType = typeof(SpatialSoundToggleConfig),
        CreateDefault = () => new SpatialSoundToggleConfig(),
        Json = SpatialSoundToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "windows sonic", "surround", "3d audio", "headphones"],
        Label = SpatialSoundToggleStr.Toggles_SpatialSound_Label.Local(),
        Description = SpatialSoundToggleStr.Toggles_SpatialSound_Desc.Local(),
        Glyph = Glyphs.SpatialSound,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SpatialSoundToggleConfig))]
internal sealed partial class SpatialSoundToggleJsonContext : JsonSerializerContext;
