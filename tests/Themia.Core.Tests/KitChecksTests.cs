using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-7037] [S-7038] The kit's check look: the Tasks widget's ladder, slots and editor rules
/// ([S-780]..[S-788]) as the kit's, proved equal to <see cref="TaskLayout"/> before it goes.
/// </summary>
public sealed class KitChecksTests
{
    private static readonly KitSpec Check = new(KitRowLook.Check, KitShow.All, () => string.Empty, () => string.Empty, KitTime.None);

    [Fact]
    public void S7037_the_check_ladder_is_the_tasks_ladder_rung_for_rung()
    {
        foreach (var width in new[] { 0.0, 100, 120, 160, 180, 200, 239, 240, 320, 400, 480, 560, 720, 800, 1000, 1400 })
        {
            foreach (var height in new[] { 0.0, 76, 106, 120, 128, 160, 196, 279, 280, 356, 376, 480, 516, 640 })
            {
                foreach (var count in new[] { 0, 1, 3, 10, 30 })
                {
                    foreach (var (writable, today, due, compact, lines) in new[] { (true, false, true, false, 2), (false, true, true, true, 3), (true, true, false, false, 1), (false, false, true, true, 6) })
                    {
                        var area = new WidgetArea(width, height);
                        var tasks = TaskLayout.Plan(area, new TaskLayoutInput(count, writable, today, due, compact ? TasksStyle.Compact : TasksStyle.List, lines), 40, 32);
                        var shows = (today ? KitShow.Subtitle : KitShow.None) | (due ? KitShow.When : KitShow.None);
                        var kit = KitLadder.Plan(area, Check, new KitChoices(shows) { Count = count, CanAdd = writable, TitleLines = lines, WhenBeside = compact });
                        var at = $"{width}x{height} n={count} w={writable} t={today} d={due} c={compact} l={lines}";

                        Assert.True(tasks.CompactRows == kit.Compact, at);
                        Assert.True(tasks.RowHeight == kit.RowHeight, at);
                        Assert.True(tasks.TitleLines == kit.TitleLines, at);
                        Assert.True((tasks.Due != DueDetail.None) == kit.When, at);
                        Assert.True((tasks.Due == DueDetail.Urgent) == kit.UrgentOnly, at);
                        Assert.True((tasks.Due == DueDetail.Long) == kit.LongWhen, at);
                        Assert.True(tasks.DueBeside == kit.WhenBeside, at);
                        Assert.True(tasks.PriorityGlyph == kit.Mark, at);
                        Assert.True(tasks.ListNames == kit.Subtitle, at);
                        Assert.True(tasks.AddRow == kit.AddRow, at);
                        Assert.True(tasks.Columns == kit.Columns, at);
                        Assert.True(tasks.CompletedHeading == kit.Headings, at);
                        Assert.True(tasks.NarrowInset == kit.Narrow, at);
                    }
                }
            }
        }
    }

    [Fact]
    public void S7038_slots_whole_lines_and_the_editors_tab_are_the_tasks()
    {
        SlotBox[] boxes = [new(0, 0, 100, 40), new(0, 40, 100, 40), new(0, 80, 100, 60)];
        KitSlot[] slots = [.. boxes.Select(b => new KitSlot(b.X, b.Y, b.Width, b.Height))];

        foreach (var (x, y) in new[] { (10.0, 50.0), (10, 45), (10, 115), (10, 85), (200, 50) })
        {
            Assert.Equal(TaskLayout.Retarget(boxes, 0, x, y), KitChecks.Retarget(slots, 0, x, y));
        }

        double[] bottoms = [40, 80, 100, 140];
        bool[] headings = [false, false, true, false];
        foreach (var room in new[] { 30.0, 80, 100, 120, 200 })
        {
            Assert.Equal(TaskLayout.WholeLineCap(bottoms, room, headings), KitChecks.WholeLineCap(bottoms, room, headings));
        }

        Assert.Equal(TaskLayout.NextEditor(["a", "b"], "a", false), KitChecks.NextEditor(["a", "b"], "a", false));
        Assert.Null(KitChecks.NextEditor(["a", "b"], "b", false));
        Assert.Equal(TaskLayout.IsDrag(3, 4), KitChecks.IsDrag(3, 4));
        Assert.Equal(TaskLayout.LinesFor(7, 2), KitChecks.LinesFor(7, 2));
    }

    [Fact]
    public void S7037_a_check_lead_says_whether_it_is_done()
    {
        Assert.True(KitLead.Check(done: true).IsDone);
        Assert.False(KitLead.Check(done: false, KitTone.Critical).IsDone);
        Assert.Equal(KitTone.Critical, KitLead.Check(done: false, KitTone.Critical).Tone);
    }
}
