using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTime;

[assembly: ThemiaWidget("screenTimeChart", typeof(ScreenTimeChartModule))]

namespace Themia.Widget.ScreenTime;

/// <summary>[S-3330] The screen-time history over a week or a month, as stacked bars or a calendar. SPEC.md beside this file.</summary>
public sealed class ScreenTimeChartModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeChart"),
        ConfigType = typeof(ScreenTimeChartConfig),
        CreateDefault = () => new ScreenTimeChartConfig(),
        Json = ScreenTimeJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "history", "week", "chart", "statistics"],
        Label = ScreenTimeStr.WidgetsC_ScreenTime_Chart_Label.Local(),
        Description = ScreenTimeStr.WidgetsC_ScreenTime_Chart_Desc.Local(),
        Glyph = Glyphs.ScreenTimeChart,
        Sizing = c => ScreenTimeChartLayout.Sizing(((ScreenTimeChartConfig)c).Style, ((ScreenTimeChartConfig)c).Period),
        Traits = WidgetTraits.None,
        Links = LinkUse.Of([LinkTopicId.HoveredApp, LinkTopicId.SelectedApp, LinkTopicId.HoveredDate], [LinkTopicId.HoveredApp, LinkTopicId.SelectedApp, LinkTopicId.HoveredDate]),
        Add = new(AddFolderId.ScreenTime, 0, 1),
    };

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => ScreenTimeChartMenu.Lines(menu, (ScreenTimeChartConfig)config, SettingsHost.ScreenTime);

    public FrameworkElement CreateView(IWidgetHost host) => new ScreenTimeChartView(host);
}
