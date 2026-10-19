using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Music;

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
/// Whatever Windows is playing, from Windows' own media session.
/// <para>
/// There is nothing to configure about <i>where</i> it comes from, and that is the point: the
/// session is the same one the volume flyout and the media keys drive, so the widget works for
/// every player on the machine with no account, no token and no per-app setting.
/// </para>
/// </summary>
public sealed class MusicConfig : WidgetConfig, IStyled, IDrawsItems
{
    /// <summary>[S-6573] The seek bar's track is the one item, drawn only with Show progress on.</summary>
    bool IDrawsItems.DrawsItems => ShowProgress;

    /// <summary>[S-6574] A track: the item text never reaches it.</summary>
    bool IDrawsItems.DrawsItemText => false;

    /// <summary>[S-6406] Artwork and text, full artwork or text only: the widget's style (what was "Show album art").</summary>
    public MusicStyle Style { get; set; } = MusicStyle.Artwork;

    private static readonly StyleOption[] MusicStyles =
    [
        StyleOption.Of(MusicStyle.Artwork, MusicStr.Styles_Music_Artwork.Local(), 0xE93C),
        StyleOption.Of(MusicStyle.Cover, MusicStr.Styles_Music_Cover.Local(), 0xE740),
        StyleOption.Of(MusicStyle.Text, MusicStr.Styles_Music_Text.Local(), 0xE8D2),
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
