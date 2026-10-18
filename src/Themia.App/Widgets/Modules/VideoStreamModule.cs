using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("videoStream", typeof(Themia.App.Widgets.Modules.VideoStreamModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The VideoStream widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class VideoStreamModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => VideoStreamConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.VideoStream(rows.Rows, (VideoStreamConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new VideoStreamView(host);
}
