using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6406] Now playing's styles: the same track and transport, three ways of setting them.</summary>
[JsonConverter(typeof(StyleJsonConverter<MusicStyle>))]
public enum MusicStyle
{
    /// <summary>The album art beside or above the lines ([S-1475]).</summary>
    Artwork = 0,

    /// <summary>[S-6407] The art fills the card edge to edge; the title and the transport sit over a scrim.</summary>
    Cover = 1,

    /// <summary>The lines and the transport alone, no art: what "Show album art" off was.</summary>
    Text = 2,
}

/// <summary>
/// [S-1464] When the now-playing tile paints nothing. Read leniently: an unknown name is
/// <see cref="Never"/> (the zero value), the visible state — a widget you can see is one you can fix.
/// </summary>
[JsonConverter(typeof(LenientEnumConverter<MusicHideWhenIdle>))]
public enum MusicHideWhenIdle
{
    /// <summary>Always drawn; idle shows the "nothing playing" sentence.</summary>
    Never = 0,

    /// <summary>Hidden only while Windows reports no media session at all.</summary>
    NoSession = 1,

    /// <summary>Hidden whenever nothing is playing, a paused session included.</summary>
    NotPlaying = 2,
}

/// <summary>
/// Whatever Windows is playing, from Windows' own media session.
/// <para>
/// There is nothing to configure about <i>where</i> it comes from, and that is the point: the
/// session is the same one the volume flyout and the media keys drive, so the widget works for
/// every player on the machine with no account, no token and no per-app setting.
/// </para>
/// </summary>
public sealed class MusicConfig : WidgetConfig, IStyled
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Music;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("music"),
        Kind = WidgetKind.Music,
        ConfigType = typeof(MusicConfig),
        CreateDefault = () => new MusicConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["spotify", "player", "playback", "song", "track", "audio", "media controls", "now playing"],
        LabelId = Str.WidgetsA_Catalog_Music_Label,
        DescriptionId = Str.WidgetsA_Catalog_Music_Description,
        Glyph = Glyphs.Music,
        DefaultWidth = 340,
        DefaultHeight = 160,
    };

    /// <summary>[S-6406] Artwork and text, full artwork or text only: the widget's style (what was "Show album art").</summary>
    public MusicStyle Style { get; set; } = MusicStyle.Artwork;

    private static readonly StyleOption[] MusicStyles =
    [
        StyleOption.Of(MusicStyle.Artwork, Str.Styles_Music_Artwork, 0xE93C),
        StyleOption.Of(MusicStyle.Cover, Str.Styles_Music_Cover, 0xE740),
        StyleOption.Of(MusicStyle.Text, Str.Styles_Music_Text, 0xE8D2),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => MusicStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (MusicStyle)value;
    }

    /// <summary>Whether the style draws the album art at all, and so whether it is fetched.</summary>
    [JsonIgnore]
    public bool ShowsArt => Style != MusicStyle.Text;

    /// <summary>Show the play/pause, next and previous buttons.</summary>
    public bool ShowTransport { get; set; } = true;

    /// <summary>Show the progress bar and the elapsed/total labels.</summary>
    public bool ShowProgress { get; set; } = true;

    /// <summary>[S-1464] Paint nothing while idle.</summary>
    public MusicHideWhenIdle HideWhenIdle { get; set; } = MusicHideWhenIdle.Never;

    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(HideWhenIdle))
        {
            return [];
        }

        HideWhenIdle = MusicHideWhenIdle.Never;
        return ["Music widget had an unknown hide-when-idle mode"];
    }

    public override WidgetConfig Clone() => new MusicConfig
    {
        Style = Style,
        ShowTransport = ShowTransport,
        ShowProgress = ShowProgress,
        HideWhenIdle = HideWhenIdle,
    };
}
