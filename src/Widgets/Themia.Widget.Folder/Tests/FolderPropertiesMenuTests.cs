using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-1337] [S-4121] The Folder widget's options on its properties menu: what each style draws under Style, the folder picked or typed.</summary>
public sealed class FolderPropertiesMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(FolderConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        FolderPropertiesMenu.Lines(lines, config);
        return lines;
    }

    [Theory]
    [InlineData(FolderStyle.List, new[] { "rows", "ext", "sort" })]
    [InlineData(FolderStyle.Details, new[] { "rows", "columns", "ext" })]
    [InlineData(FolderStyle.Grid, new[] { "tiles", "ext", "sort" })]
    [InlineData(FolderStyle.Icons, new[] { "tiles", "sort" })]
    public void S1337_each_style_offers_only_what_it_draws_under_style(FolderStyle style, string[] expected)
    {
        var names = new Dictionary<string, string>
        {
            [FolderStr.WidgetsB_Settings_Folder_RowSize_Label.Text()] = "rows",
            [FolderStr.WidgetsB_Settings_Folder_TileSize_Label.Text()] = "tiles",
            [FolderStr.WidgetsB_Settings_Folder_Columns_Label.Text()] = "columns",
            [Str.Folder_ShowExtMenu.Text()] = "ext",
            [FolderStr.WidgetsB_Settings_Folder_SortBy_Label.Text()] = "sort",
        };

        Assert.Equal(expected, Build(new FolderConfig { Style = style }).StyleLines.Select(l => names[l.Text]));
    }

    [Fact]
    public void S1337_appearance_then_folder_hidden_items_and_opening()
    {
        var lines = Build(new FolderConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [
                FolderStr.Folder_Label_Label.Text(),
                FolderStr.WidgetsB_Folder_ShowHiddenItems.Text(),
                FolderStr.WidgetsB_Settings_Folder_OpenWith_Label.Text(),
                FolderStr.WidgetsB_Settings_Folder_OpenFolders_Label.Text(),
            ],
            lines.Lines.Select(l => l.Text));
    }

    [Fact]
    public void S1337_the_folder_line_shows_its_name_and_offers_browse_and_a_typed_path()
    {
        var folder = Build(new FolderConfig { Path = @"C:\Users\Me\Downloads" }).Lines[0];

        Assert.Equal("Downloads", folder.Value);
        Assert.Equal([Str.WidgetsB_Settings_Folder_Browse.Text(), FolderStr.Folder_Menu_TypePath.Text()], folder.Children!.Select(c => c.Text));
    }

    [Fact]
    public void S1337_a_typed_path_is_refused_with_the_reason_unless_it_is_full_or_empty()
    {
        Assert.Equal(FolderFormatting.PathMessage(FolderPathProblem.NotRooted), FolderPropertiesMenu.PathRefusal(@"Documents\Work"));
        Assert.Equal(FolderFormatting.PathMessage(FolderPathProblem.InvalidCharacters), FolderPropertiesMenu.PathRefusal(@"C:\Work\*"));
        Assert.Null(FolderPropertiesMenu.PathRefusal(@"C:\Work"));
        Assert.Null(FolderPropertiesMenu.PathRefusal("   "));
    }

    [Fact]
    public void S1337_a_typed_path_is_stored_once_and_empty_means_the_user_folder()
    {
        var config = new FolderConfig { Path = @"C:\Old" };
        Build(config).Lines[0].Children![1].Action!();

        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.True(ask.AllowEmpty);
        Assert.Equal(@"C:\Old", ask.Initial);

        ask.Done(@"  C:\Work  ");
        Assert.Equal(@"C:\Work", config.Path);
        Assert.Equal(1, _saves);

        Build(config).Lines[0].Children![1].Action!();
        _asked!.Done(string.Empty);
        Assert.Equal(string.Empty, config.Path);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S1337_sort_holds_reverse_under_its_keys_and_a_column_switch_toggles_it()
    {
        var config = new FolderConfig { Style = FolderStyle.List, Descending = false };
        var sort = Build(config).StyleLines.Single(l => l.Text == FolderStr.WidgetsB_Settings_Folder_SortBy_Label.Text());
        sort.Children![^1].Action!();
        Assert.True(config.Descending);

        var details = new FolderConfig { Style = FolderStyle.Details };
        var had = details.Columns.Contains(FolderColumns.All[0]);
        Build(details).StyleLines.Single(l => l.Text == FolderStr.WidgetsB_Settings_Folder_Columns_Label.Text()).Children![0].Action!();
        Assert.Equal(!had, details.Columns.Contains(FolderColumns.All[0]));
        Assert.Equal(2, _saves);
    }
}
