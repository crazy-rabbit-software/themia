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

    // ---- the grid ([S-10902]) ----------------------------------------------------------------------------------------

    private static readonly SizeDip Cell = new(76, 100);

    [Fact]
    public void S10902_S13019_a_fence_lays_its_cells_in_reading_order_box_against_box_from_its_edge()
    {
        Assert.Equal(3, FenceGrid.Columns(240, Cell));
        Assert.Equal(3, FenceGrid.Columns(228, Cell));
        Assert.Equal(1, FenceGrid.Columns(50, Cell));
        Assert.Equal(1, FenceGrid.Columns(double.NaN, Cell));

        // [S-13019] No inset and no centring: the first cell is at the body's corner, as a free icon is at its cell's.
        var first = FenceGrid.Cell(0, 3, Cell, 240);
        var fourth = FenceGrid.Cell(3, 3, Cell, 240);
        Assert.Equal((0d, 0d), (first.X, first.Y));
        Assert.Equal(first.X, fourth.X);
        Assert.Equal(first.Y + Cell.Height, fourth.Y);
        Assert.Equal(first.X + (2 * Cell.Width), FenceGrid.Cell(2, 3, Cell, 240).X);
        Assert.Equal(2 * Cell.Height, FenceGrid.ContentHeight(4, 3, Cell));
        Assert.Equal(0, FenceGrid.ContentHeight(0, 3, Cell));
    }

    [Fact]
    public void S10902_a_point_finds_its_item_and_the_place_a_drop_there_goes()
    {
        var c = FenceGrid.Cell(4, 3, Cell, 240);
        Assert.Equal(4, FenceGrid.IndexAt(c.X + 10, c.Y + 10, 3, Cell, 240, 6));
        Assert.Equal(-1, FenceGrid.IndexAt(c.X + 10, c.Y + 10, 3, Cell, 240, 4));
        Assert.Equal(-1, FenceGrid.IndexAt(239, 1, 3, Cell, 240, 6));

        // The left half of a cell goes before it, the right half after it; past the rows, the end.
        Assert.Equal(4, FenceGrid.InsertAt(c.X + 10, c.Y + 10, 3, Cell, 240, 6));
        Assert.Equal(5, FenceGrid.InsertAt(c.X + 60, c.Y + 10, 3, Cell, 240, 6));
        Assert.Equal(6, FenceGrid.InsertAt(c.X, c.Y + 500, 3, Cell, 240, 6));
        Assert.Equal(0, FenceGrid.InsertAt(c.X, -50, 3, Cell, 240, 6));
        Assert.Equal(0, FenceGrid.InsertAt(10, 10, 3, Cell, 240, 0));
    }

    [Fact]
    public void S13021_the_preview_puts_the_dragged_items_at_their_slots_and_the_others_step_aside()
    {
        string[] shown = ["a", "b", "c", "d"];

        // From outside, before "c": the newcomers stand at slots 2 and 3, c and d step aside.
        Assert.Equal(["a", "b", "x", "y", "c", "d"], FenceGrid.Preview(shown, ["x", "y"], 2, StringComparer.OrdinalIgnoreCase));

        // Inside the fence: "a" pointed before "d" lands between c and d, counted among the items that stay.
        Assert.Equal(["b", "c", "a", "d"], FenceGrid.Preview(shown, ["A"], 3, StringComparer.OrdinalIgnoreCase).Select(k => k.ToLowerInvariant()));

        // At the end, past the end, before the start; a key dragged twice stands once.
        Assert.Equal(["a", "b", "c", "d", "x"], FenceGrid.Preview(shown, ["x"], 4, StringComparer.Ordinal));
        Assert.Equal(["a", "b", "c", "d", "x"], FenceGrid.Preview(shown, ["x"], 99, StringComparer.Ordinal));
        Assert.Equal(["x", "a", "b", "c", "d"], FenceGrid.Preview(shown, ["x", "x"], -3, StringComparer.Ordinal));
        Assert.Equal(["x"], FenceGrid.Preview([], ["x"], 0, StringComparer.Ordinal));
    }

    [Fact]
    public void S13008_a_fence_for_n_items_is_near_square_and_shows_them_all()
    {
        var one = FenceGrid.Around(1, Cell);
        var nine = FenceGrid.Around(9, Cell);
        var many = FenceGrid.Around(200, Cell);

        Assert.Equal((Cell.Width, FenceGrid.TitleHeight + Cell.Height), (one.Width, one.Height));
        Assert.Equal(3, FenceGrid.Columns(nine.Width, Cell));
        Assert.Equal(FenceGrid.TitleHeight + FenceGrid.ContentHeight(9, 3, Cell), nine.Height);
        Assert.Equal(FenceGrid.MaxColumns, FenceGrid.Columns(many.Width, Cell));
    }
}
