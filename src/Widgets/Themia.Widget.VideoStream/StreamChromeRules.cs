using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Sdk.Kit;
using Themia.Widget.VideoStream;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1312] What the camera shows besides the picture at one size. The picture is the widget, so
/// every part is optional and none of it appears on the small tiles at all.
/// </summary>
/// <param name="HoverStrip">On pointer-over: a bottom strip with the live dot and the frame's age.</param>
/// <param name="HoverDetails">On pointer-over: Pause/Resume, Rotate and Refresh, the resolution and the frame rate.</param>
/// <param name="StatusChip">Always: a chip with the stream's state and transport.</param>
public readonly record struct StreamChrome(bool HoverStrip, bool HoverDetails, bool StatusChip);

/// <summary>
/// [S-1313] Frames per second over the last few seconds of arrivals, for the hover strip. Fed with
/// monotonic timestamps by the caller; never reads a clock itself.
/// </summary>
public sealed class FrameRateMeter
{
    /// <summary>How far back the rate looks: long enough to be steady, short enough to follow a camera that slows down.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    private const int MaxSamples = 256;

    private readonly Queue<TimeSpan> _arrivals = new();

    /// <summary>Frames counted since the last <see cref="Reset"/>.</summary>
    public long Count { get; private set; }

    /// <summary>When the newest frame arrived, or null before the first.</summary>
    public TimeSpan? Last { get; private set; }

    public void Add(TimeSpan at)
    {
        // A timestamp that runs backwards is a caller bug, not a frame; dropping it keeps the rate finite.
        if (Last is { } last && at < last)
        {
            return;
        }

        Count++;
        Last = at;
        _arrivals.Enqueue(at);

        while (_arrivals.Count > MaxSamples || (_arrivals.Count > 2 && at - _arrivals.Peek() > Window))
        {
            _arrivals.Dequeue();
        }
    }

    /// <summary>Frames per second, or null until two frames span a measurable time.</summary>
    public double? Rate
    {
        get
        {
            if (_arrivals.Count < 2 || Last is not { } last)
            {
                return null;
            }

            var span = (last - _arrivals.Peek()).TotalSeconds;
            return span > 0 ? (_arrivals.Count - 1) / span : null;
        }
    }

    public void Reset()
    {
        _arrivals.Clear();
        Count = 0;
        Last = null;
    }
}

/// <summary>[S-1314] The one action a stream's Status row offers.</summary>
public enum StreamAction
{
    /// <summary>
    /// Nothing to offer here: connecting, an address only an edit mends, or a paused stream (Resume is
    /// the card menu's, Space's and the strip's; a worded button does not fit the small tiles).
    /// </summary>
    None,

    /// <summary>A stream that dropped: reconnect now, the backoff reset.</summary>
    Retry,
}

/// <summary>[S-1314] How the Status row says a stream with no picture: plain or a problem, and its one action.</summary>
public readonly record struct StreamLook(StatusKind Kind, StreamAction Action);

/// <summary>[S-1312]/[S-1313] The camera's chrome: which parts show at a size, and their words.</summary>
public static class StreamChromeRules
{
    /// <summary>
    /// [S-1314] A stream with no picture to draw, as the Status row at the top says it: an address that
    /// is refused or a stream this widget cannot show is a problem with no action (only the menu mends
    /// it); a dropped stream a problem with Retry; a paused one and connecting plain. Null
    /// while the picture is on screen, and for no address at all (the card's Set up row, [S-1862]).
    /// </summary>
    public static StreamLook? Look(bool hasAddress, bool addressValid, bool paused, StreamPhase phase)
    {
        if (!hasAddress)
        {
            return null;
        }

        if (!addressValid)
        {
            return new StreamLook(StatusKind.Problem, StreamAction.None);
        }

        if (paused)
        {
            return new StreamLook(StatusKind.Empty, StreamAction.None);
        }

        return phase switch
        {
            StreamPhase.Live => null,
            StreamPhase.Connecting => new StreamLook(StatusKind.Empty, StreamAction.None),
            StreamPhase.Unsupported => new StreamLook(StatusKind.Problem, StreamAction.None),
            _ => new StreamLook(StatusKind.Problem, StreamAction.Retry),
        };
    }

    /// <summary>
    /// [S-1314] The state dot's role on the strip and the chip: the accent while live (on, [S-6706]),
    /// caution while it waits for a frame, tertiary while paused. Never a success green, never critical
    /// for a stream that is working.
    /// </summary>
    public static ColourRole DotRole(bool live, bool paused) => live ? ColourRole.Accent : paused ? ColourRole.TextTertiary : ColourRole.Caution;

    /// <summary>
    /// [S-1312] The ladder. Nothing on XS and S (or any strip under Regular height: a Wide strip is
    /// all picture). M and Tall: the hover strip. L: plus controls and details. XL: plus the status
    /// chip, when the user turned it on. Hover parts only when the user left hover controls on.
    /// </summary>
    public static StreamChrome Chrome(WidgetArea area, bool hoverControls, bool showStatus)
    {
        if (!area.IsMeasured)
        {
            return default;
        }

        var strip = hoverControls && area.AtLeast(SizeClass.Regular, SizeClass.Regular);
        var details = strip && area.AtLeast(SizeClass.Expanded, SizeClass.Expanded);
        var chip = showStatus && area.AtLeast(SizeClass.Huge, SizeClass.Huge);
        return new StreamChrome(strip, details, chip);
    }

    /// <summary>
    /// [S-1313] The status words: "LIVE" for an open stream, "every 10 s" for an interval grab,
    /// "reconnecting 3" after the third failure in a row, "connecting" before the first frame.
    /// </summary>
    public static string Status(bool live, int reconnects, int refreshSeconds) => Status(Strings.Current, live, reconnects, refreshSeconds);

    /// <summary><see cref="Status(bool, int, int)"/> in an explicit language.</summary>
    public static string Status(Locale locale, bool live, int reconnects, int refreshSeconds)
    {
        if (live)
        {
            return refreshSeconds > VideoStreamRules.LiveRefreshSeconds
                ? VideoStreamStr.WidgetsC_VideoStream_Status_Every.Format(locale, ("seconds", VideoStreamRules.ClampRefreshSeconds(refreshSeconds)))
                : VideoStreamStr.WidgetsC_VideoStream_Status_Live.Text(locale);
        }

        return reconnects > 0
            ? VideoStreamStr.WidgetsC_VideoStream_Status_Reconnecting.Format(locale, ("count", reconnects))
            : VideoStreamStr.WidgetsC_VideoStream_Status_Connecting.Text(locale);
    }

    /// <summary>[S-1313] How old the picture on screen is: "now", "4 s ago", "2 min ago", "3 h ago".</summary>
    public static string FrameAge(TimeSpan age) => FrameAge(Strings.Current, age);

    /// <summary><see cref="FrameAge(TimeSpan)"/> in an explicit language.</summary>
    public static string FrameAge(Locale locale, TimeSpan age)
    {
        if (age < TimeSpan.FromSeconds(1))
        {
            return VideoStreamStr.WidgetsC_VideoStream_Age_Now.Text(locale);
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return VideoStreamStr.WidgetsC_VideoStream_Age_Seconds.Format(locale, ("value", (long)age.TotalSeconds));
        }

        return age < TimeSpan.FromHours(1)
            ? VideoStreamStr.WidgetsC_VideoStream_Age_Minutes.Format(locale, ("value", (long)age.TotalMinutes))
            : VideoStreamStr.WidgetsC_VideoStream_Age_Hours.Format(locale, ("value", (long)age.TotalHours));
    }

    /// <summary>[S-1313] "12 fps", "2.5 fps"; empty when there is no rate (interval mode, one snapshot, a video).</summary>
    public static string Rate(double? framesPerSecond) => Rate(Strings.Current, framesPerSecond);

    /// <summary><see cref="Rate(double?)"/> in an explicit language, with its decimal mark.</summary>
    public static string Rate(Locale locale, double? framesPerSecond)
    {
        if (framesPerSecond is not { } fps || !double.IsFinite(fps) || fps <= 0)
        {
            return string.Empty;
        }

        // Whole from ten; under it one decimal, dropped when it is a zero ("2 fps", not "2.0 fps").
        var tenths = Math.Round(fps, 1, MidpointRounding.AwayFromZero);
        var value = fps >= 10
            ? LocaleFormat.Number(locale, Math.Round(fps, MidpointRounding.AwayFromZero), 0)
            : LocaleFormat.Number(locale, tenths, tenths == Math.Floor(tenths) ? 0 : 1);
        return VideoStreamStr.WidgetsC_VideoStream_Fps.Format(locale, ("value", value));
    }

    /// <summary>[S-1313] "1280×720" for the camera's own size; empty when unknown.</summary>
    public static string Resolution(int width, int height)
        => width > 0 && height > 0
            ? $"{width.ToString(System.Globalization.CultureInfo.InvariantCulture)}×{height.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : string.Empty;

    /// <summary>[S-1313] The transport as the chip names it.</summary>
    public static string Transport(StreamTransport transport) => transport switch
    {
        StreamTransport.Mjpeg => "MJPEG",
        StreamTransport.Image => VideoStreamStr.WidgetsC_VideoStream_Transport_Snapshot.Text(),
        StreamTransport.Video => VideoStreamStr.WidgetsC_VideoStream_Transport_Video.Text(),
        StreamTransport.Hls => "HLS",
        _ => string.Empty,
    };
}

/// <summary>
/// [S-1313] The frame age's one-second tick, held only while it is wanted. The shared ticker fires a
/// new subscription at once, inside Subscribe, so the callback (which asks again whether to tick) runs
/// before the subscription is handed back: the lease marks itself held before subscribing, so that
/// first tick does not subscribe again (it did, until the stack overflowed), and a lease let go
/// during that first tick drops the subscription as soon as it arrives.
/// </summary>
public sealed class TickLease : IDisposable
{
    private IDisposable? _subscription;
    private bool _held;

    /// <summary>Whether the tick is held now.</summary>
    public bool Held => _held;

    /// <summary>Hold the tick (<paramref name="subscribe"/> is called once) or let it go.</summary>
    public void Want(bool wanted, Func<IDisposable> subscribe)
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        if (wanted == _held)
        {
            return;
        }

        _held = wanted;

        if (!wanted)
        {
            _subscription?.Dispose();
            _subscription = null;
            return;
        }

        var subscription = subscribe();

        if (_held && _subscription is null)
        {
            _subscription = subscription;
        }
        else
        {
            // Let go (or held again by a newer subscribe) while this one was being made.
            subscription.Dispose();
        }
    }

    public void Dispose() => Want(false, static () => throw new InvalidOperationException("not subscribing"));
}
