using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("gameModeToggle", typeof(Themia.App.Widgets.Modules.GameModeToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The GameModeToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class GameModeToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => GameModeToggleConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(GameModeToggleConfig.Descriptor.Id);
}
