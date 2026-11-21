using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Wi-Fi.</summary>
public sealed class WifiToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Wifi;

    public override WidgetConfig Clone() => CopyTo(new WifiToggleConfig());
}

/// <summary>[S-3900] Bluetooth.</summary>
public sealed class BluetoothToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Bluetooth;

    public override WidgetConfig Clone() => CopyTo(new BluetoothToggleConfig());
}

/// <summary>[S-3900] Airplane mode.</summary>
public sealed class AirplaneModeToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.AirplaneMode;

    public override WidgetConfig Clone() => CopyTo(new AirplaneModeToggleConfig());
}

/// <summary>[S-3900] Sound (the default output's mute).</summary>
public sealed class SoundToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Sound;

    public override WidgetConfig Clone() => CopyTo(new SoundToggleConfig());
}

/// <summary>[S-3900] Microphone (the default microphone's mute).</summary>
public sealed class MicrophoneToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Microphone;

    public override WidgetConfig Clone() => CopyTo(new MicrophoneToggleConfig());
}

/// <summary>[S-3900] Spatial sound.</summary>
public sealed class SpatialSoundToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.SpatialSound;

    public override WidgetConfig Clone() => CopyTo(new SpatialSoundToggleConfig());
}

/// <summary>[S-3900] Mono audio.</summary>
public sealed class MonoAudioToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.MonoAudio;

    public override WidgetConfig Clone() => CopyTo(new MonoAudioToggleConfig());
}

/// <summary>[S-3900] Night light.</summary>
public sealed class NightLightToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.NightLight;

    public override WidgetConfig Clone() => CopyTo(new NightLightToggleConfig());
}

/// <summary>[S-3900] Color filters.</summary>
public sealed class ColorFiltersToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.ColorFilters;

    public override WidgetConfig Clone() => CopyTo(new ColorFiltersToggleConfig());
}

/// <summary>[S-3900] Rotation lock.</summary>
public sealed class RotationLockToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.RotationLock;

    public override WidgetConfig Clone() => CopyTo(new RotationLockToggleConfig());
}

/// <summary>[S-3900] Energy saver.</summary>
public sealed class EnergySaverToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.EnergySaver;

    public override WidgetConfig Clone() => CopyTo(new EnergySaverToggleConfig());
}

/// <summary>[S-3900] Do not disturb.</summary>
public sealed class DoNotDisturbToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.DoNotDisturb;

    public override WidgetConfig Clone() => CopyTo(new DoNotDisturbToggleConfig());
}

/// <summary>[S-3900] Game mode.</summary>
public sealed class GameModeToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.GameMode;

    public override WidgetConfig Clone() => CopyTo(new GameModeToggleConfig());
}
