using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-774] [S-775] [S-1862] [S-3792] The gallery's config under the root's rules (the hourly rescan, the set up button, the sticker's switch, colour, width and clone): the config is the gallery module's, so they run here. Moved from Core's GalleryLayoutTests and StickerTests, unchanged in what they assert.</summary>
public sealed class GalleryHostTests
{
    [Fact]
    public void S3792_every_gallery_rescans_hourly()
    {
        Assert.Equal(TimeSpan.FromHours(1), new GalleryConfig().TickInterval);
        Assert.Equal(GallerySourceKey.RescanInterval, new GalleryConfig { Source = GallerySource.Bing }.TickInterval);
    }

    [Theory]
    [InlineData(GalleryStatus.NoFolder, GallerySource.Folder)]
    [InlineData(GalleryStatus.NoFile, GallerySource.File)]
    public void S1862_nothing_chosen_is_the_set_up_button_not_a_sentence(GalleryStatus status, GallerySource source)
        => Assert.Equal(string.Empty, GalleryFormatting.StatusMessage(status, FolderPathProblem.None, source));

    [Fact]
    public void S774_only_a_turned_or_outlined_file_is_a_sticker()
    {
        Assert.False(new GalleryConfig { Source = GallerySource.File }.IsSticker);
        Assert.True(new GalleryConfig { Source = GallerySource.File, Rotation = 10 }.IsSticker);
        Assert.True(new GalleryConfig { Source = GallerySource.File, Outline = true }.IsSticker);
        Assert.False(new GalleryConfig { Source = GallerySource.File, Rotation = 360 }.IsSticker);
        Assert.False(new GalleryConfig { Source = GallerySource.Folder, Rotation = 10, Outline = true }.IsSticker);
        Assert.False(new GalleryConfig { Source = GallerySource.Bing, Outline = true }.IsSticker);
    }

    [Fact]
    public void S775_a_bad_colour_is_white_and_the_width_is_clamped()
    {
        var config = new GalleryConfig { OutlineColor = "not a colour", OutlineWidth = 400 };

        Assert.Equal("#FFFFFF", config.EffectiveOutlineColor);
        Assert.Equal(StickerRules.MaxOutline, config.EffectiveOutlineWidth);
        Assert.Equal(StickerRules.MinOutline, new GalleryConfig { OutlineWidth = -3 }.EffectiveOutlineWidth);
        Assert.Equal("#FF8800", new GalleryConfig { OutlineColor = "#f80" }.EffectiveOutlineColor);
    }

    [Fact]
    public void S775_the_sticker_settings_survive_a_clone()
    {
        var copy = (GalleryConfig)new GalleryConfig { Rotation = -20, Outline = true, OutlineColor = "#112233", OutlineWidth = 9 }.Clone();

        Assert.Equal(-20, copy.Rotation);
        Assert.True(copy.Outline);
        Assert.Equal("#112233", copy.OutlineColor);
        Assert.Equal(9, copy.OutlineWidth);
    }
}
