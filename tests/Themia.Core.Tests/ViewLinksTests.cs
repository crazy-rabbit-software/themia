using Themia.Core.Links;
using Xunit;

namespace Themia.Core.Tests;

public sealed class ViewLinksTests
{
    private static readonly AppLink Code = new("code");
    private static readonly AppLink Chrome = new("chrome");

    [Fact]
    public void S3300_one_board_per_view_id()
    {
        var registry = new LinkRegistry();

        Assert.Same(registry.For("home"), registry.For("home"));
        Assert.NotSame(registry.For("home"), registry.For("work"));
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void S3300_views_do_not_share_values()
    {
        var registry = new LinkRegistry();
        var home = registry.For("home").Join();
        var work = registry.For("work").Join();

        home.Set(LinkTopics.SelectedApp, Code);

        Assert.Equal(Code, home.Get(LinkTopics.SelectedApp));
        Assert.Null(work.Get(LinkTopics.SelectedApp));
    }

    [Fact]
    public void S3301_topics_are_distinct_and_carry_their_lifetime()
    {
        Assert.Equal(LinkLifetime.Hover, LinkTopics.HoveredApp.Lifetime);
        Assert.Equal(LinkLifetime.Sticky, LinkTopics.SelectedApp.Lifetime);
        Assert.Equal(LinkLifetime.Sticky, LinkTopics.SelectedDate.Lifetime);
        Assert.Equal(LinkLifetime.Sticky, LinkTopics.VisibleRange.Lifetime);
        Assert.Equal(Enum.GetValues<LinkTopicId>().Length, new[] { LinkTopics.HoveredApp.Id, LinkTopics.SelectedApp.Id, LinkTopics.SelectedDate.Id, LinkTopics.VisibleRange.Id, LinkTopics.HoveredEvent.Id }.Distinct().Count());
    }

    [Fact]
    public void S3302_toggle_selects_then_lets_go()
    {
        var member = new LinkBoard().Join();

        Assert.Equal(Code, member.Toggle(LinkTopics.SelectedApp, Code));
        Assert.Equal(Chrome, member.Toggle(LinkTopics.SelectedApp, Chrome));
        Assert.Null(member.Toggle(LinkTopics.SelectedApp, new AppLink("chrome")));
        Assert.Null(member.Get(LinkTopics.SelectedApp));
    }

    [Fact]
    public void S3303_a_member_never_hears_its_own_change()
    {
        var board = new LinkBoard();
        var list = board.Join();
        var ring = board.Join();
        var heardByList = new List<AppLink?>();
        var heardByRing = new List<AppLink?>();
        list.Watch(LinkTopics.HoveredApp, heardByList.Add);
        ring.Watch(LinkTopics.HoveredApp, heardByRing.Add);

        list.Set(LinkTopics.HoveredApp, Code);
        list.Clear(LinkTopics.HoveredApp);

        Assert.Empty(heardByList);
        Assert.Equal([Code, null], heardByRing);
    }

    [Fact]
    public void S3303_the_same_value_again_is_no_change()
    {
        var board = new LinkBoard();
        var a = board.Join();
        var b = board.Join();
        var heard = 0;
        b.Watch(LinkTopics.SelectedApp, _ => heard++);

        Assert.True(a.Set(LinkTopics.SelectedApp, Code));
        Assert.False(a.Set(LinkTopics.SelectedApp, new AppLink("code")));
        Assert.Equal(1, heard);
    }

    [Fact]
    public void S3304_a_hover_is_only_cleared_by_whoever_set_it()
    {
        var board = new LinkBoard();
        var list = board.Join();
        var ring = board.Join();

        list.Set(LinkTopics.HoveredApp, Code);
        ring.Set(LinkTopics.HoveredApp, Chrome);

        // The list's late "pointer left" arrives after the ring lit Chrome.
        Assert.False(list.Clear(LinkTopics.HoveredApp));
        Assert.Equal(Chrome, board.Peek(LinkTopics.HoveredApp));

        Assert.True(ring.Clear(LinkTopics.HoveredApp));
        Assert.Null(board.Peek(LinkTopics.HoveredApp));
    }

    [Fact]
    public void S3304_a_selection_can_be_cleared_by_anyone()
    {
        var board = new LinkBoard();
        var list = board.Join();
        var ring = board.Join();

        list.Set(LinkTopics.SelectedApp, Code);

        Assert.True(ring.Clear(LinkTopics.SelectedApp));
        Assert.Null(board.Peek(LinkTopics.SelectedApp));
    }

    [Fact]
    public void S3304_leaving_takes_the_hover_and_keeps_the_selection()
    {
        var board = new LinkBoard();
        var list = board.Join();
        var ring = board.Join();
        var heard = new List<AppLink?>();
        ring.Watch(LinkTopics.HoveredApp, heard.Add);

        list.Set(LinkTopics.HoveredApp, Code);
        list.Set(LinkTopics.SelectedApp, Chrome);
        list.Dispose();

        Assert.Null(board.Peek(LinkTopics.HoveredApp));
        Assert.Equal(Chrome, board.Peek(LinkTopics.SelectedApp));
        Assert.Equal([Code, null], heard);
        Assert.Equal(1, board.MemberCount);
    }

    [Fact]
    public void S3304_a_disposed_member_is_inert()
    {
        var board = new LinkBoard();
        var gone = board.Join();
        var heard = 0;
        gone.Watch(LinkTopics.SelectedApp, _ => heard++);
        gone.Dispose();
        gone.Dispose();

        Assert.False(gone.Set(LinkTopics.SelectedApp, Code));
        Assert.Null(gone.Toggle(LinkTopics.SelectedApp, Code));
        board.Join().Set(LinkTopics.SelectedApp, Chrome);

        Assert.Null(gone.Get(LinkTopics.SelectedApp));
        Assert.Equal(0, heard);
        Assert.Equal(1, board.MemberCount);
    }

    [Fact]
    public void S3305_a_view_switch_keeps_the_selection_for_when_it_comes_back()
    {
        var registry = new LinkRegistry();

        // The widgets of the view detach on the switch away …
        var before = registry.For("home").Join();
        before.Set(LinkTopics.HoveredApp, Code);
        before.Set(LinkTopics.SelectedApp, Chrome);
        before.Dispose();

        // … and attach again on the way back.
        var after = registry.For("home").Join();
        Assert.Null(after.Get(LinkTopics.HoveredApp));
        Assert.Equal(Chrome, after.Get(LinkTopics.SelectedApp));
        Assert.Equal(1, registry.For("home").MemberCount);
    }

    [Fact]
    public void S3305_a_deleted_view_takes_its_board()
    {
        var registry = new LinkRegistry();
        registry.For("home").Join().Set(LinkTopics.SelectedApp, Code);
        registry.For("work");

        registry.Retain(["work"]);

        Assert.Equal(1, registry.Count);
        Assert.Null(registry.For("home").Peek(LinkTopics.SelectedApp));
    }

    [Fact]
    public void S3306_a_handler_may_publish_while_being_told()
    {
        var board = new LinkBoard();
        var list = board.Join();
        var ring = board.Join();
        var chart = board.Join();
        var chartHeard = new List<AppLink?>();

        // The ring answers a hover by selecting it (a chain); the chart hears both, current values only.
        ring.Watch(LinkTopics.HoveredApp, hovered =>
        {
            if (hovered is not null)
            {
                ring.Set(LinkTopics.SelectedApp, hovered);
            }
        });
        chart.Watch(LinkTopics.SelectedApp, chartHeard.Add);

        list.Set(LinkTopics.HoveredApp, Code);

        Assert.Equal([Code], chartHeard);
        Assert.Equal(Code, board.Peek(LinkTopics.SelectedApp));
    }

    [Fact]
    public void S3306_one_failing_handler_does_not_silence_the_rest()
    {
        var board = new LinkBoard();
        var source = board.Join();
        var broken = board.Join();
        var fine = board.Join();
        var heard = 0;
        broken.Watch(LinkTopics.SelectedApp, _ => throw new InvalidOperationException("boom"));
        fine.Watch(LinkTopics.SelectedApp, _ => heard++);

        source.Set(LinkTopics.SelectedApp, Code);

        Assert.Equal(1, heard);
    }

    [Fact]
    public void S3311_a_range_is_ordered_and_inclusive()
    {
        var range = new RangeLink(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1));

        Assert.Equal(new DateOnly(2026, 10, 1), range.First);
        Assert.Equal(new DateOnly(2026, 10, 31), range.Last);
        Assert.True(range.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(range.Contains(new DateOnly(2026, 11, 1)));
        Assert.Equal(range, new RangeLink(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }
}
