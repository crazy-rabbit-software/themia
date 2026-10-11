using System.Text.Json.Serialization;
using Themia.Core.Office;

namespace Themia.Core.Model;

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
public sealed class MusicConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Music;

    /// <summary>Show the album art the session hands over, when it hands one over.</summary>
    public bool ShowAlbumArt { get; set; } = true;

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
        ShowAlbumArt = ShowAlbumArt,
        ShowTransport = ShowTransport,
        ShowProgress = ShowProgress,
        HideWhenIdle = HideWhenIdle,
    };
}
