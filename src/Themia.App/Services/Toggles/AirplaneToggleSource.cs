using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3910] Airplane mode where Windows keeps it: the system-wide radio state of the Radio Management
/// service, through <c>IRadioManager</c>, the interface Windows' own Settings page and tile use (Karpo's
/// <c>RadioManager.cs</c>). One switch the service owns, not the sum of the radios: on, it takes every
/// radio down and remembers which were up; off, it brings them back.
/// <para>
/// Undocumented, out of process only: CLSID {581333F6-28DB-41BE-BC7A-FF201F12F3F6}, IID
/// {DB3AFBFB-08E6-46C6-AA70-BF9A34C30AB7}; slots 3 IsRMSupported(DWORD*), 5 GetSystemRadioState(BOOL*
/// enabled, BOOL* hardware, DWORD* reason), 6 SetSystemRadioState(BOOL enabled). "Enabled" is the radios',
/// so airplane mode is its opposite. Followed through the value the service writes when it changes
/// (HKLM\SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState), which a standard user may
/// watch. Where the service or its interface is not there: Windows has no switch for an app.
/// </para>
/// </summary>
internal sealed unsafe class AirplaneToggleSource : ToggleSource
{
    private static readonly Guid ManagerClass = new("581333F6-28DB-41BE-BC7A-FF201F12F3F6");
    private static readonly Guid ManagerInterface = new("DB3AFBFB-08E6-46C6-AA70-BF9A34C30AB7");

    private const string StateKey = @"SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState";
    private const int SlotSupported = 3;
    private const int SlotGetState = 5;
    private const int SlotSetState = 6;

    private readonly Lock _gate = new();
    private nint _manager;
    private RegistryWatch? _watch;

    public override void Start() => OffThread("finding the radio management service", () =>
    {
        var manager = ToggleNative.Create(ManagerClass, ManagerInterface, ToggleNative.ClsCtxLocalServer, out var made);
        if (manager == 0)
        {
            Log.Info(Scope, $"the radio management service is not there (0x{made:X8})");
            Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch));
            return;
        }

        uint supported = 0;
        var asked = ((delegate* unmanaged[Stdcall]<nint, uint*, int>)ToggleNative.Slot(manager, SlotSupported))(manager, &supported);
        if (asked < 0 || supported == 0)
        {
            ToggleNative.Release(manager);
            Publish(ToggleReading.Blocked(ToggleBlock.NoHardware));
            return;
        }

        var watch = RegistryWatch.Open(RegistryHive.LocalMachine, StateKey, Read);
        lock (_gate)
        {
            if (Disposed)
            {
                watch?.Dispose();
                ToggleNative.Release(manager);
                return;
            }

            _manager = manager;
            _watch = watch;
        }

        Read();
    });

    public override Task<bool> SetAsync(bool on) => OffThread("switching airplane mode", () =>
    {
        int result;
        lock (_gate)
        {
            if (_manager == 0)
            {
                return false;
            }

            // The radios' state, which is airplane mode's opposite.
            result = ((delegate* unmanaged[Stdcall]<nint, int, int>)ToggleNative.Slot(_manager, SlotSetState))(_manager, on ? 0 : 1);
        }

        if (result < 0)
        {
            Log.Warn(Scope, $"SetSystemRadioState answered 0x{result:X8}");
        }

        // The call answers once the service took it; the radios follow, and the state key says when.
        Read();
        return result >= 0;
    }, false);

    /// <summary>Asks the service where it stands; on the pool.</summary>
    private void Read()
    {
        int asked, enabled = 0, hardware = 0, reason = 0;
        lock (_gate)
        {
            if (_manager == 0 || Disposed)
            {
                return;
            }

            asked = ((delegate* unmanaged[Stdcall]<nint, int*, int*, int*, int>)ToggleNative.Slot(_manager, SlotGetState))(_manager, &enabled, &hardware, &reason);
        }

        Publish(QuickToggleRules.AirplaneFrom(asked, enabled) is { } on ? ToggleReading.Of(on) : ToggleReading.Pending);
    }

    protected override void Stop()
    {
        nint manager;
        RegistryWatch? watch;
        lock (_gate)
        {
            (manager, watch) = (_manager, _watch);
            (_manager, _watch) = (0, null);
        }

        watch?.Dispose();
        if (manager != 0)
        {
            // Let go on the pool: the proxy belongs to the multithreaded apartment it was made in.
            _ = Task.Run(() => ToggleNative.Release(manager));
        }
    }
}
