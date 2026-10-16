using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3914] Sound: the default output's mute, through the Core Audio worker the volume and mixer
/// widgets already share (<see cref="CoreAudio"/>), so a third subscriber costs no thread of its own.
/// On is heard (unmuted), as Karpo's sound toggle has it. The endpoint's own callback follows the
/// keyboard's mute key and Windows' panel; a new default output is followed by the worker. No output
/// device: nothing to mute.
/// </summary>
internal sealed class SoundToggleSource : ToggleSource
{
    private readonly Lock _gate = new();
    private IAudioSubscription? _subscription;
    private TaskCompletionSource<bool>? _switching;

    /// <summary>On the UI thread: the worker's snapshots come back on its dispatcher.</summary>
    public override void Start()
        => _subscription = CoreAudio.Subscribe(new AudioWant(DeviceId: null, Sessions: false), OnSnapshot);

    private void OnSnapshot(AudioSnapshot snapshot)
    {
        Publish(snapshot.Status switch
        {
            AudioStatus.Ready when snapshot.Output is { } output => ToggleReading.Of(!output.Muted),
            AudioStatus.NoDevice or AudioStatus.DeviceMissing => ToggleReading.Blocked(ToggleBlock.NoDevice),
            AudioStatus.Unavailable => ToggleReading.Blocked(ToggleBlock.NoSwitch),
            _ => ToggleReading.Pending,
        });

        TaskCompletionSource<bool>? switching;
        lock (_gate)
        {
            switching = _switching;
            _switching = null;
        }

        switching?.TrySetResult(Reading.On is not null);
    }

    /// <summary>Queues the mute with the worker; done with the snapshot that follows it.</summary>
    public override Task<bool> SetAsync(bool on)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _switching?.TrySetResult(false);
            _switching = done;
        }

        CoreAudio.SetMute(null, Themia.Core.Widgets.MixerRules.MasterKey, muted: !on);
        return done.Task;
    }

    protected override void Stop()
    {
        _subscription?.Dispose();
        _subscription = null;

        TaskCompletionSource<bool>? switching;
        lock (_gate)
        {
            switching = _switching;
            _switching = null;
        }

        switching?.TrySetResult(false);
    }
}
