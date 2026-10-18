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
using Themia.Widget.ScreenTimeDonut;

[assembly: ThemiaWidget("screenTimeDonut", typeof(ScreenTimeDonutModule))]

namespace Themia.Widget.ScreenTimeDonut;

/// <summary>[S-3340] The screen-time history as one ring: the busiest apps and the rest, the total in its hole. SPEC.md beside this file.</summary>
public sealed class ScreenTimeDonutModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeDonut"),
        ConfigType = typeof(ScreenTimeDonutConfig),
        CreateDefault = () => new ScreenTimeDonutConfig(),
        Json = ScreenTimeDonutJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "share", "pie", "ring", "statistics"],
        Label = ScreenTimeDonutStr.WidgetsC_ScreenTime_Donut_Label.Local(),
        Description = ScreenTimeDonutStr.WidgetsC_ScreenTime_Donut_Desc.Local(),
        Glyph = Glyphs.ScreenTimeDonut,
        DefaultWidth = 240,
        DefaultHeight = 260,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.ScreenTime, 0, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => ScreenTimeDonutSettings.Rows(rows.Rows, (ScreenTimeDonutConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ScreenTimeDonutView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScreenTimeDonutConfig))]
internal sealed partial class ScreenTimeDonutJsonContext : JsonSerializerContext;
