using Themia.App.Shell.Config;
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
    public void S9700_the_first_level_is_appearance_sort_rows_then_grouping()
    {
        var menu = Build(new TopProcessesConfig());

        Assert.Equal(0, menu.CardAt);
        Assert.Equal(
            [
                TopProcessesStr.Details_Settings_Processes_Sort_Label.Text(),
                TopProcessesStr.Details_Settings_Processes_Rows_Label.Text(),
                TopProcessesStr.Details_Settings_Processes_Group_Label.Text(),
            ],
            menu.Lines.Select(l => l.Text));
        Assert.Equal([TopProcessesStr.Details_Settings_Processes_Rows_Fit.Text(), "3", "5", "10", "20", "50"], menu.Lines[1].Children!.Select(c => c.Text));
        Assert.True(menu.Lines[1].Children![0].Checked);
        Assert.True(menu.Lines[2].Checked);
    }

    [Fact]
    public void S9700_each_line_sets_its_option_and_stores_once()
    {
        var config = new TopProcessesConfig();

        Build(config).Lines[0].Children![1].Action!();
        Assert.Equal(ProcessSort.Memory, config.SortBy);

        Build(config).Lines[1].Children![3].Action!();
        Assert.Equal(10, config.MaxRows);

        Build(config).Lines[2].Action!();
        Assert.False(config.GroupByName);
        Assert.Equal(3, _saves);

        Build(config).Lines[0].Children![1].Action!();
        Assert.Equal(3, _saves);
    }

    [Fact]
    public void S9700_a_row_count_not_offered_holds_fit()
        => Assert.True(Build(new TopProcessesConfig { MaxRows = 7 }).Lines[1].Children![0].Checked);
}
