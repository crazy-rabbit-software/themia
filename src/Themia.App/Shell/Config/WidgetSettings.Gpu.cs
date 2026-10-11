using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>The GPU meter's page: which adapters, then the history and refresh every meter has ([S-1762]).</summary>
internal static partial class WidgetSettings
{
    public static void Gpu(List<FrameworkElement> rows, GpuConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Gpu_Gpu.Text()));

        // [S-433] One switch per adapter, offered only when there is a choice to make. The picker
        // reflects the raw selection: unticking the last adapter leaves it empty, and the widget
        // falls back to the primary adapter on its own.
        var adapters = GpuSampler.Adapters();
        if (adapters.Count > 1)
        {
            foreach (var adapter in adapters)
            {
                var index = adapter.Index;
                var selected = GpuRules.SelectedAdapters(config.Adapters).Contains(index);

                rows.Add(FluentLayout.Row(Glyphs.Gpu, adapter.Name,
                    adapter.DedicatedBytes > 0 ? Str.WidgetsB_Settings_Gpu_OwnMemory.Format(("size", Formatting.Bytes(adapter.DedicatedBytes))) : Str.WidgetsB_Settings_Gpu_SharesMemory.Text(),
                    FluentLayout.Toggle(selected, on =>
                    {
                        var list = GpuRules.SelectedAdapters(config.Adapters);
                        list.Remove(index);

                        if (on)
                        {
                            list.Add(index);
                        }

                        config.Adapters = list;
                        save();
                    })));
            }

            rows.Add(FluentLayout.Note(Str.WidgetsB_Settings_Gpu_PanelsNote.Text()));
        }

        // [S-1200] The history toggle, its span and the refresh: the rows every meter ends with.
        MeterRows(rows, config, save);
    }
}
