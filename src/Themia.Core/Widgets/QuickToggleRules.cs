using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-3903] Why a toggle cannot be switched right now. <see cref="None"/> is a toggle that can;
/// <see cref="Reading"/> is one whose state has not come back from Windows yet (no state shown, a
/// click does nothing, nothing is dimmed); every other value draws the tile dimmed with its reason
/// in the tooltip.
/// </summary>
public enum ToggleBlock
{
    None = 0,

    /// <summary>Asked, not answered yet.</summary>
    Reading = 1,

    /// <summary>The PC does not have the thing: no Wi-Fi or Bluetooth radio, no sensor that turns the screen.</summary>
    NoHardware = 2,

    /// <summary>No audio endpoint to mute: no output, no microphone.</summary>
    NoDevice = 3,

    /// <summary>This Windows has no such setting (energy saver before Windows 11 24H2).</summary>
    NotOnWindows = 4,

    /// <summary>The PC has it but Windows will not switch it now: rotation while docked or on several screens.</summary>
    NotNow = 5,

    /// <summary>Windows' own switch for it is not there for an app (the interface or the Settings handler is missing).</summary>
    NoSwitch = 6,

    /// <summary>The output in use cannot do it (spatial sound).</summary>
    NotSupported = 7,

    /// <summary>Windows says the setting does not apply, or policy holds it.</summary>
    TurnedOff = 8,
}

/// <summary>
/// [S-3903] What a toggle source last read: whether the thing is on, and why it cannot be switched.
/// <see cref="On"/> is null while it is not known.
/// </summary>
public readonly record struct ToggleReading(bool? On, ToggleBlock Block)
{
    /// <summary>Nothing answered yet: what every source starts as.</summary>
    public static ToggleReading Pending { get; } = new(null, ToggleBlock.Reading);

    public static ToggleReading Of(bool on) => new(on, ToggleBlock.None);

    public static ToggleReading Blocked(ToggleBlock block) => new(null, block);

    /// <summary>A click can switch it: known, and nothing in the way.</summary>
    public bool CanSwitch => Block == ToggleBlock.None && On is not null;
}

/// <summary>[S-3904] What the tile shows.</summary>
public enum ToggleLook
{
    /// <summary>State not known yet: the off look without a state line, inert.</summary>
    Unknown = 0,
    Off = 1,

    /// <summary>The accent-filled tile.</summary>
    On = 2,

    /// <summary>Dimmed, inert, the reason in the tooltip.</summary>
    Unavailable = 3,
}

/// <summary>
/// The quick-setting toggles' rules: which kind is which toggle, their names, the decoders of what
/// Windows answers (kept here so they are tested without a radio), and how a state and a reason read.
/// </summary>
public static class QuickToggleRules
{
    /// <summary>[S-3900] Every toggle, in the order the picker lists their widgets (Karpo's Quick settings order).</summary>
    public static IReadOnlyList<QuickToggle> All { get; } =
    [
        QuickToggle.Wifi,
        QuickToggle.Bluetooth,
        QuickToggle.AirplaneMode,
        QuickToggle.EnergySaver,
        QuickToggle.NightLight,
        QuickToggle.RotationLock,
        QuickToggle.DoNotDisturb,
        QuickToggle.Sound,
        QuickToggle.Microphone,
        QuickToggle.SpatialSound,
        QuickToggle.MonoAudio,
        QuickToggle.ColorFilters,
        QuickToggle.GameMode,
    ];

    /// <summary>[S-3900] The toggle a kind stands for, or null for a kind that is not a toggle.</summary>
    public static QuickToggle? ForKind(WidgetKind kind) => kind switch
    {
        WidgetKind.WifiToggle => QuickToggle.Wifi,
        WidgetKind.BluetoothToggle => QuickToggle.Bluetooth,
        WidgetKind.AirplaneModeToggle => QuickToggle.AirplaneMode,
        WidgetKind.EnergySaverToggle => QuickToggle.EnergySaver,
        WidgetKind.NightLightToggle => QuickToggle.NightLight,
        WidgetKind.RotationLockToggle => QuickToggle.RotationLock,
        WidgetKind.DoNotDisturbToggle => QuickToggle.DoNotDisturb,
        WidgetKind.SoundToggle => QuickToggle.Sound,
        WidgetKind.MicrophoneToggle => QuickToggle.Microphone,
        WidgetKind.SpatialSoundToggle => QuickToggle.SpatialSound,
        WidgetKind.MonoAudioToggle => QuickToggle.MonoAudio,
        WidgetKind.ColorFiltersToggle => QuickToggle.ColorFilters,
        WidgetKind.GameModeToggle => QuickToggle.GameMode,
        _ => null,
    };

    /// <summary>[S-3900] The widget kind of a toggle: the inverse of <see cref="ForKind"/>.</summary>
    public static WidgetKind KindOf(QuickToggle toggle) => toggle switch
    {
        QuickToggle.Wifi => WidgetKind.WifiToggle,
        QuickToggle.Bluetooth => WidgetKind.BluetoothToggle,
        QuickToggle.AirplaneMode => WidgetKind.AirplaneModeToggle,
        QuickToggle.EnergySaver => WidgetKind.EnergySaverToggle,
        QuickToggle.NightLight => WidgetKind.NightLightToggle,
        QuickToggle.RotationLock => WidgetKind.RotationLockToggle,
        QuickToggle.DoNotDisturb => WidgetKind.DoNotDisturbToggle,
        QuickToggle.Sound => WidgetKind.SoundToggle,
        QuickToggle.Microphone => WidgetKind.MicrophoneToggle,
        QuickToggle.SpatialSound => WidgetKind.SpatialSoundToggle,
        QuickToggle.MonoAudio => WidgetKind.MonoAudioToggle,
        QuickToggle.ColorFilters => WidgetKind.ColorFiltersToggle,
        QuickToggle.GameMode => WidgetKind.GameModeToggle,
        _ => throw new ArgumentOutOfRangeException(nameof(toggle), toggle, "not a quick-setting toggle"),
    };

    /// <summary>[S-3901] The name under the icon: the thing, never "on/off" ("Microphone").</summary>
    public static Str NameId(QuickToggle toggle) => toggle switch
    {
        QuickToggle.Wifi => Str.Toggles_Wifi_Name,
        QuickToggle.Bluetooth => Str.Toggles_Bluetooth_Name,
        QuickToggle.AirplaneMode => Str.Toggles_AirplaneMode_Name,
        QuickToggle.EnergySaver => Str.Toggles_EnergySaver_Name,
        QuickToggle.NightLight => Str.Toggles_NightLight_Name,
        QuickToggle.RotationLock => Str.Toggles_RotationLock_Name,
        QuickToggle.DoNotDisturb => Str.Toggles_DoNotDisturb_Name,
        QuickToggle.Sound => Str.Toggles_Sound_Name,
        QuickToggle.Microphone => Str.Toggles_Microphone_Name,
        QuickToggle.SpatialSound => Str.Toggles_SpatialSound_Name,
        QuickToggle.MonoAudio => Str.Toggles_MonoAudio_Name,
        QuickToggle.ColorFilters => Str.Toggles_ColorFilters_Name,
        _ => Str.Toggles_GameMode_Name,
    };

    /// <summary>[S-3900] The picker's name: "Microphone on/off", so it never reads as another widget's ("Bluetooth").</summary>
    public static Str PickerLabelId(QuickToggle toggle) => toggle switch
    {
        QuickToggle.Wifi => Str.Toggles_Wifi_Label,
        QuickToggle.Bluetooth => Str.Toggles_Bluetooth_Label,
        QuickToggle.AirplaneMode => Str.Toggles_AirplaneMode_Label,
        QuickToggle.EnergySaver => Str.Toggles_EnergySaver_Label,
        QuickToggle.NightLight => Str.Toggles_NightLight_Label,
        QuickToggle.RotationLock => Str.Toggles_RotationLock_Label,
        QuickToggle.DoNotDisturb => Str.Toggles_DoNotDisturb_Label,
        QuickToggle.Sound => Str.Toggles_Sound_Label,
        QuickToggle.Microphone => Str.Toggles_Microphone_Label,
        QuickToggle.SpatialSound => Str.Toggles_SpatialSound_Label,
        QuickToggle.MonoAudio => Str.Toggles_MonoAudio_Label,
        QuickToggle.ColorFilters => Str.Toggles_ColorFilters_Label,
        _ => Str.Toggles_GameMode_Label,
    };

    /// <summary>The picker's one line under the name.</summary>
    public static Str DescriptionId(QuickToggle toggle) => toggle switch
    {
        QuickToggle.Wifi => Str.Toggles_Wifi_Desc,
        QuickToggle.Bluetooth => Str.Toggles_Bluetooth_Desc,
        QuickToggle.AirplaneMode => Str.Toggles_AirplaneMode_Desc,
        QuickToggle.EnergySaver => Str.Toggles_EnergySaver_Desc,
        QuickToggle.NightLight => Str.Toggles_NightLight_Desc,
        QuickToggle.RotationLock => Str.Toggles_RotationLock_Desc,
        QuickToggle.DoNotDisturb => Str.Toggles_DoNotDisturb_Desc,
        QuickToggle.Sound => Str.Toggles_Sound_Desc,
        QuickToggle.Microphone => Str.Toggles_Microphone_Desc,
        QuickToggle.SpatialSound => Str.Toggles_SpatialSound_Desc,
        QuickToggle.MonoAudio => Str.Toggles_MonoAudio_Desc,
        QuickToggle.ColorFilters => Str.Toggles_ColorFilters_Desc,
        _ => Str.Toggles_GameMode_Desc,
    };

    public static string Name(QuickToggle toggle) => NameId(toggle).Text();

    public static string Name(Locale locale, QuickToggle toggle) => Strings.Get(locale, NameId(toggle));

    /// <summary>[S-3904] What the tile shows for a reading, with a click still on its way (<paramref name="pending"/>) shown at once.</summary>
    public static ToggleLook Look(ToggleReading reading, bool? pending = null)
    {
        if (reading.Block is not ToggleBlock.None and not ToggleBlock.Reading)
        {
            return ToggleLook.Unavailable;
        }

        if (pending is { } wished)
        {
            return wished ? ToggleLook.On : ToggleLook.Off;
        }

        return reading.On switch
        {
            true when reading.Block == ToggleBlock.None => ToggleLook.On,
            false when reading.Block == ToggleBlock.None => ToggleLook.Off,
            _ => ToggleLook.Unknown,
        };
    }

    /// <summary>[S-3904] The state line: "On", "Off", "Unavailable"; empty while not known.</summary>
    public static string StateText(Locale locale, ToggleLook look) => look switch
    {
        ToggleLook.On => Strings.Get(locale, Str.Toggle_On),
        ToggleLook.Off => Strings.Get(locale, Str.Toggle_Off),
        ToggleLook.Unavailable => Strings.Get(locale, Str.Toggles_State_Unavailable),
        _ => string.Empty,
    };

    /// <summary>The longest state line in a language: the face is planned for it, so switching moves nothing.</summary>
    public static int LongestStateChars(Locale locale)
        => new[] { ToggleLook.On, ToggleLook.Off, ToggleLook.Unavailable }.Max(l => StateText(locale, l).Length);

    /// <summary>[S-3903] Why it cannot be switched, for the tooltip; null where nothing is in the way.</summary>
    public static string? Reason(Locale locale, ToggleBlock block) => block switch
    {
        ToggleBlock.NoHardware => Strings.Get(locale, Str.Toggles_Reason_NoHardware),
        ToggleBlock.NoDevice => Strings.Get(locale, Str.Toggles_Reason_NoDevice),
        ToggleBlock.NotOnWindows => Strings.Get(locale, Str.Toggles_Reason_NotOnWindows),
        ToggleBlock.NotNow => Strings.Get(locale, Str.Toggles_Reason_NotNow),
        ToggleBlock.NoSwitch => Strings.Get(locale, Str.Toggles_Reason_NoSwitch),
        ToggleBlock.NotSupported => Strings.Get(locale, Str.Toggles_Reason_NotSupported),
        ToggleBlock.TurnedOff => Strings.Get(locale, Str.Toggles_Reason_TurnedOff),
        ToggleBlock.Reading => Strings.Get(locale, Str.Common_Loading),
        _ => null,
    };

    /// <summary>
    /// [S-3904] The tooltip: "Wi-Fi: On"; unavailable, the reason ("Wi-Fi: This PC does not have it");
    /// after a refused click, that it was refused.
    /// </summary>
    public static string Tooltip(Locale locale, QuickToggle toggle, ToggleReading reading, ToggleLook look, bool refused)
    {
        var state = refused && look != ToggleLook.Unavailable
            ? Strings.Get(locale, Str.Toggles_Reason_Refused)
            : look == ToggleLook.Unavailable || look == ToggleLook.Unknown
                ? Reason(locale, reading.Block) ?? StateText(locale, look)
                : StateText(locale, look);
        return Strings.Format(locale, Str.Toggles_Tip, ("name", Name(locale, toggle)), ("state", state));
    }

    // ---- what Windows answers, decoded --------------------------------------------------------

    /// <summary>
    /// [S-3910] Airplane mode from <c>IRadioManager::GetSystemRadioState</c>: its HRESULT and the
    /// radios' "enabled", whose opposite airplane mode is. Null where the service did not answer.
    /// </summary>
    public static bool? AirplaneFrom(int result, int radiosEnabled) => result < 0 ? null : radiosEnabled == 0;

    // AR_STATE (winuser.h).
    private const uint RotationDisabled = 0x1;
    private const uint RotationCannot = 0x10 | 0x20; // AR_NOSENSOR | AR_NOT_SUPPORTED
    private const uint RotationNotNow = 0x4 | 0x8 | 0x40 | 0x80; // remote session, multimon, docked, laptop

    /// <summary>
    /// [S-3912] Rotation lock from <c>GetAutoRotationState</c>, read as Windows' own handler reads it:
    /// no sensor (or not supported) is no hardware; a remote session, several screens, docked or used
    /// as a laptop is "not now"; else locked when the person disabled turning. Null state: the call failed.
    /// </summary>
    public static ToggleReading Rotation(uint? state)
    {
        if (state is not { } now || (now & RotationCannot) != 0)
        {
            return ToggleReading.Blocked(ToggleBlock.NoHardware);
        }

        if ((now & RotationNotNow) != 0)
        {
            return new ToggleReading(true, ToggleBlock.NotNow);
        }

        return ToggleReading.Of((now & RotationDisabled) != 0);
    }

    /// <summary>
    /// [S-3911] Energy saver from <c>GUID_ENERGY_SAVER_STATUS</c>: 0 off, 1 standard, 2 high savings —
    /// either of the last two is on. A value outside that is not one Windows sends: not known.
    /// </summary>
    public static bool? EnergySaverFrom(uint status) => status switch
    {
        0 => false,
        1 or 2 => true,
        _ => null,
    };

    /// <summary>The off profile of do not disturb, as Windows names it.</summary>
    public const string QuietOffProfile = "Microsoft.QuietHoursProfile.Unrestricted";

    /// <summary>What Windows' own toggle turns on: priority notifications and alarms only.</summary>
    public const string QuietPriorityProfile = "Microsoft.QuietHoursProfile.PriorityOnly";

    /// <summary>
    /// [S-3913] Do not disturb from <c>IQuietHoursSettings</c>: on while the profile in force is not the
    /// off profile, whether the person chose it or a rule (the schedule, a game) laid it over.
    /// </summary>
    public static bool DoNotDisturbOn(string? active, string? off)
    {
        var none = string.IsNullOrEmpty(off) ? QuietOffProfile : off;
        return !string.IsNullOrEmpty(active) && !string.Equals(active, none, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Spatial sound off: GUID_NULL, as Windows writes the active format.</summary>
    public const string SpatialOff = "{00000000-0000-0000-0000-000000000000}";

    /// <summary>[S-3914] Whether an active spatial format is off: none at all, not a GUID, or GUID_NULL.</summary>
    public static bool SpatialIsOff(string? format)
        => string.IsNullOrEmpty(format) || !Guid.TryParse(format, out var id) || id == Guid.Empty;

    /// <summary>
    /// [S-3915] A setting Windows keeps as a DWORD under HKCU (game mode, color filters, mono audio):
    /// absent is the setting's own default, a DWORD is on when it is not 0, anything else is not known.
    /// </summary>
    public static bool? DwordSetting(object? value, bool absent) => value switch
    {
        null => absent,
        int dword => dword != 0,
        _ => null,
    };
}
