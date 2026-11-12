using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1624] The mixer's fill.
/// </summary>
public class Polish2LayoutTests
{
    // -----------------------------------------------------------------------------------------
    // [S-1624] what a mixer row's second line says
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, true, 1, "Playing")]
    [InlineData(false, false, true, 3, "Playing")]
    [InlineData(false, false, false, 1, "Silent")]
    [InlineData(true, false, true, 2, "Muted")]
    [InlineData(false, true, true, 1, "Windows sounds")]
    public void S1624_the_caption_says_what_the_app_is_doing(bool muted, bool system, bool active, int sessions, string expected)
    {
        var row = new MixerRow("app", "App", null, 0.5, muted, active, system, sessions);

        Assert.Equal(expected, MixerFill.Detail(row));
    }
}
