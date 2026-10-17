using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("videoStream", typeof(Themia.App.Widgets.Modules.VideoStreamModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The VideoStream widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class VideoStreamModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => VideoStreamConfig.Descriptor;

    public FrameworkElement CreateView() => new VideoStreamView();
}
