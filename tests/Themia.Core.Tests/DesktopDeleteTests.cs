using Themia.Core.Desktop;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14320] What Delete does to which selected items, given edit mode and Lock widgets (owner, 2026-11-20: "deleting
/// widgets with delete does not work in non edit mode without the lock enabled"); [S-14321] the words of its line.
/// </summary>
public sealed class DesktopDeleteTests
{
    private const string Desk = @"C:\Users\a\Desktop\";
    private const string ThisPcItem = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    private static DeskDeleting Card(string id) => new(id, null);

    private static DeskDeleting File(string id, string name) => new(id, DeskIcon.Of(new DeskIconFake { Item = Desk + name, ItemId = id }, _ => false));

    private static DeskDeleting Own(string id) => new(id, DeskIcon.Of(new DeskIconFake { ItemId = id, Target = @"C:\Tools\app.exe" }, _ => true));

    private static DeskDeleting ThisPc(string id) => new(id, DeskIcon.Of(new DeskIconFake { Item = ThisPcItem, ItemId = id }, _ => false));

    [Fact]
    public void S14320_delete_outside_edit_mode_with_lock_off_removes_the_selected_cards()
    {
        // The owner's report: Delete on selected widgets outside edit mode did nothing.
        var removal = DesktopDelete.Of([Card("clock"), Card("notes")], editMode: false, lockWidgets: false);

        Assert.Equal(["clock", "notes"], removal.Widgets);
        Assert.Empty(removal.Files);
    }

    [Fact]
    public void S14320_with_lock_widgets_on_no_card_is_removed_and_icons_are_as_before()
    {
        var removal = DesktopDelete.Of([Card("clock"), File("f", "a.txt"), Own("app")], editMode: false, lockWidgets: true);

        Assert.Equal([Desk + "a.txt"], removal.Files);
        Assert.Equal(["app"], removal.Widgets);
        Assert.True(DesktopDelete.Of([Card("clock")], editMode: false, lockWidgets: true).IsEmpty);
    }

    [Fact]
    public void S14320_a_mixed_selection_deletes_its_icons_as_before_and_removes_its_cards()
    {
        var removal = DesktopDelete.Of([File("f", "a.txt"), Card("clock"), Own("app"), ThisPc("pc"), Card("fence")], editMode: false, lockWidgets: false);

        // The file to the shell's delete; This PC is no file and stays; the own icon's widget and the cards are removed.
        Assert.Equal([Desk + "a.txt"], removal.Files);
        Assert.Equal(["clock", "app", "fence"], removal.Widgets);
    }

    [Fact]
    public void S14320_a_card_never_hands_a_file_to_the_shell_and_a_file_never_removes_a_card()
    {
        // A card stands for no file: removing it touches none. A desktop item's widget is not removed by the plan (it goes with its
        // file, by the listing), so a file's delete never takes a card or its own widget with it.
        Assert.Empty(DesktopDelete.Of([Card("clock"), Card("fence")], editMode: false, lockWidgets: false).Files);
        var files = DesktopDelete.Of([File("f", "a.txt"), File("g", "b.txt")], editMode: false, lockWidgets: false);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], files.Files);
        Assert.Empty(files.Widgets);
    }

    [Fact]
    public void S14320_in_edit_mode_every_selected_widget_leaves_the_view_and_no_file_is_touched()
    {
        // [S-5524] Edit mode's own Delete, lock or not: a desktop item's file stays.
        foreach (var locked in new[] { false, true })
        {
            var removal = DesktopDelete.Of([File("f", "a.txt"), Card("clock"), Own("app"), Card("clock")], editMode: true, lockWidgets: locked);
            Assert.Empty(removal.Files);
            Assert.Equal(["f", "clock", "app"], removal.Widgets);
        }
    }

    [Fact]
    public void S14321_the_delete_line_names_widgets_unless_a_file_goes()
    {
        Assert.Equal(DeleteWords.OneWidget, DesktopDelete.Words(DesktopDelete.Of([Card("clock")], editMode: false, lockWidgets: false)));
        Assert.Equal(DeleteWords.Widgets, DesktopDelete.Words(DesktopDelete.Of([Card("clock"), Own("app")], editMode: false, lockWidgets: false)));

        // A file among them: Explorer's own word, the shell's delete says what happens to the file.
        Assert.Equal(DeleteWords.Delete, DesktopDelete.Words(DesktopDelete.Of([Card("clock"), File("f", "a.txt")], editMode: false, lockWidgets: false)));
        Assert.Equal(DeleteWords.None, DesktopDelete.Words(DesktopDelete.Of([Card("clock")], editMode: false, lockWidgets: true)));
    }
}
