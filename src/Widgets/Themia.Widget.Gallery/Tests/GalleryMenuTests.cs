using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Gallery.Tests;

/// <summary>[S-778] The Gallery's options on its properties menu: what the layout draws under Style, the source's own lines, the paths checked as typed.</summary>
public sealed class GalleryMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(GalleryConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        GalleryMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S778_the_slideshow_offers_the_slide_time_and_the_picture_size_under_style_a_mosaic_neither()
    {
        Assert.Equal(
            [GalleryStr.WidgetsB_Settings_Gallery_Slide_Label.Text(), GalleryStr.WidgetsB_Settings_Gallery_Fit_Label.Text()],
            Build(new GalleryConfig { Style = GalleryStyle.Slideshow }).StyleLines.Select(l => l.Text));
        Assert.Empty(Build(new GalleryConfig { Style = GalleryStyle.Mosaic }).StyleLines);
    }

    [Fact]
    public void S778_a_folder_source_shows_the_folders_lines_after_appearance()
    {
        var lines = Build(new GalleryConfig { Source = GallerySource.Folder });

        Assert.Equal(
            [
                GalleryStr.Gallery_SourceLabel.Text(),
                GalleryStr.Gallery_FolderLabel.Text(),
                GalleryStr.Gallery_RecursiveLabel.Text(),
                GalleryStr.WidgetsB_Settings_Gallery_Count_Label.Text(),
                GalleryStr.WidgetsB_Settings_Gallery_Shuffle_Label.Text(),
                Str.Folder_OpenInExplorer.Text(),
            ],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S778_the_source_and_its_path_then_which_pictures_then_explorer_each_behind_a_divider()
        => Assert.Equal(
            [
                "-",
                GalleryStr.Gallery_SourceLabel.Text(),
                GalleryStr.Gallery_FolderLabel.Text(),
                "-",
                GalleryStr.Gallery_RecursiveLabel.Text(),
                GalleryStr.WidgetsB_Settings_Gallery_Count_Label.Text(),
                GalleryStr.WidgetsB_Settings_Gallery_Shuffle_Label.Text(),
                "-",
                Str.Folder_OpenInExplorer.Text(),
            ],
            Build(new GalleryConfig { Source = GallerySource.Folder }).Lines.Select(l => l.IsSeparator ? "-" : l.Text));

    private static List<ItemMenuEntry> Own(WidgetMenuLines lines) => [.. lines.Lines.Where(l => !l.IsSeparator)];

    [Fact]
    public void S778_a_picture_shows_its_turn_and_outline_and_the_outline_its_colour_and_width()
    {
        var plain = Build(new GalleryConfig { Source = GallerySource.File, Outline = false }).Lines.Select(l => l.Text).ToList();
        Assert.Contains(GalleryStr.Gallery_ImageLabel.Text(), plain);
        Assert.Contains(GalleryStr.WidgetsB_Settings_Gallery_Rotation_Label.Text(), plain);
        Assert.DoesNotContain(GalleryStr.WidgetsB_Settings_Gallery_OutlineWidth_Label.Text(), plain);

        var outlined = Build(new GalleryConfig { Source = GallerySource.File, Outline = true }).Lines;
        Assert.NotNull(outlined.Single(l => l.Text == GalleryStr.WidgetsB_Settings_Gallery_OutlineColour_Label.Text()).Colour);
        Assert.Contains(outlined, l => l.Text == GalleryStr.WidgetsB_Settings_Gallery_OutlineWidth_Label.Text());
    }

    [Fact]
    public void S778_the_photo_of_the_day_has_no_path_and_no_count()
        => Assert.Equal([GalleryStr.Gallery_SourceLabel.Text()], Build(new GalleryConfig { Source = GallerySource.Bing }).Lines.Where(l => !l.IsSeparator).Select(l => l.Text));

    [Fact]
    public void S778_a_typed_path_is_refused_with_the_reason_and_a_picture_must_be_one()
    {
        Assert.Equal(FolderFormatting.PathMessage(FolderPathProblem.NotRooted), GalleryMenu.PathRefusal(@"Pictures\Trips", picture: false));
        Assert.Null(GalleryMenu.PathRefusal(@"D:\Photos", picture: false));
        Assert.Null(GalleryMenu.PathRefusal(" ", picture: true));
        Assert.Equal(GalleryStr.WidgetsB_Settings_Gallery_NotAPicture.Text(), GalleryMenu.PathRefusal(@"D:\Photos\notes.txt", picture: true));
        Assert.Null(GalleryMenu.PathRefusal(@"D:\Photos\cat.png", picture: true));
    }

    [Fact]
    public void S778_a_typed_folder_is_stored_once_and_shown_by_its_name()
    {
        var config = new GalleryConfig { Source = GallerySource.Folder };
        var folder = Own(Build(config))[1];
        Assert.Equal(GalleryStr.WidgetsB_Settings_Gallery_NoFolder.Text(), folder.Value);

        folder.Children![1].Action!();
        _asked!.Done(@" D:\Photos ");

        Assert.Equal(@"D:\Photos", config.Path);
        Assert.Equal(1, _saves);
        Assert.Equal("Photos", Own(Build(config))[1].Value);
    }

    [Fact]
    public void S778_the_slide_time_is_asked_for_and_kept_in_its_range()
    {
        var config = new GalleryConfig { Style = GalleryStyle.Slideshow, SlideshowSeconds = 10 };
        Build(config).StyleLines[0].Action!();

        var ask = _asked!;
        Assert.NotNull(ask.Refuse("0"));
        Assert.Null(ask.Refuse("45"));
        ask.Done("45");

        Assert.Equal(45, config.SlideshowSeconds);
        Assert.Equal(1, _saves);
    }
}
