using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("soundToggle", typeof(Themia.App.Widgets.Modules.SoundToggleModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The SoundToggle widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class SoundToggleModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => SoundToggleConfig.Descriptor;

    public FrameworkElement CreateView() => new QuickToggleView(SoundToggleConfig.Descriptor.Id);
}
