using System.Buffers;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Themia.App.Services.Net;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Themia.App.Services.Web;

/// <summary>Where a stream connection stands, as the widget shows it.</summary>
internal enum StreamPhase
{
    /// <summary>A connection is open and no frame has decoded yet.</summary>
    Connecting,

    /// <summary>At least one frame is on screen.</summary>
    Live,

    /// <summary>The last attempt failed and a retry is armed.</summary>
    Retrying,

    /// <summary>The address answered with something this widget cannot show. Not retried.</summary>
    Unsupported,
}

/// <summary>The size the picture will be drawn at, so frames are decoded no larger than that.</summary>
/// <param name="Width">Physical pixels across the frame area.</param>
/// <param name="Height">Physical pixels down the frame area.</param>
internal readonly record struct DecodeTarget(int Width, int Height, StreamFit Fit, bool Sideways);

/// <summary>
/// [S-1313] What came with a decoded frame: the camera's own size (before the decode scaled it to
/// the tile) and how it arrived, for the camera's status chip and hover strip.
/// </summary>
internal readonly record struct FrameFacts(int SourceWidth, int SourceHeight, StreamTransport Transport);

/// <summary>What a session reports. Always called on the UI thread.</summary>
internal interface IStreamSink
{
    void OnPhase(StreamPhase phase, string? detail);

    /// <summary>A decoded frame. The sink owns it and must dispose it.</summary>
    void OnFrame(SoftwareBitmap frame, FrameFacts facts);

    /// <summary>The address is a video Windows can play itself; the sink takes it from here.</summary>
    void OnVideo(Uri uri, StreamTransport transport);
}

/// <summary>
/// One camera connection: open it, work out what it is, and keep pictures coming until disposed.
/// <para>
/// [S-471] The reply's content type decides the transport. MJPEG is parsed here and each JPEG is
/// decoded by WIC straight to the size it is drawn at; a single image is a snapshot; a real video
/// (MP4, HLS) is handed to the sink for Windows' own media player. No browser engine is involved in
/// any of them.
/// </para>
/// <para>
/// [S-470] With a refresh interval the session grabs one frame, <b>hangs up</b>, waits, and grabs
/// again — the only honest throttle, because a camera pushes frames at its own rate and offers no
/// way to ask for fewer. Disposing the session cancels the socket on every path: there is no
/// element to leak, which is the whole of the original's [S-18].
/// </para>
/// <para>
/// [S-464] A failure arms a retry after <see cref="VideoStreamRules.RetryDelay"/>. The delay is
/// armed by the failure and cancelled with the session; it never sequences anything else.
/// </para>
/// </summary>
internal sealed class StreamSession : IDisposable
{
    private const string Scope = "video-stream";

    /// <summary>Headers must arrive in this long or the camera is treated as unreachable.</summary>
    private static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A stream that sends nothing for this long has stalled without closing. A socket deadline, not
    /// a frame-rate watchdog: a camera at one frame a minute still sends its frames well inside it.
    /// </summary>
    private static readonly TimeSpan ReadIdleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A snapshot larger than this is not a snapshot.</summary>
    private const int MaxImageBytes = MjpegFrameReader.DefaultMaxFrameBytes;

    /// <summary>
    /// Its own clients, not <see cref="WebFetch"/>'s: those have a whole-request deadline, and a live
    /// stream is a request that is meant never to finish. [S-3840] Guarded like every other request:
    /// a camera on the local network is reached through the address typed for it, and nothing it
    /// redirects to elsewhere on the network is.
    /// </summary>
    private static readonly GuardedClients Clients = GuardedHttp.CreatePair(TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan, decompress: false);

    private readonly Uri _uri;
    private readonly int _refreshSeconds;
    private readonly Func<DecodeTarget> _target;
    private readonly IStreamSink _sink;
    private readonly DispatcherQueue _ui;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>1 while a live frame is being decoded; newer frames are dropped rather than queued.</summary>
    private int _decoding;
    private volatile bool _gotFrame;
    private int _undecodable;
    private volatile bool _disposed;

    public StreamSession(Uri uri, int refreshSeconds, Func<DecodeTarget> target, IStreamSink sink, DispatcherQueue ui)
    {
        _uri = uri;
        _refreshSeconds = refreshSeconds;
        _target = target;
        _sink = sink;
        _ui = ui;
    }

    private bool Interval => _refreshSeconds > 0;

    public void Start() => _ = RunAsync(_cts.Token);

    /// <summary>
    /// [S-470] Wait out the interval, or the retry. A cadence armed by the last grab or failure,
    /// cancelled with the session; never a guess about when something will be ready.
    /// </summary>
    private static Task WaitAsync(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token);

    private async Task RunAsync(CancellationToken token)
    {
        var attempt = 0;
        Post(() => _sink.OnPhase(StreamPhase.Connecting, null));

        try
        {
            while (!token.IsCancellationRequested)
            {
                _gotFrame = false;
                var (outcome, detail) = await AttemptAsync(token).ConfigureAwait(false);

                if (_gotFrame)
                {
                    attempt = 0;
                }

                switch (outcome)
                {
                    case Outcome.Grabbed:
                        await WaitAsync(TimeSpan.FromSeconds(_refreshSeconds), token).ConfigureAwait(false);
                        break;

                    case Outcome.Done:
                        return;

                    case Outcome.Unsupported:
                        Log.Warn(Scope, $"{_uri.Authority}: {detail}");
                        Post(() => _sink.OnPhase(StreamPhase.Unsupported, detail));
                        return;

                    default:
                        attempt++;

                        // Logged on the first failure of a run only: a camera that is off for the
                        // night would otherwise write a line every thirty seconds until morning.
                        if (attempt == 1)
                        {
                            Log.Warn(Scope, $"{_uri.Authority}: {detail}; retrying");
                        }

                        Post(() => _sink.OnPhase(StreamPhase.Retrying, detail));
                        await WaitAsync(VideoStreamRules.RetryDelay(attempt), token).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // ignore: the session was disposed — paused, hidden, re-pointed or torn down.
        }
        catch (Exception ex)
        {
            // Nothing should reach here; if something does, the widget says so instead of the
            // process going down on an unobserved task.
            Log.Error(Scope, "the stream loop failed", ex);
            Post(() => _sink.OnPhase(StreamPhase.Retrying, Str.Shell_Stream_Unexpected.Text()));
        }
    }

    private enum Outcome
    {
        /// <summary>Interval mode: one frame is on screen and the connection is closed.</summary>
        Grabbed,

        /// <summary>Nothing more to do: a still image in live mode, or a video handed to the player.</summary>
        Done,

        /// <summary>Connection failed, dropped or ended. Retried after the backoff.</summary>
        Failed,

        /// <summary>The address answered with something that is not a picture. Not retried.</summary>
        Unsupported,
    }

    private async Task<(Outcome Outcome, string? Detail)> AttemptAsync(CancellationToken token)
    {
        // [S-3840] The address the user typed may be on their network; a redirect elsewhere may not.
        var grant = Themia.Core.Net.LocalGrants.ForTyped(_uri);
        var current = _uri;
        HttpResponseMessage response;

        for (var hop = 0; ; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);

            // An intermediate cache serving yesterday's snapshot would freeze an interval-mode picture.
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            try
            {
                using var headers = CancellationTokenSource.CreateLinkedTokenSource(token);
                headers.CancelAfter(HeaderTimeout);
                response = await Clients.SendAsync(request, grant, HttpCompletionOption.ResponseHeadersRead, headers.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return (Outcome.Failed, Str.Shell_Stream_NoAnswer.Text());
            }
            catch (HttpRequestException ex) when (NetRefusedException.Find(ex) is { } refused)
            {
                // The guard's answer will not change on a timer: said, not retried.
                return (Outcome.Unsupported, refused.Message);
            }
            catch (HttpRequestException ex)
            {
                return (Outcome.Failed, ex.InnerException is System.Net.Sockets.SocketException socket ? Str.Shell_Stream_UnreachableCode.Format(("code", socket.SocketErrorCode.ToString())) : Str.Shell_Stream_Unreachable.Text());
            }

            // [S-966] Followed here, hop by hop, so every hop meets the guard.
            if (!Themia.Core.Accounts.RedirectRules.IsRedirect((int)response.StatusCode))
            {
                break;
            }

            var location = response.Headers.Location;
            var line = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
            response.Dispose();

            if (hop >= Themia.Core.Accounts.RedirectRules.MaxHops || location is null)
            {
                return (Outcome.Unsupported, Str.Shell_Stream_Refused.Format(("status", line)));
            }

            var next = location.IsAbsoluteUri ? location : new Uri(current, location);
            if (!Themia.Core.Accounts.RedirectRules.MayFollow(current, next))
            {
                return (Outcome.Unsupported, Str.Shell_Net_HostRefused.Format(("host", next.IdnHost)));
            }

            current = next;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var line = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();

                // A wrong path or a login page will not fix itself on a timer.
                return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? (Outcome.Unsupported, Str.Shell_Stream_Refused.Format(("status", line)))
                    : (Outcome.Failed, Str.Shell_Stream_Answered.Format(("status", line)));
            }

            var contentType = response.Content.Headers.ContentType;
            var transport = VideoStreamRules.Classify(contentType?.MediaType);

            try
            {
                switch (transport)
                {
                    case StreamTransport.Mjpeg:
                        return await ReadMjpegAsync(response, contentType?.ToString(), token).ConfigureAwait(false);

                    case StreamTransport.Image:
                        return await ReadImageAsync(response, token).ConfigureAwait(false);

                    case StreamTransport.Video:
                    case StreamTransport.Hls:
                        // The player opens its own connection; this one only had to say what it was.
                        Post(() => _sink.OnVideo(_uri, transport));
                        return (Outcome.Done, null);

                    default:
                        return (Outcome.Unsupported, Str.Shell_Stream_NotStream.Format(("type", contentType?.MediaType ?? "?")));
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return (Outcome.Failed, Str.Shell_Stream_Stalled.Text());
            }
            catch (IOException)
            {
                return (Outcome.Failed, Str.Shell_Stream_Dropped.Text());
            }
            catch (HttpRequestException)
            {
                return (Outcome.Failed, Str.Shell_Stream_Dropped.Text());
            }
        }
    }

    private async Task<(Outcome, string?)> ReadMjpegAsync(HttpResponseMessage response, string? contentType, CancellationToken token)
    {
        var reader = new MjpegFrameReader(VideoStreamRules.Boundary(contentType));
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
        var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);

        try
        {
            while (true)
            {
                idle.CancelAfter(ReadIdleTimeout);
                var read = await stream.ReadAsync(buffer.AsMemory(), idle.Token).ConfigureAwait(false);

                if (read == 0)
                {
                    // The camera closed a stream that is meant to run forever: a reboot, a restart
                    // of its server, a cap on stream length. Reconnect.
                    return (Outcome.Failed, Str.Shell_Stream_Closed.Text());
                }

                reader.Append(buffer.AsSpan(0, read));

                while (reader.TryReadFrame(out var frame))
                {
                    if (Interval)
                    {
                        // One frame, shown, then hang up: leaving this method disposes the
                        // response, which is what closes the socket.
                        if (await PresentAsync(frame, StreamTransport.Mjpeg, token).ConfigureAwait(false))
                        {
                            return (Outcome.Grabbed, null);
                        }
                    }
                    else
                    {
                        PresentLatest(frame, token);
                    }

                    if (_undecodable > 20 && !_gotFrame)
                    {
                        return (Outcome.Unsupported, Str.Shell_Stream_NotPictures.Text());
                    }
                }

                if (reader.Overflowed)
                {
                    return (Outcome.Failed, "A frame was too large");
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<(Outcome, string?)> ReadImageAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength is > MaxImageBytes)
        {
            return (Outcome.Unsupported, Str.Shell_Stream_TooLarge.Text());
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var body = new MemoryStream();
        var chunk = new byte[32 * 1024];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, token).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            if (body.Length + read > MaxImageBytes)
            {
                return (Outcome.Unsupported, Str.Shell_Stream_TooLarge.Text());
            }

            body.Write(chunk, 0, read);
        }

        if (!await PresentAsync(body.ToArray(), StreamTransport.Image, token).ConfigureAwait(false))
        {
            return (Outcome.Unsupported, Str.Shell_Stream_NotPicture.Text());
        }

        // Live mode on a snapshot endpoint shows the one picture and stops, as an image would; an
        // interval turns the same endpoint into a slow camera.
        return Interval ? (Outcome.Grabbed, null) : (Outcome.Done, null);
    }

    /// <summary>
    /// Live mode: decode this frame unless one is already decoding, in which case drop it. The
    /// picture is always the newest frame the decoder could keep up with, and a slow machine sheds
    /// frames instead of building a queue of stale ones.
    /// </summary>
    private void PresentLatest(byte[] frame, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _decoding, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await PresentAsync(frame, StreamTransport.Mjpeg, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore: the session ended while this frame was decoding.
            }
            catch (Exception ex)
            {
                // A fire-and-forget task: anything that escapes here would vanish unobserved.
                Log.Warn(Scope, "a frame could not be presented", ex);
            }
            finally
            {
                Volatile.Write(ref _decoding, 0);
            }
        }, CancellationToken.None);
    }

    /// <summary>Decode and hand over one frame. False when it would not decode.</summary>
    private async Task<bool> PresentAsync(byte[] frame, StreamTransport transport, CancellationToken token)
    {
        var (bitmap, width, height) = await DecodeAsync(frame, _target()).ConfigureAwait(false);

        if (bitmap is null)
        {
            _undecodable++;
            return false;
        }

        if (token.IsCancellationRequested)
        {
            bitmap.Dispose();
            token.ThrowIfCancellationRequested();
        }

        _undecodable = 0;
        var first = !_gotFrame;
        _gotFrame = true;
        var facts = new FrameFacts(width, height, transport);

        Post(() =>
        {
            if (first)
            {
                _sink.OnPhase(StreamPhase.Live, null);
            }

            _sink.OnFrame(bitmap, facts);
        }, bitmap);

        return true;
    }

    /// <summary>
    /// Decode with WIC, scaled during decode to the size it will be drawn at — a 1080p camera in a
    /// 320px tile costs a 320px bitmap. Never scaled up: the card does that for free on the GPU.
    /// </summary>
    /// <returns>The bitmap (null when the bytes would not decode) and the frame's own size before scaling.</returns>
    private static async Task<(SoftwareBitmap? Bitmap, int Width, int Height)> DecodeAsync(byte[] bytes, DecodeTarget target)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;

            if (width == 0 || height == 0)
            {
                return (null, 0, 0);
            }

            var (boxW, boxH) = target.Sideways ? (target.Height, target.Width) : (target.Width, target.Height);
            var scale = 1.0;

            if (boxW > 0 && boxH > 0)
            {
                var sx = (double)boxW / width;
                var sy = (double)boxH / height;
                scale = Math.Min(1.0, target.Fit == StreamFit.Contain ? Math.Min(sx, sy) : Math.Max(sx, sy));
            }

            var transform = new BitmapTransform
            {
                ScaledWidth = (uint)Math.Max(1, Math.Round(width * scale)),
                ScaledHeight = (uint)Math.Max(1, Math.Round(height * scale)),
                InterpolationMode = BitmapInterpolationMode.Linear,
            };

            var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            return (bitmap, (int)Math.Min(width, int.MaxValue), (int)Math.Min(height, int.MaxValue));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException or InvalidOperationException)
        {
            // A torn or non-JPEG part. One frame lost; the next one is a fraction of a second away.
            return (null, 0, 0);
        }
    }

    private void Post(Action action, IDisposable? owned = null)
    {
        if (_disposed || !_ui.TryEnqueue(() =>
        {
            if (_disposed)
            {
                owned?.Dispose();
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the stream sink failed", ex);
            }
        }))
        {
            owned?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancelled, not disposed: the loop and an in-flight decode still hold its token, and a
        // source with no timer owns nothing that needs releasing.
        _cts.Cancel();
    }
}
