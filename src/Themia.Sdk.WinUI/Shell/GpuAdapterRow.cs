using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>The adapter row the GPU meter's and the GPU details' pages share.</summary>
public static class GpuAdapterRow
{
    /// <summary>
    /// [S-433] [S-2431] Which adapter, by name, offered only with two or more: the GPU meter's and the
    /// GPU details' one row. A chosen adapter that is gone shows as the first.
    /// </summary>
    public static void Add(List<FrameworkElement> rows, int adapter, Action<int> choose, Action save)
    {
        var adapters = GpuAdapters.List();

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
