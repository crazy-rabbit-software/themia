using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("restart", typeof(Themia.App.Widgets.Modules.RestartModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Restart widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class RestartModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => RestartConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(RestartConfig.Descriptor.Id, host);
}
