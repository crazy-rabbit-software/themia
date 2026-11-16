using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Widget.DesktopItem;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-44] The files and launcher widgets' settings as users have them, generated once with the build
/// from before they became projects (`c11f637e`; Fixtures/state-files-configured.json, ids w00.., not
/// anyone's real file): a folder in Details with every setting changed and its own columns, one in Grid
/// with large tiles, one in Icons with no columns, Recent files, an own desktop icon that is an app
/// layout (a split with an app that is not reused and a folder), a named own icon to a .url with its
/// icon, a camera with every setting changed, a fresh camera and a desktop item with one name line
/// (the three desktop icons written in the one kind's shape on 2026-11-14, [S-5600]).
/// Each widget reads it from its own module's JSON context now; this pins that the file still loads
/// with no repair, reads the same values and writes back unchanged.
/// </summary>
public sealed class FilesStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-files-configured.json"));

    [Fact]
    public void S44_the_files_widgets_configured_state_loads_without_a_repair_and_writes_back_unchanged()
    {
        var json = Fixture();
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var written = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the files widgets' state does not write back as it was read");
    }

    [Fact]
    public void S44_the_files_widgets_configured_state_reads_the_choices_it_holds()
    {
        var state = AppStateStore.Parse(Fixture()).State!;
        var configs = Assert.Single(state.Views).Widgets.Select(w => w.Config).ToList();
        Assert.Equal(9, configs.Count);

        var details = Assert.IsType<FolderConfig>(configs[0]);
        Assert.Equal((@"D:\Projects\Themia", FolderStyle.Details, FolderNavigation.Navigate, ItemClickMode.Single, FolderSort.Modified, true, true, true),
            (details.Path, details.Style, details.Navigation, details.ClickMode, details.Sort, details.Descending, details.ShowHidden, details.ShowExtensions));
        Assert.Equal([FolderColumn.Modified, FolderColumn.Git, FolderColumn.Size, FolderColumn.ItemCount], details.Columns);
        var grid = Assert.IsType<FolderConfig>(configs[1]);
        Assert.Equal((@"%USERPROFILE%\Pictures", FolderStyle.Grid, FolderSort.Size), (grid.Path, grid.Style, grid.Sort));
        var icons = Assert.IsType<FolderConfig>(configs[2]);
        Assert.Equal(FolderStyle.Icons, icons.Style);
        Assert.Empty(icons.Columns);

        Assert.IsType<RecentFilesConfig>(configs[3]);

        var morning = Assert.IsType<DesktopItemConfig>(configs[4]);
        Assert.Equal((false, true, false), (morning.IsDesktopItem, morning.IsLayout, morning.OpensNothing));
        Assert.Equal(("i5", string.Empty, 1, ItemClickMode.Single), (morning.ItemId, morning.Target, morning.LabelLines, morning.ClickMode));
        var layout = morning.Layout!;
        Assert.Equal(("Morning", LayoutNodeKind.Split, SplitDirection.Horizontal, 0.3), (morning.Label, layout.Kind, layout.Direction, layout.Ratio));
        Assert.Equal((LayoutSource.App, @"C:\Start\Word.lnk", "Word", false),
            (layout.A!.Assignment!.Source, layout.A.Assignment.Target, layout.A.Assignment.Label, layout.A.Assignment.ReuseIfRunning));
        Assert.Equal((LayoutSource.Folder, @"D:\Projects", true), (layout.B!.Assignment!.Source, layout.B.Assignment.Target, layout.B.Assignment.ReuseIfRunning));
        var game = Assert.IsType<DesktopItemConfig>(configs[5]);
        Assert.Equal(("j1", "News ✓", @"C:\Users\me\Desktop\Game.url", @"C:\Games\game.ico", 2, ItemClickMode.Double),
            (game.ItemId, game.Label, game.Target, game.IconPath, game.LabelLines, game.ClickMode));
        Assert.Null(game.Layout);
        Assert.Equal((false, false, string.Empty), (game.IsDesktopItem, game.IsLayout, game.Item));

        var camera = Assert.IsType<VideoStreamConfig>(configs[6]);
        Assert.Equal(("http://192.168.1.50:8080/?action=stream", StreamFit.Cover, StreamRotation.Quarter, true, 10, true, false),
            (camera.Url, camera.Fit, camera.Rotation, camera.Paused, camera.RefreshSeconds, camera.ShowStatus, camera.HoverControls));
        var fresh = Assert.IsType<VideoStreamConfig>(configs[7]);
        Assert.Equal((string.Empty, StreamFit.Contain, false, true), (fresh.Url, fresh.Fit, fresh.Paused, fresh.HoverControls));

        var report = Assert.IsType<DesktopItemConfig>(configs[8]);
        Assert.Equal((@"C:\Users\me\Desktop\Report.docx", "k7", string.Empty, string.Empty, 1, ItemClickMode.Double),
            (report.Item, report.ItemId, report.Target, report.Label, report.LabelLines, report.ClickMode));
        Assert.True(report.IsDesktopItem);
        Assert.Null(report.Layout);
        Assert.Equal(report.Item, Themia.Sdk.DesktopItemKeys.Of(report));
    }
}
