using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

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
