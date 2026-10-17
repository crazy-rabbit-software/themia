using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("shortcuts", typeof(Themia.App.Widgets.Modules.ShortcutsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Shortcuts widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ShortcutsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ShortcutsConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Shortcuts(rows.Rows, (ShortcutsConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ShortcutsView(host);
}
