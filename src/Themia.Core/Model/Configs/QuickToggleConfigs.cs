using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3900] The closed set of quick settings a toggle widget switches. Never persisted: each toggle is
/// its own widget kind, and <see cref="QuickToggleRules.ForKind"/> maps the kind to its toggle.
/// </summary>
public enum QuickToggle
{
    Wifi = 0,
    Bluetooth = 1,
    AirplaneMode = 2,
    EnergySaver = 3,
    NightLight = 4,
    RotationLock = 5,
    DoNotDisturb = 6,
    Sound = 7,
    Microphone = 8,
    SpatialSound = 9,
    MonoAudio = 10,
    ColorFilters = 11,
    GameMode = 12,
}

/// <summary>
/// [S-3916] The looks every quick-setting toggle shares: one enum for all thirteen kinds. The first
/// is the default; an unknown stored name is repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<QuickToggleStyle>))]
public enum QuickToggleStyle
{
    /// <summary>[S-3904] The Windows 11 quick-settings tile: the whole tile accent-filled while on.</summary>
    Tile = 0,

    /// <summary>[S-3917] A circular button holding the icon, accent-filled while on, the name under it.</summary>
    Round = 1,

    /// <summary>[S-3918] The name with a Windows toggle switch and its state line, as a Settings row.</summary>
    Switch = 2,
}

/// <summary>
/// [S-3900] A quick-setting toggle widget: the whole tile is one switch. Nothing to configure beyond
/// the card's own settings and its look, so the config carries which toggle its kind stands for and
/// the style every toggle shares ([S-3916]).
/// </summary>
public abstract class QuickToggleConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] ToggleStyles =
    [
        StyleOption.Of(QuickToggleStyle.Tile, Str.Styles_Toggles_Tile, 0xECA5),
        StyleOption.Of(QuickToggleStyle.Round, Str.Styles_Toggles_Round, 0xEA3B),
        StyleOption.Of(QuickToggleStyle.Switch, Str.Styles_Toggles_Switch, 0xEC11),
    ];

    /// <summary>The toggle this widget switches, decided by its kind.</summary>
    [JsonIgnore]
    public QuickToggle Toggle => QuickToggleRules.ForKind(Kind)
        ?? throw new InvalidOperationException($"{Kind} is not a quick-setting toggle");

    /// <summary>[S-3916] How the toggle is drawn; copied by every kind's <c>Clone</c>.</summary>
    public QuickToggleStyle Style { get; set; } = QuickToggleStyle.Tile;

    IReadOnlyList<StyleOption> IStyled.Styles => ToggleStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (QuickToggleStyle)value;
    }
}

/// <summary>[S-3900] Wi-Fi.</summary>
public sealed class WifiToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WifiToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("wifiToggle"),
        Kind = WidgetKind.WifiToggle,
        ConfigType = typeof(WifiToggleConfig),
        CreateDefault = () => new WifiToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        // [S-3900] The quick-setting toggles.
        Keywords = ["quick settings", "toggle", "wifi", "wlan", "wireless", "radio", "internet"],
        LabelId = Str.Toggles_Wifi_Label,
        DescriptionId = Str.Toggles_Wifi_Desc,
        Glyph = Glyphs.Wifi,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 0, 0),
    };

    public override WidgetConfig Clone() => new WifiToggleConfig { Style = Style };
}

/// <summary>[S-3900] Bluetooth.</summary>
public sealed class BluetoothToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.BluetoothToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("bluetoothToggle"),
        Kind = WidgetKind.BluetoothToggle,
        ConfigType = typeof(BluetoothToggleConfig),
        CreateDefault = () => new BluetoothToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "radio", "wireless", "headphones"],
        LabelId = Str.Toggles_Bluetooth_Label,
        DescriptionId = Str.Toggles_Bluetooth_Desc,
        Glyph = Glyphs.Bluetooth,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 0, 1),
    };

    public override WidgetConfig Clone() => new BluetoothToggleConfig { Style = Style };
}

/// <summary>[S-3900] Airplane mode.</summary>
public sealed class AirplaneModeToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.AirplaneModeToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("airplaneModeToggle"),
        Kind = WidgetKind.AirplaneModeToggle,
        ConfigType = typeof(AirplaneModeToggleConfig),
        CreateDefault = () => new AirplaneModeToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "flight mode", "flight", "radios off", "offline"],
        LabelId = Str.Toggles_AirplaneMode_Label,
        DescriptionId = Str.Toggles_AirplaneMode_Desc,
        Glyph = Glyphs.Airplane,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 0, 2),
    };

    public override WidgetConfig Clone() => new AirplaneModeToggleConfig { Style = Style };
}

/// <summary>[S-3900] Energy saver.</summary>
public sealed class EnergySaverToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.EnergySaverToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("energySaverToggle"),
        Kind = WidgetKind.EnergySaverToggle,
        ConfigType = typeof(EnergySaverToggleConfig),
        CreateDefault = () => new EnergySaverToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "battery saver", "power saving", "battery", "eco"],
        LabelId = Str.Toggles_EnergySaver_Label,
        DescriptionId = Str.Toggles_EnergySaver_Desc,
        Glyph = Glyphs.EnergySaver,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 3, 0),
    };

    public override WidgetConfig Clone() => new EnergySaverToggleConfig { Style = Style };
}

/// <summary>[S-3900] Night light.</summary>
public sealed class NightLightToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.NightLightToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("nightLightToggle"),
        Kind = WidgetKind.NightLightToggle,
        ConfigType = typeof(NightLightToggleConfig),
        CreateDefault = () => new NightLightToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "blue light", "warm screen", "night mode", "eye strain"],
        LabelId = Str.Toggles_NightLight_Label,
        DescriptionId = Str.Toggles_NightLight_Desc,
        Glyph = Glyphs.NightLight,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 2, 0),
    };

    public override WidgetConfig Clone() => new NightLightToggleConfig { Style = Style };
}

/// <summary>[S-3900] Rotation lock.</summary>
public sealed class RotationLockToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RotationLockToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rotationLockToggle"),
        Kind = WidgetKind.RotationLockToggle,
        ConfigType = typeof(RotationLockToggleConfig),
        CreateDefault = () => new RotationLockToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "auto rotate", "screen rotation", "tablet", "orientation"],
        LabelId = Str.Toggles_RotationLock_Label,
        DescriptionId = Str.Toggles_RotationLock_Desc,
        Glyph = Glyphs.RotationLock,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 2, 2),
    };

    public override WidgetConfig Clone() => new RotationLockToggleConfig { Style = Style };
}

/// <summary>[S-3900] Do not disturb.</summary>
public sealed class DoNotDisturbToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.DoNotDisturbToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("doNotDisturbToggle"),
        Kind = WidgetKind.DoNotDisturbToggle,
        ConfigType = typeof(DoNotDisturbToggleConfig),
        CreateDefault = () => new DoNotDisturbToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "dnd", "focus assist", "quiet hours", "notifications", "silence"],
        LabelId = Str.Toggles_DoNotDisturb_Label,
        DescriptionId = Str.Toggles_DoNotDisturb_Desc,
        Glyph = Glyphs.QuietHours,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 3, 1),
    };

    public override WidgetConfig Clone() => new DoNotDisturbToggleConfig { Style = Style };
}

/// <summary>[S-3900] Sound (the default output's mute).</summary>
public sealed class SoundToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.SoundToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("soundToggle"),
        Kind = WidgetKind.SoundToggle,
        ConfigType = typeof(SoundToggleConfig),
        CreateDefault = () => new SoundToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "mute", "unmute", "speaker", "audio", "volume"],
        LabelId = Str.Toggles_Sound_Label,
        DescriptionId = Str.Toggles_Sound_Desc,
        Glyph = Glyphs.Volume3,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 0),
    };

    public override WidgetConfig Clone() => new SoundToggleConfig { Style = Style };
}

/// <summary>[S-3900] Microphone (the default microphone's mute).</summary>
public sealed class MicrophoneToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.MicrophoneToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("microphoneToggle"),
        Kind = WidgetKind.MicrophoneToggle,
        ConfigType = typeof(MicrophoneToggleConfig),
        CreateDefault = () => new MicrophoneToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "mic", "mute", "unmute", "calls", "recording"],
        LabelId = Str.Toggles_Microphone_Label,
        DescriptionId = Str.Toggles_Microphone_Desc,
        Glyph = Glyphs.Microphone,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 1),
    };

    public override WidgetConfig Clone() => new MicrophoneToggleConfig { Style = Style };
}

/// <summary>[S-3900] Spatial sound.</summary>
public sealed class SpatialSoundToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.SpatialSoundToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("spatialSoundToggle"),
        Kind = WidgetKind.SpatialSoundToggle,
        ConfigType = typeof(SpatialSoundToggleConfig),
        CreateDefault = () => new SpatialSoundToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "windows sonic", "surround", "3d audio", "headphones"],
        LabelId = Str.Toggles_SpatialSound_Label,
        DescriptionId = Str.Toggles_SpatialSound_Desc,
        Glyph = Glyphs.SpatialSound,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 2),
    };

    public override WidgetConfig Clone() => new SpatialSoundToggleConfig { Style = Style };
}

/// <summary>[S-3900] Mono audio.</summary>
public sealed class MonoAudioToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.MonoAudioToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("monoAudioToggle"),
        Kind = WidgetKind.MonoAudioToggle,
        ConfigType = typeof(MonoAudioToggleConfig),
        CreateDefault = () => new MonoAudioToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "mono", "accessibility", "one ear", "audio"],
        LabelId = Str.Toggles_MonoAudio_Label,
        DescriptionId = Str.Toggles_MonoAudio_Desc,
        Glyph = Glyphs.MonoAudio,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 1, 3),
    };

    public override WidgetConfig Clone() => new MonoAudioToggleConfig { Style = Style };
}

/// <summary>[S-3900] Color filters.</summary>
public sealed class ColorFiltersToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ColorFiltersToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("colorFiltersToggle"),
        Kind = WidgetKind.ColorFiltersToggle,
        ConfigType = typeof(ColorFiltersToggleConfig),
        CreateDefault = () => new ColorFiltersToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "grayscale", "color blind", "invert", "accessibility"],
        LabelId = Str.Toggles_ColorFilters_Label,
        DescriptionId = Str.Toggles_ColorFilters_Desc,
        Glyph = Glyphs.ColorFilters,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 2, 1),
    };

    public override WidgetConfig Clone() => new ColorFiltersToggleConfig { Style = Style };
}

/// <summary>[S-3900] Game mode.</summary>
public sealed class GameModeToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.GameModeToggle;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gameModeToggle"),
        Kind = WidgetKind.GameModeToggle,
        ConfigType = typeof(GameModeToggleConfig),
        CreateDefault = () => new GameModeToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "gaming", "games", "performance", "xbox"],
        LabelId = Str.Toggles_GameMode_Label,
        DescriptionId = Str.Toggles_GameMode_Desc,
        Glyph = Glyphs.GameMode,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 3, 2),
    };

    public override WidgetConfig Clone() => new GameModeToggleConfig { Style = Style };
}
