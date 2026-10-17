using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("removableDrives", typeof(Themia.App.Widgets.Modules.RemovableDrivesModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The RemovableDrives widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class RemovableDrivesModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => RemovableDrivesConfig.Descriptor;

    public FrameworkElement CreateView() => new RemovableDrivesView();
}
