using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.WeekPlanner;

[assembly: ThemiaWidget("weekPlanner", typeof(WeekPlannerModule))]

namespace Themia.Widget.WeekPlanner;

/// <summary>Week planner: the week's events and today's tasks, day by day. SPEC.md beside this file.</summary>
public sealed class WeekPlannerModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weekPlanner"),
        ConfigType = typeof(WeekPlannerConfig),
        CreateDefault = () => new WeekPlannerConfig(),
        Json = WeekPlannerJsonContext.Default,
        Keywords = ["planner", "week", "schedule", "agenda", "todo", "to do", "tasks", "events", "calendar", "plan"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = WeekPlannerStr.Planner_Label.Local(),
        Description = WeekPlannerStr.Planner_Desc.Local(),
        Glyph = Glyphs.WeekPlanner,
        DefaultWidth = 480,
        DefaultHeight = 280,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 0, 2),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WeekPlannerSettings.Rows(rows, (WeekPlannerConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new WeekPlannerView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeekPlannerConfig))]
internal sealed partial class WeekPlannerJsonContext : JsonSerializerContext;
