using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-44] The files and launcher widgets' settings as users have them, generated once with the build
/// from before they became projects (`c11f637e`; Fixtures/state-files-configured.json, ids w00.., not
/// anyone's real file): a folder in Details with every setting changed and its own columns, one in Grid
/// with large tiles, one in Icons with no columns, Recent files, a Shortcuts list with a file, a named
/// web address, the Recycle Bin, a .url with its icon and an app layout (a split with an app that is not
/// reused and a folder), a Shortcuts table, a camera with every setting changed and a fresh camera.
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
        Assert.Equal(8, configs.Count);

        var details = Assert.IsType<FolderConfig>(configs[0]);
        Assert.Equal((@"D:\Projects\Themia", FolderStyle.Details, FolderNavigation.Navigate, ItemClickMode.Single, FolderSort.Modified, true, true, FolderItemSize.Small, true),
            (details.Path, details.Style, details.Navigation, details.ClickMode, details.Sort, details.Descending, details.ShowHidden, details.ItemSize, details.ShowExtensions));
        Assert.Equal([FolderColumn.Modified, FolderColumn.Git, FolderColumn.Size, FolderColumn.ItemCount], details.Columns);
        var grid = Assert.IsType<FolderConfig>(configs[1]);
        Assert.Equal((@"%USERPROFILE%\Pictures", FolderStyle.Grid, FolderTileSize.Large, FolderSort.Size), (grid.Path, grid.Style, grid.TileSize, grid.Sort));
        var icons = Assert.IsType<FolderConfig>(configs[2]);
        Assert.Equal((FolderStyle.Icons, FolderTileSize.Small), (icons.Style, icons.TileSize));
        Assert.Empty(icons.Columns);

        Assert.IsType<RecentFilesConfig>(configs[3]);

        var list = Assert.IsType<ShortcutsConfig>(configs[4]);
        Assert.Equal((ShortcutsStyle.List, ShortcutItemSize.Large, ShortcutItemSize.Small, true, true, true, ItemClickMode.Single),
            (list.Style, list.ListSize, list.GridSize, list.GridStretchHeight, list.GridIconOnly, list.ShowFileExtension, list.ClickMode));
        Assert.Equal(["i1", "i2", "i3", "i4", "i5"], list.Items.Select(i => i.Id));
        Assert.Equal(("News ✓", "https://example.com/news"), (list.Items[1].Label, list.Items[1].Target));
        Assert.Equal(@"C:\Games\game.ico", list.Items[3].IconPath);
        var layout = list.Items[4].Layout!;
        Assert.Equal(("Morning", LayoutNodeKind.Split, SplitDirection.Horizontal, 0.3), (list.Items[4].Label, layout.Kind, layout.Direction, layout.Ratio));
        Assert.Equal((LayoutSource.App, @"C:\Start\Word.lnk", "Word", false),
            (layout.A!.Assignment!.Source, layout.A.Assignment.Target, layout.A.Assignment.Label, layout.A.Assignment.ReuseIfRunning));
        Assert.Equal((LayoutSource.Folder, @"D:\Projects", true), (layout.B!.Assignment!.Source, layout.B.Assignment.Target, layout.B.Assignment.ReuseIfRunning));
        var table = Assert.IsType<ShortcutsConfig>(configs[5]);
        Assert.Equal((ShortcutsStyle.Details, "shell:MyComputerFolder"), (table.Style, Assert.Single(table.Items).Target));

        var camera = Assert.IsType<VideoStreamConfig>(configs[6]);
        Assert.Equal(("http://192.168.1.50:8080/?action=stream", StreamFit.Cover, StreamRotation.Quarter, true, 10, true, false),
            (camera.Url, camera.Fit, camera.Rotation, camera.Paused, camera.RefreshSeconds, camera.ShowStatus, camera.HoverControls));
        var fresh = Assert.IsType<VideoStreamConfig>(configs[7]);
        Assert.Equal((string.Empty, StreamFit.Contain, false, true), (fresh.Url, fresh.Fit, fresh.Paused, fresh.HoverControls));
    }
}
