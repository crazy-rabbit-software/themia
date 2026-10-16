using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    /// <summary>[S-1190] [S-1762] The network meter shows its reading and its history only: the rows every meter has.</summary>
    public static void Network(List<FrameworkElement> rows, NetworkConfig network, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(save);

        // [S-6010] The kind's heading, so the shared Style row is the first row under it, also in
        // a look whose page has no history rows ([S-6074]).
        rows.Add(FluentLayout.Header(Str.WidgetsA_Catalog_Network_Label.Text()));

        MeterRows(rows, network, save);
    }
}
