using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("rotationLockToggle", typeof(Themia.App.Widgets.Modules.RotationLockToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The RotationLockToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class RotationLockToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => RotationLockToggleConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(RotationLockToggleConfig.Descriptor.Id);
}
