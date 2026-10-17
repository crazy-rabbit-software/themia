using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("rss", typeof(Themia.App.Widgets.Modules.RssModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Rss widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class RssModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => RssConfig.Descriptor;

    public FrameworkElement CreateView() => new RssView();
}
