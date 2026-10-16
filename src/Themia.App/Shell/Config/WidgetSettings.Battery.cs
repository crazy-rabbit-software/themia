using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    /// <summary>[S-1180] [S-1762] The battery meter shows its reading and its history only: the rows every meter has.</summary>
    public static void Battery(List<FrameworkElement> rows, BatteryConfig battery, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(battery);
        ArgumentNullException.ThrowIfNull(save);

        // [S-6010] The kind's heading, so the shared Style row is the first row under it, also in
        // a look whose page has no history rows ([S-6074]).
        rows.Add(FluentLayout.Header(Themia.App.Widgets.WidgetCatalog.Definition(battery.Kind).Label));

        MeterRows(rows, battery, save);
    }
}
