using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("audioVisualizer", typeof(Themia.App.Widgets.Modules.AudioVisualizerModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The AudioVisualizer widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class AudioVisualizerModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => AudioVisualizerConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new AudioVisualizerView();
}
