using Themia.Core.Desktop;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13101]..[S-13103] Windows' desktop, made whole: Ctrl+Shift+N, Explorer's undo of the desktop's file operations, its menu line.</summary>
public sealed class DesktopFileUndoTests
{
    private const string Desk = @"C:\Users\a\Desktop\";
    private const string Docs = @"C:\Users\a\Documents\";

    private static Func<string, bool> There(params string[] paths)
    {
        var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }

    [Fact]
    public void S13101_ctrl_shift_n_is_the_new_folder_key_and_nothing_else_is()
    {
        Assert.Equal(WidgetKey.NewFolder, WidgetKeys.FromVirtualKey(0x4E, shift: true, ctrl: true, alt: false));
        Assert.Null(WidgetKeys.FromVirtualKey(0x4E, shift: false, ctrl: true, alt: false));
        Assert.Null(WidgetKeys.FromVirtualKey(0x4E, shift: true, ctrl: true, alt: true));
        Assert.Null(WidgetKeys.FromVirtualKey(0x4E, shift: true, ctrl: false, alt: false));
    }

    // ---- recording ----------------------------------------------------------------------------------------------------

    [Fact]
    public void S13102_a_paste_of_a_copy_records_each_arrival_with_its_source_by_name()
    {
        var step = DesktopFileUndo.Record(DesktopFileUndoKind.Copy, [Desk + "a.txt", Desk + "Photos"], [Docs + "A.TXT", Docs + "Photos"])!;

        Assert.Equal(DesktopFileUndoKind.Copy, step.Kind);
        Assert.Equal([new DesktopFileChange(Docs + "A.TXT", Desk + "a.txt"), new DesktopFileChange(Docs + "Photos", Desk + "Photos")], step.Changes);
    }

    [Fact]
    public void S13102_a_move_keeps_only_arrivals_it_can_send_back_and_nothing_is_no_step()
    {
        var step = DesktopFileUndo.Record(DesktopFileUndoKind.Move, [Desk + "a.txt", Desk + "stranger.txt"], [Docs + "a.txt"])!;
        Assert.Equal([new DesktopFileChange(Docs + "a.txt", Desk + "a.txt")], step.Changes);

        Assert.Null(DesktopFileUndo.Record(DesktopFileUndoKind.Move, [Desk + "stranger.txt"], [Docs + "a.txt"]));
        Assert.Null(DesktopFileUndo.Record(DesktopFileUndoKind.Copy, [], [Docs + "a.txt"]));
        Assert.Null(DesktopFileUndo.Record(DesktopFileUndoKind.NewFolder, ["::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"], []));
        Assert.Null(DesktopFileUndo.Renamed(Desk + "a.txt", Desk + "A.TXT"));
    }

    // ---- undo ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void S13102_undo_of_a_copy_a_new_folder_or_a_shortcut_sends_what_was_made_to_the_recycle_bin()
    {
        foreach (var kind in new[] { DesktopFileUndoKind.Copy, DesktopFileUndoKind.NewFolder, DesktopFileUndoKind.Shortcut })
        {
            var step = new DesktopFileStep(kind, [new DesktopFileChange(kind == DesktopFileUndoKind.Copy ? Docs + "a.txt" : null, Desk + "a.txt")]);
            var plan = DesktopFileUndo.Undo(step, There(Desk + "a.txt", Docs + "a.txt"));

            Assert.Equal([new DesktopFileAction(DesktopFileOp.Recycle, Desk + "a.txt")], plan.Actions);
            Assert.Empty(plan.Expected);
            Assert.Equal(0, plan.Left);
        }
    }

    [Fact]
    public void S13102_undo_of_a_move_sends_it_back_and_leaves_one_whose_place_is_taken()
    {
        var step = new DesktopFileStep(DesktopFileUndoKind.Move, [new DesktopFileChange(Docs + "a.txt", Desk + "a.txt"), new DesktopFileChange(Docs + "b.txt", Desk + "b.txt")]);

        var plan = DesktopFileUndo.Undo(step, There(Desk + "a.txt", Desk + "b.txt", Docs + "b.txt"));

        Assert.Equal([new DesktopFileAction(DesktopFileOp.MoveInto, Desk + "a.txt", Folder: @"C:\Users\a\Documents")], plan.Actions);
        Assert.Equal(1, plan.Left);
    }

    [Fact]
    public void S13102_undo_of_a_rename_takes_the_old_name_back_and_keeps_its_icon_waiting_for_it()
    {
        var step = DesktopFileUndo.Renamed(Desk + "old.txt", Desk + "new.txt")!;

        var plan = DesktopFileUndo.Undo(step, There(Desk + "new.txt"));

        Assert.Equal([new DesktopFileAction(DesktopFileOp.Rename, Desk + "new.txt", Name: "old.txt")], plan.Actions);
        Assert.Equal([Desk + "old.txt"], plan.Expected);

        // Never over something: the old name taken now leaves it.
        var taken = DesktopFileUndo.Undo(step, There(Desk + "new.txt", Desk + "old.txt"));
        Assert.Empty(taken.Actions);
        Assert.Equal(1, taken.Left);

        // A case-only rename back is no clash with itself.
        var caseOnly = DesktopFileUndo.Undo(new DesktopFileStep(DesktopFileUndoKind.Rename, [new DesktopFileChange(Desk + "a.txt", Desk + "A.txt")]), There(Desk + "A.txt"));
        Assert.Single(caseOnly.Actions);
    }

    [Fact]
    public void S13102_a_change_whose_file_is_gone_is_left_and_said()
    {
        var step = new DesktopFileStep(DesktopFileUndoKind.Copy, [new DesktopFileChange(Docs + "a.txt", Desk + "a.txt")]);

        var plan = DesktopFileUndo.Undo(step, There());

        Assert.Empty(plan.Actions);
        Assert.Equal(1, plan.Left);
    }

    // ---- redo ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void S13102_redo_does_the_operation_again_where_it_still_can()
    {
        var copy = new DesktopFileStep(DesktopFileUndoKind.Copy, [new DesktopFileChange(Docs + "a.txt", Desk + "a.txt")]);
        Assert.Equal([new DesktopFileAction(DesktopFileOp.CopyInto, Docs + "a.txt", Folder: @"C:\Users\a\Desktop")], DesktopFileUndo.Redo(copy, There(Docs + "a.txt")).Actions);

        var move = new DesktopFileStep(DesktopFileUndoKind.Move, [new DesktopFileChange(Docs + "a.txt", Desk + "a.txt")]);
        var moved = DesktopFileUndo.Redo(move, There(Docs + "a.txt"));
        Assert.Equal([new DesktopFileAction(DesktopFileOp.MoveInto, Docs + "a.txt", Folder: @"C:\Users\a\Desktop")], moved.Actions);
        Assert.Equal([Desk + "a.txt"], moved.Expected);

        var folder = new DesktopFileStep(DesktopFileUndoKind.NewFolder, [new DesktopFileChange(null, Desk + "New folder")]);
        Assert.Equal([new DesktopFileAction(DesktopFileOp.MakeFolder, Desk + "New folder")], DesktopFileUndo.Redo(folder, There()).Actions);

        var rename = DesktopFileUndo.Renamed(Desk + "old.txt", Desk + "new.txt")!;
        Assert.Equal([new DesktopFileAction(DesktopFileOp.Rename, Desk + "old.txt", Name: "new.txt")], DesktopFileUndo.Redo(rename, There(Desk + "old.txt")).Actions);

        // A shortcut the shell's wizard made is not made again; a place taken is never written over.
        Assert.Equal(1, DesktopFileUndo.Redo(new DesktopFileStep(DesktopFileUndoKind.Shortcut, [new DesktopFileChange(null, Desk + "x.lnk")]), There()).Left);
        Assert.Equal(1, DesktopFileUndo.Redo(copy, There(Docs + "a.txt", Desk + "a.txt")).Left);
    }

    // ---- the log and the menu -------------------------------------------------------------------------------------------

    [Fact]
    public void S13102_a_step_with_a_file_side_is_recorded_even_where_the_arrangement_stays()
    {
        var state = new AppState { Views = [ViewState.Create("Home")] };
        var history = new LayoutHistory(state);

        Assert.False(history.Commit(state, UndoAction.Change));
        var rename = DesktopFileUndo.Renamed(Desk + "old.txt", Desk + "new.txt")!;
        Assert.True(history.Commit(state, UndoAction.DesktopFiles, rename));
        Assert.Equal(UndoAction.DesktopFiles, history.NextUndo);
        Assert.Same(rename, history.NextUndoEffect);

        IUndoEffect? seen = null;
        Assert.Equal(UndoAction.DesktopFiles, history.Undo(state, e => (seen = e) is not null));
        Assert.Same(rename, seen);
    }

    [Fact]
    public void S13103_the_background_menu_offers_undo_for_the_desktops_own_file_steps_only()
    {
        var rename = DesktopFileUndo.Renamed(Desk + "old.txt", Desk + "new.txt")!;
        Assert.Equal(DesktopUndoName.Rename, DesktopFileUndo.Offered(UndoAction.DesktopFiles, rename));
        Assert.Equal(DesktopUndoName.Delete, DesktopFileUndo.Offered(UndoAction.DeleteDesktopItems, null));
        Assert.Null(DesktopFileUndo.Offered(UndoAction.Move, null));
        Assert.Null(DesktopFileUndo.Offered(null, null));

        var without = DesktopBackground.Menu(new DesktopBackgroundFacts(ClipboardHasFiles: true, HasTerminal: false));
        var with = DesktopBackground.Menu(new DesktopBackgroundFacts(ClipboardHasFiles: true, HasTerminal: false, Undo: DesktopUndoName.Rename));
        Assert.DoesNotContain(without, l => l.Command == DesktopMenuCommand.Undo);
        var at = with.ToList().FindIndex(l => l.Command == DesktopMenuCommand.Undo);
        Assert.Equal(DesktopMenuCommand.PasteShortcut, with[at - 1].Command);
        Assert.True(with[at + 1].Group == DesktopMenuGroup.New);
    }

    [Fact]
    public void S13102_a_landing_carries_its_commands_undo_to_each_arrival_it_places()
    {
        var landings = new DesktopLandings();
        var hint = new DesktopLandingHint("home", 0.2, 0.3, ["a.txt"]) { Undo = DesktopFileUndoKind.Copy, Sources = [Docs + "a.txt"] };
        landings.Expect(hint);

        var spots = landings.Place([new DesktopItem(Desk + "a.txt", "a.txt", DesktopItemKind.File), new DesktopItem(Desk + "b.txt", "b.txt", DesktopItemKind.File)]);

        Assert.Same(hint, spots[0]!.Value.Hint);
        Assert.Null(spots[1]);
    }
}
