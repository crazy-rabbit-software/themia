using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13025] [S-13027] [S-13028] The desk fixes of 2026-11-19: the first drag, the band over a fence, the roll's glide.</summary>
public sealed class DeskFixesTests
{
    // ---- [S-13025] the first press's drag ------------------------------------------------------------

    [Fact]
    public void S13025_a_shell_drag_takes_the_foreground_now_even_with_the_press_held()
        => Assert.Equal(ForegroundStep.Now, PressForeground.When(isForeground: false, mayTake: true, pressHeld: true, shellDrag: true));

    [Fact]
    public void S13017_a_plain_press_still_waits_for_its_release()
        => Assert.Equal(ForegroundStep.AfterPress, PressForeground.When(isForeground: false, mayTake: true, pressHeld: true, shellDrag: false));

    [Fact]
    public void S13025_no_press_held_takes_it_now()
        => Assert.Equal(ForegroundStep.Now, PressForeground.When(isForeground: false, mayTake: true, pressHeld: false, shellDrag: false));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S13025_the_foreground_already_needs_nothing(bool drag)
        => Assert.Equal(ForegroundStep.Already, PressForeground.When(isForeground: true, mayTake: false, pressHeld: true, shellDrag: drag));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void S13025_a_full_screen_app_over_the_display_keeps_it(bool drag)
        => Assert.Equal(ForegroundStep.Never, PressForeground.When(isForeground: false, mayTake: false, pressHeld: true, shellDrag: drag));

    // ---- [S-13027] the band over a fence --------------------------------------------------------------

    private const string Fence = "widget:fence-1";
    private const string Other = "widget:fence-2";

    private static string? FenceOf(string key) => key switch
    {
        @"C:\d\a.txt" or @"C:\d\b.txt" => Fence,
        @"C:\d\c.txt" => Other,
        _ => null,
    };

    [Fact]
    public void S13027_a_band_that_catches_a_fences_items_selects_the_items_and_not_the_fence()
    {
        var hits = DesktopFences.BandHits([Fence, @"C:\d\a.txt", @"C:\d\b.txt", "widget:clock"], FenceOf);
        Assert.Equal([@"C:\d\a.txt", @"C:\d\b.txt", "widget:clock"], hits);
    }

    [Fact]
    public void S13027_a_fence_touched_with_none_of_its_items_stays_selected()
    {
        var hits = DesktopFences.BandHits([Fence, Other, @"C:\d\c.txt"], FenceOf);
        Assert.Equal([Fence, @"C:\d\c.txt"], hits);
    }

    [Fact]
    public void S13027_items_alone_stay_as_they_are()
        => Assert.Equal([@"C:\d\x.txt", "widget:clock"], DesktopFences.BandHits([@"C:\d\x.txt", "widget:clock"], FenceOf));

    // ---- [S-13028] the roll's glide -------------------------------------------------------------------

    private static readonly FracRect Open = new(0.1, 0.2, 0.3, 0.4);
    private static readonly FracRect Rolled = new(0.1, 0.2, 0.3, 0.05);

    [Fact]
    public void S13028_the_glide_starts_where_the_card_was_and_ends_where_it_goes()
    {
        Assert.Equal(Open, CardGlide.At(Open, Rolled, 0));
        Assert.Equal(Rolled, CardGlide.At(Open, Rolled, CardGlide.Ms));
        Assert.Equal(Rolled, CardGlide.At(Open, Rolled, CardGlide.Ms * 3));
        Assert.True(CardGlide.Done(CardGlide.Ms));
        Assert.False(CardGlide.Done(CardGlide.Ms / 2));
    }

    [Fact]
    public void S13028_the_foot_eases_out_and_the_top_left_stays()
    {
        var mid = CardGlide.At(Open, Rolled, CardGlide.Ms / 2);
        Assert.Equal(Open.X, mid.X, 9);
        Assert.Equal(Open.Y, mid.Y, 9);
        Assert.True(mid.H < Open.H && mid.H > Rolled.H);

        // Eased out: past half way at half the time.
        Assert.True(Open.H - mid.H > (Open.H - Rolled.H) / 2);
    }

    [Fact]
    public void S13028_a_glide_with_no_usable_time_lands_at_once()
        => Assert.Equal(Rolled, CardGlide.At(Open, Rolled, double.NaN));
}
