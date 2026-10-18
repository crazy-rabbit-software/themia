using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3203] What the speakers play, as the audio visualizer reaches it (<see cref="IWidgetHost.Loopback"/>):
/// the host's one WASAPI loopback capture of the default output, started by the first subscriber and
/// stopped by the last. The COM capture stays host code.
/// </summary>
public interface IAudioCapture
{
    /// <summary>
    /// Receive frames (<paramref name="onFrame"/>, on the capture worker: the widget marshals) and whether
    /// there is anything to capture (<paramref name="onAvailability"/>). Dispose to let go.
    /// </summary>
    IDisposable Subscribe(Action<AudioFrame> onFrame, Action<bool> onAvailability);
}
