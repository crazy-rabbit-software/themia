using Themia.App.Services;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6913] Windows' own media session as the music widget reaches it (<see cref="IWidgetHost.Media"/>):
/// the session the media keys and the volume flyout's transport drive, so the widget needs no account
/// and no per-player integration. The WinRT session manager stays host code.
/// </summary>
public interface IMediaSessions
{
    /// <summary>
    /// A connection for one widget: <paramref name="onChanged"/> is raised whenever something about the
    /// session changed, on whatever thread WinRT used (the widget marshals). Dispose to unhook.
    /// </summary>
    IMediaSession Open(Action onChanged);
}

/// <summary>One widget's connection to the current media session. Event-driven: nothing is polled.</summary>
public interface IMediaSession : IDisposable
{
    /// <summary>Connect to the session manager and start listening; a second call is nothing.</summary>
    Task StartAsync(CancellationToken token);

    /// <summary>What is playing now, folded into one snapshot.</summary>
    Task<NowPlaying> ReadAsync(CancellationToken token);

    /// <summary>The current track's cover art, or null when it has none.</summary>
    Task<byte[]?> ReadArtworkAsync(CancellationToken token);

    /// <summary>Press a transport button; false where the session refused.</summary>
    Task<bool> InvokeAsync(MediaTransport command, CancellationToken token);

    /// <summary>[S-1465] Move to <paramref name="position"/> on the session's own <paramref name="origin"/>; false where it refused.</summary>
    Task<bool> SeekAsync(TimeSpan position, TimeSpan origin, CancellationToken token);
}
