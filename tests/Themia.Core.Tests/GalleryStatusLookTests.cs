using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6714] A gallery scan with nothing to draw, as the Status row says it (GalleryFormatting.Look, Retries; the gallery's [S-779]).</summary>
public sealed class GalleryStatusLookTests
{
    [Theory]
    [InlineData(GalleryStatus.Loading, StatusKind.Empty)]
    [InlineData(GalleryStatus.NoImages, StatusKind.Empty)]
    [InlineData(GalleryStatus.Offline, StatusKind.Empty)]
    [InlineData(GalleryStatus.NoFolder, StatusKind.Setup)]
    [InlineData(GalleryStatus.NoFile, StatusKind.Setup)]
    [InlineData(GalleryStatus.InvalidPath, StatusKind.Problem)]
    [InlineData(GalleryStatus.NotFound, StatusKind.Problem)]
    [InlineData(GalleryStatus.NotAFolder, StatusKind.Problem)]
    [InlineData(GalleryStatus.AccessDenied, StatusKind.Problem)]
    [InlineData(GalleryStatus.Failed, StatusKind.Problem)]
    [InlineData(GalleryStatus.NotAPicture, StatusKind.Problem)]
    [InlineData(GalleryStatus.Unavailable, StatusKind.Problem)]
    public void S6714_gallery_waiting_and_emptiness_are_plain_every_failure_a_problem(GalleryStatus status, StatusKind kind)
        => Assert.Equal(kind, GalleryFormatting.Look(status));

    [Fact]
    public void S6714_gallery_only_a_failed_read_and_an_unanswered_photo_of_the_day_offer_retry()
    {
        var retried = Enum.GetValues<GalleryStatus>().Where(GalleryFormatting.Retries).ToArray();
        Assert.Equal([GalleryStatus.Failed, GalleryStatus.Unavailable], retried);
    }
}
