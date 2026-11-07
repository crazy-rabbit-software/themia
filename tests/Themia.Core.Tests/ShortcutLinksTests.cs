using Themia.App.Services.Files;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1401] Which dropped links stay links: what the target alone would lose.</summary>
public sealed class ShortcutLinksTests
{
    [Fact]
    public void S1401_issue_190_a_link_with_its_own_icon_is_kept_as_the_link()
        => Assert.True(ShortcutLinks.KeepsLinkItself(string.Empty, @"C:\Icons\rammap.ico"));

    [Fact]
    public void S1401_a_link_passing_arguments_is_kept_as_the_link()
        => Assert.True(ShortcutLinks.KeepsLinkItself("-Ew", string.Empty));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  ", " ")]
    public void S1401_a_plain_link_is_replaced_by_its_target(string? arguments, string? icon)
        => Assert.False(ShortcutLinks.KeepsLinkItself(arguments, icon));
}
