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
using Themia.Widget.ColorFiltersToggle;

[assembly: ThemiaWidget("colorFiltersToggle", typeof(ColorFiltersToggleModule))]

namespace Themia.Widget.ColorFiltersToggle;

/// <summary>[S-9300] The Color filters quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class ColorFiltersToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("colorFiltersToggle"),
        ConfigType = typeof(ColorFiltersToggleConfig),
        CreateDefault = () => new ColorFiltersToggleConfig(),
        Json = ColorFiltersToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "grayscale", "color blind", "invert", "accessibility"],
        Label = ColorFiltersToggleStr.Toggles_ColorFilters_Label.Local(),
        Description = ColorFiltersToggleStr.Toggles_ColorFilters_Desc.Local(),
        Glyph = Glyphs.ColorFilters,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 2, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ColorFiltersToggleConfig))]
internal sealed partial class ColorFiltersToggleJsonContext : JsonSerializerContext;
