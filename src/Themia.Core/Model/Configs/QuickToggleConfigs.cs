using System.Text.Json.Serialization;
using Themia.Core.Widgets;

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
/// [S-3900] A quick-setting toggle widget: the whole tile is one switch. Nothing to configure beyond
/// the card's own settings, so the config only carries which toggle its kind stands for.
/// </summary>
public abstract class QuickToggleConfig : WidgetConfig
{
    /// <summary>The toggle this widget switches, decided by its kind.</summary>
    [JsonIgnore]
    public QuickToggle Toggle => QuickToggleRules.ForKind(Kind)
        ?? throw new InvalidOperationException($"{Kind} is not a quick-setting toggle");
}

/// <summary>[S-3900] Wi-Fi.</summary>
public sealed class WifiToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WifiToggle;

    public override WidgetConfig Clone() => new WifiToggleConfig();
}

/// <summary>[S-3900] Bluetooth.</summary>
public sealed class BluetoothToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.BluetoothToggle;

    public override WidgetConfig Clone() => new BluetoothToggleConfig();
}

/// <summary>[S-3900] Airplane mode.</summary>
public sealed class AirplaneModeToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.AirplaneModeToggle;

    public override WidgetConfig Clone() => new AirplaneModeToggleConfig();
}

/// <summary>[S-3900] Energy saver.</summary>
public sealed class EnergySaverToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.EnergySaverToggle;

    public override WidgetConfig Clone() => new EnergySaverToggleConfig();
}

/// <summary>[S-3900] Night light.</summary>
public sealed class NightLightToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.NightLightToggle;

    public override WidgetConfig Clone() => new NightLightToggleConfig();
}

/// <summary>[S-3900] Rotation lock.</summary>
public sealed class RotationLockToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RotationLockToggle;

    public override WidgetConfig Clone() => new RotationLockToggleConfig();
}

/// <summary>[S-3900] Do not disturb.</summary>
public sealed class DoNotDisturbToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.DoNotDisturbToggle;

    public override WidgetConfig Clone() => new DoNotDisturbToggleConfig();
}

/// <summary>[S-3900] Sound (the default output's mute).</summary>
public sealed class SoundToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.SoundToggle;

    public override WidgetConfig Clone() => new SoundToggleConfig();
}

/// <summary>[S-3900] Microphone (the default microphone's mute).</summary>
public sealed class MicrophoneToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.MicrophoneToggle;

    public override WidgetConfig Clone() => new MicrophoneToggleConfig();
}

/// <summary>[S-3900] Spatial sound.</summary>
public sealed class SpatialSoundToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.SpatialSoundToggle;

    public override WidgetConfig Clone() => new SpatialSoundToggleConfig();
}

/// <summary>[S-3900] Mono audio.</summary>
public sealed class MonoAudioToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.MonoAudioToggle;

    public override WidgetConfig Clone() => new MonoAudioToggleConfig();
}

/// <summary>[S-3900] Color filters.</summary>
public sealed class ColorFiltersToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ColorFiltersToggle;

    public override WidgetConfig Clone() => new ColorFiltersToggleConfig();
}

/// <summary>[S-3900] Game mode.</summary>
public sealed class GameModeToggleConfig : QuickToggleConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.GameModeToggle;

    public override WidgetConfig Clone() => new GameModeToggleConfig();
}
