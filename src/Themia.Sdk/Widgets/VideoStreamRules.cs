using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-471] How a stream address is played, decided from the <c>Content-Type</c> it answers with
/// rather than from its spelling: <c>/?action=stream</c>, <c>/video.cgi</c> and <c>/stream</c> say
/// nothing about what comes back, and the header is the one thing every camera does send.
/// </summary>
public enum StreamTransport
{
    /// <summary>The reply is not something this widget can show.</summary>
    Unsupported = 0,

    /// <summary><c>multipart/x-mixed-replace</c>: a JPEG per part, for as long as the socket stays open.</summary>
    Mjpeg,

    /// <summary>One still image: a snapshot endpoint.</summary>
    Image,

    /// <summary>A video file or progressive stream Windows' own media stack can play.</summary>
    Video,

    /// <summary>An HLS playlist, played by the adaptive media source.</summary>
    Hls,
}

/// <summary>
/// The pure rules behind the video stream widget: titles, rotation, the reconnect backoff, the
/// refresh clamp and what kind of stream an address turned out to be.
/// </summary>
public static class VideoStreamRules
{
    /// <summary>The <c>RefreshSeconds</c> value that means "hold the stream open".</summary>
    public const int LiveRefreshSeconds = 0;

    public const int MaxRefreshSeconds = 300;

    /// <summary>[S-464] First reconnect delay, doubled per failure up to <see cref="RetryMax"/>.</summary>
    public static readonly TimeSpan RetryBase = TimeSpan.FromSeconds(2);

    /// <summary>However long a camera stays down, it is tried again at least this often.</summary>
    public static readonly TimeSpan RetryMax = TimeSpan.FromSeconds(30);

    /// <summary>[S-470] A refresh outside 0..300 seconds is clamped; it never stalls the grab loop.</summary>
    public static int ClampRefreshSeconds(int seconds) => Math.Clamp(seconds, LiveRefreshSeconds, MaxRefreshSeconds);

    /// <summary>
    /// [S-464] The wait before reconnect attempt <paramref name="attempt"/> (1-based): 2s, 4s, 8s,
    /// capped at 30s. A nonsense attempt count gets the base delay rather than an exception.
    /// </summary>
    public static TimeSpan RetryDelay(int attempt)
    {
        if (attempt < 1)
        {
            return RetryBase;
        }

        // 2^4 * 2s already passes the cap, so the shift is bounded long before it could overflow.
        var steps = Math.Min(attempt - 1, 10);
        var delay = TimeSpan.FromTicks(RetryBase.Ticks << steps);
        return delay > RetryMax ? RetryMax : delay;
    }

    /// <summary>
    /// [S-460] What the header calls a stream when the user has not named it: the camera's
    /// <c>host:port</c>, which is how a LAN camera is actually recognised. Empty when the address is
    /// unusable, so the card falls back to the widget's own name.
    /// </summary>
    public static string Title(string? url)
    {
        var resolved = WebInput.Url(url);

        if (!resolved.IsValid || !Uri.TryCreate(resolved.Url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        // Authority, not Host: it keeps a non-default port, and two cameras on one box differ by
        // nothing else.
        return uri.Authority;
    }

    /// <summary>[S-468] The next rotation in the 0 → 90 → 180 → 270 → 0 cycle.</summary>
    public static StreamRotation NextRotation(StreamRotation current) => current switch
    {
        StreamRotation.None => StreamRotation.Quarter,
        StreamRotation.Quarter => StreamRotation.Half,
        StreamRotation.Half => StreamRotation.ThreeQuarter,
        _ => StreamRotation.None,
    };

    /// <summary>Whether a rotation turns the picture on its side, so it must be fitted against the opposite axes.</summary>
    public static bool IsSideways(StreamRotation rotation)
        => rotation is StreamRotation.Quarter or StreamRotation.ThreeQuarter;

    /// <summary>
    /// [S-471] Classify a reply by its media type (the part of <c>Content-Type</c> before any
    /// parameters). Case-insensitive, because servers disagree about case and the type is not
    /// case-sensitive.
    /// </summary>
    public static StreamTransport Classify(string? mediaType)
    {
        var type = mediaType?.Trim().ToLowerInvariant() ?? string.Empty;

        if (type.Length == 0)
        {
            return StreamTransport.Unsupported;
        }

        // x-mixed-replace is the MJPEG type; "multipart/mixed" is what a few older cameras send for
        // the same thing, and both are handled by the same part reader.
        if (type is "multipart/x-mixed-replace" or "multipart/mixed")
        {
            return StreamTransport.Mjpeg;
        }

        if (type is "application/vnd.apple.mpegurl" or "application/x-mpegurl" or "audio/mpegurl" or "audio/x-mpegurl")
        {
            return StreamTransport.Hls;
        }

        if (type.StartsWith("image/", StringComparison.Ordinal))
        {
            // An SVG is a document, not a picture, and the decoder used here does not read it.
            return type == "image/svg+xml" ? StreamTransport.Unsupported : StreamTransport.Image;
        }

        if (type.StartsWith("video/", StringComparison.Ordinal))
        {
            return StreamTransport.Video;
        }

        return StreamTransport.Unsupported;
    }

    /// <summary>
    /// The multipart boundary from a <c>Content-Type</c> header value, or null when it names none.
    /// <para>
    /// Tolerant on purpose, because this is where cameras disagree most: a quoted value, a value
    /// that already carries the <c>--</c> delimiter prefix (which the RFC says it should not, and
    /// mjpg-streamer-era firmware routinely does), other parameters before or after it.
    /// </para>
    /// </summary>
    public static string? Boundary(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return null;
        }

        foreach (var raw in contentType.Split(';'))
        {
            var part = raw.Trim();
            var eq = part.IndexOf('=', StringComparison.Ordinal);

            if (eq <= 0 || !part[..eq].Trim().Equals("boundary", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = part[(eq + 1)..].Trim();

            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                value = value[2..];
            }

            // RFC 2046 caps a boundary at 70 characters. Anything past that, or with a line break
            // in it, is not a delimiter this reader should go looking for.
            if (value.Length is 0 or > 70 || value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            {
                return null;
            }

            return value;
        }

        return null;
    }
}
