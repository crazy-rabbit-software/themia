using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("calendar", typeof(Themia.App.Widgets.Modules.CalendarModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Calendar widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class CalendarModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => CalendarWidgetConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Calendar(rows.Rows, (CalendarConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new CalendarView(host);
}
