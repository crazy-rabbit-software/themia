using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3734] A menu row's words and its value share the row as Karpo's do.</summary>
public sealed class MenuShareTests
{
    [Fact]
    public void S3734_both_whole_where_both_fit()
        => Assert.Equal((120.0, 80.0), MenuShare.Share(240, 120, 80));

    [Fact]
    public void S3734_a_short_value_keeps_all_it_needs_and_the_words_are_trimmed()
        => Assert.Equal((150.0, 50.0), MenuShare.Share(200, 300, 50));

    [Fact]
    public void S3734_short_words_keep_all_they_need_and_the_value_has_the_rest()
        => Assert.Equal((100.0, 100.0), MenuShare.Share(200, 100, 300));

    [Fact]
    public void S3734_where_neither_is_short_the_value_has_two_fifths()
        => Assert.Equal((120.0, 80.0), MenuShare.Share(200, 300, 300));

    [Fact]
    public void S3734_no_room_shares_nothing()
        => Assert.Equal((0.0, 0.0), MenuShare.Share(-10, 50, 50));
}
