using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("clock", typeof(Themia.App.Widgets.Modules.ClockModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Clock widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ClockModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ClockConfig.Descriptor;

    public FrameworkElement CreateView() => new ClockView();
}
