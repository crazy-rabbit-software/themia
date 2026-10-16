using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>The GPU meter's page: which adapter, then the history and refresh every meter has ([S-1762]).</summary>
internal static partial class WidgetSettings
{
    public static void Gpu(List<FrameworkElement> rows, GpuConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        // [S-433] One adapter per widget, offered only when there is a choice to make.
        AdapterRow(rows, config.Adapter, value => config.Adapter = value, save);

        // [S-1200] The history toggle, its span and the refresh: the rows every meter ends with.
        MeterRows(rows, config, save);
    }

    /// <summary>
    /// [S-433] [S-2431] Which adapter, by name, offered only with two or more: the GPU meter's and the
    /// GPU details' one row. A chosen adapter that is gone shows as the first.
    /// </summary>
    private static void AdapterRow(List<FrameworkElement> rows, int adapter, Action<int> choose, Action save)
    {
        var adapters = GpuSampler.Adapters();

        if (adapters.Count < 2)
        {
            return;
        }

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Gpu_Gpu.Text()));
        var current = adapter >= 0 && adapter < adapters.Count ? adapter : 0;

        rows.Add(FluentLayout.Row(Glyphs.Gpu, Str.Details_Settings_Gpu_Adapter_Label.Text(), Str.Details_Settings_Gpu_Adapter_Desc.Text(),
            FluentLayout.Choice(adapters.Select(a => (a.Index, a.Name)).ToList(), current, value => { choose(value); save(); }),
            "adapter", "graphics card"));
    }
}
