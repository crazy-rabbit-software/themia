using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("recycleBin", typeof(Themia.App.Widgets.Modules.RecycleBinModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The RecycleBin widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class RecycleBinModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => RecycleBinConfig.Descriptor;

    public FrameworkElement CreateView() => new RecycleBinView();
}
