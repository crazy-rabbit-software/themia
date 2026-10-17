using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("stopwatch", typeof(Themia.App.Widgets.Modules.StopwatchModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Stopwatch widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class StopwatchModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => StopwatchConfig.Descriptor;

    public FrameworkElement CreateView() => new StopwatchView();
}
