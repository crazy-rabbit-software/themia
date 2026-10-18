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
using Themia.Widget.MonoAudioToggle;

[assembly: ThemiaWidget("monoAudioToggle", typeof(MonoAudioToggleModule))]

namespace Themia.Widget.MonoAudioToggle;

/// <summary>[S-9200] The Mono audio quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class MonoAudioToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("monoAudioToggle"),
        ConfigType = typeof(MonoAudioToggleConfig),
        CreateDefault = () => new MonoAudioToggleConfig(),
        Json = MonoAudioToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "mono", "accessibility", "one ear", "audio"],
        Label = MonoAudioToggleStr.Toggles_MonoAudio_Label.Local(),
        Description = MonoAudioToggleStr.Toggles_MonoAudio_Desc.Local(),
        Glyph = Glyphs.MonoAudio,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 3),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MonoAudioToggleConfig))]
internal sealed partial class MonoAudioToggleJsonContext : JsonSerializerContext;
