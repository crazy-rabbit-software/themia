using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("screenTimeDonut", typeof(Themia.App.Widgets.Modules.ScreenTimeDonutModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The ScreenTimeDonut widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ScreenTimeDonutModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ScreenTimeDonutConfig.Descriptor;

    public FrameworkElement CreateView() => new ScreenTimeDonutView();
}
