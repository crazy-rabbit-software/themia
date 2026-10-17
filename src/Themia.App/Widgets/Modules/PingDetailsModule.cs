using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("pingDetails", typeof(Themia.App.Widgets.Modules.PingDetailsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The PingDetails widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class PingDetailsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => PingDetailsConfig.Descriptor;

    public FrameworkElement CreateView() => new DetailView(WidgetKind.PingDetails);
}
