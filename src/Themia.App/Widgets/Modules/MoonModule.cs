using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("moon", typeof(Themia.App.Widgets.Modules.MoonModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Moon widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MoonModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MoonConfig.Descriptor;

    public FrameworkElement CreateView() => new MoonView();
}
