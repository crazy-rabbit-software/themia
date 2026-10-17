using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("music", typeof(Themia.App.Widgets.Modules.MusicModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Music widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MusicModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MusicConfig.Descriptor;

    public FrameworkElement CreateView() => new MusicView();
}
