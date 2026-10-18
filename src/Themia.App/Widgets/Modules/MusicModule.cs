using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("music", typeof(Themia.App.Widgets.Modules.MusicModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Music widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MusicModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MusicConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Music(rows.Rows, (MusicConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new MusicView(host);
}
