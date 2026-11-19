using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13004] A fence's rules, and [S-10902] [S-13008] its grid (the SDK's pure parts).</summary>
public sealed class FenceRulesTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    [Theory]
    [InlineData("holiday.JPG", FenceFileType.Images)]
    [InlineData("scan.png", FenceFileType.Images)]
    [InlineData("report.pdf", FenceFileType.Documents)]
    [InlineData("Budget.XLSX", FenceFileType.Documents)]
    [InlineData("setup.exe", FenceFileType.Installers)]
    [InlineData("tool.msi", FenceFileType.Installers)]
    [InlineData("backup.7z", FenceFileType.Archives)]
    [InlineData("clip.mkv", FenceFileType.Videos)]
    [InlineData("song.flac", FenceFileType.Music)]
    public void S13004_a_file_matches_its_types_group_and_no_other(string name, FenceFileType type)
    {
        var rules = new FenceRuleSet { Types = [type] };
        Assert.True(FenceRules.Matches(rules, File(name)));

        foreach (var other in Enum.GetValues<FenceFileType>().Where(t => t != type))
        {
            Assert.False(FenceRules.Matches(new FenceRuleSet { Types = [other] }, File(name)));
        }
    }

    [Fact]
    public void S13004_folders_and_shortcuts_match_by_their_kind_never_by_a_name()
    {
        var folder = new DesktopItem(Desk + "photos.png", "photos.png", DesktopItemKind.Folder);
        var link = new DesktopItem(Desk + "Setup.exe.lnk", "Setup.exe", DesktopItemKind.Shortcut);

        Assert.True(FenceRules.Matches(new FenceRuleSet { Types = [FenceFileType.Folders] }, folder));
        Assert.False(FenceRules.Matches(new FenceRuleSet { Types = [FenceFileType.Images] }, folder));
        Assert.True(FenceRules.Matches(new FenceRuleSet { Types = [FenceFileType.Shortcuts] }, link));
        Assert.False(FenceRules.Matches(new FenceRuleSet { Types = [FenceFileType.Installers] }, link));
    }

    [Fact]
    public void S13004_the_users_extensions_and_a_names_text_match_case_aside_and_shell_items_never()
    {
        var rules = new FenceRuleSet { Extensions = [".psd"], NameContains = "invoice" };

        Assert.True(FenceRules.Matches(rules, File("art.PSD")));
        Assert.True(FenceRules.Matches(rules, File("INVOICE 2026.txt")));
        Assert.True(FenceRules.Matches(rules, new DesktopItem(Desk + "Invoices", "Invoices", DesktopItemKind.Folder)));
        Assert.False(FenceRules.Matches(rules, File("notes.txt")));
        Assert.False(FenceRules.Matches(rules, new DesktopItem("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin invoice", DesktopItemKind.ShellItem)));
        Assert.False(FenceRules.Matches(new FenceRuleSet(), File("art.psd")));
        Assert.False(FenceRules.Matches(null, File("art.psd")));
    }

    [Theory]
    [InlineData("pdf", ".pdf")]
    [InlineData(".PDF", ".pdf")]
    [InlineData("*.docx", ".docx")]
    [InlineData("  txt ", ".txt")]
    [InlineData("tar.gz", null)]
    [InlineData("a?b", null)]
    [InlineData("", null)]
    [InlineData("*", null)]
    [InlineData("averyveryverylongextension", null)]
    public void S13004_a_typed_extension_is_made_one_or_refused(string typed, string? expected)
        => Assert.Equal(expected, FenceRules.Extension(typed));

    [Fact]
    public void S13004_a_typed_list_keeps_each_extension_once_in_order()
        => Assert.Equal([".pdf", ".docx", ".txt"], FenceRules.ParseExtensions("pdf, DOCX; *.txt .pdf bad.one"));

    [Fact]
    public void S13002_a_stored_rule_set_is_made_whole_and_said()
    {
        var rules = new FenceRuleSet
        {
            Types = [FenceFileType.Images, (FenceFileType)99, FenceFileType.Images],
            Extensions = ["PDF", "x.y"],
            NameContains = "  bill\u0007 " + new string('z', 200),
        };

        var repairs = FenceRules.Repair(rules);

        Assert.Equal(3, repairs.Count);
        Assert.Equal([FenceFileType.Images], rules.Types);
        Assert.Equal([".pdf"], rules.Extensions);
        Assert.Equal(FenceRules.MaxText, rules.NameContains.Length);
        Assert.StartsWith("bill", rules.NameContains, StringComparison.Ordinal);
        Assert.Empty(FenceRules.Repair(rules));
    }

    // ---- the cells ([S-10902], [S-13040]) ---------------------------------------------------------------------------

    private static readonly SizeDip Cell = new(76, 100);

    /// <summary>A body at (10, 30), 240 × 250, of <see cref="Cell"/> boxes, from <paramref name="scroll"/>.</summary>
    private static FenceGrid.Layout Body(int scroll = 0) => new(10, 30, 240, 250, Cell.Width, Cell.Height, scroll);

    [Fact]
    public void S10902_S13019_a_fence_lays_its_cells_in_reading_order_box_against_box_from_its_body_corner()
    {
        Assert.Equal(3, Body().Columns);
        Assert.Equal(2, Body().Rows);
        Assert.Equal(1, (Body() with { Width = 50 }).Columns);
        Assert.Equal(1, (Body() with { Width = double.NaN }).Columns);
        Assert.Equal(0, (Body() with { Height = 0 }).Rows);

        // [S-13019] No inset and no centring: the first cell is at the body's corner, as a free icon is at its cell's.
        Assert.Equal((10d, 30d, Cell.Width, Cell.Height), Body().Cell(0));
        Assert.Equal((10d, 130d, Cell.Width, Cell.Height), Body().Cell(3));
        Assert.Equal((10 + (2 * Cell.Width), 30d, Cell.Width, Cell.Height), Body().Cell(2));
        Assert.Equal(2, FenceGrid.RowsOf(4, 3));
        Assert.Equal(0, FenceGrid.RowsOf(0, 3));
    }

    [Fact]
    public void S13040_a_point_finds_the_place_its_cell_stands_for_scrolled_rows_counted()
    {
        var (x, y, _, _) = Body().Cell(4);
        Assert.Equal(4, Body().SlotAt(x + 10, y + 10));
        Assert.Equal(-1, Body().SlotAt(x + 10, y + 500));
        Assert.Equal(-1, Body().SlotAt(5, 40));
        Assert.Equal(-1, Body().SlotAt(10 + (3 * Cell.Width) + 1, 40));
        Assert.Equal(7, Body(scroll: 1).SlotAt(x + 10, y + 10));
    }

    [Fact]
    public void S13043_the_body_scrolls_by_whole_rows_and_shows_only_the_rows_it_holds()
    {
        // Ten items in three columns are four rows; the body shows two.
        Assert.Equal(2, Body().MaxScroll(10));
        Assert.Equal(2, Body(scroll: 99).Clamped(10).Scroll);
        Assert.Equal(0, Body(scroll: -3).Clamped(10).Scroll);
        Assert.Equal(0, Body(scroll: 2).Clamped(6).Scroll);

        var scrolled = Body(scroll: 1);
        Assert.False(scrolled.Shows(2));
        Assert.True(scrolled.Shows(3));
        Assert.True(scrolled.Shows(8));
        Assert.False(scrolled.Shows(9));

        // The first row it shows stands at the body's top: a whole row moved, the cells where they were.
        Assert.Equal(Body().Cell(0), scrolled.Cell(3));
        Assert.False((Body() with { Height = 0 }).Shows(0));
    }

    [Fact]
    public void S13008_a_fence_for_n_items_is_near_square_and_shows_them_all()
    {
        var one = FenceGrid.Around(1, Cell);
        var nine = FenceGrid.Around(9, Cell);
        var many = FenceGrid.Around(200, Cell);

        Assert.Equal((Cell.Width, FenceGrid.TitleDip + Cell.Height), (one.Width, one.Height));
        Assert.Equal(3, (Body() with { Width = nine.Width }).Columns);
        Assert.Equal(FenceGrid.TitleDip + (3 * Cell.Height), nine.Height);
        Assert.Equal(FenceGrid.MaxColumns, (Body() with { Width = many.Width }).Columns);
    }
}
