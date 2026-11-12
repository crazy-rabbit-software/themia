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
using Themia.App.Services.Accounts;

[assembly: ThemiaWidget("weekPlanner", typeof(WeekPlannerModule))]

namespace Themia.Widget.WeekPlanner;

/// <summary>Week planner: the week's events and today's tasks, day by day. SPEC.md beside this file.</summary>
public sealed class WeekPlannerModule : IWidgetModule
{
    /// <summary>[S-2042] A click on the card opens the calendar of the account it reads, on the web ([S-2005]).</summary>
    internal static CardOpen? Opens(IReadOnlyList<string> accountIds, IReadOnlyList<AccountInfo> accounts)
        => AccountPages.Home(HostFeatures.Calendar, accountIds, accounts);

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
        Sizing = c => WeekPlannerRules.Sizing(((WeekPlannerConfig)c).Days, ((WeekPlannerConfig)c).RowsPerDay),
        SizeSets = SetRows,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 0, 3),
    };

    /// <summary>[S-3719] [S-5324] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => WeekPlannerMenu.Lines(menu, (WeekPlannerConfig)config, SettingsHost.Office, SettingsHost.Tasks?.Accounts ?? []);

    /// <summary>[S-11410] Dragging the bottom edge sets the rows a day shows.</summary>
    private static WidgetConfig SetRows(WidgetConfig config, SizeDip size)
    {
        if (config is not WeekPlannerConfig planner)
        {
            return config;
        }

        var rows = WeekPlannerRules.Sizing(planner.Days, planner.RowsPerDay).HeightSteps!.Nearest(size.Height);
        if (rows == planner.RowsPerDay)
        {
            return config;
        }

        var copy = (WeekPlannerConfig)planner.Clone();
        copy.RowsPerDay = rows;
        return copy;
    }

    public FrameworkElement CreateView(IWidgetHost host) => new WeekPlannerView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeekPlannerConfig))]
internal sealed partial class WeekPlannerJsonContext : JsonSerializerContext;
