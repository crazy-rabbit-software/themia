using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("shutdown", typeof(Themia.App.Widgets.Modules.ShutdownModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Shutdown widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ShutdownModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ShutdownConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(ShutdownConfig.Descriptor.Id, host);
}
