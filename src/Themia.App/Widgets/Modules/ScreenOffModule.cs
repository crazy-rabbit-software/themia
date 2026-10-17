using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("screenOff", typeof(Themia.App.Widgets.Modules.ScreenOffModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ScreenOff widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ScreenOffModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ScreenOffConfig.Descriptor;

    public FrameworkElement CreateView() => new PowerButtonView(WidgetKind.ScreenOff);
}
