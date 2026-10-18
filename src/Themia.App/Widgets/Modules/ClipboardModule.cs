using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("clipboard", typeof(Themia.App.Widgets.Modules.ClipboardModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Clipboard widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ClipboardModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ClipboardConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Clipboard(rows.Rows, (ClipboardConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ClipboardView(host);
}
