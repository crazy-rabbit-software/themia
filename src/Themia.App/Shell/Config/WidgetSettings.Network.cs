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

        MeterRows(rows, network, save);
    }
}
