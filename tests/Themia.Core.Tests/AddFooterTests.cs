using Themia.Core.Habits;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3850] The add footer shared by the list widgets that add in place.</summary>
public sealed class AddFooterTests
{
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);

    private static bool Show(WidgetArea area, bool writable = true, bool empty = false, double row = 40)
    {
        var room = new Disclosure(area);
        room.Reserve(row);
        return AddFooter.Show(room, area, writable, empty, row);
    }

    [Fact]
    public void S3850_shows_from_regular_height_and_hides_on_a_short_tile()
    {
        Assert.False(Show(XS));
        Assert.False(Show(S));
        Assert.False(Show(Wide));
        Assert.True(Show(M));
        Assert.True(Show(Tall));
        Assert.True(Show(L));
        Assert.True(Show(XL));
    }

    [Fact]
    public void S3850_an_empty_list_always_has_it_at_every_size()
    {
        foreach (var area in new[] { XS, S, M, Wide, Tall, L, XL })
        {
            Assert.True(Show(area, empty: true));
        }
    }

    [Fact]
    public void S3850_never_where_nothing_can_be_added()
    {
        Assert.False(Show(XL, writable: false));
        Assert.False(Show(XS, writable: false, empty: true));
    }

    [Fact]
    public void S3850_spends_a_whole_row_and_only_when_it_fits()
    {
        var room = new Disclosure(new WidgetArea(320, 135));
        room.Reserve(40);
        Assert.True(AddFooter.Show(room, new WidgetArea(320, 135), true, false, 40));
        Assert.Equal(55, room.Remaining, 3);

        // Regular height but not a whole row left after the first: no footer, and the ladder closes.
        var tight = new WidgetArea(320, 130);
        var small = new Disclosure(tight.Width, 70);
        small.Reserve(40);
        Assert.False(AddFooter.Show(small, tight, true, false, 40));
        Assert.False(small.Take(1));
    }

    [Fact]
    public void S3850_habits_and_tasks_agree_at_every_tier()
    {
        foreach (var area in new[] { XS, S, M, Wide, Tall, L, XL })
        {
            foreach (var count in new[] { 0, 3 })
            {
                var habits = HabitLayout.Plan(area, new HabitLayoutInput(count, true, true, 7), 40, 32).AddRow;
                var tasks = TaskLayout.Plan(area, new TaskLayoutInput(count, true, false, true, Themia.Core.Model.TasksStyle.List, 2), 40, 32).AddRow;
                Assert.Equal(habits, tasks);
            }
        }
    }
}
