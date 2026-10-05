using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Package PH: Clipboard, Countdown and Mood ladders and rules ([S-1440]..[S-1457]). Areas are the
/// size tiers' slots (card less a 44px header).
/// </summary>
public class TrackersLayoutTests
{
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);

    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(2));

    // ---- Clipboard ---------------------------------------------------------------------------

    [Fact]
    public void S1440_capacity_is_1_to_200_default_50()
    {
        Assert.Equal(1, ClipboardHistory.MinCapacity);
        Assert.Equal(200, ClipboardHistory.MaxCapacity);
        Assert.Equal(50, new ClipboardConfig().EffectiveCapacity);
        Assert.Equal(200, new ClipboardConfig { Capacity = 500 }.EffectiveCapacity);
        Assert.Equal(1, new ClipboardConfig { Capacity = 0 }.EffectiveCapacity);
    }

    [Fact]
    public void S1441_one_line_preview_is_the_collapsed_preview()
    {
        const string text = "line one\n  line   two\n";
        Assert.Equal(ClipboardFormatting.Preview(text, 120), ClipboardLayout.PreviewLines(text, 1, 120));
    }

    [Fact]
    public void S1441_multi_line_preview_keeps_breaks_drops_blank_lines_and_marks_more()
    {
        const string text = "Street 1\r\n\r\n  8000   Zurich \nSwitzerland\nEurope";

        Assert.Equal("Street 1\n8000 Zurich", ClipboardLayout.PreviewLines(text, 2, 120).Split(" …")[0]);
        Assert.EndsWith(" …", ClipboardLayout.PreviewLines(text, 2, 120));
        Assert.Equal("Street 1\n8000 Zurich\nSwitzerland …", ClipboardLayout.PreviewLines(text, 3, 120));
        Assert.Equal("Street 1\n8000 Zurich\nSwitzerland\nEurope", ClipboardLayout.PreviewLines(text, 4, 120));
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("", 3)]
    [InlineData("abc", 0)]
    public void S1441_nothing_to_preview_is_empty(string? text, int lines)
        => Assert.Equal(string.Empty, ClipboardLayout.PreviewLines(text, lines, 120));

    [Theory]
    [InlineData("https://example.com/a?b=1", ClipboardContent.Link)]
    [InlineData("anna@example.org", ClipboardContent.Email)]
    [InlineData("#60cdff", ClipboardContent.Colour)]
    [InlineData("rgb(1, 2, 3)", ClipboardContent.Colour)]
    [InlineData("123", ClipboardContent.Number)]
    [InlineData("-1,234.5", ClipboardContent.Number)]
    [InlineData("NaN", ClipboardContent.Text)]
    [InlineData("cafe00", ClipboardContent.Text)]
    [InlineData("two\nlines", ClipboardContent.Lines)]
    [InlineData("hello world", ClipboardContent.Text)]
    [InlineData("a@b", ClipboardContent.Text)]
    [InlineData("C:\\Windows", ClipboardContent.Text)]
    public void S1442_entries_are_classified_conservatively(string text, ClipboardContent expected)
        => Assert.Equal(expected, ClipboardLayout.Classify(text));

    [Fact]
    public void S1442_a_colour_entry_yields_its_swatch()
    {
        Assert.True(ClipboardLayout.TryColour(" #ff0000 ", out var red));
        Assert.Equal(new RgbColor(255, 0, 0), red);
        Assert.False(ClipboardLayout.TryColour("ff0000", out _));
    }

    [Fact]
    public void S1443_clipboard_ladder_by_tier()
    {
        Assert.Equal(new ClipboardPlan(1, false, false, false, true), ClipboardLayout.Plan(XS));
        Assert.Equal(new ClipboardPlan(1, false, false, false, true), ClipboardLayout.Plan(S));
        Assert.Equal(new ClipboardPlan(1, false, true, false, false), ClipboardLayout.Plan(M));
        Assert.Equal(new ClipboardPlan(2, false, true, false, false), ClipboardLayout.Plan(Wide));
        Assert.Equal(new ClipboardPlan(2, true, false, false, false), ClipboardLayout.Plan(Tall));
        Assert.Equal(new ClipboardPlan(3, true, false, true, false), ClipboardLayout.Plan(L));
        Assert.Equal(new ClipboardPlan(3, true, false, true, false), ClipboardLayout.Plan(XL));
    }

    [Fact]
    public void S1443_unmeasured_area_is_one_plain_line()
        => Assert.Equal(1, ClipboardLayout.Plan(default).PreviewLines);

    // ---- Keyboard cursor ---------------------------------------------------------------------

    [Theory]
    [InlineData(5, null, WidgetKey.Down, 0)]
    [InlineData(5, null, WidgetKey.Up, 4)]
    [InlineData(5, 2, WidgetKey.Down, 3)]
    [InlineData(5, 4, WidgetKey.Down, 4)]
    [InlineData(5, 0, WidgetKey.Up, 0)]
    [InlineData(5, 3, WidgetKey.Home, 0)]
    [InlineData(5, 1, WidgetKey.End, 4)]
    [InlineData(5, 2, WidgetKey.Right, 3)]
    [InlineData(5, 2, WidgetKey.Left, 1)]
    [InlineData(5, 2, WidgetKey.Enter, 2)]
    [InlineData(5, 9, WidgetKey.Enter, null)]
    [InlineData(5, 2, WidgetKey.Escape, null)]
    [InlineData(0, 2, WidgetKey.Down, null)]
    public void S1457_cursor_moves_clamped(int count, int? current, WidgetKey key, int? expected)
        => Assert.Equal(expected, ItemCursor.Move(count, current, key));

    // ---- Countdown ---------------------------------------------------------------------------

    private static List<CountdownTimer> Timers(params int[] hoursFromNow)
        => [.. hoursFromNow.Select((h, i) => CountdownTimer.Create($"t{i}", Now.AddHours(h)))];

    [Fact]
    public void S1446_featured_is_the_soonest_ahead_else_the_latest_past()
    {
        Assert.Equal(2, CountdownLayout.Featured(Timers(30, -1, 5, 100), Now));
        Assert.Equal(1, CountdownLayout.Featured(Timers(-30, -1, -5), Now));
        Assert.Equal(-1, CountdownLayout.Featured([], Now));
    }

    [Fact]
    public void S1446_next_line_names_the_soonest_other_upcoming()
    {
        var timers = Timers(30, -1, 5, 100);
        Assert.Equal("Next: t0 in 1d 6h", CountdownLayout.NextLine(timers, 2, Now, Locale.En));
        Assert.Null(CountdownLayout.NextLine(Timers(5, -1), 0, Now, Locale.En));
    }

    [Fact]
    public void S1447_countdown_ladder_by_tier()
    {
        Assert.Equal(CountdownHero.Only, CountdownLayout.Plan(XS, 3).Hero);
        Assert.True(CountdownLayout.Plan(S, 3).NextLine);
        Assert.Equal(CountdownHero.Top, CountdownLayout.Plan(M, 3).Hero);
        Assert.Equal(CountdownHero.Left, CountdownLayout.Plan(Wide, 3).Hero);
        Assert.Equal(CountdownHero.Top, CountdownLayout.Plan(Tall, 3).Hero);
        Assert.Equal(CountdownHero.Top, CountdownLayout.Plan(L, 3).Hero);
        Assert.True(CountdownLayout.Plan(L, 3).HeroProgress);
        Assert.False(CountdownLayout.Plan(L, 3).RowProgress);
        Assert.True(CountdownLayout.Plan(XL, 3).RowProgress);

        // One timer is the whole tile at any size; none is the empty state.
        Assert.Equal(CountdownHero.Only, CountdownLayout.Plan(L, 1).Hero);
        Assert.False(CountdownLayout.Plan(L, 1).NextLine);
        Assert.Equal(CountdownHero.None, CountdownLayout.Plan(L, 0).Hero);
    }

    [Fact]
    public void S1448_progress_runs_from_creation_to_target()
    {
        var timer = CountdownTimer.Create("x", Now.AddHours(10), Now.AddHours(-30));
        Assert.Equal(0.75, CountdownLayout.Progress(timer, Now)!.Value, 6);
        Assert.Equal(1, CountdownLayout.Progress(timer, Now.AddDays(5)));
        Assert.Equal(0, CountdownLayout.Progress(timer, Now.AddDays(-5)));

        Assert.Null(CountdownLayout.Progress(CountdownTimer.Create("x", Now), Now));
        Assert.Null(CountdownLayout.Progress(CountdownTimer.Create("x", Now, Now.AddHours(1)), Now));
    }

    [Fact]
    public void S1448_clone_keeps_the_creation_time()
    {
        var timer = CountdownTimer.Create("x", Now.AddHours(10), Now);
        Assert.Equal(Now, timer.Clone().Created);
    }

    [Fact]
    public void S1447_hero_width_is_the_formats_widest()
    {
        Assert.True(CountdownLayout.HeroCharacters(CountdownFormat.DaysHoursMinutesSeconds, false)
            > CountdownLayout.HeroCharacters(CountdownFormat.Days, false));
        Assert.Equal(CountdownLayout.HeroCharacters(CountdownFormat.Auto, false) + 4, CountdownLayout.HeroCharacters(CountdownFormat.Auto, true));
    }
}
