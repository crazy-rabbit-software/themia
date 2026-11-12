using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.MusicCover;

[assembly: ThemiaWidget("musicCover", typeof(MusicCoverModule))]

namespace Themia.Widget.MusicCover;

/// <summary>[S-11320] The cover of what Windows is playing, alone: a square by its Size, edge to edge. SPEC.md beside this file.</summary>
public sealed class MusicCoverModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+E93C, a picture), the module's own.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE93C);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("musicCover"),
        ConfigType = typeof(MusicCoverConfig),
        CreateDefault = () => new MusicCoverConfig(),
        Json = MusicCoverJsonContext.Default,
        Keywords = ["album art", "cover", "artwork", "music", "now playing", "song", "spotify"],
        Label = MusicCoverStr.MusicCover_Label.Local(),
        Description = MusicCoverStr.MusicCover_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => MusicCoverFace.Sizing((MusicCoverConfig)c),
        Traits = WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Media, 0, 4),
    };

    /// <summary>[S-11320] Its options: its Size, then when it hides ([S-1464]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var cover = (MusicCoverConfig)config;
        menu.Lines.Add(KitMenuLines.ReadoutSize(menu, cover));
        menu.Lines.Add(menu.Choice(Glyphs.Hide, MusicCoverStr.Music_HideWhenIdle_Label.Text(),
            [
                (MusicHideWhenIdle.Never, MusicCoverStr.Music_HideWhenIdle_Never.Text()),
                (MusicHideWhenIdle.NoSession, MusicCoverStr.WidgetsB_Settings_Music_HideWhenIdle_NoSession.Text()),
                (MusicHideWhenIdle.NotPlaying, MusicCoverStr.WidgetsB_Settings_Music_HideWhenIdle_NotPlaying.Text()),
            ],
            cover.HideWhenIdle, value => cover.HideWhenIdle = value));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MusicCoverView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MusicCoverConfig))]
internal sealed partial class MusicCoverJsonContext : JsonSerializerContext;
