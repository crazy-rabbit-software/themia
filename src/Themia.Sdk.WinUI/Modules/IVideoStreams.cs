using Themia.Core.Widgets;
using Windows.Graphics.Imaging;

namespace Themia.App.Services.Web;

/// <summary>What a session reports. Always called on the UI thread.</summary>
public interface IStreamSink
{
    void OnPhase(StreamPhase phase, string? detail);

    /// <summary>A decoded frame. The sink owns it and must dispose it.</summary>
    void OnFrame(SoftwareBitmap frame, FrameFacts facts);

    /// <summary>
    /// The address is a video Windows can play itself; the sink takes it from here. For an HLS master
    /// playlist, <paramref name="variants"/> are its renditions ([S-6602]); otherwise empty.
    /// </summary>
    void OnVideo(Uri uri, StreamTransport transport, IReadOnlyList<HlsVariant> variants);
}

/// <summary>
/// [S-471] The camera connections a video stream widget opens (<see cref="Themia.Sdk.WinUI.IWidgetHost.Streams"/>):
/// the host's sessions, which read the reply's content type, parse MJPEG and decode each frame to the
/// size it is drawn at, grab a snapshot on an interval, or hand a real video back for Windows' own
/// player. The sockets, the guard and the decoding stay host code.
/// </summary>
public interface IVideoStreams
{
    /// <summary>
    /// Open <paramref name="uri"/> and start: <paramref name="sink"/> hears every phase, frame and video
    /// on the UI thread. <paramref name="refreshSeconds"/> above zero grabs one frame each interval and
    /// hangs up between ([S-470]). Dispose to close it on every path.
    /// </summary>
    IDisposable Open(Uri uri, int refreshSeconds, Func<DecodeTarget> target, IStreamSink sink);
}
