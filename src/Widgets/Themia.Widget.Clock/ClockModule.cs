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
using Themia.Widget.Clock;

[assembly: ThemiaWidget("clock", typeof(ClockModule))]

namespace Themia.Widget.Clock;

/// <summary>[S-6011] The clock: the time in seven styles, a zone of its own, a label and the date. SPEC.md beside this file.</summary>
public sealed class ClockModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("clock"),
        ConfigType = typeof(ClockConfig),
        CreateDefault = () => new ClockConfig(),
        Json = ClockJsonContext.Default,
        Keywords =
        [
            "time", "timezone", "watch", "world clock", "date", "analog", "digital", "day", "today", "weekday",
            "month", "calendar", "wallpaper",
        ],
        Label = ClockStr.Widget_Clock_Label.Local(),
        Description = ClockStr.WidgetsA_Catalog_Clock_Description.Local(),
        Glyph = Glyphs.Clock,
        DefaultWidth = 280,
        DefaultHeight = 140,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Time, 0, 0, Popular: 0),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => ClockMenu.Lines(menu, (ClockConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new ClockView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ClockConfig))]
internal sealed partial class ClockJsonContext : JsonSerializerContext;
