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

    /// <summary>[S-1458] The history is kept while any view holds a Clipboard widget, on screen or not, at the longest capacity asked for.</summary>
    [Fact]
    public void S1458_the_history_is_wanted_while_any_view_holds_a_clipboard_widget()
    {
        var state = AppState.CreateDefault();
        foreach (var view in state.Views)
        {
            view.Widgets.Clear();
        }

        Assert.Null(ClipboardInterest.Capacity(state));

        // A clipboard widget on a view that is not the one on screen still wants the history.
        var hidden = ViewState.Create("Hidden");
        var small = WidgetInstance.Create(WidgetKind.Clipboard, new Geometry.FracRect(0, 0, 0.2, 0.2));
        ((ClipboardConfig)small.Config).Capacity = 10;
        hidden.Widgets.Add(small);
        state.Views.Add(hidden);
        Assert.Equal(10, ClipboardInterest.Capacity(state));

        var large = WidgetInstance.Create(WidgetKind.Clipboard, new Geometry.FracRect(0, 0, 0.2, 0.2));
        ((ClipboardConfig)large.Config).Capacity = 120;
        state.Views[0].Widgets.Add(large);
        Assert.Equal(120, ClipboardInterest.Capacity(state));

        // Any other widget wants nothing of the clipboard.
        hidden.Widgets.Clear();
        state.Views[0].Widgets.Clear();
        state.Views[0].Widgets.Add(WidgetInstance.Create(WidgetKind.Notes, new Geometry.FracRect(0, 0, 0.2, 0.2)));
        Assert.Null(ClipboardInterest.Capacity(state));
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
        // L and XL give the rows a third line; no count footer under them (concise: the entries only).
        Assert.Equal(new ClipboardPlan(1, false, false, true), ClipboardLayout.Plan(XS));
        Assert.Equal(new ClipboardPlan(1, false, false, true), ClipboardLayout.Plan(S));
        Assert.Equal(new ClipboardPlan(1, false, true, false), ClipboardLayout.Plan(M));
        Assert.Equal(new ClipboardPlan(2, false, true, false), ClipboardLayout.Plan(Wide));
        Assert.Equal(new ClipboardPlan(2, true, false, false), ClipboardLayout.Plan(Tall));
        Assert.Equal(new ClipboardPlan(3, true, false, false), ClipboardLayout.Plan(L));
        Assert.Equal(new ClipboardPlan(3, true, false, false), ClipboardLayout.Plan(XL));
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

    [Fact]
    public void S1447_the_progress_bar_is_a_rung_of_the_large_tiles_alone()
    {
        Assert.False(CountdownLayout.ShowsProgress(XS));
        Assert.False(CountdownLayout.ShowsProgress(S));
        Assert.False(CountdownLayout.ShowsProgress(M));
        Assert.False(CountdownLayout.ShowsProgress(Wide));
        Assert.False(CountdownLayout.ShowsProgress(Tall));
        Assert.True(CountdownLayout.ShowsProgress(L));
        Assert.True(CountdownLayout.ShowsProgress(XL));
    }

    [Fact]
    public void S1448_progress_runs_from_when_the_target_was_set_to_the_target()
    {
        var config = new CountdownConfig();
        config.Retarget(Now.AddHours(10), Now.AddHours(-30));
        Assert.Equal(0.75, CountdownLayout.Progress(config, Now)!.Value, 6);
        Assert.Equal(1, CountdownLayout.Progress(config, Now.AddDays(5)));
        Assert.Equal(0, CountdownLayout.Progress(config, Now.AddDays(-5)));

        // Unknown when it was set, set after the target, or no target at all: no bar.
        Assert.Null(CountdownLayout.Progress(new CountdownConfig { Target = Now }, Now));
        Assert.Null(CountdownLayout.Progress(new CountdownConfig { Target = Now, Created = Now.AddHours(1) }, Now));
        Assert.Null(CountdownLayout.Progress(new CountdownConfig(), Now));
    }

    [Fact]
    public void S1448_clone_keeps_the_creation_time()
    {
        var config = new CountdownConfig();
        config.Retarget(Now.AddHours(10), Now);
        Assert.Equal(Now, ((CountdownConfig)config.Clone()).Created);
    }

    [Fact]
    public void S1447_hero_width_is_the_formats_widest()
    {
        Assert.True(CountdownLayout.HeroCharacters(CountdownFormat.DaysHoursMinutesSeconds, false)
            > CountdownLayout.HeroCharacters(CountdownFormat.Days, false));
        Assert.Equal(CountdownLayout.HeroCharacters(CountdownFormat.Auto, false) + 4, CountdownLayout.HeroCharacters(CountdownFormat.Auto, true));
    }
}
