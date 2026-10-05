using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

public sealed class ViewListRulesTests
{
    private static readonly ViewSummary[] Views =
    [
        new("a", "Work"),
        new("b", "Home office"),
        new("c", "Gaming"),
    ];

    [Theory]
    [InlineData("Work", "WO")]
    [InlineData("Home office", "HO")]
    [InlineData("  spaced   out  name ", "SO")]
    [InlineData("x", "X")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    [InlineData(null, "?")]
    public void S911_initials_take_two_words_or_two_letters(string? name, string expected)
        => Assert.Equal(expected, ViewListRules.Initials(name));

    [Fact]
    public void S911_initials_do_not_split_a_character_built_from_two_code_points()
    {
        // "e" followed by a combining acute accent is one character on screen.
        var initials = ViewListRules.Initials("école");

        Assert.Equal("ÉC", initials);
    }

    [Fact]
    public void S908_an_empty_or_dangling_whitelist_offers_every_view()
    {
        Assert.Equal(["a", "b", "c"], ViewListRules.EffectiveVisible(Views, []).Order());
        Assert.Equal(["a", "b", "c"], ViewListRules.EffectiveVisible(Views, ["gone"]).Order());
        Assert.Equal(["b"], ViewListRules.EffectiveVisible(Views, ["gone", "b"]));
    }

    [Fact]
    public void S908_unticking_a_view_leaves_the_rest_in_view_order()
    {
        var next = ViewListRules.Toggle(Views, [], "b", on: false);

        Assert.Equal(["a", "c"], next);
    }

    [Fact]
    public void S908_the_last_view_cannot_be_unticked()
    {
        var next = ViewListRules.Toggle(Views, ["a"], "a", on: false);

        Assert.Equal(["a"], next);
    }

    [Fact]
    public void S908_ticking_every_view_goes_back_to_all()
    {
        var next = ViewListRules.Toggle(Views, ["a", "b"], "c", on: true);

        Assert.Empty(next);
    }
}
