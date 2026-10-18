using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static class NetworkSettings
{
    /// <summary>[S-1190] [S-1762] The network meter shows its reading and its history only: the rows every meter has.</summary>
    public static void Rows(List<FrameworkElement> rows, NetworkConfig network, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(save);

        MeterSettingsRows.Rows(rows, network, save);
    }
}
