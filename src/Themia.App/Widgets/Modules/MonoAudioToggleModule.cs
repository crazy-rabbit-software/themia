using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("monoAudioToggle", typeof(Themia.App.Widgets.Modules.MonoAudioToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The MonoAudioToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MonoAudioToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MonoAudioToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(MonoAudioToggleConfig.Descriptor.Id);
}
