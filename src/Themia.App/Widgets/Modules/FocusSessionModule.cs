using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("focusSession", typeof(Themia.App.Widgets.Modules.FocusSessionModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The FocusSession widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class FocusSessionModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => FocusSessionConfig.Descriptor;

    public FrameworkElement CreateView() => new FocusSessionView();
}
