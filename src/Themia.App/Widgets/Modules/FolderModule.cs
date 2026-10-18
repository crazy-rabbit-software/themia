using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("folder", typeof(Themia.App.Widgets.Modules.FolderModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Folder widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class FolderModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => FolderConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Folder(rows.Rows, (FolderConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new FolderView(host);
}
