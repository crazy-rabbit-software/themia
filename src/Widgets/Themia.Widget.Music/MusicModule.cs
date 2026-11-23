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
using Themia.App.Services;
using Themia.Sdk.Kit;

[assembly: ThemiaWidget("music", typeof(MusicModule))]

namespace Themia.Widget.Music;

/// <summary>
/// [S-16500] Now playing: what Windows is playing, through Windows' own media session, as one widget in a few styles, every
/// element of its face shown or hidden on its own. SPEC.md beside this file.
/// </summary>
public sealed class MusicModule : IWidgetModule
{
    /// <summary>[S-2030] A click on the card opens the app that owns the media session (its id as Windows reported it, [S-2003]); nothing with no session.</summary>
    internal static CardOpen? Opens(NowPlaying now) => now.State is MusicState.NoSession or MusicState.Loading ? null : CardOpen.App(now.AppId);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("music"),
        ConfigType = typeof(MusicConfig),
        CreateDefault = () => new MusicConfig(),
        Json = MusicJsonContext.Default,
        Keywords = ["spotify", "player", "playback", "song", "track", "audio", "media controls", "now playing", "album art", "cover",
            "artwork", "progress", "seek", "elapsed"],
        Label = MusicStr.WidgetsA_Catalog_Music_Label.Local(),
        Description = MusicStr.WidgetsA_Catalog_Music_Description.Local(),
        Glyph = Glyphs.Music,
        Sizing = c => MusicPlan.Sizing(((MusicConfig)c).Style, ((MusicConfig)c).Shows),

        // [S-16509] Its face is one item that lights where a click opens the player.
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.Media, 0, 0, Popular: 7),

        // [S-16230] [S-16513] The parts it was split into fold back into it where they stand, with what each showed.
        Merges = [.. Parts.Select(part => new MergeFrom(part.Kind) { Fold = MergeFold.Touching, Shows = (ShowsKey, Names(part.Shows)) })],
    };

    /// <summary>[S-16513] The config's key of the set it shows (<see cref="MusicConfig.Shows"/>), as the fold writes it.</summary>
    internal const string ShowsKey = "shows";

    /// <summary>
    /// [S-16513] Its own card from before the set (what the track showed: the texts and the transport) and the removed kinds
    /// whose placed cards fold into it: the cover and the progress (2026-11-12..23) and the parts of 21-22 Nov.
    /// </summary>
    internal static readonly (string Kind, MusicElement[] Shows)[] Parts =
    [
        ("music", [MusicElement.Title, MusicElement.Artist, MusicElement.Album, MusicElement.Previous, MusicElement.PlayPause, MusicElement.Next]),
        ("musicCover", [MusicElement.Cover]),
        ("musicProgress", [MusicElement.Elapsed, MusicElement.Bar, MusicElement.Length]),
        ("musicArtist", [MusicElement.Artist]),
        ("musicAlbum", [MusicElement.Album]),
        ("musicPrevious", [MusicElement.Previous]),
        ("musicPlayPause", [MusicElement.PlayPause]),
        ("musicNext", [MusicElement.Next]),
        ("musicElapsed", [MusicElement.Elapsed]),
        ("musicDuration", [MusicElement.Length]),
    ];

    /// <summary>The elements' names on the wire (camelCase), as the config's set writes them.</summary>
    private static string[] Names(IEnumerable<MusicElement> elements)
        => [.. elements.Select(e => e.ToString()).Select(n => char.ToLowerInvariant(n[0]) + n[1..])];

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
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
