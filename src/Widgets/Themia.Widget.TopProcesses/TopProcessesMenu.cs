using Themia.Core.Model;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.TopProcesses;

namespace Themia.App.Shell.Config;

/// <summary>[S-9700] The top processes' options, all on its properties menu ([S-3719]): Appearance, Sort by ([S-2411]), Rows ([S-2412]) and Group by program ([S-2413]). It has no settings page.</summary>
internal static class TopProcessesMenu
{
    public static void Lines(WidgetMenuLines menu, TopProcessesConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.Lines.Add(menu.Choice(Glyphs.Sort, TopProcessesStr.Details_Settings_Processes_Sort_Label.Text(),
            [(ProcessSort.Cpu, TopProcessesStr.Details_Settings_Processes_Sort_Cpu.Text()), (ProcessSort.Memory, TopProcessesStr.Details_Settings_Processes_Sort_Memory.Text())],
            config.SortBy, value => config.SortBy = value));

        // [S-2412] How many rows, as every list states them ([S-2440]); a row always has its two lines.
        menu.Lines.AddRange(KitMenuLines.Rows(menu, config));

        menu.Lines.Add(menu.Toggle(Glyphs.Group, TopProcessesStr.Details_Settings_Processes_Group_Label.Text(), config.GroupByName, on => config.GroupByName = on));
    }
}
