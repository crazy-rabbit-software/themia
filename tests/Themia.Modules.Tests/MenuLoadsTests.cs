using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-3745] [S-3746] [S-3729] The menu vocabulary the account widgets added: the line that opens
/// Settings › Accounts, lines read asynchronously, and a list's fewest items.
/// </summary>
public sealed class MenuLoadsTests
{
    private int _saves;
    private int _shown;

    private WidgetMenuLines Lines(bool host = true)
        => new(() => _saves++, _ => { }) { ShowAccounts = host ? () => _shown++ : null };

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S3745_the_accounts_line_opens_settings_accounts_and_saves_nothing()
    {
        var line = Lines().AccountsLine(null, "Manage accounts");

        Assert.False(line.Disabled);
        line.Action!();
        Assert.Equal(1, _shown);
        Assert.Equal(0, _saves);
    }

    [Fact]
    public void S3745_without_a_settings_window_the_accounts_line_is_greyed()
        => Assert.True(Lines(host: false).AccountsLine(null, "Manage accounts").Disabled);

    [Fact]
    public void S3729_a_list_greys_remove_at_its_fewest_items()
    {
        var items = new List<string> { "a" };
        var list = Lines().List(null, "Columns", items, new MenuList<string>(s => s, "none") { Min = 1 });

        var remove = Line(list.Children![0].Children!, Str.Common_Remove.Text());
        Assert.True(remove.Disabled);

        items.Add("b");
        list = Lines().List(null, "Columns", items, new MenuList<string>(s => s, "none") { Min = 1 });
        Assert.False(Line(list.Children![0].Children!, Str.Common_Remove.Text()).Disabled);
    }

    // ---- lines read asynchronously -----------------------------------------------------------

    [Fact]
    public void S3746_a_line_shows_nothing_until_its_answer_comes_then_the_menu_is_outdated()
    {
        var loads = new MenuLoads<string>((a, b) => a == b);
        var menu = Lines();
        var outdated = 0;
        menu.Outdated += () => outdated++;
        var answer = new TaskCompletionSource<string>();

        Assert.Null(loads.Read("work", menu, () => answer.Task).Value);
        answer.SetResult("Calendar");

        Assert.Equal(1, outdated);
        Assert.Equal("Calendar", loads.Read("work", Lines(), () => Task.FromResult("Calendar")).Value);
    }

    [Fact]
    public void S3746_the_same_answer_again_outdates_nothing_so_building_again_never_loops()
    {
        var loads = new MenuLoads<string>((a, b) => a == b);
        loads.Read("work", Lines(), () => Task.FromResult("Calendar"));

        var menu = Lines();
        var outdated = 0;
        menu.Outdated += () => outdated++;
        Assert.Equal("Calendar", loads.Read("work", menu, () => Task.FromResult("Calendar")).Value);
        Assert.Equal(0, outdated);

        // A changed answer is news.
        loads.Read("work", menu, () => Task.FromResult("Calendar, Birthdays"));
        Assert.Equal(1, outdated);
    }

    [Fact]
    public void S3746_one_read_per_key_at_a_time_and_every_menu_built_meanwhile_is_told()
    {
        var loads = new MenuLoads<string>((a, b) => a == b);
        var answer = new TaskCompletionSource<string>();
        var reads = 0;
        Task<string> Read()
        {
            reads++;
            return answer.Task;
        }

        var first = Lines();
        var second = Lines();
        var told = 0;
        first.Outdated += () => told++;
        second.Outdated += () => told++;

        loads.Read("work", first, Read);
        loads.Read("work", second, Read);
        answer.SetResult("Calendar");

        Assert.Equal(1, reads);
        Assert.Equal(2, told);
    }

    [Fact]
    public void S3746_a_read_that_throws_says_it_failed_once_and_keeps_nothing_half_read()
    {
        var loads = new MenuLoads<string>((a, b) => a == b);
        var menu = Lines();
        var outdated = 0;
        menu.Outdated += () => outdated++;

        var loaded = loads.Read("work", menu, () => Task.FromException<string>(new InvalidOperationException("offline")));
        Assert.True(loaded.Failed);
        Assert.Null(loaded.Value);

        // Failing again is no news.
        var again = Lines();
        again.Outdated += () => outdated++;
        loads.Read("work", again, () => Task.FromException<string>(new InvalidOperationException("offline")));
        Assert.Equal(1, outdated);
    }
}
