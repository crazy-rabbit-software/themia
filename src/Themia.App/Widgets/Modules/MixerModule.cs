using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("mixer", typeof(Themia.App.Widgets.Modules.MixerModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Mixer widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class MixerModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => MixerConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Mixer(rows.Rows, (MixerConfig)config, rows.Save);

    public FrameworkElement CreateView() => new MixerView();
}
