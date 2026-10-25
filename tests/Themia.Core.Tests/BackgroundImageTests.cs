using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6590]..[S-6593] A widget's own background image: the path rule and how the fill lies over it.</summary>
public class BackgroundImageTests
{
    [Theory]
    [InlineData(@"C:\Users\me\Pictures\lake.jpg")]
    [InlineData(@"D:\photo.PNG")]
    [InlineData(@"\\nas\share\walls\forest.webp")]
    [InlineData(@"C:\a b\c.jpeg")]
    public void S6591_a_picture_files_full_path_is_kept(string path)
    {
        Assert.True(BackgroundImage.IsImagePath(path));
        Assert.Equal((path, (string?)null), BackgroundImage.Repair(path));
    }

    [Theory]
    [InlineData(@"lake.jpg")]
    [InlineData(@"..\lake.jpg")]
    [InlineData(@"C:\Users\me\notes.txt")]
    [InlineData(@"C:\Users\me\Pictures")]
    [InlineData("https://example.com/lake.jpg")]
    [InlineData("C:\\bad\u0001name.png")]
    public void S6591_anything_else_is_no_image_loudly(string path)
    {
        Assert.False(BackgroundImage.IsImagePath(path));
        var (kept, repair) = BackgroundImage.Repair(path);
        Assert.Equal(string.Empty, kept);
        Assert.NotNull(repair);
    }

    [Fact]
    public void S6591_none_is_no_repair_and_a_path_too_long_is()
    {
        Assert.Equal((string.Empty, (string?)null), BackgroundImage.Repair(null));
        Assert.Equal((string.Empty, (string?)null), BackgroundImage.Repair("   "));

        var long_ = @"C:\" + new string('a', BackgroundImage.MaxPathLength) + ".png";
        Assert.NotNull(BackgroundImage.Repair(long_).Repair);
    }

    [Fact]
    public void S6592_over_an_image_only_a_colour_of_the_widgets_own_is_painted()
    {
        var colour = new CardFill(FillKind.Colour, new RgbColor(0, 0, 0), 0.4);

        Assert.Equal(colour, BackgroundImage.Over(colour, hasImage: true));
        Assert.Equal(FillKind.Transparent, BackgroundImage.Over(new CardFill(FillKind.Default, null, 1), hasImage: true).Kind);
        Assert.False(BackgroundImage.Over(new CardFill(FillKind.Default, null, 1), hasImage: true).Painted);
        Assert.False(BackgroundImage.Over(new CardFill(FillKind.Transparent, null, 0), hasImage: true).Painted);
    }

    [Fact]
    public void S6592_without_an_image_the_fill_is_drawn_as_it_is()
    {
        var fill = new CardFill(FillKind.Default, null, 1);

        Assert.Equal(fill, BackgroundImage.Over(fill, hasImage: false));
    }

    [Fact]
    public void S6593_the_menu_line_names_the_file()
    {
        Assert.Equal("lake.jpg", BackgroundImage.Name(@"C:\Users\me\Pictures\lake.jpg"));
        Assert.Equal(string.Empty, BackgroundImage.Name(""));
    }
}
