using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.QuickToggles;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("wifiToggle", typeof(WifiToggleModule))]
[assembly: ThemiaWidget("bluetoothToggle", typeof(BluetoothToggleModule))]
[assembly: ThemiaWidget("airplaneModeToggle", typeof(AirplaneModeToggleModule))]
[assembly: ThemiaWidget("soundToggle", typeof(SoundToggleModule))]
[assembly: ThemiaWidget("microphoneToggle", typeof(MicrophoneToggleModule))]
[assembly: ThemiaWidget("spatialSoundToggle", typeof(SpatialSoundToggleModule))]
[assembly: ThemiaWidget("monoAudioToggle", typeof(MonoAudioToggleModule))]
[assembly: ThemiaWidget("nightLightToggle", typeof(NightLightToggleModule))]
[assembly: ThemiaWidget("colorFiltersToggle", typeof(ColorFiltersToggleModule))]
[assembly: ThemiaWidget("rotationLockToggle", typeof(RotationLockToggleModule))]
[assembly: ThemiaWidget("energySaverToggle", typeof(EnergySaverToggleModule))]
[assembly: ThemiaWidget("doNotDisturbToggle", typeof(DoNotDisturbToggleModule))]
[assembly: ThemiaWidget("gameModeToggle", typeof(GameModeToggleModule))]

namespace Themia.Widget.QuickToggles;

/// <summary>
/// [S-3900] The thirteen quick-setting toggles as data, one setting each: the family project's kinds, each with its own id,
/// config and words, the one view (<c>QuickToggleView</c>) and rules (<c>QuickToggleRules</c>) the SDK's. The order here is the
/// add page's (Quick settings, run by run).
/// </summary>
internal static class QuickToggleKinds
{
    /// <summary>Every kind, in the add page's order.</summary>
    public static IReadOnlyList<QuickToggleKind> All { get; } =
    [
        new("wifiToggle", () => new WifiToggleConfig(), Glyphs.Wifi,
            QuickTogglesStr.Toggles_Wifi_Label.Local(), QuickTogglesStr.Toggles_Wifi_Desc.Local(),
            ["quick settings", "toggle", "wifi", "wlan", "wireless", "radio", "internet"], 0, 0),
        new("bluetoothToggle", () => new BluetoothToggleConfig(), Glyphs.Bluetooth,
            QuickTogglesStr.Toggles_Bluetooth_Label.Local(), QuickTogglesStr.Toggles_Bluetooth_Desc.Local(),
            ["quick settings", "toggle", "radio", "wireless", "headphones"], 0, 1),
        new("airplaneModeToggle", () => new AirplaneModeToggleConfig(), Glyphs.Airplane,
            QuickTogglesStr.Toggles_AirplaneMode_Label.Local(), QuickTogglesStr.Toggles_AirplaneMode_Desc.Local(),
            ["quick settings", "toggle", "flight mode", "flight", "radios off", "offline"], 0, 2),
        new("soundToggle", () => new SoundToggleConfig(), Glyphs.Volume3,
            QuickTogglesStr.Toggles_Sound_Label.Local(), QuickTogglesStr.Toggles_Sound_Desc.Local(),
            ["quick settings", "toggle", "mute", "unmute", "speaker", "audio", "volume"], 1, 0),
        new("microphoneToggle", () => new MicrophoneToggleConfig(), Glyphs.Microphone,
            QuickTogglesStr.Toggles_Microphone_Label.Local(), QuickTogglesStr.Toggles_Microphone_Desc.Local(),
            ["quick settings", "toggle", "mic", "mute", "unmute", "calls", "recording"], 1, 1),
        new("spatialSoundToggle", () => new SpatialSoundToggleConfig(), Glyphs.SpatialSound,
            QuickTogglesStr.Toggles_SpatialSound_Label.Local(), QuickTogglesStr.Toggles_SpatialSound_Desc.Local(),
            ["quick settings", "toggle", "windows sonic", "surround", "3d audio", "headphones"], 1, 2),
        new("monoAudioToggle", () => new MonoAudioToggleConfig(), Glyphs.MonoAudio,
            QuickTogglesStr.Toggles_MonoAudio_Label.Local(), QuickTogglesStr.Toggles_MonoAudio_Desc.Local(),
            ["quick settings", "toggle", "mono", "accessibility", "one ear", "audio"], 1, 3),
        new("nightLightToggle", () => new NightLightToggleConfig(), Glyphs.NightLight,
            QuickTogglesStr.Toggles_NightLight_Label.Local(), QuickTogglesStr.Toggles_NightLight_Desc.Local(),
            ["quick settings", "toggle", "blue light", "warm screen", "night mode", "eye strain"], 2, 0),
        new("colorFiltersToggle", () => new ColorFiltersToggleConfig(), Glyphs.ColorFilters,
            QuickTogglesStr.Toggles_ColorFilters_Label.Local(), QuickTogglesStr.Toggles_ColorFilters_Desc.Local(),
            ["quick settings", "toggle", "grayscale", "color blind", "invert", "accessibility"], 2, 1),
        new("rotationLockToggle", () => new RotationLockToggleConfig(), Glyphs.RotationLock,
            QuickTogglesStr.Toggles_RotationLock_Label.Local(), QuickTogglesStr.Toggles_RotationLock_Desc.Local(),
            ["quick settings", "toggle", "auto rotate", "screen rotation", "tablet", "orientation"], 2, 2),
        new("energySaverToggle", () => new EnergySaverToggleConfig(), Glyphs.EnergySaver,
            QuickTogglesStr.Toggles_EnergySaver_Label.Local(), QuickTogglesStr.Toggles_EnergySaver_Desc.Local(),
            ["quick settings", "toggle", "battery saver", "power saving", "battery", "eco"], 3, 0),
        new("doNotDisturbToggle", () => new DoNotDisturbToggleConfig(), Glyphs.QuietHours,
            QuickTogglesStr.Toggles_DoNotDisturb_Label.Local(), QuickTogglesStr.Toggles_DoNotDisturb_Desc.Local(),
            ["quick settings", "toggle", "dnd", "focus assist", "quiet hours", "notifications", "silence"], 3, 1),
        new("gameModeToggle", () => new GameModeToggleConfig(), Glyphs.GameMode,
            QuickTogglesStr.Toggles_GameMode_Label.Local(), QuickTogglesStr.Toggles_GameMode_Desc.Local(),
            ["quick settings", "toggle", "gaming", "games", "performance", "xbox"], 3, 2),
    ];

    /// <summary>The kind of <paramref name="id"/>.</summary>
    public static QuickToggleKind Of(string id) => All.Single(k => k.Id == id);
}

/// <summary>One kind of the family as data: its id, its config (which toggle it is), its words and its place on the add page.</summary>
internal sealed record QuickToggleKind(
    string Id,
    Func<QuickToggleConfig> Create,
    string Glyph,
    LocalText Label,
    LocalText Description,
    IReadOnlyList<string> Keywords,
    int Group,
    int Rank);

/// <summary>[S-6902] A kind's module: its descriptor from its row of <see cref="QuickToggleKinds"/>, the one shared view.</summary>
public abstract class QuickToggleModule : IWidgetModule
{
    /// <param name="id">The kind's id: its row of the table.</param>
    protected QuickToggleModule(string id)
    {
        var kind = QuickToggleKinds.Of(id);
        Descriptor = new()
        {
            Id = new(kind.Id),
            ConfigType = kind.Create().GetType(),
            CreateDefault = kind.Create,
            Json = QuickTogglesJsonContext.Default,
            Keywords = kind.Keywords,
            Label = kind.Label,
            Description = kind.Description,
            Glyph = kind.Glyph,
            Sizing = c => TileRules.Sizing(((QuickToggleConfig)c).Look),
            Traits = WidgetTraits.HasItems | WidgetTraits.OneReadout,
            Add = new(AddFolderId.QuickSettings, kind.Group, kind.Rank),
        };
    }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

// [S-6901] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything else
// is the family's.

/// <summary>[S-8200] The Wi-Fi quick-setting toggle: the whole tile its switch.</summary>
public sealed class WifiToggleModule() : QuickToggleModule("wifiToggle");

/// <summary>[S-8300] The Bluetooth quick-setting toggle: the whole tile its switch.</summary>
public sealed class BluetoothToggleModule() : QuickToggleModule("bluetoothToggle");

/// <summary>[S-8400] The Airplane mode quick-setting toggle: the whole tile its switch.</summary>
public sealed class AirplaneModeToggleModule() : QuickToggleModule("airplaneModeToggle");

/// <summary>[S-8900] The Sound quick-setting toggle: the whole tile its switch.</summary>
public sealed class SoundToggleModule() : QuickToggleModule("soundToggle");

/// <summary>[S-9000] The Microphone quick-setting toggle: the whole tile its switch.</summary>
public sealed class MicrophoneToggleModule() : QuickToggleModule("microphoneToggle");

/// <summary>[S-9100] The Spatial sound quick-setting toggle: the whole tile its switch.</summary>
public sealed class SpatialSoundToggleModule() : QuickToggleModule("spatialSoundToggle");

/// <summary>[S-9200] The Mono audio quick-setting toggle: the whole tile its switch.</summary>
public sealed class MonoAudioToggleModule() : QuickToggleModule("monoAudioToggle");

/// <summary>[S-8600] The Night light quick-setting toggle: the whole tile its switch.</summary>
public sealed class NightLightToggleModule() : QuickToggleModule("nightLightToggle");

/// <summary>[S-9300] The Color filters quick-setting toggle: the whole tile its switch.</summary>
public sealed class ColorFiltersToggleModule() : QuickToggleModule("colorFiltersToggle");

/// <summary>[S-8700] The Rotation lock quick-setting toggle: the whole tile its switch.</summary>
public sealed class RotationLockToggleModule() : QuickToggleModule("rotationLockToggle");

/// <summary>[S-8500] The Energy saver quick-setting toggle: the whole tile its switch.</summary>
public sealed class EnergySaverToggleModule() : QuickToggleModule("energySaverToggle");

/// <summary>[S-8800] The Do not disturb quick-setting toggle: the whole tile its switch.</summary>
public sealed class DoNotDisturbToggleModule() : QuickToggleModule("doNotDisturbToggle");

/// <summary>[S-9400] The Game mode quick-setting toggle: the whole tile its switch.</summary>
public sealed class GameModeToggleModule() : QuickToggleModule("gameModeToggle");

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WifiToggleConfig))]
[JsonSerializable(typeof(BluetoothToggleConfig))]
[JsonSerializable(typeof(AirplaneModeToggleConfig))]
[JsonSerializable(typeof(SoundToggleConfig))]
[JsonSerializable(typeof(MicrophoneToggleConfig))]
[JsonSerializable(typeof(SpatialSoundToggleConfig))]
[JsonSerializable(typeof(MonoAudioToggleConfig))]
[JsonSerializable(typeof(NightLightToggleConfig))]
[JsonSerializable(typeof(ColorFiltersToggleConfig))]
[JsonSerializable(typeof(RotationLockToggleConfig))]
[JsonSerializable(typeof(EnergySaverToggleConfig))]
[JsonSerializable(typeof(DoNotDisturbToggleConfig))]
[JsonSerializable(typeof(GameModeToggleConfig))]
internal sealed partial class QuickTogglesJsonContext : JsonSerializerContext;
