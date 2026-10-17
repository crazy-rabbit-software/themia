using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("doNotDisturbToggle", typeof(Themia.App.Widgets.Modules.DoNotDisturbToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The DoNotDisturbToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class DoNotDisturbToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => DoNotDisturbToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(WidgetKind.DoNotDisturbToggle);
}
