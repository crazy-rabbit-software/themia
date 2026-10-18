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
            case PingDetailsConfig ping:
                PingDetailRows(rows, ping, save);
                break;
            case GpuDetailsConfig gpu:
                GpuAdapterRow.Add(rows, gpu.Adapter, value => gpu.Adapter = value, save);
                break;
            default:
                break;
        }
    }

    /// <summary>[S-2426] [S-2427] The host, as the ping meter's page has it, then the window.</summary>
    private static void PingDetailRows(List<FrameworkElement> rows, PingDetailsConfig config, Action save)
    {
        rows.Add(FluentLayout.Header(Str.Details_Catalog_Ping_Label.Text()));
        PingHostRow.Add(rows, () => config.Host, value => config.Host = value, save);

        int[] windows = [10, 30, 60, 120, 300];
        var current = windows.Contains(config.EffectiveWindow) ? config.EffectiveWindow : 60;

        rows.Add(FluentLayout.Row(Glyphs.Hours, Str.Details_Settings_Ping_Window_Label.Text(), Str.Details_Settings_Ping_Window_Desc.Text(),
            FluentLayout.Choice(windows.Select(w => (w, Str.Details_Settings_Ping_Window_Probes.Format(("count", LocaleFormat.Integer(w))))).ToList(), current,
                value => { config.Window = value; save(); }),
            "window", "samples"));
    }
}
