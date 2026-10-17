using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("scratchpad", typeof(Themia.App.Widgets.Modules.ScratchpadModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Scratchpad widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ScratchpadModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ScratchpadConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Scratchpad(rows.Rows, (ScratchpadConfig)config, rows.Save);

    public FrameworkElement CreateView() => new ScratchpadView();
}
