using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-4121]..[S-4123] The folder: one kind, four styles, each showing only the settings it draws.</summary>
public sealed class FolderKindsTests
{
    public static TheoryData<FolderStyle, string> Styles() => new()
    {
        { FolderStyle.List, "list" },
        { FolderStyle.Details, "details" },
        { FolderStyle.Grid, "grid" },
        { FolderStyle.Icons, "icons" },
    };

    private static HashSet<string> Properties(WidgetConfig config)
    {
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void S4123_every_style_round_trips_under_the_one_kind(FolderStyle style, string wire)
    {
        var config = Assert.IsType<FolderConfig>(WidgetConfig.CreateDefault(new WidgetId("folder")));
        Assert.Equal(FolderStyle.List, config.Style);
        config.Style = style;

        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
        using (var document = JsonDocument.Parse(json))
        {
            Assert.Equal(wire, document.RootElement.GetProperty("style").GetString());
        }

        var back = Assert.IsType<FolderConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(new WidgetId("folder"), back.Kind);
        Assert.Equal(style, back.Style);
        Assert.Empty(back.Repair());
    }

    [Fact]
    public void S4123_the_styles_are_listed_list_details_grid_icons()
        => Assert.Equal(["list", "details", "grid", "icons"], WidgetStyles.For(new WidgetId("folder")).Select(s => s.Name));

    [Fact]
    public void S4123_an_unknown_style_is_repaired_to_the_list()
    {
        var config = Assert.IsType<FolderConfig>(ConfigJson.Read("Folder", """{"kind":"folder","style":"carousel"}"""));
        Assert.Equal(FolderStyle.List, config.Style);
    }

    [Fact]
    public void S4121_the_folder_stores_the_shared_settings_and_the_union_of_its_styles()
    {
        string[] expected = ["kind", "style", "path", "navigation", "clickMode", "sort", "descending", "showHidden", "itemSize", "showExtensions", "columns", "tileSize"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), Properties(new FolderConfig()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void S4121_the_retired_settings_are_gone()
    {
        var names = Properties(new FolderConfig());
        Assert.DoesNotContain("filter", names);
        Assert.DoesNotContain("foldersFirst", names);
        Assert.DoesNotContain("stretchTiles", names);
        Assert.DoesNotContain("view", names);
    }

    public static TheoryData<FolderStyle, FolderSetting[]> StyleSettings() => new()
    {
        { FolderStyle.List, [FolderSetting.RowSize, FolderSetting.Extensions, FolderSetting.Sort] },
        { FolderStyle.Details, [FolderSetting.RowSize, FolderSetting.Columns, FolderSetting.Extensions] },
        { FolderStyle.Grid, [FolderSetting.TileSize, FolderSetting.Extensions, FolderSetting.Sort] },
        { FolderStyle.Icons, [FolderSetting.TileSize, FolderSetting.Sort] },
    };

    [Theory]
    [MemberData(nameof(StyleSettings))]
    public void S4121_each_style_shows_only_the_settings_it_draws(FolderStyle style, FolderSetting[] rows)
        => Assert.Equal(rows, new FolderConfig { Style = style }.ShownSettings);

    [Fact]
    public void S4121_a_clone_keeps_every_setting_and_the_style()
    {
        var details = new FolderConfig
        {
            Style = FolderStyle.Details,
            Path = @"C:\Work",
            Navigation = FolderNavigation.Navigate,
            ClickMode = ItemClickMode.Single,
            Sort = FolderSort.Modified,
            Descending = true,
            ShowHidden = true,
            ItemSize = FolderItemSize.Large,
            ShowExtensions = true,
            Columns = [FolderColumn.Git],
            TileSize = FolderTileSize.Large,
        };

        var copy = Assert.IsType<FolderConfig>(details.Clone());
        Assert.Equal(FolderStyle.Details, copy.Style);
        Assert.Equal(@"C:\Work", copy.Path);
        Assert.Equal(FolderNavigation.Navigate, copy.Navigation);
        Assert.Equal(ItemClickMode.Single, copy.ClickMode);
        Assert.Equal(FolderSort.Modified, copy.Sort);
        Assert.True(copy.Descending);
        Assert.True(copy.ShowHidden);
        Assert.Equal(FolderItemSize.Large, copy.ItemSize);
        Assert.True(copy.Extensions);
        Assert.Equal([FolderColumn.Git], copy.DetailColumns);
        Assert.Equal(FolderTileSize.Large, copy.TileSize);

        copy.Columns.Add(FolderColumn.Size);
        Assert.Equal([FolderColumn.Git], details.Columns);
    }

    [Fact]
    public void S4121_what_a_style_does_not_draw_reads_as_its_neutral_value()
    {
        // The icons draw no names, so a rename there edits the stem; the tile styles have no rows,
        // the row styles no tiles, and only the details table has columns.
        var config = new FolderConfig { ShowExtensions = true, ItemSize = FolderItemSize.Large, TileSize = FolderTileSize.Large };

        config.Style = FolderStyle.Icons;
        Assert.False(config.Extensions);
        Assert.Equal(FolderItemSize.Medium, config.Rows);
        Assert.Equal(FolderTileSize.Large, config.Tiles);
        Assert.Empty(config.DetailColumns);

        config.Style = FolderStyle.List;
        Assert.True(config.Extensions);
        Assert.Equal(FolderItemSize.Large, config.Rows);
        Assert.Equal(FolderTileSize.Small, config.Tiles);
        Assert.Empty(config.DetailColumns);

        config.Style = FolderStyle.Details;
        Assert.Equal(config.Columns, config.DetailColumns);
    }

    [Fact]
    public void S4121_a_hand_edited_null_path_is_repaired_to_the_profile_default()
    {
        var config = Assert.IsType<FolderConfig>(ConfigJson.Read("Folder", """{"kind":"folder","style":"grid","path":null}"""));
        config.Repair();
        Assert.Equal(string.Empty, config.Path);
        Assert.Equal(FolderStyle.Grid, config.Style);
    }

    [Fact]
    public void S4121_the_list_never_has_a_column_header_and_details_always_tries_one()
    {
        var area = new WidgetArea(800, 516);

        Assert.False(FolderLayout.Plan(area, new FolderConfig(), 20, true, false, false, false).ColumnHeader);
        Assert.True(FolderLayout.Plan(area, new FolderConfig { Style = FolderStyle.Details }, 20, true, false, false, false).ColumnHeader);
        Assert.Null(FolderLayout.Plan(area, new FolderConfig { Style = FolderStyle.Details }, 20, true, false, false, false).Grid);
    }

    [Fact]
    public void S4121_icons_tiles_pack_to_the_icon_and_grid_tiles_keep_their_label_room()
    {
        var area = new WidgetArea(320, 240);
        var icons = FolderLayout.Plan(area, new FolderConfig { Style = FolderStyle.Icons, TileSize = FolderTileSize.Small }, 20, true, false, false, false).Grid!.Value;
        var grid = FolderLayout.Plan(area, new FolderConfig { Style = FolderStyle.Grid, TileSize = FolderTileSize.Small }, 20, true, false, false, false).Grid!.Value;

        Assert.Equal(icons.Icon + 24, icons.CellHeight);
        Assert.Equal(FolderLayout.Tile(FolderTileSize.Small).Row, grid.CellHeight);
        Assert.True(icons.Columns > grid.Columns);
    }
}
