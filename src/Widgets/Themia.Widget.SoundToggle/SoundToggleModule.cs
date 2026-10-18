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
using Themia.Widget.SoundToggle;

[assembly: ThemiaWidget("soundToggle", typeof(SoundToggleModule))]

namespace Themia.Widget.SoundToggle;

/// <summary>[S-8900] The Sound quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class SoundToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("soundToggle"),
        ConfigType = typeof(SoundToggleConfig),
        CreateDefault = () => new SoundToggleConfig(),
        Json = SoundToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "mute", "unmute", "speaker", "audio", "volume"],
        Label = SoundToggleStr.Toggles_Sound_Label.Local(),
        Description = SoundToggleStr.Toggles_Sound_Desc.Local(),
        Glyph = Glyphs.Volume3,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 0),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SoundToggleConfig))]
internal sealed partial class SoundToggleJsonContext : JsonSerializerContext;
