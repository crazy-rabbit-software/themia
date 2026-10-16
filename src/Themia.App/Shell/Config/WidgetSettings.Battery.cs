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

        MeterRows(rows, battery, save);
    }
}
