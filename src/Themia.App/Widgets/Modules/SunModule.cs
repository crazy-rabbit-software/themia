using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("sun", typeof(Themia.App.Widgets.Modules.SunModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Sun widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class SunModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => SunConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Sun(rows.Rows, (SunConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new SunView();
}
