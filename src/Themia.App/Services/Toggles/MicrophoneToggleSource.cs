using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3914] Microphone: the default capture endpoint's mute, through Core Audio, as Karpo's microphone
/// toggle has it (<c>MuteSwitch</c> over <c>VolumeWatch</c> with the capture flow). On is heard
/// (unmuted). Followed, not polled: the endpoint's <c>IAudioEndpointVolumeCallback</c> says when its mute
/// changes, whoever changed it (a call app, the headset's button), and the enumerator's
/// <c>IMMNotificationClient</c> when another microphone becomes the default, which is then followed.
/// <para>
/// The shared Core Audio worker serves outputs only, so this is a small watch of its own: COM in the
/// multithreaded apartment (the pool), two immortal callback objects in native memory whose only work is
/// to hand a re-read to the pool, never calling back into Core Audio from inside a notification and never
/// letting an exception out.
/// </para>
/// </summary>
internal sealed unsafe class MicrophoneToggleSource : ToggleSource
{
    private static readonly Guid EnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid EnumeratorInterface = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid EndpointVolumeInterface = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid NotificationClientInterface = new("7991EEC9-7C89-4D85-8390-6C703CEC60C0");
    private static readonly Guid VolumeCallbackInterface = new("657804FA-D6AD-4496-8A60-352752AF4F89");
    private static readonly Guid UnknownInterface = new("00000000-0000-0000-C000-000000000046");

    private const int FlowCapture = 1;
    private const int RoleConsole = 0;
    private const int NotFound = unchecked((int)0x80070490);

    private const int SlotGetDefault = 4;
    private const int SlotRegisterClient = 6;
    private const int SlotUnregisterClient = 7;
    private const int SlotActivate = 3;
    private const int SlotRegisterNotify = 3;
    private const int SlotUnregisterNotify = 4;
    private const int SlotSetMute = 14;
    private const int SlotGetMute = 15;

    /// <summary>The one source the callbacks reach (the hub makes one per toggle).</summary>
    private static volatile MicrophoneToggleSource? _current;

    private readonly Lock _gate = new();
    private nint _enumerator;
    private nint _volume;

    public override void Start()
    {
        _current = this;
        OffThread("opening the microphone", () =>
        {
            var enumerator = ToggleNative.Create(EnumeratorClass, EnumeratorInterface, ToggleNative.ClsCtxAll, out var made);
            if (enumerator == 0)
            {
                Log.Warn(Scope, $"Core Audio's device enumerator could not be made (0x{made:X8})");
                Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch));
                return;
            }

            lock (_gate)
            {
                if (Disposed)
                {
                    ToggleNative.Release(enumerator);
                    return;
                }

                _enumerator = enumerator;
                _ = ((delegate* unmanaged[Stdcall]<nint, nint, int>)ToggleNative.Slot(enumerator, SlotRegisterClient))(enumerator, Callbacks.DeviceClient);
            }

            Rebind();
        });
    }

    public override Task<bool> SetAsync(bool on) => OffThread("switching the microphone", () =>
    {
        int result;
        lock (_gate)
        {
            if (_volume == 0)
            {
                return false;
            }

            result = ((delegate* unmanaged[Stdcall]<nint, int, Guid*, int>)ToggleNative.Slot(_volume, SlotSetMute))(_volume, on ? 0 : 1, null);
        }

        if (result < 0)
        {
            Log.Warn(Scope, $"the microphone's mute answered 0x{result:X8}");
        }

        Read();
        return result >= 0;
    }, false);

    /// <summary>The default microphone again: let go of the old one, open the new one, follow it, read it.</summary>
    private void Rebind()
    {
        lock (_gate)
        {
            if (Disposed || _enumerator == 0)
            {
                return;
            }

            Unfollow();

            nint device = 0;
            var found = ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)ToggleNative.Slot(_enumerator, SlotGetDefault))(_enumerator, FlowCapture, RoleConsole, &device);
            if (found < 0 || device == 0)
            {
                if (found != NotFound)
                {
                    Log.Warn(Scope, $"the default microphone could not be found (0x{found:X8})");
                }

                Publish(ToggleReading.Blocked(ToggleBlock.NoDevice));
                return;
            }

            try
            {
                var iid = EndpointVolumeInterface;
                nint volume = 0;
                var activated = ((delegate* unmanaged[Stdcall]<nint, Guid*, uint, void*, nint*, int>)ToggleNative.Slot(device, SlotActivate))(
                    device, &iid, ToggleNative.ClsCtxAll, null, &volume);
                if (activated < 0 || volume == 0)
                {
                    Log.Warn(Scope, $"the microphone's volume could not be opened (0x{activated:X8})");
                    Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch));
                    return;
                }

                _volume = volume;
                _ = ((delegate* unmanaged[Stdcall]<nint, nint, int>)ToggleNative.Slot(volume, SlotRegisterNotify))(volume, Callbacks.VolumeCallback);
            }
            finally
            {
                ToggleNative.Release(device);
            }
        }

        Read();
    }

    private void Read()
    {
        int result, muted = 0;
        lock (_gate)
        {
            if (Disposed || _volume == 0)
            {
                return;
            }

            result = ((delegate* unmanaged[Stdcall]<nint, int*, int>)ToggleNative.Slot(_volume, SlotGetMute))(_volume, &muted);
        }

        Publish(result >= 0 ? ToggleReading.Of(muted == 0) : ToggleReading.Pending);
    }

    /// <summary>Under the lock.</summary>
    private void Unfollow()
    {
        if (_volume != 0)
        {
            _ = ((delegate* unmanaged[Stdcall]<nint, nint, int>)ToggleNative.Slot(_volume, SlotUnregisterNotify))(_volume, Callbacks.VolumeCallback);
            ToggleNative.Release(_volume);
            _volume = 0;
        }
    }

    protected override void Stop()
    {
        if (ReferenceEquals(_current, this))
        {
            _current = null;
        }

        nint enumerator;
        nint volume;
        lock (_gate)
        {
            (enumerator, volume) = (_enumerator, _volume);
            (_enumerator, _volume) = (0, 0);
        }

        // On the pool: the objects belong to the multithreaded apartment they were made in.
        _ = Task.Run(() =>
        {
            try
            {
                if (volume != 0)
                {
                    _ = ((delegate* unmanaged[Stdcall]<nint, nint, int>)ToggleNative.Slot(volume, SlotUnregisterNotify))(volume, Callbacks.VolumeCallback);
                    ToggleNative.Release(volume);
                }

                if (enumerator != 0)
                {
                    _ = ((delegate* unmanaged[Stdcall]<nint, nint, int>)ToggleNative.Slot(enumerator, SlotUnregisterClient))(enumerator, Callbacks.DeviceClient);
                    ToggleNative.Release(enumerator);
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the microphone could not be let go", ex);
            }
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    /// <summary>
    /// The two objects Core Audio calls back: a pointer to a vtable in native memory, allocated once
    /// and never freed (reference counting is a no-op), so a late callback during teardown is harmless.
    /// </summary>
    private static class Callbacks
    {
        public static readonly nint DeviceClient = Build(
        [
            (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&QueryClient,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, char*, uint, int>)&OnDeviceStateChanged,
            (nint)(delegate* unmanaged<nint, char*, int>)&OnDeviceAdded,
            (nint)(delegate* unmanaged<nint, char*, int>)&OnDeviceRemoved,
            (nint)(delegate* unmanaged<nint, int, int, char*, int>)&OnDefaultDeviceChanged,
            (nint)(delegate* unmanaged<nint, char*, PropertyKey, int>)&OnPropertyValueChanged,
        ]);

        public static readonly nint VolumeCallback = Build(
        [
            (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&QueryVolume,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, nint, int>)&OnNotify,
        ]);

        private static nint Build(nint[] methods)
        {
            var table = (nint*)NativeMemory.Alloc((nuint)(methods.Length * sizeof(nint)));
            for (var i = 0; i < methods.Length; i++)
            {
                table[i] = methods[i];
            }

            var instance = (nint*)NativeMemory.Alloc((nuint)sizeof(nint));
            *instance = (nint)table;
            return (nint)instance;
        }

        private static int Answer(nint self, Guid* iid, nint* result, Guid own)
        {
            if (result is null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            if (*iid == UnknownInterface || *iid == own)
            {
                *result = self;
                return 0;
            }

            *result = 0;
            return unchecked((int)0x80004002); // E_NOINTERFACE
        }

        [UnmanagedCallersOnly]
        private static int QueryClient(nint self, Guid* iid, nint* result) => Answer(self, iid, result, NotificationClientInterface);

        [UnmanagedCallersOnly]
        private static int QueryVolume(nint self, Guid* iid, nint* result) => Answer(self, iid, result, VolumeCallbackInterface);

        [UnmanagedCallersOnly]
        private static uint Immortal(nint self) => 1;

        /// <summary>Hand the work to the pool and return; nothing that can throw runs outside the try.</summary>
        private static int Hand(bool rebind)
        {
            try
            {
                if (_current is { } source)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            if (rebind)
                            {
                                source.Rebind();
                            }
                            else
                            {
                                source.Read();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("toggles", "the microphone could not be read", ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error("toggles", "a microphone notification could not be passed on", ex);
            }

            return 0;
        }

        [UnmanagedCallersOnly]
        private static int OnDeviceStateChanged(nint self, char* id, uint state) => Hand(rebind: true);

        [UnmanagedCallersOnly]
        private static int OnDeviceAdded(nint self, char* id) => 0;

        [UnmanagedCallersOnly]
        private static int OnDeviceRemoved(nint self, char* id) => Hand(rebind: true);

        [UnmanagedCallersOnly]
        private static int OnDefaultDeviceChanged(nint self, int flow, int role, char* id)
            => flow == FlowCapture && role == RoleConsole ? Hand(rebind: true) : 0;

        [UnmanagedCallersOnly]
        private static int OnPropertyValueChanged(nint self, char* id, PropertyKey key) => 0;

        [UnmanagedCallersOnly]
        private static int OnNotify(nint self, nint data) => Hand(rebind: false);
    }
}
