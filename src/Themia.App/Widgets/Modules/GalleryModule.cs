using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("gallery", typeof(Themia.App.Widgets.Modules.GalleryModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Gallery widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class GalleryModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => GalleryConfig.Descriptor;

    public FrameworkElement CreateView() => new GalleryView();
}
