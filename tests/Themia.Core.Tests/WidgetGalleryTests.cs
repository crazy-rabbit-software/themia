using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-14912] [S-14913] The widget gallery's arrangement: blocks on whole cells, screens, the arrows' walk.</summary>
public sealed class WidgetGalleryTests
{
    private static readonly CellBox Area = new(4, 10, 150, 80);

    private static WidgetId K(string id) => new(id);

    private static GalleryBlock Block(string key, string? title, params string[] kinds)
        => new(key, title is null ? null : LocalText.Fixed(title), [.. kinds.Select(K)]);

    private static GalleryCategory Category(params GalleryBlock[] blocks) => new(AddFolderId.Weather, LocalText.Fixed("Weather"), Glyphs.Apps, blocks);

    /// <summary>Sizes by the kind's name: "w30x20" (or "w30x20b", a second of that size) is 30 × 20 cells; anything else 24 × 12.</summary>
    private static (int W, int H) Size(WidgetId kind)
    {
        var name = kind.Value;
        var x = name.IndexOf('x', StringComparison.Ordinal);
        if (!name.StartsWith('w') || x < 2)
        {
            return (24, 12);
        }

        var tall = new string([.. name[(x + 1)..].TakeWhile(char.IsAsciiDigit)]);
        return (int.Parse(name[1..x], System.Globalization.CultureInfo.InvariantCulture), int.Parse(tall, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void AssertTidy(IReadOnlyList<GalleryScreen> screens, CellBox area)
    {
        foreach (var screen in screens)
        {
            var boxes = screen.Cards.Select(c => c.Box).Concat(screen.Heads.Select(h => h.Box)).Append(screen.Title).ToList();
            Assert.All(boxes, b => Assert.True(area.Holds(b), $"{b} is outside {area}"));
            for (var i = 0; i < boxes.Count; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    Assert.False(boxes[i].Overlaps(boxes[j]), $"{boxes[i]} and {boxes[j]} share cells");
                }
            }

            // No two cards touch: none joins another ([S-2240]).
            foreach (var a in screen.Cards)
            {
                foreach (var b in screen.Cards.Where(b => !ReferenceEquals(a, b)))
                {
                    var (x, y) = (a.Box, b.Box);
                    var side = (x.Right == y.X || y.Right == x.X) && x.Y < y.Bottom && y.Y < x.Bottom;
                    var foot = (x.Bottom == y.Y || y.Bottom == x.Y) && x.X < y.Right && y.X < x.Right;
                    Assert.False(side || foot, $"{a.Kind} and {b.Kind} touch");
                }
            }
        }
    }

    [Fact]
    public void S14912_a_category_lays_its_blocks_on_whole_cells_inside_the_area_never_overlapping()
    {
        var category = Category(
            Block("weather/Widgets", null, "w30x20", "w40x12", "w24x24", "w60x30"),
            Block("weather/family:Facts", "Weather facts", "w20x8", "w20x8b", "w20x8c", "w20x8d", "w20x8e"),
            Block("weather/family:Sun", "Sun", "w30x10", "w30x10b"));

        var screens = WidgetGallery.Lay(category, Area, Size);

        Assert.NotEmpty(screens);
        AssertTidy(screens, Area);

        // Every kind exactly once, at its own cells.
        var laid = screens.SelectMany(s => s.Cards).ToList();
        Assert.Equal(category.Blocks.SelectMany(b => b.Kinds), laid.Select(c => c.Kind));
        Assert.All(laid, c => Assert.Equal(Size(c.Kind), (c.Box.W, c.Box.H)));

        // The title line on top of each screen, the cards under it.
        Assert.All(screens, s => Assert.All(s.Cards, c => Assert.True(c.Box.Y >= s.Title.Bottom)));
    }

    [Fact]
    public void S14912_a_familys_cards_stay_together_under_its_name()
    {
        var category = Category(
            Block("own", null, "w24x12", "w24x12b"),
            Block("fam", "Facts", "w20x8", "w20x8b", "w20x8c"),
            Block("sun", "Sun", "w30x10"));

        var screen = Assert.Single(WidgetGallery.Lay(category, Area, Size));

        foreach (var block in category.Blocks)
        {
            var mine = screen.Cards.Where(c => c.Block == block.Key).Select(c => c.Box).ToList();
            var left = mine.Min(b => b.X);
            var top = mine.Min(b => b.Y);
            var box = new CellBox(left, top, mine.Max(b => b.Right) - left, mine.Max(b => b.Bottom) - top);

            // No card of another block inside this block's box: blocks, never scattered.
            Assert.DoesNotContain(screen.Cards, c => c.Block != block.Key && c.Box.Overlaps(box));
            if (block.Title is { } title)
            {
                var head = Assert.Single(screen.Heads, h => h.Text.Equals(title));
                Assert.Equal((box.X, box.Y), (head.Box.X, head.Box.Bottom));
            }
        }

        // The block's cards in reading order, a row's gap apart.
        var fam = screen.Cards.Where(c => c.Block == "fam").ToList();
        Assert.Equal(fam[0].Box.Right + WidgetGallery.CardGap, fam[1].Box.X);
        Assert.Equal(fam[0].Box.Y, fam[1].Box.Y);
    }

    [Fact]
    public void S14912_a_category_that_does_not_fit_one_screen_continues_on_more()
    {
        // Twelve tall cards: never one screen of 80 rows.
        var kinds = Enumerable.Range(0, 12).Select(i => $"w60x30{(char)('a' + i)}").ToArray();
        var category = Category(Block("own", null, kinds[..6]), Block("fam", "Family", kinds[6..]));

        var screens = WidgetGallery.Lay(category, Area, Size);

        Assert.True(screens.Count > 1);
        AssertTidy(screens, Area);
        Assert.All(screens, s => Assert.Equal((screens.Count, AddFolderId.Weather), (s.Pages, s.Category)));
        Assert.Equal(Enumerable.Range(0, screens.Count), screens.Select(s => s.Page));
        Assert.Equal(kinds.Select(K), screens.SelectMany(s => s.Cards).Select(c => c.Kind));

        // A family split over screens carries its name on each.
        Assert.All(screens.Where(s => s.Cards.Any(c => c.Block == "fam")), s => Assert.Contains(s.Heads, h => h.Text.Equals(LocalText.Fixed("Family"))));
    }

    [Fact]
    public void S14912_a_card_larger_than_the_area_is_held_to_it()
    {
        var small = new CellBox(0, 0, 40, 30);
        var screen = Assert.Single(WidgetGallery.Lay(Category(Block("own", null, "w90x90")), small, Size));
        var card = Assert.Single(screen.Cards);

        Assert.True(small.Holds(card.Box));
    }

    [Fact]
    public void S14912_the_same_input_lays_the_same_screens()
    {
        var category = Category(Block("own", null, "w30x20", "w40x12"), Block("fam", "Facts", "w20x8", "w20x8b"));

        var one = WidgetGallery.Lay(category, Area, Size);
        var two = WidgetGallery.Lay(category, Area, Size);

        Assert.Equal(one.SelectMany(s => s.Cards), two.SelectMany(s => s.Cards));
    }

    [Fact]
    public void S14913_the_arrows_walk_every_screen_round_and_a_tab_goes_to_its_first()
    {
        Assert.Equal(1, WidgetGallery.Step(0, 3, back: false));
        Assert.Equal(0, WidgetGallery.Step(2, 3, back: false));
        Assert.Equal(2, WidgetGallery.Step(0, 3, back: true));
        Assert.Equal(0, WidgetGallery.Step(5, 0, back: false));

        var weather = Category(Block("own", null, "w24x12"));
        var time = weather with { Id = AddFolderId.Time };
        var screens = WidgetGallery.Lay([weather, time], Area, Size);
        Assert.Equal(1, WidgetGallery.FirstOf(screens, AddFolderId.Time));
        Assert.Equal(0, WidgetGallery.FirstOf(screens, AddFolderId.System));
    }
}
