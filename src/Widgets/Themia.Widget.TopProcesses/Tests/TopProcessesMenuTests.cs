using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.TopProcesses.Tests;

/// <summary>[S-9700] The top processes' options on its properties menu: Appearance, Sort by, Rows, Group by program.</summary>
public sealed class TopProcessesMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(TopProcessesConfig config)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        TopProcessesMenu.Lines(menu, config);
        return menu;
    }

    [Fact]
    public void S9700_the_first_level_is_sort_rows_then_grouping_and_a_row_has_no_lines_choice()
    {
        var menu = Build(new TopProcessesConfig());

        Assert.Equal(
            [
                TopProcessesStr.Details_Settings_Processes_Sort_Label.Text(),
                Str.Kit_Rows.Text(),
                TopProcessesStr.Details_Settings_Processes_Group_Label.Text(),
            ],
            menu.Lines.Select(l => l.Text));
        Assert.True(menu.Lines[2].Checked);
    }

    [Fact]
    public void S9700_each_line_sets_its_option_and_stores_once()
    {
        var config = new TopProcessesConfig();

        Build(config).Lines[0].Children![1].Action!();
        Assert.Equal(ProcessSort.Memory, config.SortBy);

        Build(config).Lines[2].Action!();
        Assert.False(config.GroupByName);
        Assert.Equal(2, _saves);

        Build(config).Lines[0].Children![1].Action!();
        Assert.Equal(2, _saves);
    }
}
