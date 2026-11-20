using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14200] Where a face's ink stands is measured as drawn by the host's meter; without one, the family's model stands.
/// </summary>
public class InkCentringTests
{
    [Fact]
    public void S14200_without_a_meter_a_line_stands_on_the_model()
    {
        // Core's tests install no meter (the host does, at start): the family's model of capitals on the baseline stands.
        Assert.Null(InkMeter.Measure(new InkText("Sleep", "Segoe UI Variable", 14, 400, 20)));
        Assert.Equal(LineInk.Of(FamilyType.Body), InkMeter.Line(FamilyType.Body, "Sleep", "Segoe UI Variable", 400));
    }

    [Fact]
    public void S14200_a_line_saying_nothing_is_a_planned_line_never_measured()
    {
        Assert.Null(InkMeter.Measure(new InkText(string.Empty, "Segoe UI Variable", 14, 400, 20)));
        Assert.Null(InkMeter.Measure(new InkText("On", "Segoe UI Variable", 0, 400, 20)));
        Assert.Equal(LineInk.Of(FamilyType.Body), InkMeter.Line(FamilyType.Body, null, "Segoe UI Variable", 400));
    }
}
