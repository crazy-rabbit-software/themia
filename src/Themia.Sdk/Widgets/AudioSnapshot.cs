using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>What one audio widget asks the worker for.</summary>
/// <param name="DeviceId">[S-1221] The output device's endpoint id, or null for Windows' default output.</param>
/// <param name="Sessions">Read the device's audio sessions (the mixer; the volume tile at its largest).</param>
public readonly record struct AudioWant(string? DeviceId, bool Sessions);

/// <summary>Whether a widget's device could be read.</summary>
public enum AudioStatus
{
    Ready,

    /// <summary>There is no output device at all (or no default one).</summary>
    NoDevice,

    /// <summary>[S-1221] The chosen device is not connected.</summary>
    DeviceMissing,

    /// <summary>Core Audio would not answer for the device.</summary>
    Unavailable,
}

/// <summary>One endpoint's level, as a row or a readout shows it.</summary>
public sealed record AudioLevel(string Id, string Name, double Level, bool Muted);

/// <summary>
/// What one audio widget can see right now: its device's level and sessions, and every connected
/// output device for the device picker.
/// </summary>
/// <param name="Output">The bound device's master level, or null when it could not be read.</param>
/// <param name="Sessions">Every live session of the bound device, ungrouped. Empty unless asked for.</param>
/// <param name="Devices">The active output devices, in Windows' order.</param>
/// <param name="DefaultId">The default output device's id, or null when there is none.</param>
public sealed record AudioSnapshot(
    AudioStatus Status,
    AudioLevel? Output,
    IReadOnlyList<AudioSessionInfo> Sessions,
    IReadOnlyList<AudioDevice> Devices,
    string? DefaultId,
    AudioTarget Target)
{
    public static AudioSnapshot Unavailable { get; } = new(AudioStatus.Unavailable, null, [], [], null, AudioTarget.Default);
}

/// <summary>A live subscription to the host's Core Audio (<c>IAudioOutputs.Subscribe</c>). Change what it wants, or dispose it to stop.</summary>
public interface IAudioSubscription : IDisposable
{
    void Want(AudioWant want);
}
