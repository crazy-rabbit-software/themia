using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-440] [S-1220] The output devices as an audio widget reaches them (<see cref="IWidgetHost.Audio"/>):
/// the host's one Core Audio worker, started by the first audio widget and stopped by the last, kept
/// current by Core Audio's own notifications. The COM conversation stays host code.
/// </summary>
public interface IAudioOutputs
{
    /// <summary>
    /// Receive this widget's snapshots on the calling thread's dispatcher; the first arrives once the
    /// worker has read the device. Dispose the result to stop.
    /// </summary>
    IAudioSubscription Subscribe(AudioWant want, Action<AudioSnapshot> onSnapshot);

    /// <summary>[S-446] Queue a level for a row of a device (null: the default output); the newest value per row wins.</summary>
    void SetLevel(string? deviceId, string key, double level);

    /// <summary>[S-447] Queue a mute for a row of a device.</summary>
    void SetMute(string? deviceId, string key, bool muted);

    /// <summary>[S-1614] What makes a device Windows' default output (the policy config's call).</summary>
    IDefaultOutputSwitch DefaultOutput { get; }
}
