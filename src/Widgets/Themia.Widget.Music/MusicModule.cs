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
using Themia.Widget.Music;

[assembly: ThemiaWidget("music", typeof(MusicModule))]

namespace Themia.Widget.Music;

/// <summary>[S-1475] What Windows is playing: the art, the title and artist, the transport and the seek bar, through Windows' own media session. SPEC.md beside this file.</summary>
public sealed class MusicModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("music"),
        ConfigType = typeof(MusicConfig),
        CreateDefault = () => new MusicConfig(),
        Json = MusicJsonContext.Default,
        Keywords = ["spotify", "player", "playback", "song", "track", "audio", "media controls", "now playing"],
        Label = MusicStr.WidgetsA_Catalog_Music_Label.Local(),
        Description = MusicStr.WidgetsA_Catalog_Music_Description.Local(),
        Glyph = Glyphs.Music,
        DefaultWidth = 340,
        DefaultHeight = 160,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Media, 0, 0, Popular: 7),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => MusicMenu.Lines(menu, (MusicConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new MusicView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MusicConfig))]
internal sealed partial class MusicJsonContext : JsonSerializerContext;
