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
using Themia.Widget.Tasks;

[assembly: ThemiaWidget("tasks", typeof(TasksModule))]

namespace Themia.Widget.Tasks;

/// <summary>Tasks: a task list of an account or kept on this PC, ticked, renamed, reordered and added to. SPEC.md beside this file.</summary>
public sealed class TasksModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("tasks"),
        ConfigType = typeof(TasksConfig),
        CreateDefault = () => new TasksConfig(),
        Json = TasksJsonContext.Default,
        Keywords = ["todo", "to-do", "checklist", "reminders", "microsoft to do", "google tasks"],
        Providers = [HostProviders.Microsoft, HostProviders.Google],
        Label = TasksStr.WidgetsA_Catalog_Tasks_Label.Local(),
        Description = TasksStr.WidgetsA_Catalog_Tasks_Description.Local(),
        Glyph = Glyphs.Task,
        DefaultWidth = 320,
        DefaultHeight = 320,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 1, 1, Popular: 5),
    };

    /// <summary>[S-3719] [S-7516] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => TasksMenu.Lines(menu, (TasksConfig)config, SettingsHost.Tasks);

    public FrameworkElement CreateView(IWidgetHost host) => new TasksView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TasksConfig))]
internal sealed partial class TasksJsonContext : JsonSerializerContext;
