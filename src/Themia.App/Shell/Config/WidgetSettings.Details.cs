using Microsoft.UI.Xaml;
using Themia.App.Services;
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
            case TopProcessesConfig processes:
                ProcessRows(rows, processes, save);
                break;
            case NetworkDetailsConfig network:
                rows.Add(FluentLayout.Header(Str.Details_Catalog_Network_Label.Text()));
                rows.Add(FluentLayout.Row(Glyphs.Globe, Str.Details_Settings_Network_Public_Label.Text(), Str.Details_Settings_Network_Public_Desc.Format(("minutes", (long)NetworkDetailRules.PublicAddressLifetime.TotalMinutes)),
                    FluentLayout.Toggle(network.ShowPublicAddress, on => { network.ShowPublicAddress = on; save(); }),
                    "ip", "public ip", "address"));
                break;
            case PingDetailsConfig ping:
                PingDetailRows(rows, ping, save);
                break;
            case GpuDetailsConfig gpu:
                AdapterRow(rows, gpu, save);
                break;
            default:
                break;
        }
    }

    /// <summary>[S-2411]..[S-2413] Rank by, how many rows, and whether a program's processes are one row.</summary>
    private static void ProcessRows(List<FrameworkElement> rows, TopProcessesConfig config, Action save)
    {
        rows.Add(FluentLayout.Header(Str.Details_Catalog_Processes_Label.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Sort, Str.Details_Settings_Processes_Sort_Label.Text(), null,
            FluentLayout.Choice(
                [(ProcessSort.Cpu, Str.Details_Settings_Processes_Sort_Cpu.Text()), (ProcessSort.Memory, Str.Details_Settings_Processes_Sort_Memory.Text())],
                config.SortBy,
                value => { config.SortBy = value; save(); }),
            "sort", "order"));

        (int Value, string Label)[] counts = [(0, Str.Details_Settings_Processes_Rows_Fit.Text()), (3, "3"), (5, "5"), (10, "10"), (20, "20"), (TopProcessesConfig.MaxRowsLimit, "50")];
        var current = counts.Any(c => c.Value == config.MaxRows) ? config.MaxRows : 0;

        rows.Add(FluentLayout.Row(Glyphs.ListLayout, Str.Details_Settings_Processes_Rows_Label.Text(), Str.Details_Settings_Processes_Rows_Desc.Text(),
            FluentLayout.Choice(counts, current, value => { config.MaxRows = value; save(); }),
            "count", "rows"));

        rows.Add(FluentLayout.Row(Glyphs.BulletList, Str.Details_Settings_Processes_Group_Label.Text(), Str.Details_Settings_Processes_Group_Desc.Text(),
            FluentLayout.Toggle(config.GroupByName, on => { config.GroupByName = on; save(); }),
            "group"));
    }

    /// <summary>[S-2426] [S-2427] The host, as the ping meter's page has it, then the window.</summary>
    private static void PingDetailRows(List<FrameworkElement> rows, PingDetailsConfig config, Action save)
    {
        rows.Add(FluentLayout.Header(Str.Details_Catalog_Ping_Label.Text()));
        AddHostRow(rows, () => config.Host, value => config.Host = value, save);

        int[] windows = [10, 30, 60, 120, 300];
        var current = windows.Contains(config.EffectiveWindow) ? config.EffectiveWindow : 60;

        rows.Add(FluentLayout.Row(Glyphs.Hours, Str.Details_Settings_Ping_Window_Label.Text(), Str.Details_Settings_Ping_Window_Desc.Text(),
            FluentLayout.Choice(windows.Select(w => (w, Str.Details_Settings_Ping_Window_Probes.Format(("count", LocaleFormat.Integer(w))))).ToList(), current,
                value => { config.Window = value; save(); }),
            "window", "samples"));
    }

    /// <summary>[S-2431] Which adapter, offered only when there is a choice.</summary>
    private static void AdapterRow(List<FrameworkElement> rows, GpuDetailsConfig config, Action save)
    {
        var adapters = GpuSampler.Adapters();

        if (adapters.Count < 2)
        {
            return;
        }

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Gpu_Gpu.Text()));
        var current = config.Adapter < adapters.Count ? config.Adapter : 0;

        rows.Add(FluentLayout.Row(Glyphs.Gpu, Str.Details_Settings_Gpu_Adapter_Label.Text(), Str.Details_Settings_Gpu_Adapter_Desc.Text(),
            FluentLayout.Choice(adapters.Select(a => (a.Index, a.Name)).ToList(), current, value => { config.Adapter = value; save(); }),
            "adapter", "graphics card"));
    }
}
