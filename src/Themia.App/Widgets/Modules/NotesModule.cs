using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("notes", typeof(Themia.App.Widgets.Modules.NotesModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Notes widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NotesModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NotesConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Notes(rows.Rows, (NotesConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new NotesView(host);
}
