using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-3201]..[S-3204] The spectrum of whatever the PC is playing: a WASAPI loopback capture of the
/// default output device, analysed into <see cref="Spectrum.Bands"/> levels about thirty times a
/// second, shared by every audio visualizer. [S-3214] Each frame carries the waveform's trace
/// (<see cref="Oscilloscope"/>) beside the bands, read from the same analysis window: one capture
/// for all six designs.
/// <para>
/// <b>Cheap by construction.</b> One worker thread for all tiles, started by the first subscriber and
/// stopped by the last ([S-3203]); a tile subscribes only while it is on screen, so a hidden view,
/// a preview or game mode (which detaches every widget) costs nothing. In silence the worker
/// delivers nothing once the bars have fallen and drains at the slower idle cadence ([S-3202]), so
/// the UI thread does no work either.
/// </para>
/// <para>
/// <b>AOT.</b> Hand-rolled COM vtable calls, as in <see cref="CoreAudio"/>: no interop assembly, no
/// runtime marshalling stubs. Every interface pointer and the mix format are released on every path.
/// </para>
/// <para>
/// <b>Self-healing</b> ([S-3204]): switching the output device, a format change or an invalidated
/// device ends the capture, and the worker re-acquires the new default endpoint after a short pause
/// instead of freezing the bars until restart.
/// </para>
/// </summary>
internal static unsafe partial class AudioLoopback
{
    private const string Scope = "loopback";

    private static readonly Lock Gate = new();
    private static readonly CaptureLease Lease = new();
    private static Subscriber[] _subscribers = [];
    private static Worker? _worker;

    /// <summary>
    /// Receive frames (the bands, 0..1, and the trace, −1..1: <see cref="AudioFrame"/>) on the capture
    /// thread until the returned handle is disposed. A frame is shared between subscribers: read it,
    /// do not keep or change it. <paramref name="onAvailability"/> hears whether the output device can be
    /// captured ([S-3204]): on every change, and at once when a running worker already failed. Both
    /// arrive on the capture thread (or the caller's). Call from any thread.
    /// </summary>
    public static IDisposable Subscribe(Action<AudioFrame> onFrame, Action<bool> onAvailability)
    {
        ArgumentNullException.ThrowIfNull(onFrame);
        ArgumentNullException.ThrowIfNull(onAvailability);

        var subscriber = new Subscriber(onFrame, onAvailability);
        bool? known;

        lock (Gate)
        {
            _subscribers = [.. _subscribers, subscriber];

            if (Lease.Add() == CaptureAction.Start)
            {
                _worker = Worker.Start();
            }

            known = Lease.Available;
        }

        if (known == false)
        {
            Notify(subscriber, available: false);
        }

        return new Subscription(subscriber);
    }

    /// <summary>The worker reached the device, or failed to: tell every subscriber once per change.</summary>
    private static void ReportAvailability(Worker worker, bool available)
    {
        Subscriber[] told;

        lock (Gate)
        {
            if (!ReferenceEquals(_worker, worker) || !Lease.Report(available))
            {
                return;
            }

            told = _subscribers;
        }

        foreach (var subscriber in told)
        {
            Notify(subscriber, available);
        }
    }

    private static void Notify(Subscriber subscriber, bool available)
    {
        try
        {
            subscriber.OnAvailability(available);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a spectrum subscriber threw on an availability change", ex);
        }
    }

    private static void Unsubscribe(Subscriber subscriber)
    {
        lock (Gate)
        {
            var index = Array.IndexOf(_subscribers, subscriber);

            if (index < 0)
            {
                return;
            }

            _subscribers = [.. _subscribers[..index], .. _subscribers[(index + 1)..]];

            if (Lease.Remove() == CaptureAction.Stop)
            {
                // Signalled, not joined: the worker notices within a frame and releases its handles
                // on its own thread, so the UI thread never waits on Core Audio.
                _worker?.Stop();
                _worker = null;
            }
        }
    }

    private static void Deliver(AudioFrame frame)
    {
        foreach (var subscriber in Volatile.Read(ref _subscribers))
        {
            try
            {
                subscriber.OnFrame(frame);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a spectrum subscriber threw", ex);
            }
        }
    }

    /// <summary>One subscription's callbacks. A class with reference identity, so two identical lambdas are two subscribers.</summary>
    private sealed class Subscriber(Action<AudioFrame> onFrame, Action<bool> onAvailability)
    {
        public Action<AudioFrame> OnFrame { get; } = onFrame;

        public Action<bool> OnAvailability { get; } = onAvailability;
    }

    private sealed class Subscription(Subscriber subscriber) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Unsubscribe(subscriber);
            }
        }
    }

    // ---- the worker -----------------------------------------------------------------------------

    private sealed class Worker
    {
        /// <summary>How long a failed capture waits before trying the (possibly new) default device again.</summary>
        private const int RetryMs = 500;

        /// <summary>How far a bar falls per frame ([S-3202]): from full to nothing in about half a second.</summary>
        private const float Fall = 0.06f;

        private readonly ManualResetEvent _stop = new(false);

        public static Worker Start()
        {
            var worker = new Worker();
            var thread = new Thread(worker.Run) { IsBackground = true, Name = "Themia loopback capture" };
            thread.Start();
            return worker;
        }

        public void Stop() => _stop.Set();

        private bool Stopped => _stop.WaitOne(0);

        private void Run()
        {
            var com = CoInitializeEx(0, CoInitMultithreaded);

            try
            {
                var lastFailure = 0;

                while (!Stopped)
                {
                    var result = Capture();

                    if (Stopped)
                    {
                        break;
                    }

                    // A device switch is not a failure: go straight to the new default.
                    if (result == RetryNow)
                    {
                        continue;
                    }

                    ReportAvailability(this, available: false);

                    if (result != lastFailure)
                    {
                        lastFailure = result;
                        Log.Warn(Scope, $"loopback capture stopped (0x{result:X8}); retrying");
                    }

                    _stop.WaitOne(RetryMs);
                }
            }
            catch (Exception ex)
            {
                // A thread of our own: an exception escaping it ends the process.
                Log.Error(Scope, "the loopback capture failed", ex);
            }
            finally
            {
                if (com >= 0)
                {
                    CoUninitialize();
                }

                lock (Gate)
                {
                    if (ReferenceEquals(_worker, this))
                    {
                        Lease.WorkerEnded();
                        _worker = null;
                    }
                }

                _stop.Dispose();
            }
        }

        /// <summary>One capture session on the current default device, until stopped, switched or broken.</summary>
        private int Capture()
        {
            nint enumerator = 0, device = 0, client = 0, capture = 0;
            WaveFormat* format = null;
            var registered = false;

            try
            {
                var clsid = DeviceEnumeratorClass;
                var iid = DeviceEnumeratorIid;
                var hr = CoCreateInstance(&clsid, 0, ClsCtxAll, &iid, &enumerator);
                if (hr < 0)
                {
                    return hr;
                }

                Interlocked.Exchange(ref _endpointChanged, 0);
                registered = Call(enumerator, SlotRegisterClient, NotificationClient) >= 0;

                hr = ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Slot(enumerator, SlotGetDefault))(enumerator, DataFlowRender, RoleConsole, &device);
                if (hr < 0)
                {
                    return hr;
                }

                var clientIid = AudioClientIid;
                hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, uint, void*, nint*, int>)Slot(device, SlotActivate))(device, &clientIid, ClsCtxAll, null, &client);
                if (hr < 0)
                {
                    return hr;
                }

                hr = ((delegate* unmanaged[Stdcall]<nint, WaveFormat**, int>)Slot(client, SlotGetMixFormat))(client, &format);
                if (hr < 0 || format is null)
                {
                    return hr < 0 ? hr : NoFormat;
                }

                var encoding = Encoding(format);
                var channels = Math.Max(1, (int)format->Channels);
                var blockAlign = Math.Max(1, (int)format->BlockAlign);

                hr = ((delegate* unmanaged[Stdcall]<nint, int, uint, long, long, WaveFormat*, Guid*, int>)Slot(client, SlotInitialize))(
                    client, ShareModeShared, StreamFlagsLoopback, BufferDuration, 0, format, null);
                if (hr < 0)
                {
                    return hr;
                }

                var captureIid = CaptureClientIid;
                hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(client, SlotGetService))(client, &captureIid, &capture);
                if (hr < 0)
                {
                    return hr;
                }

                hr = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(client, SlotStart))(client);
                if (hr < 0)
                {
                    return hr;
                }

                ReportAvailability(this, available: true);

                try
                {
                    return Pump(capture, encoding, channels, blockAlign);
                }
                finally
                {
                    // ignore: stopping a stream on a device that has gone fails, and it is released next either way.
                    _ = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(client, SlotStop))(client);
                }
            }
            finally
            {
                Release(capture);
                Release(client);
                Release(device);

                if (format is not null)
                {
                    CoTaskMemFree(format);
                }

                if (registered)
                {
                    // ignore: unregistering from an enumerator that is being released cannot fail usefully.
                    _ = Call(enumerator, SlotUnregisterClient, NotificationClient);
                }

                Release(enumerator);
            }
        }

        /// <summary>Drain packets, analyse, deliver; once a frame until something ends it.</summary>
        private int Pump(nint capture, SampleEncoding encoding, int channels, int blockAlign)
        {
            var spectrum = new Spectrum();
            var window = new float[Spectrum.FftSize];
            var filled = 0;
            var fresh = new float[Spectrum.Bands];
            var shown = new float[Spectrum.Bands];
            var trace = new float[Oscilloscope.Points];
            var shownTrace = new float[Oscilloscope.Points];
            var scratch = new float[4096];
            var lit = false;
            var wait = Spectrum.FrameMs;

            while (!_stop.WaitOne(wait))
            {
                if (Interlocked.Exchange(ref _endpointChanged, 0) != 0)
                {
                    return RetryNow;
                }

                var arrived = false;
                uint packet;
                var hr = ((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(capture, SlotGetNextPacketSize))(capture, &packet);

                while (hr >= 0 && packet > 0)
                {
                    byte* data;
                    uint frames;
                    uint flags;
                    hr = ((delegate* unmanaged[Stdcall]<nint, byte**, uint*, uint*, ulong*, ulong*, int>)Slot(capture, SlotGetBuffer))(capture, &data, &frames, &flags, null, null);

                    if (hr < 0)
                    {
                        return hr;
                    }

                    try
                    {
                        var count = (int)Math.Min(frames, (uint)scratch.Length);
                        var bytes = new ReadOnlySpan<byte>(data, data is null ? 0 : count * blockAlign);
                        var written = Spectrum.MixDown(bytes, encoding, channels, count, (flags & BufferFlagsSilent) != 0, scratch);
                        Append(window, ref filled, scratch.AsSpan(0, written));
                        arrived |= written > 0;
                    }
                    finally
                    {
                        hr = ((delegate* unmanaged[Stdcall]<nint, uint, int>)Slot(capture, SlotReleaseBuffer))(capture, frames);
                    }

                    if (hr < 0)
                    {
                        return hr;
                    }

                    hr = ((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(capture, SlotGetNextPacketSize))(capture, &packet);
                }

                if (hr < 0)
                {
                    return hr;
                }

                // Loopback delivers no packets at all while nothing plays: the bars fall to silence
                // and then nothing more is sent, so an idle desktop costs the UI thread nothing.
                if (arrived)
                {
                    spectrum.Analyze(window.AsSpan(Spectrum.FftSize - filled), fresh);

                    // [S-3214] The waveform from the same window: the trigger finds its own start.
                    Oscilloscope.Trace(window, trace);
                }
                else
                {
                    Array.Clear(fresh);
                }

                Spectrum.Settle(shown, fresh, Fall);
                var moving = Oscilloscope.Settle(shownTrace, trace, arrived);

                var any = moving || Array.Exists(shown, level => level > 0);
                if (any || lit)
                {
                    Deliver(new AudioFrame((float[])shown.Clone(), (float[])shownTrace.Clone()));
                }

                lit = any;
                wait = Spectrum.FrameWait(arrived, lit);
            }

            return 0;
        }

        /// <summary>Slide new samples into the analysis window, keeping the newest.</summary>
        private static void Append(float[] window, ref int filled, ReadOnlySpan<float> samples)
        {
            if (samples.Length >= window.Length)
            {
                samples[^window.Length..].CopyTo(window);
                filled = window.Length;
                return;
            }

            window.AsSpan(samples.Length).CopyTo(window);
            samples.CopyTo(window.AsSpan(window.Length - samples.Length));
            filled = Math.Min(window.Length, filled + samples.Length);
        }

        private static SampleEncoding Encoding(WaveFormat* format)
        {
            var tag = format->Tag;

            if (tag == FormatExtensible && format->Size >= 22)
            {
                var sub = ((WaveFormatExtensible*)format)->SubFormat;
                tag = (ushort)(sub == FloatSubFormat ? FormatFloat : sub == PcmSubFormat ? FormatPcm : 0);
            }

            return (tag, format->BitsPerSample) switch
            {
                (FormatFloat, 32) => SampleEncoding.Float32,
                (FormatPcm, 16) => SampleEncoding.Int16,
                (FormatPcm, 32) => SampleEncoding.Int32,
                _ => SampleEncoding.Unsupported,
            };
        }
    }

    // ---- the default-device watch --------------------------------------------------------------

    /// <summary>Set by the notification client when the default output changes; read by the worker.</summary>
    private static int _endpointChanged;

    /// <summary>
    /// An immortal <c>IMMNotificationClient</c>: a pointer to a vtable in native memory, allocated
    /// once. Reference counting is a no-op, so a late callback during teardown is harmless. A callback
    /// only sets a flag; it never calls back into Core Audio and never lets an exception escape.
    /// </summary>
    private static readonly nint NotificationClient = BuildClient();

    private static nint BuildClient()
    {
        nint[] methods =
        [
            (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&QueryClient,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, char*, uint, int>)&OnDeviceStateChanged,
            (nint)(delegate* unmanaged<nint, char*, int>)&OnDeviceAddedOrRemoved,
            (nint)(delegate* unmanaged<nint, char*, int>)&OnDeviceAddedOrRemoved,
            (nint)(delegate* unmanaged<nint, int, int, char*, int>)&OnDefaultDeviceChanged,
            (nint)(delegate* unmanaged<nint, char*, PropertyKey, int>)&OnPropertyValueChanged,
        ];

        var table = (nint*)NativeMemory.Alloc((nuint)(methods.Length * sizeof(nint)));
        for (var i = 0; i < methods.Length; i++)
        {
            table[i] = methods[i];
        }

        var instance = (nint*)NativeMemory.Alloc((nuint)sizeof(nint));
        *instance = (nint)table;
        return (nint)instance;
    }

    [UnmanagedCallersOnly]
    private static int QueryClient(nint self, Guid* iid, nint* result)
    {
        if (result is null)
        {
            return unchecked((int)0x80004003); // E_POINTER
        }

        if (*iid == UnknownIid || *iid == NotificationClientIid)
        {
            *result = self;
            return 0;
        }

        *result = 0;
        return unchecked((int)0x80004002); // E_NOINTERFACE
    }

    [UnmanagedCallersOnly]
    private static uint Immortal(nint self) => 1;

    [UnmanagedCallersOnly]
    private static int OnDeviceStateChanged(nint self, char* id, uint state) => 0;

    [UnmanagedCallersOnly]
    private static int OnDeviceAddedOrRemoved(nint self, char* id) => 0;

    [UnmanagedCallersOnly]
    private static int OnPropertyValueChanged(nint self, char* id, PropertyKey key) => 0;

    /// <summary>The console render default moved (headphones plugged in, output switched): re-acquire.</summary>
    [UnmanagedCallersOnly]
    private static int OnDefaultDeviceChanged(nint self, int flow, int role, char* id)
    {
        if (flow == DataFlowRender && role == RoleConsole)
        {
            Interlocked.Exchange(ref _endpointChanged, 1);
        }

        return 0;
    }

    // ---- COM plumbing ---------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort Tag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormatExtensible
    {
        public WaveFormat Format;
        public ushort ValidBitsPerSample;
        public uint ChannelMask;
        public Guid SubFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    private const uint CoInitMultithreaded = 0x0;
    private const uint ClsCtxAll = 0x17;
    private const int DataFlowRender = 0;
    private const int RoleConsole = 0;
    private const int ShareModeShared = 0;
    private const uint StreamFlagsLoopback = 0x00020000;
    private const uint BufferFlagsSilent = 0x2;

    /// <summary>200 ms, in 100-ns units: room for several frames between drains.</summary>
    private const long BufferDuration = 2_000_000;

    private const ushort FormatPcm = 1;
    private const ushort FormatFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;

    /// <summary>Not an HRESULT Windows returns: the worker's own "device switched, go again".</summary>
    private const int RetryNow = 1;

    /// <summary>GetMixFormat answered without a format.</summary>
    private const int NoFormat = unchecked((int)0x80004005);

    private const int SlotRelease = 2;
    private const int SlotGetDefault = 4;
    private const int SlotRegisterClient = 6;
    private const int SlotUnregisterClient = 7;
    private const int SlotActivate = 3;
    private const int SlotInitialize = 3;
    private const int SlotGetMixFormat = 8;
    private const int SlotStart = 10;
    private const int SlotStop = 11;
    private const int SlotGetService = 14;
    private const int SlotGetBuffer = 3;
    private const int SlotReleaseBuffer = 4;
    private const int SlotGetNextPacketSize = 5;

    private static readonly Guid DeviceEnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid DeviceEnumeratorIid = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid AudioClientIid = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid CaptureClientIid = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
    private static readonly Guid NotificationClientIid = new("7991EEC9-7C89-4D85-8390-6C703CEC60C0");
    private static readonly Guid UnknownIid = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");

    private static nint Slot(nint com, int index) => (*(nint**)com)[index];

    private static int Call(nint com, int slot, nint arg)
        => ((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(com, slot))(com, arg);

    private static void Release(nint com)
    {
        if (com != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(com, SlotRelease))(com);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint flags);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(void* memory);
}
