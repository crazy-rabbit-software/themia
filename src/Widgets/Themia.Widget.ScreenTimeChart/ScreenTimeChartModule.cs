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
using Themia.Widget.ScreenTimeChart;

[assembly: ThemiaWidget("screenTimeChart", typeof(ScreenTimeChartModule))]

namespace Themia.Widget.ScreenTimeChart;

/// <summary>[S-3330] The screen-time history over a week or a month, as stacked bars or a calendar. SPEC.md beside this file.</summary>
public sealed class ScreenTimeChartModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeChart"),
        ConfigType = typeof(ScreenTimeChartConfig),
        CreateDefault = () => new ScreenTimeChartConfig(),
        Json = ScreenTimeChartJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "history", "week", "chart", "statistics"],
        Label = ScreenTimeChartStr.WidgetsC_ScreenTime_Chart_Label.Local(),
        Description = ScreenTimeChartStr.WidgetsC_ScreenTime_Chart_Desc.Local(),
        Glyph = Glyphs.ScreenTimeChart,
        DefaultWidth = 320,
        DefaultHeight = 220,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.ScreenTime, 0, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => ScreenTimeChartSettings.Rows(rows.Rows, (ScreenTimeChartConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ScreenTimeChartView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScreenTimeChartConfig))]
internal sealed partial class ScreenTimeChartJsonContext : JsonSerializerContext;
