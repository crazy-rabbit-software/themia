using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// One audio widget's connection to <see cref="CoreAudio"/>: what it wants, the snapshots and meters
/// that come back, and the writes it sends — the part the mixer and the volume tile share.
/// <para>
/// [S-1233] A preview card (a debug render) never writes: its sliders and buttons are inert, so
/// rendering one can never move the machine's real volume.
/// </para>
/// </summary>
/// <param name="onPeaks">Meter samples; null for a widget that never asks for them (the volume tile, [S-2730]).</param>
internal sealed class AudioFeed(Action<AudioSnapshot> onSnapshot, Action<AudioPeaks>? onPeaks = null) : IDisposable
{
    private IAudioSubscription? _subscription;
    private AudioWant? _want;
    private bool _picking;

    /// <summary>Set from the surface before the first <see cref="Want"/>: a preview never writes.</summary>
    public bool Preview { get; set; }

    /// <summary>Start, or change what is asked for. Cheap when nothing changed.</summary>
    public void Want(AudioWant want)
    {
        if (_want == want)
        {
            return;
        }

        _want = want;

        if (_subscription is null)
        {
            _subscription = CoreAudio.Subscribe(want, onSnapshot, onPeaks);
        }
        else
        {
            _subscription.Want(want);
        }
    }

    /// <summary>[S-446] Queue a level for a row of this widget's device; nothing in a preview.</summary>
    public void SetLevel(string key, double level)
    {
        if (!Preview && _want is { } want)
        {
            CoreAudio.SetLevel(want.DeviceId, key, level);
        }
    }

    /// <summary>[S-447] Queue a mute for a row of this widget's device; nothing in a preview.</summary>
    public void SetMute(string key, bool muted)
    {
        if (!Preview && _want is { } want)
        {
            CoreAudio.SetMute(want.DeviceId, key, muted);
        }
    }

    /// <summary>
    /// [S-1614]..[S-1617] A pick in this widget's device picker: make the device Windows' default
    /// output (the widget then follows the default), or bind the widget to it when Windows refuses.
    /// Null when nothing changes — always in a preview, and while an earlier pick is still being
    /// answered (one at a time: a second click on a slow switch must not race the first).
    /// </summary>
    /// <param name="picked">The picked device, or null for "Follow the default output".</param>
    /// <param name="boundId">The widget's configured device id.</param>
    /// <param name="boundName">The widget's remembered device name.</param>
    /// <param name="defaultId">Windows' default output as the last snapshot saw it.</param>
    public async Task<DevicePickResult?> PickAsync(AudioDevice? picked, string? boundId, string? boundName, string? defaultId)
    {
        if (_picking)
        {
            return null;
        }

        _picking = true;

        try
        {
            return await AudioDefaultRules.PickAsync(Preview, picked, boundId, boundName, defaultId, CoreAudio.DefaultOutputSwitch.Instance);
        }
        finally
        {
            _picking = false;
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
        _want = null;
    }
}
