using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3907] One source per toggle for the whole app, counted by its widgets: the first widget of a
/// toggle starts it, the last one to go stops it, and two Wi-Fi widgets on two displays read one radio.
/// A change reaches each widget on its own thread.
/// </summary>
internal static class ToggleHub
{
    private const string Scope = "toggles";

    private static readonly Lock Gate = new();
    private static readonly Dictionary<QuickToggle, Entry> Entries = [];

    /// <summary>
    /// Follow <paramref name="toggle"/>; <paramref name="changed"/> is called on the calling thread's
    /// dispatcher whenever its reading moves. Dispose the lease to stop.
    /// </summary>
    public static ToggleLease Acquire(QuickToggle toggle, Action changed)
    {
        var queue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("acquire a toggle from a thread with a dispatcher");

        Entry entry;
        bool start;
        ToggleLease lease;

        lock (Gate)
        {
            start = !Entries.TryGetValue(toggle, out var found);
            entry = found ?? new Entry(toggle, Create(toggle));
            if (start)
            {
                Entries[toggle] = entry;
            }

            lease = new ToggleLease(entry, queue, changed);
            entry.Leases.Add(lease);
        }

        if (start)
        {
            try
            {
                entry.Source.Start();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, $"{toggle} could not start following Windows", ex);
            }
        }

        return lease;
    }

    private static ToggleSource Create(QuickToggle toggle) => toggle switch
    {
        QuickToggle.Wifi => new RadioToggleSource(global::Windows.Devices.Radios.RadioKind.WiFi),
        QuickToggle.Bluetooth => new RadioToggleSource(global::Windows.Devices.Radios.RadioKind.Bluetooth),
        QuickToggle.AirplaneMode => new AirplaneToggleSource(),
        QuickToggle.EnergySaver => new EnergySaverToggleSource(),
        QuickToggle.NightLight => new NightLightToggleSource(),
        QuickToggle.RotationLock => new RotationToggleSource(),
        QuickToggle.DoNotDisturb => new DoNotDisturbToggleSource(),
        QuickToggle.Sound => new SoundToggleSource(),
        QuickToggle.Microphone => new MicrophoneToggleSource(),
        QuickToggle.SpatialSound => new SpatialToggleSource(),
        QuickToggle.MonoAudio => new SettingToggleSource(SettingPlace.MonoAudio),
        QuickToggle.ColorFilters => new SettingToggleSource(SettingPlace.ColorFilters),
        QuickToggle.GameMode => new SettingToggleSource(SettingPlace.GameMode),

        // No fall-back: this decides what a click switches, so a new toggle without an arm fails loudly.
        _ => throw new ArgumentOutOfRangeException(nameof(toggle), toggle, "no source for this toggle"),
    };

    internal static void Release(ToggleLease lease)
    {
        var entry = lease.Entry;
        bool last;

        lock (Gate)
        {
            if (!entry.Leases.Remove(lease))
            {
                return;
            }

            last = entry.Leases.Count == 0;
            if (last)
            {
                Entries.Remove(entry.Toggle);
            }
        }

        if (last)
        {
            entry.Source.Changed -= entry.OnChanged;
            entry.Source.Dispose();
        }
    }

    internal sealed class Entry
    {
        public Entry(QuickToggle toggle, ToggleSource source)
        {
            Toggle = toggle;
            Source = source;
            Source.Changed += OnChanged;
        }

        public QuickToggle Toggle { get; }

        public ToggleSource Source { get; }

        /// <summary>Guarded by the hub's lock.</summary>
        public List<ToggleLease> Leases { get; } = [];

        /// <summary>From any thread: hand the change to every widget, each on its own thread.</summary>
        public void OnChanged()
        {
            ToggleLease[] leases;
            lock (Gate)
            {
                leases = [.. Leases];
            }

            foreach (var lease in leases)
            {
                lease.Post();
            }
        }
    }
}

/// <summary>One widget's hold on a toggle's source.</summary>
internal sealed class ToggleLease : IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly Action _changed;
    private volatile bool _disposed;

    internal ToggleLease(ToggleHub.Entry entry, DispatcherQueue queue, Action changed)
    {
        Entry = entry;
        _queue = queue;
        _changed = changed;
    }

    internal ToggleHub.Entry Entry { get; }

    public ToggleReading Reading => Entry.Source.Reading;

    public Task<bool> SetAsync(bool on) => Entry.Source.SetAsync(on);

    public void Refresh() => Entry.Source.Refresh();

    internal void Post()
    {
        if (_disposed)
        {
            return;
        }

        _ = _queue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _changed();
            }
            catch (Exception ex)
            {
                Log.Error("toggles", "a toggle widget could not take a change", ex);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ToggleHub.Release(this);
    }
}
