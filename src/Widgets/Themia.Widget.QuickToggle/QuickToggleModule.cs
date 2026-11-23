using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.QuickToggle;

[assembly: ThemiaWidget("quickToggle", typeof(QuickToggleModule))]

namespace Themia.Widget.QuickToggle;

// The project's namespace is named after the kind; the setting it targets is the SDK's enum of the same name.
using QuickToggle = Themia.Core.Model.QuickToggle;

/// <summary>
/// [S-16700] [S-16703] The quick setting's targets as data, one row each (the thirteen kinds' that were, owner 2026-11-23:
/// "they should actually be merged into one widget and not be multiple ones. in the gallery and everywhere else they should
/// still appear as if they were multiple widgets though"): its key (its config's target, its preset's key; the removed
/// kind was the key and "Toggle"), its add-page name ("Wi-Fi on/off") and caption, its search words and its place in the
/// Quick settings subject, run by run. Its name on the tile, its glyph and its unavailable reason are the SDK's row
/// (<see cref="QuickToggleRules.Targets"/>). The order here is the add page's.
/// </summary>
internal static class QuickToggleTargets
{
    /// <summary>Every target, in the add page's order.</summary>
    public static IReadOnlyList<QuickToggleEntry> All { get; } =
    [
        new(QuickToggle.Wifi, QuickToggleStr.Toggles_Wifi_Label.Local(), QuickToggleStr.Toggles_Wifi_Desc.Local(),
            ["quick settings", "toggle", "wifi", "wlan", "wireless", "radio", "internet"], 0, 0),
        new(QuickToggle.Bluetooth, QuickToggleStr.Toggles_Bluetooth_Label.Local(), QuickToggleStr.Toggles_Bluetooth_Desc.Local(),
            ["quick settings", "toggle", "radio", "wireless", "headphones"], 0, 1),
        new(QuickToggle.AirplaneMode, QuickToggleStr.Toggles_AirplaneMode_Label.Local(), QuickToggleStr.Toggles_AirplaneMode_Desc.Local(),
            ["quick settings", "toggle", "flight mode", "flight", "radios off", "offline"], 0, 2),
        new(QuickToggle.Sound, QuickToggleStr.Toggles_Sound_Label.Local(), QuickToggleStr.Toggles_Sound_Desc.Local(),
            ["quick settings", "toggle", "mute", "unmute", "speaker", "audio", "volume"], 1, 0),
        new(QuickToggle.Microphone, QuickToggleStr.Toggles_Microphone_Label.Local(), QuickToggleStr.Toggles_Microphone_Desc.Local(),
            ["quick settings", "toggle", "mic", "mute", "unmute", "calls", "recording"], 1, 1),
        new(QuickToggle.SpatialSound, QuickToggleStr.Toggles_SpatialSound_Label.Local(), QuickToggleStr.Toggles_SpatialSound_Desc.Local(),
            ["quick settings", "toggle", "windows sonic", "surround", "3d audio", "headphones"], 1, 2),
        new(QuickToggle.MonoAudio, QuickToggleStr.Toggles_MonoAudio_Label.Local(), QuickToggleStr.Toggles_MonoAudio_Desc.Local(),
            ["quick settings", "toggle", "mono", "accessibility", "one ear", "audio"], 1, 3),
        new(QuickToggle.NightLight, QuickToggleStr.Toggles_NightLight_Label.Local(), QuickToggleStr.Toggles_NightLight_Desc.Local(),
            ["quick settings", "toggle", "blue light", "warm screen", "night mode", "eye strain"], 2, 0),
        new(QuickToggle.ColorFilters, QuickToggleStr.Toggles_ColorFilters_Label.Local(), QuickToggleStr.Toggles_ColorFilters_Desc.Local(),
            ["quick settings", "toggle", "grayscale", "color blind", "invert", "accessibility"], 2, 1),
        new(QuickToggle.RotationLock, QuickToggleStr.Toggles_RotationLock_Label.Local(), QuickToggleStr.Toggles_RotationLock_Desc.Local(),
            ["quick settings", "toggle", "auto rotate", "screen rotation", "tablet", "orientation"], 2, 2),
        new(QuickToggle.EnergySaver, QuickToggleStr.Toggles_EnergySaver_Label.Local(), QuickToggleStr.Toggles_EnergySaver_Desc.Local(),
            ["quick settings", "toggle", "battery saver", "power saving", "battery", "eco"], 3, 0),
        new(QuickToggle.DoNotDisturb, QuickToggleStr.Toggles_DoNotDisturb_Label.Local(), QuickToggleStr.Toggles_DoNotDisturb_Desc.Local(),
            ["quick settings", "toggle", "dnd", "focus assist", "quiet hours", "notifications", "silence"], 3, 1),
        new(QuickToggle.GameMode, QuickToggleStr.Toggles_GameMode_Label.Local(), QuickToggleStr.Toggles_GameMode_Desc.Local(),
            ["quick settings", "toggle", "gaming", "games", "performance", "xbox"], 3, 2),
    ];

    /// <summary>[S-16703] Each target as a widget of its own: its preset (the add page, search, the gallery, groups, the seed).</summary>
    public static IReadOnlyList<WidgetPreset> Presets { get; } = [.. All.Select(e => new WidgetPreset
    {
        Key = e.Key,
        Label = e.Label,
        Description = e.Description,
        Glyph = QuickToggleRules.Target(e.Toggle).Glyph,
        Keywords = e.Keywords,
        Add = new AddPlacement(AddFolderId.QuickSettings, e.Group, e.Rank),
        Apply = config => ((QuickToggleConfig)config).Target = e.Toggle,
        Holds = config => config is QuickToggleConfig { Target: var target } && target == e.Toggle,
    })];

    /// <summary>[S-16690] [S-16704] Each removed kind (<c>wifiToggle</c> …): a placed one becomes the quick setting of its target alone.</summary>
    public static IReadOnlyList<MergeFrom> Merges { get; } =
        [.. All.Select(e => new MergeFrom(e.Key + "Toggle") { Fold = MergeFold.Alone, Sets = ("target", e.Key) })];
}

/// <summary>One target as the add page offers it: which setting, its picker name and caption, its search words, its place.</summary>
internal sealed record QuickToggleEntry(
    QuickToggle Toggle,
    LocalText Label,
    LocalText Description,
    IReadOnlyList<string> Keywords,
    int Group,
    int Rank)
{
    /// <summary>Its key: the target's camelCase name (<c>airplaneMode</c>).</summary>
    public string Key => QuickToggleRules.Key(Toggle);
}

/// <summary>
/// [S-16700] The quick setting: one kind whose config targets one of the thirteen settings (its Toggles line) in one of the
/// three looks; offered as a widget per target ([S-16703]), drawn by the SDK's one view.
/// </summary>
public sealed class QuickToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("quickToggle"),
        ConfigType = typeof(QuickToggleConfig),
        CreateDefault = () => new QuickToggleConfig(),
        Json = QuickToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle"],
        Label = QuickToggleStr.Toggles_Kind_Label.Local(),
        Description = QuickToggleStr.Toggles_Kind_Desc.Local(),
        Glyph = Glyphs.Settings,
        Sizing = c => TileRules.Sizing(((QuickToggleConfig)c).Look),
        Traits = WidgetTraits.HasItems | WidgetTraits.OneReadout,
        Add = new(AddFolderId.QuickSettings, 0, 0),
        Presets = QuickToggleTargets.Presets,
        Merges = QuickToggleTargets.Merges,
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(host);

    /// <summary>[S-16700] Its own line: Toggles, which setting it switches, every target by its name on the tile.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (config is not QuickToggleConfig toggle)
        {
            return;
        }

        (QuickToggle, string)[] targets = [.. QuickToggleTargets.All.Select(e => (e.Toggle, QuickToggleRules.Name(e.Toggle)))];
        menu.Lines.Add(menu.Choice(QuickToggleRules.Glyph(toggle.Target), QuickToggleStr.Toggles_Target_Label.Text(), targets, toggle.Target, value => toggle.Target = value));
    }
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(QuickToggleConfig))]
internal sealed partial class QuickToggleJsonContext : JsonSerializerContext;
