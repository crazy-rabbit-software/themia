using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2400] The detail widgets' pages: each kind's own rows. No refresh ([S-3793]: each kind reads
/// at its own cadence) and no "which facts" switches: the tile shows more as it grows ([S-2402]).
/// </summary>
internal static partial class WidgetSettings
{
    /// <summary>The CPU, memory and battery details have nothing to choose: their page is the card's own rows.</summary>
    public static void Details(List<FrameworkElement> rows, DetailConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        switch (config)
        {
            case GpuDetailsConfig gpu:
                GpuAdapterRow.Add(rows, gpu.Adapter, value => gpu.Adapter = value, save);
                break;
            default:
                break;
        }
    }
}
