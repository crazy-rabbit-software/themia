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
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTimeApps;

[assembly: ThemiaWidget("screenTimeApps", typeof(ScreenTimeAppsModule))]

namespace Themia.Widget.ScreenTimeApps;

/// <summary>[S-3350] The apps of the screen-time history, busiest first, for a day or a rolling week or month, each with its limit. SPEC.md beside this file.</summary>
public sealed class ScreenTimeAppsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTimeApps"),
        ConfigType = typeof(ScreenTimeAppsConfig),
        CreateDefault = () => new ScreenTimeAppsConfig(),
        Json = ScreenTimeAppsJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "app usage", "list", "limits"],
        Label = ScreenTimeAppsStr.WidgetsC_ScreenTime_Apps_Label.Local(),
        Description = ScreenTimeAppsStr.WidgetsC_ScreenTime_Apps_Desc.Local(),
        Glyph = Glyphs.ScreenTime,
        Sizing = c => KitSizing.Sizing(((ScreenTimeAppsConfig)c).RowsShown, addFooter: false),
        SizeSets = (c, size) => KitSizing.SizeSets(c, size, ((ScreenTimeAppsConfig)c).RowsShown, addFooter: false),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Links = LinkUse.Of([LinkTopicId.HoveredApp, LinkTopicId.SelectedApp], [LinkTopicId.HoveredApp, LinkTopicId.SelectedApp]),
        Add = new(AddFolderId.ScreenTime, 0, 3),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => ScreenTimeAppsMenu.Lines(menu, (ScreenTimeAppsConfig)config, SettingsHost.ScreenTime);

    public FrameworkElement CreateView(IWidgetHost host) => new ScreenTimeAppsView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScreenTimeAppsConfig))]
internal sealed partial class ScreenTimeAppsJsonContext : JsonSerializerContext;
