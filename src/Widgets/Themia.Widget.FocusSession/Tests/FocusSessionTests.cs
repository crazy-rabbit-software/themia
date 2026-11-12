using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.FocusSession.Tests;

/// <summary>[S-11305] The focus widget on the timers' sized face.</summary>
public sealed class FocusSessionTests
{
    [Fact]
    public void S11305_the_time_is_on_its_sizes_step_and_in_a_ring_only_where_ring_is_on()
    {
        var plain = FocusSessionLayout.Sized(new FocusSessionConfig());
        Assert.Equal(FamilyType.Hero68, plain.Step);
        Assert.Equal(0, plain.Ring);

        var ring = FocusSessionLayout.Sized(new FocusSessionConfig { Ring = true });
        Assert.Equal(FamilyType.Big40, ring.Step);
        Assert.True(ring.Ring > 0);
        Assert.False(new FocusSessionConfig().Ring);
    }

    [Fact]
    public void S11305_a_session_of_an_hour_or_more_is_sized_for_its_hours()
    {
        var minutes = FocusSessionLayout.Sizing(new FocusSessionConfig { Minutes = 30 }, Locale.En);
        var hours = FocusSessionLayout.Sizing(new FocusSessionConfig { Minutes = 90 }, Locale.En);
        Assert.True(hours.Natural.Width > minutes.Natural.Width);
        Assert.Equal(minutes.Natural.Height, hours.Natural.Height);
    }

    [Fact]
    public void S11305_the_card_is_the_face_and_its_row_one_height_up_to_half_again_wider()
    {
        var sizing = FocusSessionLayout.Sizing(new FocusSessionConfig(), Locale.En);
        Assert.True(sizing.FixedHeight && !sizing.FixedWidth);
        Assert.Equal(12 + 92 + 20 + 8 + 28 + 12 + 2, sizing.Natural.Height);

        var copy = (FocusSessionConfig)new FocusSessionConfig { Size = ReadoutSize.Large, Ring = true }.Clone();
        Assert.Equal((ReadoutSize.Large, true), (copy.Size, copy.Ring));
    }
}
