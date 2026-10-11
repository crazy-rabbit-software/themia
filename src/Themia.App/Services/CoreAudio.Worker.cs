using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

internal static unsafe partial class CoreAudio
{
    /// <summary>[S-1223] The meters' sampling period: ten a second, the most a meter bar needs.</summary>
    private const int PeakPeriodMs = 100;

    /// <summary>One session the worker holds and listens to, until it expires or its device is let go.</summary>
    private sealed class Tracked
    {
        public nint Control;
        public nint Volume;
        public nint Meter;
        public string GroupKey = string.Empty;
        public bool Seen;
    }

    /// <summary>
    /// One endpoint the worker has bound: its volume (and the callback on it) and — when a widget
    /// wants them — its session manager and the sessions it tracks, each with its own meter. The
    /// endpoint's own meter is not read: no widget shows the device's level as a meter ([S-3251]). Every pointer here is
    /// owned and released by <see cref="Worker.Unbind"/>.
    /// </summary>
    private sealed class Endpoint(string id, string name)
    {
        public string Id { get; } = id;

        public string Name { get; set; } = name;

        public nint Volume;
        public nint Manager;
        public bool Failed;
        public readonly Dictionary<string, Tracked> Tracked = new(StringComparer.Ordinal);
        public List<AudioSessionInfo> Sessions = [];
    }

    private sealed partial class Worker
    {
        private readonly AutoResetEvent _wake = new(false);
        private readonly LatestWrites<double> _levels = new();
        private readonly LatestWrites<bool> _mutes = new();
        private readonly Dictionary<string, Endpoint> _outputs = new(StringComparer.OrdinalIgnoreCase);
        private Thread? _thread;
        private int _changes = (int)(Change.Devices | Change.Default | Change.Wants);
        private volatile bool _stopping;

        private nint _enumerator;
        private bool _clientRegistered;

        private IReadOnlyList<AudioDevice> _devices = [];
        private string? _defaultOutput;
        private long _lastPeaks;

        public void Start()
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "themia-audio" };
            _thread.Start();
        }

        public void Stop()
        {
            _stopping = true;
            Wake();
        }

        public void Signal(Change change)
        {
            int seen, next;
            do
            {
                seen = _changes;
                next = seen | (int)change;
            }
            while (Interlocked.CompareExchange(ref _changes, next, seen) != seen);

            Wake();
        }

        public void PostLevel(string key, double level)
        {
            _levels.Post(key, level);
            Wake();
        }

        public void PostMute(string key, bool muted)
        {
            _mutes.Post(key, muted);
            Wake();
        }

        private void Wake()
        {
            try
            {
                _wake.Set();
            }
            catch (ObjectDisposedException)
            {
                // ignore: a notification or a write that raced the worker's own shutdown; there is
                // nothing left to wake, and the next audio widget starts a new worker.
            }
        }

        private void Run()
        {
            var initialised = CoInitializeEx(0, CoInitMultithreaded);

            // RPC_E_CHANGED_MODE would mean this thread is already single-threaded, where session
            // notifications never arrive. A fresh thread cannot be, but the log would say why if so.
            if (initialised < 0)
            {
                Log.Error(Scope, $"could not enter the multithreaded apartment: 0x{initialised:X8}");
                foreach (var subscriber in CoreAudio.Snapshot())
                {
                    subscriber.Deliver(AudioSnapshot.Unavailable);
                }

                return;
            }

            try
            {
                while (!_stopping)
                {
                    var changes = (Change)Interlocked.Exchange(ref _changes, 0);
                    var subscribers = CoreAudio.Snapshot();

                    try
                    {
                        Cycle(changes, subscribers);
                    }
                    catch (Exception ex)
                    {
                        // One bad cycle must not end the thread: the next notification retries.
                        Log.Error(Scope, "an audio refresh failed", ex);
                    }

                    if (_stopping)
                    {
                        break;
                    }

                    var metering = subscribers.Any(static s => s.Current.Peaks);

                    try
                    {
                        if (metering)
                        {
                            SamplePeaks(subscribers);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Scope, "a meter sample failed", ex);
                    }

                    // [S-1223] Asleep until something happens; awake ten times a second only while a
                    // widget has a meter on screen.
                    _wake.WaitOne(metering ? PeakPeriodMs : Timeout.Infinite);
                }
            }
            finally
            {
                // [S-1615] A pick still waiting is answered, never left hanging.
                FailDefaultRequests();

                foreach (var output in _outputs.Values)
                {
                    Unbind(output);
                }

                _outputs.Clear();

                ReleaseEnumerator();
                CoUninitialize();
                _wake.Dispose();
            }
        }

        private void Cycle(Change changes, Subscriber[] subscribers)
        {
            // [S-1615] First, and before the enumerator: switching the default needs neither, and
            // the change notification it causes is what rebinds the widgets below.
            ApplyDefaultRequests();

            if (_enumerator == 0 && !OpenEnumerator())
            {
                if (changes != Change.None)
                {
                    foreach (var subscriber in subscribers)
                    {
                        subscriber.Deliver(AudioSnapshot.Unavailable);
                    }
                }

                return;
            }

            if (changes.HasFlag(Change.Devices))
            {
                _devices = EnumerateOutputs(_enumerator);
            }

            if (changes.HasFlag(Change.Devices) || changes.HasFlag(Change.Default))
            {
                _defaultOutput = DefaultId(_enumerator, DataFlowRender);
            }

            if (changes.HasFlag(Change.Devices) || changes.HasFlag(Change.Default) || changes.HasFlag(Change.Wants))
            {
                Rebind(subscribers);
            }

            var wrote = ApplyWrites();

            if (changes == Change.None && !wrote)
            {
                return;
            }

            foreach (var output in _outputs.Values)
            {
                ReadEndpoint(output);
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.Deliver(SnapshotFor(subscriber.Current));
            }
        }

        // ---- what is bound -------------------------------------------------------------------

        /// <summary>
        /// Bind every device some widget asks for, and let go of every one nobody does. A device
        /// keeps its binding across a change of what is wanted, so a widget opening its meters does
        /// not drop the session callbacks another widget depends on.
        /// </summary>
        private void Rebind(Subscriber[] subscribers)
        {
            var needed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var subscriber in subscribers)
            {
                var want = subscriber.Current;

                if (AudioRules.Resolve(want.DeviceId, _devices, _defaultOutput).Id is { } id)
                {
                    needed[id] = (needed.TryGetValue(id, out var sessions) && sessions) || want.Sessions;
                }
            }

            // Let go of what nobody wants, and of what failed to bind: a device change is the moment a
            // device that would not open may open now.
            foreach (var id in _outputs.Where(pair => pair.Value.Failed || !needed.ContainsKey(pair.Key)).Select(pair => pair.Key).ToList())
            {
                Unbind(_outputs[id]);
                _outputs.Remove(id);
            }

            foreach (var (id, sessions) in needed)
            {
                if (!_outputs.TryGetValue(id, out var output))
                {
                    output = Bind(id, Name(id));
                    _outputs[id] = output;
                }

                output.Name = Name(id);

                if (sessions && output.Manager == 0 && !output.Failed)
                {
                    OpenSessions(output);
                }
                else if (!sessions && output.Manager != 0)
                {
                    CloseSessions(output);
                }
            }
        }

        private string Name(string id) => AudioRules.DeviceName(id, _devices, null);

        private Endpoint Bind(string id, string name)
        {
            var endpoint = new Endpoint(id, name);
            var device = OpenDevice(_enumerator, id);

            if (device == 0)
            {
                endpoint.Failed = true;
                return endpoint;
            }

            try
            {
                endpoint.Volume = Activate(device, Iids.EndpointVolume);

                if (endpoint.Volume == 0)
                {
                    endpoint.Failed = true;
                    return endpoint;
                }

                Call(endpoint.Volume, Slots.EndpointRegisterNotify, Callbacks.EndpointVolume);

                Note(0, null);
                return endpoint;
            }
            finally
            {
                ReleaseRaw(device);
            }
        }

        private void OpenSessions(Endpoint output)
        {
            var device = OpenDevice(_enumerator, output.Id);
            if (device == 0)
            {
                return;
            }

            try
            {
                output.Manager = Activate(device, Iids.SessionManager2);

                if (output.Manager != 0)
                {
                    Call(output.Manager, Slots.ManagerRegisterSessionNotification, Callbacks.SessionCreated);
                }
            }
            finally
            {
                ReleaseRaw(device);
            }
        }

        private static void CloseSessions(Endpoint output)
        {
            foreach (var tracked in output.Tracked.Values)
            {
                Forget(tracked);
            }

            output.Tracked.Clear();
            output.Sessions = [];

            if (output.Manager != 0)
            {
                Call(output.Manager, Slots.ManagerUnregisterSessionNotification, Callbacks.SessionCreated);
                ReleaseRaw(output.Manager);
                output.Manager = 0;
            }
        }

        private static void Unbind(Endpoint endpoint)
        {
            CloseSessions(endpoint);

            if (endpoint.Volume != 0)
            {
                Call(endpoint.Volume, Slots.EndpointUnregisterNotify, Callbacks.EndpointVolume);
                ReleaseRaw(endpoint.Volume);
                endpoint.Volume = 0;
            }
        }

        private bool OpenEnumerator()
        {
            var clsid = Iids.DeviceEnumeratorClass;
            var iid = Iids.DeviceEnumerator;
            nint enumerator;

            var created = CoCreateInstance(&clsid, 0, ClsCtxInprocServer, &iid, &enumerator);
            if (created < 0)
            {
                Note(created, "could not open the audio device enumerator");
                return false;
            }

            _enumerator = enumerator;

            // [S-440] [S-1221] Follow devices coming and going and the defaults changing: a headset
            // plugged in moves every app's sound.
            _clientRegistered = Call(_enumerator, Slots.EnumeratorRegisterClient, Callbacks.DeviceClient) >= 0;
            return true;
        }

        private void ReleaseEnumerator()
        {
            if (_enumerator == 0)
            {
                return;
            }

            if (_clientRegistered)
            {
                Call(_enumerator, Slots.EnumeratorUnregisterClient, Callbacks.DeviceClient);
                _clientRegistered = false;
            }

            ReleaseRaw(_enumerator);
            _enumerator = 0;
        }

        // ---- reading -------------------------------------------------------------------------

        private AudioSnapshot SnapshotFor(AudioWant want)
        {
            var (id, target) = AudioRules.Resolve(want.DeviceId, _devices, _defaultOutput);

            if (id is null)
            {
                var status = target == AudioTarget.Missing && !string.IsNullOrEmpty(want.DeviceId)
                    ? AudioStatus.DeviceMissing
                    : AudioStatus.NoDevice;

                return new AudioSnapshot(status, null, [], _devices, _defaultOutput, target);
            }

            if (!_outputs.TryGetValue(id, out var output) || output.Failed || ReadLevel(output) is not { } master)
            {
                return new AudioSnapshot(AudioStatus.Unavailable, null, [], _devices, _defaultOutput, target);
            }

            return new AudioSnapshot(
                AudioStatus.Ready,
                master,
                want.Sessions ? output.Sessions : [],
                _devices,
                _defaultOutput,
                target);
        }

        private static AudioLevel? ReadLevel(Endpoint endpoint)
        {
            if (endpoint.Volume == 0)
            {
                return null;
            }

            float level;
            int muted;
            var gotLevel = ((delegate* unmanaged[Stdcall]<nint, float*, int>)Slot(endpoint.Volume, Slots.EndpointGetScalar))(endpoint.Volume, &level);
            var gotMute = ((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(endpoint.Volume, Slots.EndpointGetMute))(endpoint.Volume, &muted);

            if (gotLevel < 0 || gotMute < 0)
            {
                Note(gotLevel < 0 ? gotLevel : gotMute, "could not read a device's level");
                return null;
            }

            return new AudioLevel(endpoint.Id, endpoint.Name, MixerRules.ClampLevel(level), muted != 0);
        }

        /// <summary>Refresh an endpoint's sessions, when it has a session manager.</summary>
        private static void ReadEndpoint(Endpoint endpoint)
        {
            if (endpoint.Manager == 0)
            {
                return;
            }

            nint list;
            if (((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(endpoint.Manager, Slots.ManagerGetSessionEnumerator))(endpoint.Manager, &list) < 0)
            {
                return;
            }

            var sessions = new List<AudioSessionInfo>();

            try
            {
                int count;
                if (((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(list, Slots.ListGetCount))(list, &count) < 0)
                {
                    return;
                }

                foreach (var tracked in endpoint.Tracked.Values)
                {
                    tracked.Seen = false;
                }

                for (var i = 0; i < count; i++)
                {
                    nint control;
                    if (((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(list, Slots.ListGetSession))(list, i, &control) < 0)
                    {
                        // [S-446] One app exiting mid-enumeration must not blank the whole mixer.
                        continue;
                    }

                    try
                    {
                        if (ReadSession(endpoint, control, i) is { } info)
                        {
                            sessions.Add(info);
                        }
                    }
                    finally
                    {
                        ReleaseRaw(control);
                    }
                }

                // Sessions that expired or disconnected since the last read: stop listening.
                foreach (var key in endpoint.Tracked.Where(pair => !pair.Value.Seen).Select(pair => pair.Key).ToList())
                {
                    Forget(endpoint.Tracked[key]);
                    endpoint.Tracked.Remove(key);
                }

                endpoint.Sessions = sessions;
            }
            finally
            {
                ReleaseRaw(list);
            }
        }

        private static AudioSessionInfo? ReadSession(Endpoint endpoint, nint control, int index)
        {
            int state;
            if (((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(control, Slots.ControlGetState))(control, &state) < 0
                || state == SessionStateExpired)
            {
                // Expired: the app is gone and Windows has not reaped the session yet.
                return null;
            }

            var control2 = Query(control, Iids.SessionControl2);
            if (control2 == 0)
            {
                return null;
            }

            try
            {
                // S_FALSE is a success code that means "no", so only an exact S_OK is a yes.
                var isSystem = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(control2, Slots.Control2IsSystemSounds))(control2) == 0;

                uint pid;
                if (((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(control2, Slots.Control2GetProcessId))(control2, &pid) < 0)
                {
                    pid = 0;
                }

                var path = isSystem ? null : ProcessNames.ExePath(pid);
                var display = TakeString(control, Slots.ControlGetDisplayName);
                var name = isSystem ? string.Empty : MixerRules.SessionName(path is null ? null : ProcessNames.DisplayName(path), path, display);

                var groupKey = isSystem
                    ? MixerRules.SystemSoundsKey
                    : path?.ToLowerInvariant() ?? $"pid:{pid}";

                var instance = TakeString(control2, Slots.Control2GetInstanceIdentifier);
                var sessionKey = string.IsNullOrEmpty(instance) ? $"{groupKey}#{index}" : instance;

                if (!endpoint.Tracked.TryGetValue(sessionKey, out var tracked))
                {
                    tracked = Track(control, groupKey);
                    if (tracked is null)
                    {
                        return null;
                    }

                    endpoint.Tracked[sessionKey] = tracked;
                }

                tracked.Seen = true;

                float level;
                int muted;
                var volume = tracked.Volume;
                var gotLevel = ((delegate* unmanaged[Stdcall]<nint, float*, int>)Slot(volume, Slots.SimpleGetMasterVolume))(volume, &level) >= 0;
                var gotMute = ((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(volume, Slots.SimpleGetMute))(volume, &muted) >= 0;

                return new AudioSessionInfo(
                    sessionKey,
                    groupKey,
                    name,
                    path,
                    gotLevel ? level : 0,
                    gotMute && muted != 0,
                    state == SessionStateActive,
                    isSystem);
            }
            finally
            {
                ReleaseRaw(control2);
            }
        }

        /// <summary>Hold a session and listen to it. The control, volume and meter are kept until <see cref="Forget"/>.</summary>
        private static Tracked? Track(nint control, string groupKey)
        {
            var volume = Query(control, Iids.SimpleAudioVolume);
            if (volume == 0)
            {
                return null;
            }

            ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(control, Slots.AddRef))(control);
            Call(control, Slots.ControlRegisterNotification, Callbacks.SessionEvents);

            return new Tracked
            {
                Control = control,
                Volume = volume,

                // [S-1223] Optional, as on the endpoint: no meter is a row without a meter bar.
                Meter = Query(control, Iids.MeterInformation),
                GroupKey = groupKey,
            };
        }

        private static void Forget(Tracked tracked)
        {
            if (tracked.Control != 0)
            {
                Call(tracked.Control, Slots.ControlUnregisterNotification, Callbacks.SessionEvents);
                ReleaseRaw(tracked.Control);
                tracked.Control = 0;
            }

            if (tracked.Volume != 0)
            {
                ReleaseRaw(tracked.Volume);
                tracked.Volume = 0;
            }

            if (tracked.Meter != 0)
            {
                ReleaseRaw(tracked.Meter);
                tracked.Meter = 0;
            }
        }

        // ---- meters --------------------------------------------------------------------------

        /// <summary>
        /// [S-1223] Read every bound meter once and hand each metering widget its device's. At most one
        /// sample per period however often the worker wakes: a slider drag wakes it a hundred
        /// times a second, and the meters need ten.
        /// </summary>
        private void SamplePeaks(Subscriber[] subscribers)
        {
            var now = Environment.TickCount64;
            if (now - _lastPeaks < PeakPeriodMs - 10)
            {
                return;
            }

            _lastPeaks = now;

            var byDevice = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, output) in _outputs)
            {
                var sessions = output.Tracked.Select(pair => (pair.Key, pair.Value.GroupKey, PeakOf(pair.Value.Meter)));
                byDevice[id] = AudioRules.PeaksByKey(sessions);
            }

            foreach (var subscriber in subscribers)
            {
                var want = subscriber.Current;
                if (!want.Peaks)
                {
                    continue;
                }

                var device = AudioRules.Resolve(want.DeviceId, _devices, _defaultOutput).Id;
                var byKey = device is not null && byDevice.TryGetValue(device, out var found)
                    ? found
                    : new Dictionary<string, double>(StringComparer.Ordinal);

                if (!AudioRules.PeaksWorthSending(subscriber.PeaksSilent, byKey))
                {
                    continue;
                }

                subscriber.PeaksSilent = byKey.Values.All(static peak => peak <= 0);
                subscriber.DeliverPeaks(new AudioPeaks(byKey));
            }
        }

        private static double PeakOf(nint meter)
        {
            if (meter == 0)
            {
                return 0;
            }

            float peak;
            return ((delegate* unmanaged[Stdcall]<nint, float*, int>)Slot(meter, Slots.MeterGetPeak))(meter, &peak) >= 0
                ? AudioRules.Peak(peak)
                : 0;
        }

        // ---- writing -------------------------------------------------------------------------

        /// <summary>
        /// [S-446] Send what is waiting: mutes first, then levels, newest value per key. A key that
        /// matches nothing is not an error — the app can exit between the snapshot that drew its row
        /// and the drag that moves it, and a device can be unplugged under a slider.
        /// </summary>
        private bool ApplyWrites()
        {
            var mutes = _mutes.Drain();
            var levels = _levels.Drain();

            foreach (var (writeKey, muted) in mutes)
            {
                var (selector, key) = SplitWriteKey(writeKey);

                foreach (var target in Targets(selector, key))
                {
                    var result = target.Endpoint
                        ? ((delegate* unmanaged[Stdcall]<nint, int, Guid*, int>)Slot(target.Pointer, Slots.EndpointSetMute))(target.Pointer, muted ? 1 : 0, null)
                        : ((delegate* unmanaged[Stdcall]<nint, int, Guid*, int>)Slot(target.Pointer, Slots.SimpleSetMute))(target.Pointer, muted ? 1 : 0, null);

                    if (result < 0)
                    {
                        Note(result, "could not set mute");
                    }
                }
            }

            foreach (var (writeKey, level) in levels)
            {
                var (selector, key) = SplitWriteKey(writeKey);
                var value = (float)level;

                foreach (var target in Targets(selector, key))
                {
                    var result = target.Endpoint
                        ? ((delegate* unmanaged[Stdcall]<nint, float, Guid*, int>)Slot(target.Pointer, Slots.EndpointSetScalar))(target.Pointer, value, null)
                        : ((delegate* unmanaged[Stdcall]<nint, float, Guid*, int>)Slot(target.Pointer, Slots.SimpleSetMasterVolume))(target.Pointer, value, null);

                    if (result < 0)
                    {
                        Note(result, "could not set a level");
                    }
                }
            }

            return mutes.Count > 0 || levels.Count > 0;
        }

        /// <summary>
        /// [S-446] What a write names: a device's own master, or every session of the
        /// device whose own key or group key matches — the two key spaces are disjoint, so one write
        /// serves the grouped row and the ungrouped one without being told which.
        /// </summary>
        private IEnumerable<(nint Pointer, bool Endpoint)> Targets(string selector, string key)
        {
            var id = AudioRules.Resolve(selector.Length == 0 ? null : selector, _devices, _defaultOutput).Id;

            if (id is null || !_outputs.TryGetValue(id, out var output))
            {
                yield break;
            }

            if (key == MixerRules.MasterKey)
            {
                if (output.Volume != 0)
                {
                    yield return (output.Volume, true);
                }

                yield break;
            }

            foreach (var (sessionKey, tracked) in output.Tracked)
            {
                if (tracked.Volume != 0 && (sessionKey == key || tracked.GroupKey == key))
                {
                    yield return (tracked.Volume, false);
                }
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Devices
    // ---------------------------------------------------------------------------------------------

    /// <summary>The settings page's device list: its own apartment, its own enumerator, all released.</summary>
    private static IReadOnlyList<AudioDevice> ListOutputDevices()
    {
        var initialised = CoInitializeEx(0, CoInitMultithreaded);

        // S_FALSE (already initialised) still has to be balanced; RPC_E_CHANGED_MODE does not.
        var balance = initialised >= 0;

        try
        {
            var clsid = Iids.DeviceEnumeratorClass;
            var iid = Iids.DeviceEnumerator;
            nint enumerator;

            var created = CoCreateInstance(&clsid, 0, ClsCtxInprocServer, &iid, &enumerator);
            if (created < 0)
            {
                Log.Warn(Scope, $"could not list the output devices: 0x{created:X8}");
                return [];
            }

            try
            {
                return EnumerateOutputs(enumerator);
            }
            finally
            {
                ReleaseRaw(enumerator);
            }
        }
        finally
        {
            if (balance)
            {
                CoUninitialize();
            }
        }
    }

    /// <summary>[S-1221] The active output devices with their friendly names, in Windows' order.</summary>
    private static List<AudioDevice> EnumerateOutputs(nint enumerator)
    {
        var devices = new List<AudioDevice>();

        nint collection;
        var listed = ((delegate* unmanaged[Stdcall]<nint, int, uint, nint*, int>)Slot(enumerator, Slots.EnumeratorEnumEndpoints))(
            enumerator, DataFlowRender, DeviceStateActive, &collection);

        if (listed < 0)
        {
            Note(listed, "could not list the output devices");
            return devices;
        }

        try
        {
            uint count;
            if (((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(collection, Slots.CollectionGetCount))(collection, &count) < 0)
            {
                return devices;
            }

            for (uint i = 0; i < count; i++)
            {
                nint device;
                if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(collection, Slots.CollectionItem))(collection, i, &device) < 0)
                {
                    continue;
                }

                try
                {
                    var id = TakeString(device, Slots.DeviceGetId);
                    if (id.Length > 0)
                    {
                        devices.Add(new AudioDevice(id, FriendlyName(device)));
                    }
                }
                finally
                {
                    ReleaseRaw(device);
                }
            }
        }
        finally
        {
            ReleaseRaw(collection);
        }

        return devices;
    }

    /// <summary>The default (console role) device's id for a direction, or null when there is none.</summary>
    private static string? DefaultId(nint enumerator, int flow)
    {
        nint device;
        var found = ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Slot(enumerator, Slots.EnumeratorGetDefault))(
            enumerator, flow, RoleConsole, &device);

        if (found < 0)
        {
            // E_NOTFOUND is a machine with nothing to play sound out of (or record from): not a fault.
            Note(found, found == NotFound ? null : "no default audio device");
            return null;
        }

        try
        {
            var id = TakeString(device, Slots.DeviceGetId);
            return id.Length > 0 ? id : null;
        }
        finally
        {
            ReleaseRaw(device);
        }
    }

    /// <summary>A device by id, or 0 when it is gone. The caller releases it.</summary>
    private static nint OpenDevice(nint enumerator, string id)
    {
        nint device;
        fixed (char* text = id)
        {
            var result = ((delegate* unmanaged[Stdcall]<nint, char*, nint*, int>)Slot(enumerator, Slots.EnumeratorGetDevice))(enumerator, text, &device);
            if (result < 0)
            {
                Note(result, "could not open an audio device");
                return 0;
            }
        }

        return device;
    }

    /// <summary>An interface activated on a device, or 0. The caller releases it.</summary>
    private static nint Activate(nint device, Guid iid)
    {
        nint result;
        return ((delegate* unmanaged[Stdcall]<nint, Guid*, uint, nint, nint*, int>)Slot(device, Slots.DeviceActivate))(
            device, &iid, ClsCtxInprocServer, 0, &result) >= 0
            ? result
            : 0;
    }

    /// <summary>
    /// [S-1221] A device's friendly name from its property store, or empty. The PROPVARIANT is read
    /// as a raw block (24 bytes on x64) and cleared by <c>PropVariantClear</c> on every path.
    /// </summary>
    private static string FriendlyName(nint device)
    {
        nint store;
        if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(device, Slots.DeviceOpenPropertyStore))(device, StgmRead, &store) < 0)
        {
            return string.Empty;
        }

        try
        {
            var key = new PropertyKey { Format = Iids.DeviceFriendlyNameFormat, Id = DeviceFriendlyNameId };
            var value = stackalloc byte[PropVariantSize];
            new Span<byte>(value, PropVariantSize).Clear();

            if (((delegate* unmanaged[Stdcall]<nint, PropertyKey*, void*, int>)Slot(store, Slots.StoreGetValue))(store, &key, value) < 0)
            {
                return string.Empty;
            }

            try
            {
                var type = *(ushort*)value;
                var text = *(char**)(value + 8);
                return type == VtLpwstr && text is not null ? new string(text).Trim() : string.Empty;
            }
            finally
            {
                PropVariantClear(value);
            }
        }
        finally
        {
            ReleaseRaw(store);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }
}
