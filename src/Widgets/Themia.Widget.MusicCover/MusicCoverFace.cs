using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.MusicCover;

/// <summary>[S-11320] [S-15102] The cover atom's box: a picture atom, as large or small as its card.</summary>
public static class MusicCoverFace
{
    /// <summary>[S-11320] A new cover's square side.</summary>
    public const double Side = 160;

    /// <summary>
    /// [S-11320] The cover's sizing ([S-2438]): a picture atom's (<see cref="AtomRules.PictureSizing"/>), a new one a
    /// square of <see cref="Side"/>, the picture in the universal spacing; the card's size is the user's (no Size setting,
    /// [S-2449] retired): down to the grid's least widget (WR-14) and up as far as the work area goes ([S-12411]).
    /// </summary>
    public static WidgetSizing Sizing(MusicCoverConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return AtomRules.PictureSizing(Side);
    }

    /// <summary>
    /// [S-11321] What the cover's picture was read for: the track and the reading of the session's media properties
    /// (<see cref="NowPlaying.Media"/>). Another track, or the same track's properties changed (a browser hands over a
    /// video's picture after its title, and may still hold the last video's then), is another key: the picture is read
    /// again. Nothing playing: no key.
    /// </summary>
    public static string ArtKey(NowPlaying now)
        => now.HasTrack ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{now.Media}\n{now.TrackKey}") : string.Empty;
}
