using Themia.Core.Shell;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Tasks.Tests;

/// <summary>[S-616] A task's menu and keys, as the kit builds every item menu and key map.</summary>
public sealed class TaskSpecTests
{
    private static readonly TaskItem Open = new("t1", "l1", "Water the plants", Completed: false);

    [Fact]
    public void S616_a_tasks_menu_is_the_kits_one_order()
    {
        var lines = KitMenu.Lines(TaskSpec.Actions(Open, renamable: true));

        Assert.Equal([KitActionKind.Toggle, (KitActionKind?)null, KitActionKind.Rename, null, KitActionKind.Delete], lines.Select(l => l.Action?.Kind));
        Assert.Equal([QuickCommand.Rename, QuickCommand.Delete], lines.Where(l => l.Quick is not null).Select(l => l.Quick!.Value));
        Assert.Equal(KeyChord.Plain(ChordKey.F2), lines[2].Shortcut);
        Assert.Null(lines[0].Action!.Checked); // "Mark as done" says what it does: a plain line
    }

    [Fact]
    public void S616_done_offers_undo_and_an_unconfirmed_task_cannot_be_renamed_yet()
    {
        var done = TaskSpec.Actions(Open with { Completed = true }, renamable: false);

        Assert.Equal(KitIcon.Undo, done[0].Icon);
        Assert.False(done[1].Runnable);
        Assert.Null(KitMenu.Primary(done));
    }

    [Fact]
    public void S616_double_click_and_enter_rename_space_ticks_alt_shift_moves()
    {
        Assert.Equal(TaskSpec.RenameId, KitMenu.Primary(TaskSpec.Actions(Open, renamable: true))?.Id);
        Assert.Equal(KitKeyCommand.Open, KitKeys.Decide(WidgetKey.Enter, 0, 3).Command);
        Assert.Equal(KitKeyCommand.Toggle, KitKeys.Decide(WidgetKey.Space, 0, 3).Command);
        Assert.Equal(KitKeyCommand.MoveDown, KitKeys.Decide(WidgetKey.MoveDown, 0, 3).Command);
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 0), KitKeys.Decide(WidgetKey.Up, -1, 3)); // Up with nothing focused: the first
    }
}
