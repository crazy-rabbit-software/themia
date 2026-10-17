using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("drive", typeof(Themia.App.Widgets.Modules.DriveModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Drive widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class DriveModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => DriveConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Drive(rows.Rows, (DriveConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new DriveView(host);
}
