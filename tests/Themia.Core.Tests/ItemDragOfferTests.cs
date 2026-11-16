using Themia.Core.Files;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5549] [S-5586] [S-1332] What a drag a view starts offers a target as files and folders, and where its picture is held.</summary>
public sealed class ItemDragOfferTests
{
    private const string ThisPc = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    [Fact]
    public void S5586_files_folders_and_shares_are_offered_as_they_are_in_their_order()
    {
        var offer = new ItemDragOffer([@"C:\Users\me\Desktop\b.txt", @"D:\Folder", @"\\server\share\a.lnk"], DropEffect.Copy | DropEffect.Move);

        Assert.Equal([@"C:\Users\me\Desktop\b.txt", @"D:\Folder", @"\\server\share\a.lnk"], offer.Paths);
        Assert.Equal(DropEffect.Copy | DropEffect.Move, offer.Effects);
    }

    [Fact]
    public void S5586_a_shell_name_a_relative_path_and_a_blank_are_no_storage_item()
    {
        var offered = ItemDragOffer.StoragePaths([ThisPc, "::{645FF040-5081-101B-9F08-00AA002F954E}", "a.txt", @"folder\a.txt", @"C:a.txt", @"\a.txt", "", "   ", "shell:Downloads", "https://example.com/a", @"C:\kept.txt"]);

        Assert.Equal([@"C:\kept.txt"], offered);
    }

    [Fact]
    public void S5586_a_path_given_twice_is_offered_once_whatever_its_case()
        => Assert.Equal([@"C:\Desk\a.txt", @"C:\Desk\b.txt"], ItemDragOffer.StoragePaths([@"C:\Desk\a.txt", @"C:\DESK\A.TXT", @"C:\Desk\b.txt", @"C:\Desk\a.txt"]));

    [Fact]
    public void S5586_a_drag_of_shell_items_alone_offers_no_file_and_keeps_its_effects()
    {
        var offer = new ItemDragOffer([ThisPc], DropEffect.Move);

        Assert.Empty(offer.Paths);
        Assert.Equal(DropEffect.Move, offer.Effects);
    }

    [Fact]
    public void S5586_two_drags_of_the_same_files_are_two_offers()
    {
        var first = new ItemDragOffer([@"C:\a.txt"], DropEffect.Copy);
        var second = new ItemDragOffer([@"C:\a.txt"], DropEffect.Copy);

        // The host tells a drag from the one that replaced it by the offer itself.
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(1.0, 48, 20, 48.0, 20.0, 96.0)]
    [InlineData(1.5, 48, 21, 32.0, 14.0, 144.0)]
    [InlineData(2.0, 0, 7, 0.0, 3.5, 192.0)]
    public void S5549_the_pointer_keeps_its_place_in_the_picture_at_any_scale(double scale, int hotX, int hotY, double anchorX, double anchorY, double dpi)
    {
        var picture = new DragPicture(new byte[100 * 50 * 4], 100, 50, hotX, hotY, scale);

        Assert.Equal((anchorX, anchorY), picture.Anchor);
        Assert.Equal(dpi, picture.Dpi);
        Assert.True(picture.Drawable);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void S5549_a_scale_that_is_no_number_is_one_pixel_a_dip(double scale)
    {
        var picture = new DragPicture(new byte[4], 1, 1, 30, 40, scale);

        Assert.Equal((30.0, 40.0), picture.Anchor);
        Assert.Equal(96.0, picture.Dpi);
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(5, 0, 0)]
    [InlineData(2, 2, 15)]
    [InlineData(2, 2, 17)]
    [InlineData(-1, 4, 16)]
    public void S5549_a_picture_without_a_size_or_with_the_wrong_pixels_is_not_drawn(int width, int height, int bytes)
        => Assert.False(new DragPicture(new byte[bytes], width, height, 0, 0, 1).Drawable);
}
