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
using Themia.Widget.Mixer;

[assembly: ThemiaWidget("mixer", typeof(MixerModule))]

namespace Themia.Widget.Mixer;

/// <summary>[S-440] Per-app volume for an output device: a row per app, its icon, name, slider and mute. SPEC.md beside this file.</summary>
public sealed class MixerModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("mixer"),
        ConfigType = typeof(MixerConfig),
        CreateDefault = () => new MixerConfig(),
        Json = MixerJsonContext.Default,
        Keywords = ["sound", "audio", "per app volume", "speaker", "levels", "faders"],
        Label = MixerStr.WidgetsA_Catalog_Mixer_Label.Local(),
        Description = MixerStr.WidgetsA_Catalog_Mixer_Description.Local(),
        Glyph = Glyphs.Mixer,
        DefaultWidth = 300,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Media, 0, 3),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => MixerMenu.Lines(menu, (MixerConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new MixerView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MixerConfig))]
internal sealed partial class MixerJsonContext : JsonSerializerContext;
