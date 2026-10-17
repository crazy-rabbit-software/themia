using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("mixer", typeof(Themia.App.Widgets.Modules.MixerModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Mixer widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MixerModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MixerConfig.Descriptor;

    public FrameworkElement CreateView() => new MixerView();
}
