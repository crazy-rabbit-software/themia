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

[assembly: ThemiaWidget("screenTimeDonut", typeof(ScreenTimeDonutModule))]

namespace Themia.Widget.ScreenTime;

/// <summary>[S-3340] The screen-time history as one ring: the busiest apps and the rest, the total in its hole. SPEC.md beside this file.</summary>
public sealed class ScreenTimeDonutModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeDonut"),
        ConfigType = typeof(ScreenTimeDonutConfig),
        CreateDefault = () => new ScreenTimeDonutConfig(),
        Json = ScreenTimeJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "share", "pie", "ring", "statistics"],
        Label = ScreenTimeStr.WidgetsC_ScreenTime_Donut_Label.Local(),
        Description = ScreenTimeStr.WidgetsC_ScreenTime_Donut_Desc.Local(),
        Glyph = Glyphs.ScreenTimeDonut,
        Sizing = c => ScreenTimeDonutLayout.Sizing(((ScreenTimeDonutConfig)c).Period),
        Traits = WidgetTraits.None,
        Links = LinkUse.Of([LinkTopicId.HoveredApp, LinkTopicId.SelectedApp], [LinkTopicId.HoveredApp, LinkTopicId.SelectedApp, LinkTopicId.SelectedDate]),
        Add = new(AddFolderId.ScreenTime, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => ScreenTimeDonutMenu.Lines(menu, (ScreenTimeDonutConfig)config, SettingsHost.ScreenTime);

    public FrameworkElement CreateView(IWidgetHost host) => new ScreenTimeDonutView(host);
}
