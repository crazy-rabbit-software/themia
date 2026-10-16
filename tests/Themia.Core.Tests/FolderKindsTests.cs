using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-4120]..[S-4122] The folder layouts as four widget kinds, each storing only what it draws.</summary>
public sealed class FolderKindsTests
{
    public static TheoryData<WidgetKind, FolderViewMode> Kinds() => new()
    {
        { WidgetKind.Folder, FolderViewMode.List },
        { WidgetKind.FolderDetails, FolderViewMode.Details },
        { WidgetKind.FolderGrid, FolderViewMode.Grid },
        { WidgetKind.FolderIcons, FolderViewMode.Icons },
    };

    private static HashSet<string> Properties(WidgetConfig config)
    {
        var json = JsonSerializer.Serialize(config, AppStateJsonContext.Default.WidgetConfig);
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S4120_each_layout_is_its_own_kind_and_round_trips_under_it(WidgetKind kind, FolderViewMode view)
    {
        var config = (FolderConfig)WidgetConfig.CreateDefault(kind);

        Assert.Equal(kind, config.Kind);
        Assert.Equal(view, config.View);
        Assert.Equal(view, FolderConfig.ViewOf(kind));
        Assert.Equal(kind, FolderConfig.For(view).Kind);

        var json = JsonSerializer.Serialize<WidgetConfig>(config, AppStateJsonContext.Default.WidgetConfig);
        var back = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.WidgetConfig);
        Assert.Equal(kind, back!.Kind);
        Assert.Empty(back.Repair());
    }

    [Fact]
    public void S4120_no_other_kind_is_a_folder_layout()
    {
        WidgetKind[] folders = [WidgetKind.Folder, WidgetKind.FolderDetails, WidgetKind.FolderGrid, WidgetKind.FolderIcons];
        Assert.All(Enum.GetValues<WidgetKind>().Except(folders), kind => Assert.Null(FolderConfig.ViewOf(kind)));
    }

    [Fact]
    public void S4120_the_layout_is_never_stored()
        => Assert.All(new FolderConfig[] { new FolderListConfig(), new FolderDetailsConfig(), new FolderGridConfig(), new FolderIconsConfig() },
            config => Assert.DoesNotContain("view", Properties(config)));

    [Fact]
    public void S4121_every_kind_stores_only_the_settings_its_layout_uses()
    {
        string[] shared = ["kind", "path", "navigation", "clickMode", "sort", "descending", "showHidden"];

        static string[] Order(IEnumerable<string> names) => [.. names.Order(StringComparer.Ordinal)];

        Assert.Equal(Order([.. shared, "itemSize", "showExtensions"]), Order(Properties(new FolderListConfig())));
        Assert.Equal(Order([.. shared, "itemSize", "showExtensions", "columns"]), Order(Properties(new FolderDetailsConfig())));
        Assert.Equal(Order([.. shared, "tileSize", "showExtensions"]), Order(Properties(new FolderGridConfig())));
        Assert.Equal(Order([.. shared, "tileSize"]), Order(Properties(new FolderIconsConfig())));
    }

    [Fact]
    public void S4121_the_retired_settings_are_gone_from_every_kind()
    {
        foreach (var config in new FolderConfig[] { new FolderListConfig(), new FolderDetailsConfig(), new FolderGridConfig(), new FolderIconsConfig() })
        {
            var names = Properties(config);
            Assert.DoesNotContain("filter", names);
            Assert.DoesNotContain("foldersFirst", names);
            Assert.DoesNotContain("stretchTiles", names);
        }
    }

    [Fact]
    public void S4121_a_clone_keeps_the_shared_and_the_kinds_own_settings()
    {
        var details = new FolderDetailsConfig
        {
            Path = @"C:\Work",
            Navigation = FolderNavigation.Navigate,
            ClickMode = ItemClickMode.Single,
            Sort = FolderSort.Modified,
            Descending = true,
            ShowHidden = true,
            ItemSize = FolderItemSize.Large,
            ShowExtensions = true,
            Columns = [FolderColumn.Git],
        };

        var copy = Assert.IsType<FolderDetailsConfig>(details.Clone());
        Assert.Equal(@"C:\Work", copy.Path);
        Assert.Equal(FolderNavigation.Navigate, copy.Navigation);
        Assert.Equal(ItemClickMode.Single, copy.ClickMode);
        Assert.Equal(FolderSort.Modified, copy.Sort);
        Assert.True(copy.Descending);
        Assert.True(copy.ShowHidden);
        Assert.Equal(FolderItemSize.Large, copy.ItemSize);
        Assert.True(copy.Extensions);
        Assert.Equal([FolderColumn.Git], copy.DetailColumns);

        var icons = Assert.IsType<FolderIconsConfig>(new FolderIconsConfig { TileSize = FolderTileSize.Large, Path = "x" }.Clone());
        Assert.Equal(FolderTileSize.Large, icons.Tiles);
        Assert.Equal("x", icons.Path);
    }

    [Fact]
    public void S4121_what_a_layout_does_not_draw_reads_as_its_neutral_value()
    {
        // The icons layout draws no names, so its rename edits the stem; the tile layouts have no rows.
        Assert.False(new FolderIconsConfig().Extensions);
        Assert.Empty(new FolderListConfig().DetailColumns);
        Assert.Empty(new FolderGridConfig().DetailColumns);
        Assert.Equal(FolderItemSize.Medium, new FolderGridConfig().Rows);
        Assert.Equal(FolderTileSize.Small, new FolderListConfig().Tiles);
    }

    [Fact]
    public void S4121_a_hand_edited_null_path_is_repaired_to_the_profile_default()
    {
        var config = Assert.IsType<FolderGridConfig>(ConfigJson.Read("FolderGrid", """{"kind":"folderGrid","path":null}"""));
        config.Repair();
        Assert.Equal(string.Empty, config.Path);
    }

    [Fact]
    public void S4121_the_list_never_has_a_column_header_and_details_always_tries_one()
    {
        var area = new WidgetArea(800, 516);

        Assert.False(FolderLayout.Plan(area, new FolderListConfig(), 20, false, false, false).ColumnHeader);
        Assert.True(FolderLayout.Plan(area, new FolderDetailsConfig(), 20, false, false, false).ColumnHeader);
        Assert.Null(FolderLayout.Plan(area, new FolderDetailsConfig(), 20, false, false, false).Grid);
    }

    [Fact]
    public void S4121_icons_tiles_pack_to_the_icon_and_grid_tiles_keep_their_label_room()
    {
        var area = new WidgetArea(320, 240);
        var icons = FolderLayout.Plan(area, new FolderIconsConfig(), 20, false, false, false).Grid!.Value;
        var grid = FolderLayout.Plan(area, new FolderGridConfig { TileSize = FolderTileSize.Small }, 20, false, false, false).Grid!.Value;

        Assert.Equal(icons.Icon + 24, icons.CellHeight);
        Assert.Equal(FolderLayout.Tile(FolderTileSize.Small).Row, grid.CellHeight);
        Assert.True(icons.Columns > grid.Columns);
    }
}
