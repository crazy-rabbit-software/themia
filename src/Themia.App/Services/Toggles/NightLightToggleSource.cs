using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3911] [S-3919] Night light, as Karpo switches it (<c>NightLight.cs</c>): through the tile's own Settings
/// handler (<c>SettingsHandlers_Display.dll</c>, <c>SystemSettings_Display_BlueLight_ManualToggleQuickAction</c>),
/// which sets it and says whether night light applies here at all. <b>Its state is the value the handler
/// writes</b>, the account's CloudStore value for blue-light reduction (<see cref="NightLightValue"/>),
/// read as it is: the handler's item, made once in the process, keeps answering what it last knew, so a
/// switch in Windows' panel, in Settings or by the sunset schedule once fired the watch while the tile
/// asked the handler and kept the old state. The handler answers only where the value does not read
/// (<see cref="NightLightFollow"/>). The value's key is watched with the key that holds it, so the
/// CloudStore writing the key again from scratch is heard too. Karpo's fall-back of rewriting the value
/// by hand is not ported: without the handler the widget says Windows does not let apps switch it here.
/// </summary>
internal sealed class NightLightToggleSource : ToggleSource
{
    private const string Library = "SettingsHandlers_Display.dll";
    private const string Id = "SystemSettings_Display_BlueLight_ManualToggleQuickAction";

    /// <summary>The key holding the value's key, watched with everything under it ([S-3921]).</summary>
    private const string Holder =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\"
        + @"default$windows.data.bluelightreduction.bluelightreductionstate";

    private const string StateKey = Holder + @"\windows.data.bluelightreduction.bluelightreductionstate";
    private const string StateValue = "Data";

    private readonly Lock _gate = new();
    private readonly NightLightFollow _follow = new();
    private SettingHandler? _handler;
    private RegistryWatch? _watch;
    private volatile bool _looked;
    private volatile bool _usable = true;

    // Whether a value not of the shape was logged already: once per run, not on every change.
    private static int _unreadable;

    public override void Start() => OffThread("finding night light's handler", () =>
    {
        var handler = SettingHandler.OpenAsync(Library, Id).GetAwaiter().GetResult();
        var watch = RegistryWatch.Open(RegistryHive.CurrentUser, Holder, () => _ = ReadAsync(), subtree: true);
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

        var before = Stored();
        var done = await handler.SetAsync(on).ConfigureAwait(false);
        if (done)
        {
            // The handler writes the value on its own time; until it moves, the tile shows where it went.
            _follow.Switched(on, before);
        }

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

        var read = BeginRead();
        if (_handler is not { } handler)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch), read);
            return;
        }

        if (!_usable)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.TurnedOff), read);
            return;
        }

        var stored = Stored();

        // The handler is only asked where the value does not read.
        var answered = stored is null ? await handler.ValueAsync().ConfigureAwait(false) : null;
        Publish(_follow.Shown(stored, answered) is { } on ? ToggleReading.Of(on) : ToggleReading.Pending, read);
    }

    /// <summary>What the value says now; null where it is not there, or (logged once) not the shape Windows writes.</summary>
    private static NightLightValue.State? Stored()
    {
        byte[]? data;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StateKey, writable: false);
            data = key?.GetValue(StateValue) as byte[];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Warn(Scope, $"cannot read night light's value: {ex.Message}");
            return null;
        }

        if (data is null)
        {
            return null;
        }

        var state = NightLightValue.Read(data);
        if (state is null && Interlocked.Exchange(ref _unreadable, 1) == 0)
        {
            Log.Warn(Scope, $"night light's value is not the shape this reads ({data.Length} bytes): its handler answers instead");
        }

        return state;
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
