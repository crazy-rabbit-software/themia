using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3912] Rotation lock, as Karpo has it (<c>Rotation.cs</c>): the state is <c>GetAutoRotationState</c>,
/// documented and read as Windows' own handler reads it (<see cref="QuickToggleRules.Rotation"/>); the
/// switch is <c>SetAutoRotation</c>, which user32 exports by ordinal only (2507, as Windows' handler and
/// twinui import it): TRUE lets the screen turn, FALSE locks it. Windows keeps the state under
/// HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AutoRotation and reads it again when that key changes;
/// so does this. A PC whose screen does not turn shows the widget as having no such hardware.
/// </summary>
internal sealed unsafe class RotationToggleSource : ToggleSource
{
    private const string Key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AutoRotation";
    private const int SetAutoRotationOrdinal = 2507;

    private readonly Lock _gate = new();
    private RegistryWatch? _watch;

    public override void Start() => OffThread("following rotation", () =>
    {
        var watch = RegistryWatch.Open(RegistryHive.LocalMachine, Key, Read);
        lock (_gate)
        {
            if (Disposed)
            {
                watch?.Dispose();
                return;
            }

            _watch = watch;
        }

        Read();
    });

    /// <summary>Docking, a second screen or folding a convertible change it without touching the key.</summary>
    public override void Refresh() => OffThread("reading rotation", Read);

    public override Task<bool> SetAsync(bool on) => OffThread("switching rotation lock", () =>
    {
        var user32 = ToggleNative.GetModuleHandle("user32.dll");
        var export = user32 == 0 ? 0 : ToggleNative.GetProcAddressOrdinal(user32, SetAutoRotationOrdinal);
        if (export == 0)
        {
            Log.Warn(Scope, "user32 has no SetAutoRotation here");
            return false;
        }

        // Locked is "may not turn". Its answer is not a result: Windows' own handler ignores it too.
        _ = ((delegate* unmanaged[Stdcall]<int, int>)export)(on ? 0 : 1);
        Read();
        return Reading.On == on;
    }, false);

    private void Read()
    {
        if (Disposed)
        {
            return;
        }

        uint state = 0;
        Publish(QuickToggleRules.Rotation(ToggleNative.GetAutoRotationState(&state) ? state : null));
    }

    protected override void Stop()
    {
        RegistryWatch? watch;
        lock (_gate)
        {
            watch = _watch;
            _watch = null;
        }

        watch?.Dispose();
    }
}
