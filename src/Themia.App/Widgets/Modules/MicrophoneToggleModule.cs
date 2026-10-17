using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("microphoneToggle", typeof(Themia.App.Widgets.Modules.MicrophoneToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The MicrophoneToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MicrophoneToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MicrophoneToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(WidgetKind.MicrophoneToggle);
}
