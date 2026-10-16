using Themia.Core.Shell;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6416]..[S-6419] The folder widget's item and background menus, Explorer's commands in Explorer's order.</summary>
public sealed class FolderMenuTests
{
    private const int CloudOnly = 0x00400000; // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS

    private static FolderEntry File(string name, int attributes = 0) => new(name, $@"C:\Root\{name}", false, 10, DateTimeOffset.UnixEpoch, false, attributes);

    private static FolderEntry Dir(string name) => new(name, $@"C:\Root\{name}", true, 0, DateTimeOffset.UnixEpoch, false);

    private static FolderCommand[] Commands(IReadOnlyList<FolderMenuLine> lines) => [.. lines.Select(l => l.Command)];

    private static QuickCommand[] Row(IReadOnlyList<FolderMenuLine> lines)
    {
        var split = ContextMenuLayout.Split([.. lines.Select(l => l.IsSeparator ? MenuLine.Separator : MenuLine.Entry(FolderMenu.Quick(l.Command)))]);
        return [.. split.Row.Select(i => FolderMenu.Quick(lines[i].Command)!.Value)];
    }

    [Fact]
    public void S6416_one_file_gets_explorers_row_and_list()
    {
        var lines = FolderMenu.Item([File("a.txt")], navigates: true, clipboardHasFiles: true);

        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Rename, QuickCommand.Share, QuickCommand.Delete], Row(lines));
        Assert.Equal(
            [
                FolderCommand.Cut, FolderCommand.Copy, FolderCommand.Rename, FolderCommand.Share, FolderCommand.Delete,
                FolderCommand.Open, FolderCommand.OpenWith, FolderCommand.OpenFileLocation, FolderCommand.CompressToZip,
                FolderCommand.CopyAsPath, FolderCommand.Properties, FolderCommand.Separator, FolderCommand.ShowMoreOptions,
            ],
            Commands(lines));
    }

    [Fact]
    public void S6416_one_folder_pastes_into_itself_pins_and_opens_a_new_window_but_has_no_share()
    {
        var lines = FolderMenu.Item([Dir("Docs")], navigates: true, clipboardHasFiles: false);

        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Paste, QuickCommand.Rename, QuickCommand.Delete], Row(lines));
        Assert.False(lines.Single(l => l.Command == FolderCommand.Paste).Enabled);
        Assert.DoesNotContain(FolderCommand.Share, Commands(lines));
        Assert.DoesNotContain(FolderCommand.OpenWith, Commands(lines));
        Assert.Contains(FolderCommand.OpenInNewWindow, Commands(lines));
        Assert.Contains(FolderCommand.PinToQuickAccess, Commands(lines));
        Assert.Contains(FolderCommand.PinToStart, Commands(lines));
    }

    [Fact]
    public void S6416_explorer_mode_has_no_new_window_line_open_already_is_one()
        => Assert.DoesNotContain(FolderCommand.OpenInNewWindow, Commands(FolderMenu.Item([Dir("Docs")], navigates: false, clipboardHasFiles: false)));

    [Fact]
    public void S6416_several_items_lose_the_one_item_commands()
    {
        var files = FolderMenu.Item([File("a.txt"), File("b.txt")], true, true);
        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Share, QuickCommand.Delete], Row(files));
        Assert.DoesNotContain(FolderCommand.OpenWith, Commands(files));
        Assert.DoesNotContain(FolderCommand.PinToStart, Commands(files));

        var mixed = FolderMenu.Item([File("a.txt"), Dir("Docs")], true, true);
        Assert.Equal([QuickCommand.Cut, QuickCommand.Copy, QuickCommand.Delete], Row(mixed));
        Assert.DoesNotContain(FolderCommand.PinToQuickAccess, Commands(mixed));

        var folders = FolderMenu.Item([Dir("A"), Dir("B")], true, true);
        Assert.Contains(FolderCommand.PinToQuickAccess, Commands(folders));
        Assert.DoesNotContain(FolderCommand.Paste, Commands(folders));
    }

    [Theory]
    [InlineData("setup.exe", true)]
    [InlineData("Tool.LNK", true)]
    [InlineData("a.txt", false)]
    public void S6416_pin_to_start_is_for_programs_and_shortcuts(string name, bool pinnable)
        => Assert.Equal(pinnable, FolderMenu.Item([File(name)], true, false).Any(l => l.Command == FolderCommand.PinToStart));

    [Fact]
    public void S6416_a_cloud_item_adds_its_pins_before_show_more_options()
    {
        var lines = Commands(FolderMenu.Item([File("a.txt", CloudOnly)], true, false));

        Assert.Equal(
            [FolderCommand.Properties, FolderCommand.Separator, FolderCommand.KeepOnDevice, FolderCommand.FreeUpSpace, FolderCommand.Separator, FolderCommand.ShowMoreOptions],
            lines[^6..]);
    }

    [Fact]
    public void S6416_nothing_selected_is_no_item_menu()
        => Assert.Empty(FolderMenu.Item([], true, true));

    [Fact]
    public void S6418_the_background_menu_has_no_view_or_sort_and_greys_what_cannot_run()
    {
        var lines = FolderMenu.Background(new FolderBackgroundFacts(HasPath: true, Listed: true, HasItems: false, ClipboardHasFiles: false, CanGoUp: false));

        Assert.Equal(
            [
                FolderCommand.Paste, FolderCommand.SelectAll, FolderCommand.Separator, FolderCommand.Refresh, FolderCommand.Separator,
                FolderCommand.New, FolderCommand.Separator, FolderCommand.OpenInExplorer, FolderCommand.CopyAsPath,
                FolderCommand.Properties, FolderCommand.Separator, FolderCommand.ShowMoreOptions,
            ],
            Commands(lines));
        Assert.False(lines[0].Enabled);
        Assert.False(lines[1].Enabled);

        // A lone Paste is no row ([S-1741]): the background menu is the list alone.
        Assert.Empty(Row(lines));
    }

    [Fact]
    public void S6418_up_appears_where_there_is_somewhere_to_go()
        => Assert.Contains(FolderCommand.Up, Commands(FolderMenu.Background(new FolderBackgroundFacts(true, true, true, true, CanGoUp: true))));

    [Fact]
    public void S6417_every_key_a_menu_shows_is_one_the_widget_answers()
    {
        // The virtual key and modifiers of each accelerator; the lease must turn each into a key.
        (int Vk, bool Shift, bool Ctrl)? Press(FolderAccelerator accelerator) => accelerator switch
        {
            FolderAccelerator.Enter => (0x0D, false, false),
            FolderAccelerator.CtrlX => (0x58, false, true),
            FolderAccelerator.CtrlC => (0x43, false, true),
            FolderAccelerator.CtrlV => (0x56, false, true),
            FolderAccelerator.CtrlA => (0x41, false, true),
            FolderAccelerator.F2 => (0x71, false, false),
            FolderAccelerator.Delete => (0x2E, false, false),
            FolderAccelerator.ShiftF10 => (0x79, true, false),
            _ => null,
        };

        foreach (var command in Enum.GetValues<FolderCommand>())
        {
            var accelerator = FolderMenu.Accelerator(command);

            if (accelerator == FolderAccelerator.None)
            {
                continue;
            }

            var (vk, shift, ctrl) = Press(accelerator)!.Value;
            Assert.NotNull(WidgetKeys.FromVirtualKey(vk, shift, ctrl, alt: false));
        }

        Assert.Equal(FolderAccelerator.None, FolderMenu.Accelerator(FolderCommand.CopyAsPath));
        Assert.Equal(FolderAccelerator.None, FolderMenu.Accelerator(FolderCommand.Properties));
    }

    [Fact]
    public void S6419_zip_names_follow_explorer()
    {
        Assert.Equal("report.zip", FolderMenu.ZipName(File("report.docx"), []));
        Assert.Equal("my.data.zip", FolderMenu.ZipName(File("my.data.csv"), []));
        Assert.Equal("Docs.v2.zip", FolderMenu.ZipName(Dir("Docs.v2"), []));
        Assert.Equal(".gitignore.zip", FolderMenu.ZipName(File(".gitignore"), []));
        Assert.Equal("report (2).zip", FolderMenu.ZipName(File("report.docx"), ["Report.zip", "report.docx"]));
    }
}
