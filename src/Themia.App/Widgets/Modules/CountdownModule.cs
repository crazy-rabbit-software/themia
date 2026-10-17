using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("countdown", typeof(Themia.App.Widgets.Modules.CountdownModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Countdown widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class CountdownModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => CountdownConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Countdown(rows.Rows, (CountdownConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new CountdownView();
}
