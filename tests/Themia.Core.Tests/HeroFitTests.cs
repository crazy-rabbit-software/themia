using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-659] A readout is measured by its glyphs (the fill rule that used to go with it is retired).</summary>
public sealed class HeroFitTests
{
    [Fact]
    public void S659_glyphs_are_measured_by_their_class()
    {
        Assert.Equal((4 * 0.58) + 0.25, HeroFit.Ems("88:88"), 6);
        Assert.Equal((6 * 0.58) + (2 * 0.25), HeroFit.Ems("88:88:88"), 6);
        Assert.Equal(0.72 + 0.95, HeroFit.Ems("AM"), 6);
        Assert.Equal(0.41 + (2 * 0.58) + 0.39 + 0.72, HeroFit.Ems("-18°C"), 6);
        Assert.Equal(0, HeroFit.Ems(string.Empty));

        // A script the table does not know counts a full em a character, a combining mark nothing.
        Assert.Equal(2, HeroFit.Ems("午後"), 6);
        Assert.Equal(1, HeroFit.Ems("க்"), 6);
    }
}
