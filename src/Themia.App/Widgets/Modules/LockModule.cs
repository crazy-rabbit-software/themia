using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("lock", typeof(Themia.App.Widgets.Modules.LockModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Lock widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class LockModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => LockConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(LockConfig.Descriptor.Id, host);
}
