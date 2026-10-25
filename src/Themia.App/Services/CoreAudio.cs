using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-440] [S-1220] Core Audio for the mixer and the volume tile: the bound output devices' levels and
/// sessions and the device list, kept current by Core Audio's
/// own notifications rather than by polling.
/// <para>
/// <b>One worker thread, in the multithreaded apartment.</b> Session notifications are only
/// delivered to a session manager used from the MTA, and every call here is a blocking COM call
/// that has no business on the UI thread. So the whole conversation with Core Audio happens on one
/// dedicated thread: it binds the devices its widgets ask for, registers for devices coming and
/// going and the default changing, sessions appearing, each session's volume, state and disconnect,
/// and each endpoint's master volume; and it sleeps until one of those — or a write from a slider —
/// wakes it. A wake reads once and publishes one snapshot per widget, however many notifications
/// arrived meanwhile. The volume tile, which once polled the endpoint every two seconds, is just
/// another subscriber that does not ask for sessions.
/// </para>
/// <para>
/// <b>Started by the first audio widget and stopped by the last.</b> Nothing is registered and no
/// thread exists while none is on the desktop.
/// </para>
/// </summary>
internal static unsafe partial class CoreAudio
{
    private const string Scope = "audio";

    /// <summary>Separates a write's device selector from its row key. Neither can contain it.</summary>
    private const char KeySeparator = '\u001F';

    private static readonly Lock Gate = new();
    private static readonly List<Subscriber> Subscribers = [];
    private static volatile Worker? _worker;

    /// <summary>
    /// Receive this widget's snapshots on the calling thread's dispatcher. The first arrives as soon as the worker has read
    /// the device. Dispose the result to stop; the last subscriber to leave stops the worker.
    /// </summary>
    public static IAudioSubscription Subscribe(AudioWant want, Action<AudioSnapshot> onSnapshot)
    {
        ArgumentNullException.ThrowIfNull(onSnapshot);

        var dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("subscribe from a thread with a dispatcher");

        var subscriber = new Subscriber(dispatcher, want, onSnapshot);

        lock (Gate)
        {
            Subscribers.Add(subscriber);

            if (_worker is null)
            {
                _worker = new Worker();
                _worker.Start();
            }
        }

        Notify(Change.Wants);
        return subscriber;
    }

    /// <summary>
    /// [S-446] Queue a level for a row of a device (null: the default output); the newest value per
    /// row wins. <see cref="MixerRules.MasterKey"/> is the device itself.
    /// </summary>
    public static void SetLevel(string? deviceId, string key, double level)
        => _worker?.PostLevel(WriteKey(deviceId, key), MixerRules.ClampLevel(level));

    /// <summary>[S-447] Queue a mute for a row of a device.</summary>
    public static void SetMute(string? deviceId, string key, bool muted)
        => _worker?.PostMute(WriteKey(deviceId, key), muted);

    /// <summary>
    /// [S-1222] The active output devices, for a properties menu: enumerated on a pool thread in its own
    /// apartment, so it neither blocks the UI nor needs the worker to be running. Empty when Core
    /// Audio would not answer (logged).
    /// </summary>
    public static Task<IReadOnlyList<AudioDevice>> OutputDevicesAsync() => Task.Run(ListOutputDevices);

    private static string WriteKey(string? deviceId, string key) => (deviceId ?? string.Empty) + KeySeparator + key;

    private static (string Selector, string Key) SplitWriteKey(string writeKey)
    {
        var at = writeKey.IndexOf(KeySeparator, StringComparison.Ordinal);
        return (writeKey[..at], writeKey[(at + 1)..]);
    }

    private static void Unsubscribe(Subscriber subscriber)
    {
        Worker? stopping = null;

        lock (Gate)
        {
            if (!Subscribers.Remove(subscriber))
            {
                return;
            }

            if (Subscribers.Count > 0)
            {
                // What is still wanted shrank: let the worker release what nobody asks for.
                _worker?.Signal(Change.Wants);
                return;
            }

            stopping = _worker;
            _worker = null;
        }

        stopping?.Stop();
    }

    private static Subscriber[] Snapshot()
    {
        lock (Gate)
        {
            return [.. Subscribers];
        }
    }

    /// <summary>A notification arrived: wake the worker that is current, whichever one that is.</summary>
    private static void Notify(Change change) => _worker?.Signal(change);

    [Flags]
    private enum Change
    {
        None = 0,

        /// <summary>A session appeared, changed volume or state, or went away; or a master level moved.</summary>
        Sessions = 1,

        /// <summary>A default device changed.</summary>
        Default = 2,

        /// <summary>A device was added, removed, enabled, disabled or renamed.</summary>
        Devices = 4,

        /// <summary>A widget subscribed, left, or changed what it wants.</summary>
        Wants = 8,
    }

    private sealed class Subscriber(DispatcherQueue dispatcher, AudioWant want, Action<AudioSnapshot> onSnapshot)
        : IAudioSubscription
    {
        private volatile bool _disposed;

        /// <summary>Written on the UI thread, read on the worker: a record struct swapped whole under the lock.</summary>
        public AudioWant Current
        {
            get
            {
                lock (Gate)
                {
                    return want;
                }
            }
        }

        public void Want(AudioWant next)
        {
            lock (Gate)
            {
                if (want == next)
                {
                    return;
                }

                want = next;
            }

            Notify(Change.Wants);
        }

        public void Deliver(AudioSnapshot snapshot)
        {
            if (_disposed)
            {
                return;
            }

            dispatcher.TryEnqueue(() =>
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    onSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "an audio widget could not show its device", ex);
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
            Unsubscribe(this);
        }
    }
}
