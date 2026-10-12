using Microsoft.Win32;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3911] Night light, as Karpo switches it (<c>NightLight.cs</c>): through the tile's own Settings
/// handler (<c>SettingsHandlers_Display.dll</c>, <c>SystemSettings_Display_BlueLight_ManualToggleQuickAction</c>),
/// which both reads and sets it and says whether night light applies here at all. Its state is followed
/// where the handler writes it, the account's CloudStore value for blue-light reduction, watched so a
/// switch in Windows' panel or the sunset schedule shows at once; the handler is asked again on each
/// change. Karpo's fall-back of rewriting that Bond blob by hand is not ported: without the handler the
/// widget says Windows does not let apps switch it here.
/// </summary>
internal sealed class NightLightToggleSource : ToggleSource
{
    private const string Library = "SettingsHandlers_Display.dll";
    private const string Id = "SystemSettings_Display_BlueLight_ManualToggleQuickAction";

    private const string StateKey =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\"
        + @"default$windows.data.bluelightreduction.bluelightreductionstate\"
        + @"windows.data.bluelightreduction.bluelightreductionstate";

    private readonly Lock _gate = new();
    private SettingHandler? _handler;
    private RegistryWatch? _watch;
    private volatile bool _looked;
    private volatile bool _usable = true;

    public override void Start() => OffThread("finding night light's handler", () =>
    {
        var handler = SettingHandler.OpenAsync(Library, Id).GetAwaiter().GetResult();
        var watch = RegistryWatch.Open(RegistryHive.CurrentUser, StateKey, () => _ = ReadAsync());
        var kept = false;
        lock (_gate)
        {
            if (!Disposed)
            {
                (_handler, _watch, kept) = (handler, watch, true);
            }
        }

        if (!kept)
        {
            watch?.Dispose();
            handler?.Release();
            return;
        }

        _looked = true;
        ReadAsync().GetAwaiter().GetResult();
    });

    public override async Task<bool> SetAsync(bool on)
    {
        if (_handler is not { } handler)
        {
            return false;
        }

        var done = await handler.SetAsync(on).ConfigureAwait(false);
        await ReadAsync().ConfigureAwait(false);
        return done;
    }

    /// <summary>
    /// Whether night light still applies here (a display that cannot do it, a remote session): asked
    /// when the pointer comes by, not at start, because the handler only says so once its thread has
    /// run its loop for a while — and a wall-clock wait for that is not taken.
    /// </summary>
    public override void Refresh() => OffThread("asking whether night light applies", () =>
    {
        if (_handler is { } handler)
        {
            _usable = handler.UsableAsync().GetAwaiter().GetResult();
        }

        ReadAsync().GetAwaiter().GetResult();
    });

    private async Task ReadAsync()
    {
        if (Disposed || !_looked)
        {
            return;
        }

        if (_handler is not { } handler)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch));
            return;
        }

        if (!_usable)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.TurnedOff));
            return;
        }

        var value = await handler.ValueAsync().ConfigureAwait(false);
        Publish(value is { } on ? ToggleReading.Of(on) : ToggleReading.Pending);
    }

    protected override void Stop()
    {
        SettingHandler? handler;
        RegistryWatch? watch;
        lock (_gate)
        {
            (handler, watch) = (_handler, _watch);
            (_handler, _watch) = (null, null);
        }

        watch?.Dispose();
        handler?.Release();
    }
}
