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
using Themia.Widget.MicrophoneToggle;

[assembly: ThemiaWidget("microphoneToggle", typeof(MicrophoneToggleModule))]

namespace Themia.Widget.MicrophoneToggle;

/// <summary>[S-9000] The Microphone quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class MicrophoneToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("microphoneToggle"),
        ConfigType = typeof(MicrophoneToggleConfig),
        CreateDefault = () => new MicrophoneToggleConfig(),
        Json = MicrophoneToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "mic", "mute", "unmute", "calls", "recording"],
        Label = MicrophoneToggleStr.Toggles_Microphone_Label.Local(),
        Description = MicrophoneToggleStr.Toggles_Microphone_Desc.Local(),
        Glyph = Glyphs.Microphone,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MicrophoneToggleConfig))]
internal sealed partial class MicrophoneToggleJsonContext : JsonSerializerContext;
