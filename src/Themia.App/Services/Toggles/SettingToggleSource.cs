using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3915] Where one of the DWORD settings is switched and kept (Karpo's <c>SettingToggles.cs</c>):
/// switched through the Settings handler Windows' own switch goes through (which applies it at once,
/// where writing the value alone would wait for the next sign-in), and read where the handler writes it.
/// </summary>
internal sealed record SettingPlace(string Library, string Id, string Key, string Value, bool Absent)
{
    public static readonly SettingPlace GameMode = new(
        "SettingsHandlers_Gaming.dll", "SystemSettings_Gaming_GameMode_Toggle",
        @"Software\Microsoft\GameBar", "AutoGameModeEnabled", Absent: true);

    public static readonly SettingPlace ColorFilters = new(
        "SettingsHandlers_nt.dll", "SystemSettings_Accessibility_ColorFiltering_IsEnabled",
        @"Software\Microsoft\ColorFiltering", "Active", Absent: false);

    public static readonly SettingPlace MonoAudio = new(
        "SettingsHandlers_Accessibility.dll", "SystemSettings_Accessibility_IsAudioMonoMixStateEnabled",
        @"Software\Microsoft\Multimedia\Audio", "AccessibilityMonoMixState", Absent: false);
}

/// <summary>
/// [S-3915] Game mode, color filters or mono audio: on and off through the tile's handler, followed
/// through the HKCU value Windows keeps (watched, so Win+Ctrl+C or Settings show at once). Without
/// the handler it cannot be switched, and says so.
/// </summary>
internal sealed class SettingToggleSource(SettingPlace place) : ToggleSource
{
    private readonly Lock _gate = new();
    private SettingHandler? _handler;
    private RegistryWatch? _watch;
    private bool _usable = true;
    private volatile bool _looked;

    public override void Start() => OffThread("finding its handler", () =>
    {
        var handler = SettingHandler.OpenAsync(place.Library, place.Id).GetAwaiter().GetResult();
        var watch = RegistryWatch.Open(RegistryHive.CurrentUser, place.Key, Read);
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
            handler?.Release();
            watch?.Dispose();
            return;
        }

        _looked = true;
        Read();
    });

    public override async Task<bool> SetAsync(bool on)
    {
        if (_handler is not { } handler)
        {
            return false;
        }

        var done = await handler.SetAsync(on).ConfigureAwait(false);
        Read();
        return done;
    }

    /// <summary>Whether Windows still offers it (the handler may say it no longer applies), then the value.</summary>
    public override void Refresh() => OffThread("asking whether it applies", () =>
    {
        if (_handler is { } handler)
        {
            _usable = handler.UsableAsync().GetAwaiter().GetResult();
        }

        Read();
    });

    private void Read()
    {
        if (Disposed || !_looked)
        {
            return;
        }

        if (_handler is null)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.NoSwitch));
            return;
        }

        if (!_usable)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.TurnedOff));
            return;
        }

        object? value;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(place.Key, writable: false);
            value = key?.GetValue(place.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Warn(Scope, $"cannot read {place.Value}: {ex.Message}");
            Publish(ToggleReading.Pending);
            return;
        }

        Publish(QuickToggleRules.DwordSetting(value, place.Absent) is { } on ? ToggleReading.Of(on) : ToggleReading.Pending);
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
