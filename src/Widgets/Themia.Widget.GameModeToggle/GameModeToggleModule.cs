using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.GameModeToggle;

[assembly: ThemiaWidget("gameModeToggle", typeof(GameModeToggleModule))]

namespace Themia.Widget.GameModeToggle;

/// <summary>[S-9400] The Game mode quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class GameModeToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gameModeToggle"),
        ConfigType = typeof(GameModeToggleConfig),
        CreateDefault = () => new GameModeToggleConfig(),
        Json = GameModeToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "gaming", "games", "performance", "xbox"],
        Label = GameModeToggleStr.Toggles_GameMode_Label.Local(),
        Description = GameModeToggleStr.Toggles_GameMode_Desc.Local(),
        Glyph = Glyphs.GameMode,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 3, 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GameModeToggleConfig))]
internal sealed partial class GameModeToggleJsonContext : JsonSerializerContext;
