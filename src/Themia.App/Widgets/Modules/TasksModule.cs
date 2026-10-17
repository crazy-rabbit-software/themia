using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("tasks", typeof(Themia.App.Widgets.Modules.TasksModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Tasks widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class TasksModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => TasksConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Tasks(rows.Rows, (TasksConfig)config, rows.Save, rows.ShowAccounts);

    public FrameworkElement CreateView() => new TasksView();
}
