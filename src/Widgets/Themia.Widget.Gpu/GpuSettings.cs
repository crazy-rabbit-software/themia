using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>The GPU meter's page: which adapter, then the history and refresh every meter has ([S-1762]).</summary>
internal static class GpuSettings
{
    public static void Rows(List<FrameworkElement> rows, GpuConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        // [S-433] One adapter per widget, offered only when there is a choice to make.
        GpuAdapterRow.Add(rows, config.Adapter, value => config.Adapter = value, save);

        // [S-1200] The history toggle, its span and the refresh: the rows every meter ends with.
        MeterSettingsRows.Rows(rows, config, save);
    }
}
