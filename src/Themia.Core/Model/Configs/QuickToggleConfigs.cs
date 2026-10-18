using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-3900] Spatial sound.</summary>
public sealed class SpatialSoundToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.SpatialSound;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("spatialSoundToggle"),
        ConfigType = typeof(SpatialSoundToggleConfig),
        CreateDefault = () => new SpatialSoundToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "windows sonic", "surround", "3d audio", "headphones"],
        Label = Str.Toggles_SpatialSound_Label.Local(),
        Description = Str.Toggles_SpatialSound_Desc.Local(),
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
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.MonoAudio;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("monoAudioToggle"),
        ConfigType = typeof(MonoAudioToggleConfig),
        CreateDefault = () => new MonoAudioToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "mono", "accessibility", "one ear", "audio"],
        Label = Str.Toggles_MonoAudio_Label.Local(),
        Description = Str.Toggles_MonoAudio_Desc.Local(),
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
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.ColorFilters;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("colorFiltersToggle"),
        ConfigType = typeof(ColorFiltersToggleConfig),
        CreateDefault = () => new ColorFiltersToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "grayscale", "color blind", "invert", "accessibility"],
        Label = Str.Toggles_ColorFilters_Label.Local(),
        Description = Str.Toggles_ColorFilters_Desc.Local(),
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
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.GameMode;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gameModeToggle"),
        ConfigType = typeof(GameModeToggleConfig),
        CreateDefault = () => new GameModeToggleConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["quick settings", "toggle", "gaming", "games", "performance", "xbox"],
        Label = Str.Toggles_GameMode_Label.Local(),
        Description = Str.Toggles_GameMode_Desc.Local(),
        Glyph = Glyphs.GameMode,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 3, 2),
    };

    public override WidgetConfig Clone() => new GameModeToggleConfig { Style = Style };
}
