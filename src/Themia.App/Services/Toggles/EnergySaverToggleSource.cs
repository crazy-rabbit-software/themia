using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3911] Energy saver, as Karpo's switch has it (<c>EnergySaver.cs</c>, <c>Switches.cs</c>): its state is
/// <c>GUID_ENERGY_SAVER_STATUS</c> (Windows 11 24H2 and later; off, standard or high savings), which
/// Windows sends once at registration and again on every change (<c>PowerSettingRegisterNotification</c>
/// with a callback, so no window is needed). Switched through the tile's own Settings handler
/// (<c>SettingsHandlers_OneCore_BatterySaver.dll</c>, <c>SystemSettings_BatterySaver_LandingPage_OverrideControl</c>):
/// there is no public API that switches it. Where the status cannot be registered for, this Windows has
/// no energy saver to show.
/// </summary>
internal sealed unsafe class EnergySaverToggleSource : ToggleSource
{
    private const string Library = "SettingsHandlers_OneCore_BatterySaver.dll";
    private const string Id = "SystemSettings_BatterySaver_LandingPage_OverrideControl";

    private readonly Lock _gate = new();
    private GCHandle _self;
    private ToggleNative.SubscribeParameters* _parameters;
    private nint _registration;
    private SettingHandler? _handler;
    private volatile bool _looked;

    // Last status Windows sent: -1 until it has.
    private int _status = -1;

    public override void Start() => OffThread("following energy saver", () =>
    {
        var handler = SettingHandler.OpenAsync(Library, Id).GetAwaiter().GetResult();
        lock (_gate)
        {
            if (Disposed)
            {
                handler?.Release();
                return;
            }

            _handler = handler;
            _looked = true;
            _self = GCHandle.Alloc(this);
            _parameters = (ToggleNative.SubscribeParameters*)NativeMemory.Alloc((nuint)sizeof(ToggleNative.SubscribeParameters));
            _parameters->Callback = &OnSetting;
            _parameters->Context = (void*)GCHandle.ToIntPtr(_self);

            var setting = ToggleNative.EnergySaverStatus;
            nint registration = 0;
            var result = ToggleNative.PowerSettingRegisterNotification(&setting, ToggleNative.DeviceNotifyCallback, (nint)_parameters, &registration);
            if (result != 0 || registration == 0)
            {
                Log.Info(Scope, $"energy saver's status is not offered here ({result})");
            }
            else
            {
                Volatile.Write(ref _registration, registration);
            }
        }

        Read();
    });

    /// <remarks>The notification follows; the status it carries is what stands.</remarks>
    public override Task<bool> SetAsync(bool on) => _handler is { } handler ? handler.SetAsync(on) : Task.FromResult(false);

    private void Read()
    {
        if (Disposed || !_looked)
        {
            return;
        }

        if (Volatile.Read(ref _registration) == 0)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.NotOnWindows));
            return;
        }

        if (_handler is null)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch));
            return;
        }

        var status = Volatile.Read(ref _status);
        Publish(status >= 0 && QuickToggleRules.EnergySaverFrom((uint)status) is { } on ? ToggleReading.Of(on) : ToggleReading.Pending);
    }

    /// <summary>Windows' callback: a power setting changed. Nothing may escape into the power manager.</summary>
    [UnmanagedCallersOnly]
    private static uint OnSetting(void* context, uint type, void* setting)
    {
        try
        {
            if (type != ToggleNative.PbtPowerSettingChange || setting is null || context is null)
            {
                return 0;
            }

            var broadcast = (ToggleNative.PowerBroadcastSetting*)setting;
            if (broadcast->Setting != ToggleNative.EnergySaverStatus || broadcast->Length < sizeof(uint))
            {
                return 0;
            }

            if (GCHandle.FromIntPtr((nint)context).Target is EnergySaverToggleSource source)
            {
                Volatile.Write(ref source._status, (int)Math.Min(*(uint*)&broadcast->Data, int.MaxValue));
                source.Read();
            }
        }
        catch (Exception ex)
        {
            Log.Error("toggles", "an energy saver notification could not be read", ex);
        }

        return 0;
    }

    protected override void Stop()
    {
        SettingHandler? handler;
        nint registration;
        lock (_gate)
        {
            handler = _handler;
            _handler = null;
            registration = Interlocked.Exchange(ref _registration, 0);
        }

        // Outside the lock: unregistering waits for a callback in flight, which may be waiting on it.
        if (registration != 0)
        {
            var result = ToggleNative.PowerSettingUnregisterNotification(registration);
            if (result != 0)
            {
                Log.Warn(Scope, $"energy saver's notification could not be unregistered ({result})");
            }
        }

        // Only now no callback can still be reading what the registration pointed at.
        lock (_gate)
        {
            if (_parameters is not null)
            {
                NativeMemory.Free(_parameters);
                _parameters = null;
            }

            if (_self.IsAllocated)
            {
                _self.Free();
            }
        }

        handler?.Release();
    }
}
