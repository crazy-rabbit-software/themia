using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-761] [S-762] [S-3112] The gallery rules the host's service shares: the photo of the day's trust boundary and its credit, and the empty state each source names. Moved out of GalleryLayoutTests when the gallery's own layouts moved to its module, unchanged in what they assert.</summary>
public sealed class GalleryRulesTests
{
    [Fact]
    public void S762_a_bing_reply_becomes_a_photo_on_bings_own_host()
    {
        const string json = """
            {"images":[{"url":"/th?id=OHR.Example_EN-US123_1920x1080.jpg&rf=x&pid=hp","title":"A lake","copyright":"© Someone"}]}
            """;

        var photo = BingPhoto.Parse(json);

        Assert.NotNull(photo);
        Assert.Equal("https", photo.Image.Scheme);
        Assert.Equal("www.bing.com", photo.Image.Host);
        Assert.StartsWith("/th", photo.Image.PathAndQuery, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"images":[{"url":"//evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[{"url":"https://evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[{"url":"th?id=x"}]}""")]
    [InlineData("""{"images":[{"url":"/\\evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[{"url":"@evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[]}""")]
    [InlineData("""{"images":[{"title":"no url"}]}""")]
    [InlineData("""{}""")]
    [InlineData("null")]
    [InlineData("<html>not json</html>")]
    [InlineData("")]
    public void S762_anything_that_is_not_a_path_on_bing_is_refused(string json)
        => Assert.Null(BingPhoto.Parse(json));

    [Fact]
    public void S3112_a_bing_reply_carries_only_the_credit_not_the_title()
    {
        const string json = """
            {"images":[{"url":"/th?id=OHR.Example_EN-US123_1920x1080.jpg","title":"A lake","copyright":"Lake Bled, Slovenia (© Jane Doe/Agency)"}]}
            """;

        Assert.Equal("© Jane Doe/Agency", BingPhoto.Parse(json)?.Credit?.Text);
    }

    [Fact]
    public void S3112_a_missing_credit_is_no_tooltip_but_still_a_photo()
    {
        var photo = BingPhoto.Parse("""{"images":[{"url":"/th?id=x.jpg"}]}""");

        Assert.NotNull(photo);
        Assert.Null(photo.Credit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t\u0000")]
    [InlineData("‮​")]
    [InlineData("(© )")]
    public void S3112_a_blank_credit_is_none(string? raw)
        => Assert.Null(BingCredit.Parse(raw));

    [Theory]
    [InlineData("  © Someone  ", "© Someone")]
    [InlineData("© Some\r\none\u0007here", "© Some one here")]
    [InlineData("© Evil‮gnp.exe", "© Evilgnp.exe")]
    [InlineData("Place (© Open-ended", "© Open-ended")]
    [InlineData("Credit without a mark", "Credit without a mark")]
    [InlineData("A (© First) B (© Second)", "© Second")]
    public void S3112_the_credit_is_cleaned(string raw, string expected)
        => Assert.Equal(expected, BingCredit.Parse(raw)?.Text);

    [Fact]
    public void S3112_a_long_credit_is_capped()
    {
        var credit = BingCredit.Parse("© " + new string('x', 1000));

        Assert.NotNull(credit);
        Assert.Equal(BingCredit.MaxLength, credit.Text.Length);
    }

    [Fact]
    public void S3112_the_cap_never_splits_a_surrogate_pair()
    {
        var credit = BingCredit.Parse("© " + new string('x', BingCredit.MaxLength - 3) + "😀😀");

        Assert.NotNull(credit);
        Assert.True(credit.Text.Length <= BingCredit.MaxLength);
        Assert.False(char.IsHighSurrogate(credit.Text[^1]));
    }

    [Theory]
    [InlineData(GalleryStatus.NotFound, GallerySource.File, "picture")]
    [InlineData(GalleryStatus.NotFound, GallerySource.Folder, "folder")]
    [InlineData(GalleryStatus.Offline, GallerySource.Bing, "Offline")]
    [InlineData(GalleryStatus.Unavailable, GallerySource.Bing, "today's photo")]
    [InlineData(GalleryStatus.Loading, GallerySource.Bing, "today's photo")]
    public void S761_each_source_names_its_own_empty_state(GalleryStatus status, GallerySource source, string expected)
        => Assert.Contains(expected, GalleryFormatting.StatusMessage(status, FolderPathProblem.None, source), StringComparison.Ordinal);
}
