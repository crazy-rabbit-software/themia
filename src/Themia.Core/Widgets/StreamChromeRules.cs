using Themia.Core.Localization;

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

/// <summary>[S-1312]/[S-1313] The camera's chrome: which parts show at a size, and their words.</summary>
public static class StreamChromeRules
{
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
                ? Strings.Format(locale, Str.WidgetsC_VideoStream_Status_Every, ("seconds", VideoStreamRules.ClampRefreshSeconds(refreshSeconds)))
                : Strings.Get(locale, Str.WidgetsC_VideoStream_Status_Live);
        }

        return reconnects > 0
            ? Strings.Format(locale, Str.WidgetsC_VideoStream_Status_Reconnecting, ("count", reconnects))
            : Strings.Get(locale, Str.WidgetsC_VideoStream_Status_Connecting);
    }

    /// <summary>[S-1313] How old the picture on screen is: "now", "4 s ago", "2 min ago", "3 h ago".</summary>
    public static string FrameAge(TimeSpan age) => FrameAge(Strings.Current, age);

    /// <summary><see cref="FrameAge(TimeSpan)"/> in an explicit language.</summary>
    public static string FrameAge(Locale locale, TimeSpan age)
    {
        if (age < TimeSpan.FromSeconds(1))
        {
            return Strings.Get(locale, Str.WidgetsC_VideoStream_Age_Now);
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return Strings.Format(locale, Str.WidgetsC_VideoStream_Age_Seconds, ("value", (long)age.TotalSeconds));
        }

        return age < TimeSpan.FromHours(1)
            ? Strings.Format(locale, Str.WidgetsC_VideoStream_Age_Minutes, ("value", (long)age.TotalMinutes))
            : Strings.Format(locale, Str.WidgetsC_VideoStream_Age_Hours, ("value", (long)age.TotalHours));
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
        return Strings.Format(locale, Str.WidgetsC_VideoStream_Fps, ("value", value));
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
        StreamTransport.Image => Str.WidgetsC_VideoStream_Transport_Snapshot.Text(),
        StreamTransport.Video => Str.WidgetsC_VideoStream_Transport_Video.Text(),
        StreamTransport.Hls => "HLS",
        _ => string.Empty,
    };
}
