using System.Buffers.Binary;
using System.Text;
using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5597] Ctrl+Z after a desktop delete puts the items back from the Recycle Bin, at the spots they stood.</summary>
public sealed class DesktopDeleteUndoTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static readonly DateTimeOffset At = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static DeletedDesktopItem Item(string name) => new(Desk + name, Desk + name);

    private static RecycledItem Bin(string id, string name, double secondsAfter) => new(id, Desk + name, At.AddSeconds(secondsAfter));

    // ---- the bin's own record ---------------------------------------------------------------------------------

    private static byte[] InfoV2(string path, DateTimeOffset deleted, long size = 42)
    {
        var chars = path.Length + 1;
        var bytes = new byte[28 + (chars * 2)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), size);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16), deleted.UtcDateTime.ToFileTimeUtc());
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), chars);
        Encoding.Unicode.GetBytes(path).CopyTo(bytes, 28);
        return bytes;
    }

    private static byte[] InfoV1(string path, DateTimeOffset deleted)
    {
        var bytes = new byte[24 + 520];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16), deleted.UtcDateTime.ToFileTimeUtc());
        Encoding.Unicode.GetBytes(path).CopyTo(bytes, 24);
        return bytes;
    }

    [Fact]
    public void S5597_the_bins_record_gives_the_original_path_and_when_in_both_versions()
    {
        Assert.Equal((Desk + "Folder", At), RecycleBinInfo.Parse(InfoV2(Desk + "Folder", At)));
        Assert.Equal((Desk + "a.txt", At), RecycleBinInfo.Parse(InfoV1(Desk + "a.txt", At)));
    }

    [Fact]
    public void S5597_a_record_that_is_not_one_is_refused()
    {
        Assert.Null(RecycleBinInfo.Parse([]));
        Assert.Null(RecycleBinInfo.Parse(new byte[20]));

        var unknown = InfoV2(Desk + "a.txt", At);
        BinaryPrimitives.WriteInt64LittleEndian(unknown, 3);
        Assert.Null(RecycleBinInfo.Parse(unknown));

        // A length past the end, a relative path, no time.
        var cut = InfoV2(Desk + "a.txt", At);
        BinaryPrimitives.WriteInt32LittleEndian(cut.AsSpan(24), 4000);
        Assert.Null(RecycleBinInfo.Parse(cut));
        Assert.Null(RecycleBinInfo.Parse(InfoV2("a.txt", At)));
        var timeless = InfoV2(Desk + "a.txt", At);
        BinaryPrimitives.WriteInt64LittleEndian(timeless.AsSpan(16), 0);
        Assert.Null(RecycleBinInfo.Parse(timeless));
    }

    [Fact]
    public void S5597_the_record_lies_beside_the_recycled_copy()
    {
        Assert.Equal(@"C:\$Recycle.Bin\S-1-5-21\$IAB12CD.txt", RecycleBinInfo.InfoPathOf(@"C:\$Recycle.Bin\S-1-5-21\$RAB12CD.txt"));
        Assert.Null(RecycleBinInfo.InfoPathOf(@"C:\Users\a\Desktop\a.txt"));
    }

    // ---- which recycled copy -----------------------------------------------------------------------------------

    [Fact]
    public void S5597_the_copy_put_back_is_the_one_this_delete_made()
    {
        var item = Item("Report.docx");
        var bin = new[]
        {
            Bin("old", "Report.docx", -3600),     // an earlier delete of the same name: another's
            Bin("ours", "REPORT.DOCX", 1),        // case aside, this delete's
            Bin("later", "Report.docx", 600),     // the same name deleted again later
            Bin("other", "Other.docx", 1),
        };

        Assert.Equal("ours", DesktopDeleteUndo.Match(item, At, bin)!.Id);

        // The bin's clock may stamp it a moment before the delete was asked for.
        Assert.Equal("early", DesktopDeleteUndo.Match(item, At, [Bin("early", "Report.docx", -2)])!.Id);
        Assert.Null(DesktopDeleteUndo.Match(item, At, [Bin("old", "Report.docx", -3600)]));
    }

    [Fact]
    public void S5597_an_undo_puts_back_what_the_bin_holds_and_leaves_what_it_cannot()
    {
        var step = DesktopDeleteUndo.Record([Item("a.txt"), Item("Folder"), Item("gone.txt"), Item("a.txt")], At)!;
        Assert.Equal(3, step.Items.Count);

        var bin = new[] { Bin("ra", "a.txt", 1), Bin("rf", "Folder", 1) };
        var plan = DesktopDeleteUndo.Plan(step, bin, path => path == Desk + "Folder");

        // a.txt back; Folder's path taken by a new folder of that name: left in the bin, never put over it.
        Assert.Equal([(Desk + "a.txt", "ra")], plan.Restore.Select(r => (r.Item.Path, r.From.Id)));
        Assert.Equal([(Desk + "Folder", RestoreSkip.PathTaken), (Desk + "gone.txt", RestoreSkip.NotInBin)], plan.Skipped.Select(s => (s.Item.Path, s.Why)));
    }

    [Fact]
    public void S5597_a_delete_that_sent_nothing_records_nothing()
        => Assert.Null(DesktopDeleteUndo.Record([], At));

    // ---- the undo log carries it ---------------------------------------------------------------------------------

    [Fact]
    public void S5597_the_step_carries_its_files_both_ways()
    {
        var log = new UndoLog<string>();
        var effect = DesktopDeleteUndo.Record([Item("a.txt")], At)!;
        log.Record(UndoAction.DeleteDesktopItems, "before", effect);

        Assert.Same(effect, log.NextEffect(undo: true));
        Assert.True(log.TryUndo("after", out var undone));
        Assert.Same(effect, undone.Effect);
        Assert.Same(effect, log.NextEffect(undo: false));
        Assert.True(log.TryRedo("before", out var redone));
        Assert.Same(effect, redone.Effect);
        Assert.Same(effect, log.NextEffect(undo: true));
    }

    [Fact]
    public void S5597_an_undo_whose_files_cannot_be_handled_is_not_taken()
    {
        // A step of the log with a file side (a view renamed stands in for the icons going: no widget kind in Core's tests).
        var state = new AppState { Views = [ViewState.Create("Home")] };
        var history = new LayoutHistory(state);
        state.Views[0].Name = "After";
        var effect = DesktopDeleteUndo.Record([Item("a.txt")], At)!;
        Assert.True(history.Commit(state, UndoAction.DeleteDesktopItems, effect));

        Assert.Null(history.Undo(state, _ => false));
        Assert.True(history.CanUndo);
        Assert.Equal("After", state.Views[0].Name);

        IUndoEffect? seen = null;
        Assert.Equal(UndoAction.DeleteDesktopItems, history.Undo(state, e => (seen = e) is not null));
        Assert.Same(effect, seen);
        Assert.Equal("Home", state.Views[0].Name);
    }

    [Fact]
    public void S5597_the_desktops_keys_undo_and_redo_as_explorers()
    {
        Assert.Equal(Themia.Core.Widgets.WidgetKey.Undo, Themia.Core.Widgets.WidgetKeys.FromVirtualKey(0x5A, shift: false, ctrl: true, alt: false));
        Assert.Equal(Themia.Core.Widgets.WidgetKey.Redo, Themia.Core.Widgets.WidgetKeys.FromVirtualKey(0x59, shift: false, ctrl: true, alt: false));
        Assert.Equal(Themia.Core.Widgets.WidgetKey.Redo, Themia.Core.Widgets.WidgetKeys.FromVirtualKey(0x5A, shift: true, ctrl: true, alt: false));
        Assert.Null(Themia.Core.Widgets.WidgetKeys.FromVirtualKey(0x5A, shift: false, ctrl: true, alt: true));
    }

    // ---- the icons stay where they stood ---------------------------------------------------------------------------

    [Fact]
    public void S5597_an_item_being_put_back_keeps_its_icon_and_is_no_arrival_when_it_comes()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var spot = new FracRect(0.5, 0.4, 0.09, 0.125);
        home.Widgets.Add(new WidgetInstance { Id = "w", Config = new DeskIconFake { Item = Desk + "a.txt" }, Rect = spot });
        state.Desktop.Known = [Desk + "a.txt"];
        var expected = new HashSet<string>([Desk + "a.txt"], StringComparer.OrdinalIgnoreCase);

        // Not listed yet: the icon the undo brought back stays, the item stays known.
        var waiting = DesktopReconcile.Plan(state, [], [], expected);
        Assert.Empty(waiting.Gone);
        Assert.Equal([Desk + "a.txt"], waiting.Known);

        // Listed: no arrival, the icon where it stood.
        var come = DesktopReconcile.Plan(state, [new DesktopItem(Desk + "a.txt", "a.txt", DesktopItemKind.File)], []);
        Assert.Empty(come.Arrivals);
        Assert.Empty(come.Gone);
        Assert.Equal(spot, home.Widgets[0].Rect);

        // Without the wait it would have gone.
        Assert.Equal(["w"], DesktopReconcile.Plan(state, [], []).Gone);
    }
}
