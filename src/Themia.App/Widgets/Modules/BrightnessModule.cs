using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("brightness", typeof(Themia.App.Widgets.Modules.BrightnessModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Brightness widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class BrightnessModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => BrightnessConfig.Descriptor;

    public FrameworkElement CreateView() => new BrightnessView();
}
