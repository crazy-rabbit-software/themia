using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.TopProcesses;

namespace Themia.App.Shell.Config;

/// <summary>[S-2411]..[S-2413] The top processes' settings page: the ranking, how many rows, and whether a program's processes are one row.</summary>
internal static class TopProcessesSettings
{
    /// <summary>[S-2411]..[S-2413] Rank by, how many rows, and whether a program's processes are one row.</summary>
    public static void Rows(List<FrameworkElement> rows, TopProcessesConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(TopProcessesStr.Details_Catalog_Processes_Label.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Sort, TopProcessesStr.Details_Settings_Processes_Sort_Label.Text(), null,
            FluentLayout.Choice(
                [(ProcessSort.Cpu, TopProcessesStr.Details_Settings_Processes_Sort_Cpu.Text()), (ProcessSort.Memory, TopProcessesStr.Details_Settings_Processes_Sort_Memory.Text())],
                config.SortBy,
                value => { config.SortBy = value; save(); }),
            "sort", "order"));

        (int Value, string Label)[] counts = [(0, TopProcessesStr.Details_Settings_Processes_Rows_Fit.Text()), (3, "3"), (5, "5"), (10, "10"), (20, "20"), (TopProcessesConfig.MaxRowsLimit, "50")];
        var current = counts.Any(c => c.Value == config.MaxRows) ? config.MaxRows : 0;

        rows.Add(FluentLayout.Row(Glyphs.ListLayout, TopProcessesStr.Details_Settings_Processes_Rows_Label.Text(), TopProcessesStr.Details_Settings_Processes_Rows_Desc.Text(),
            FluentLayout.Choice(counts, current, value => { config.MaxRows = value; save(); }),
            "count", "rows"));

        rows.Add(FluentLayout.Row(Glyphs.BulletList, TopProcessesStr.Details_Settings_Processes_Group_Label.Text(), TopProcessesStr.Details_Settings_Processes_Group_Desc.Text(),
            FluentLayout.Toggle(config.GroupByName, on => { config.GroupByName = on; save(); }),
            "group"));
    }
}
