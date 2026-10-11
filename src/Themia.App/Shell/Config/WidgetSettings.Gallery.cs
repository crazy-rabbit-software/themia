using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell.Config;

/// <summary>
/// The Gallery widget's settings rows.
/// <para>
/// The folder and picture boxes are the controls here that are trust boundaries: whatever is typed
/// goes on to the filesystem, so it is checked by <see cref="FolderPath"/> before it is written and
/// the reason it was refused is shown under the box rather than swallowed. A refused value is not
/// corrected into a working one — the same rule the folder widget's box follows, and for the same
/// reason.
/// </para>
/// <para>
/// An empty box is not an error and not a fallback either. It means "nothing chosen", which the
/// widget says out loud: a photo frame that starts showing pictures nobody pointed it at, on a
/// surface that sits on the desktop in front of whoever walks past, is a different kind of mistake
/// from a file list showing the wrong folder.
/// </para>
/// <para>
/// Rows that do not apply to the chosen source or layout are hidden rather than disabled
/// ([S-36]): a "Pictures" slider on a photo-of-the-day widget is a setting that does nothing.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    private const string GalleryScope = "gallery-settings";

    public static void Gallery(List<FrameworkElement> rows, GalleryConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        var folderOnly = new List<FrameworkElement>();
        var slideshowOnly = new List<FrameworkElement>();
        FrameworkElement? fileRow = null;
        FrameworkElement? fitRow = null;

        void Update()
        {
            var folder = config.Source == GallerySource.Folder;

            foreach (var row in folderOnly)
            {
                row.Visibility = folder ? Visibility.Visible : Visibility.Collapsed;
            }

            foreach (var row in slideshowOnly)
            {
                row.Visibility = folder && config.Layout == GalleryLayout.Slideshow ? Visibility.Visible : Visibility.Collapsed;
            }

            fileRow!.Visibility = config.Source == GallerySource.File ? Visibility.Visible : Visibility.Collapsed;

            // [S-15] A mosaic always crops to fill, so the choice would do nothing there.
            fitRow!.Visibility = folder && config.Layout == GalleryLayout.Mosaic ? Visibility.Collapsed : Visibility.Visible;
        }

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Gallery_Gallery.Text()));

        // [S-760] The source first: every other row depends on it.
        rows.Add(FluentLayout.Row(Glyphs.Gallery, Str.Gallery_SourceLabel.Text(), Str.WidgetsB_Settings_Gallery_Source_Desc.Text(),
            FluentLayout.Choice(
                [(GallerySource.Folder, Str.WidgetsB_Settings_Gallery_SourceFolder.Text()), (GallerySource.File, Str.WidgetsB_Settings_Gallery_SourceFile.Text()), (GallerySource.Bing, Str.WidgetsB_Settings_Gallery_SourceBing.Text())],
                config.Source,
                value => { config.Source = value; save(); Update(); })));

        var folderRow = GalleryPathRow(config, save, pickFile: false);
        rows.Add(folderRow);
        folderOnly.Add(folderRow);

        fileRow = GalleryPathRow(config, save, pickFile: true);
        rows.Add(fileRow);

        var recursive = FluentLayout.Row(Glyphs.Folder, Str.Gallery_RecursiveLabel.Text(),
            Str.WidgetsB_Settings_Gallery_Recursive_Desc.Text(),
            FluentLayout.Toggle(config.Recursive, value => { config.Recursive = value; save(); }));
        rows.Add(recursive);
        folderOnly.Add(recursive);

        var layout = FluentLayout.Row(Glyphs.Grid, Str.Gallery_LayoutLabel.Text(), Str.WidgetsB_Settings_Gallery_Layout_Desc.Text(),
            FluentLayout.Choice(
                [(GalleryLayout.Slideshow, Str.Gallery_ModeSlideshow.Text()), (GalleryLayout.Grid, Str.Gallery_ModeGrid.Text()), (GalleryLayout.Scatter, Str.Gallery_ModeScatter.Text()), (GalleryLayout.Mosaic, Str.Gallery_ModeMosaic.Text())],
                config.Layout,
                value => { config.Layout = value; save(); Update(); }));
        rows.Add(layout);
        folderOnly.Add(layout);

        var slide = FluentLayout.Row(Glyphs.Timer, Str.WidgetsB_Settings_Gallery_Slide_Label.Text(), Str.WidgetsB_Settings_Gallery_Slide_Desc.Text(),
            FluentLayout.Slider(config.EffectiveSlideshowSeconds, GalleryConfig.MinSlideSeconds, 120, 1, "s",
                value => { config.SlideshowSeconds = (int)Math.Round(value); save(); }));
        rows.Add(slide);
        slideshowOnly.Add(slide);

        fitRow = FluentLayout.Row(Glyphs.Scale, Str.WidgetsB_Settings_Gallery_Fit_Label.Text(),
            Str.WidgetsB_Settings_Gallery_Fit_Desc.Text(),
            FluentLayout.Choice(
                [(GalleryFit.Fill, Str.WidgetsB_Settings_Gallery_Fill.Text()), (GalleryFit.Fit, Str.WidgetsB_Settings_Gallery_FitChoice.Text())],
                config.Fit,
                value => { config.Fit = value; save(); }));
        rows.Add(fitRow);

        var count = FluentLayout.Row(Glyphs.Count, Str.WidgetsB_Settings_Gallery_Count_Label.Text(), Str.WidgetsB_Settings_Gallery_Count_Desc.Text(),
            FluentLayout.Slider(
                config.EffectiveMaxImages,
                1,
                GalleryConfig.MaxImageCount,
                1,
                string.Empty,
                value => { config.MaxImages = (int)Math.Round(value); save(); }));
        rows.Add(count);
        folderOnly.Add(count);

        var shuffle = FluentLayout.Row(Glyphs.Shuffle, Str.WidgetsB_Settings_Gallery_Shuffle_Label.Text(),
            Str.WidgetsB_Settings_Gallery_Shuffle_Desc.Text(),
            FluentLayout.Toggle(config.Shuffle, value => { config.Shuffle = value; save(); }));
        rows.Add(shuffle);
        folderOnly.Add(shuffle);

        var open = new Button { Content = Str.Folder_OpenInExplorer.Text() };

        open.Click += (_, _) =>
        {
            var result = FolderPath.Validate(config.Path);

            if (result.IsValid)
            {
                Services.Files.ShellFileOps.Open(result.Path);
            }
        };

        // The widget will not move, rename or delete anything, so this is the way out to the place
        // that will.
        var files = FluentLayout.Row(Glyphs.Open, Str.WidgetsB_Settings_Gallery_Files_Label.Text(), Str.WidgetsB_Settings_Gallery_Files_Desc.Text(), open);
        rows.Add(files);
        folderOnly.Add(files);

        Update();
    }

    /// <summary>
    /// The folder box or the picture box: typed or browsed, validated on commit, with the verdict
    /// under it. One shape for both, so the two cannot drift on what a valid path is.
    /// </summary>
    private static Border GalleryPathRow(GalleryConfig config, Action save, bool pickFile)
    {
        var verdict = FluentLayout.Verdict();
        string Current() => pickFile ? config.FilePath : config.Path;

        var box = new TextBox
        {
            // Read once to seed the control, then never mirrored: the config stays the only copy of
            // the value, and the box writes to it rather than shadowing it.
            Text = Current(),
            Width = 260,
            PlaceholderText = pickFile ? Str.WidgetsB_Settings_Gallery_NoPicture.Text() : Str.WidgetsB_Settings_Gallery_NoFolder.Text(),
        };

        var browse = new Button { Content = Str.WidgetsB_Settings_Folder_Browse.Text() };

        void Commit()
        {
            var typed = box.Text;
            var blank = typed.Trim().Length == 0;
            var result = FolderPath.Validate(typed);

            // [S-761] A picture box also refuses a name that is not a picture: it would only come
            // back from the widget as "not a picture this app can show".
            var refusedType = pickFile && !blank && result.IsValid && !ImageFiles.IsImage(result.Path);

            if ((blank || result.IsValid) && !refusedType)
            {
                var value = blank ? string.Empty : result.Path;

                if (pickFile)
                {
                    config.FilePath = value;
                }
                else
                {
                    config.Path = value;
                }

                box.Text = value;
                save();
            }

            ShowGalleryVerdict(verdict, Current(), blank, result, pickFile, refusedType);
        }

        // On commit, not per keystroke: a path is only ever valid once it has been fully typed, so
        // validating each character would spend the whole time saying it is wrong.
        box.LostFocus += (_, _) => Commit();

        box.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                args.Handled = true;
                Commit();
            }
        };

        // The same picker and the same commit the folder widget uses, so there is one way into this
        // setting rather than two that can disagree about what a valid path is.
        browse.Click += (_, _) => _ = pickFile ? BrowsePictureAsync(box, Commit) : BrowseAsync(box, Commit);

        var current = Current();
        ShowGalleryVerdict(verdict, current, current.Trim().Length == 0, FolderPath.Validate(current), pickFile, refusedType: false);

        return FluentLayout.FieldRow(pickFile ? Glyphs.File : Glyphs.Folder, pickFile ? Str.Gallery_ImageLabel.Text() : Str.Gallery_FolderLabel.Text(),
            pickFile ? Str.WidgetsB_Settings_Gallery_Picture_Desc.Text() : Str.WidgetsB_Settings_Gallery_Folder_Desc.Text(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2,
                Children = { box, browse },
            },
            verdict);
    }

    /// <summary>[S-761] Pick one picture, offering only the types the widget can draw.</summary>
    private static async Task BrowsePictureAsync(TextBox box, Action commit)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary,
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
            };

            foreach (var extension in ImageFiles.Extensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            // The picker needs an owner window, and the click that opened it came from the settings
            // window, so the foreground window is that window at this instant.
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Win32.GetForegroundWindow());

            var file = await picker.PickSingleFileAsync();

            if (file is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            box.Text = file.Path;
            commit();
        }
        catch (Exception ex)
        {
            // The dialog is a convenience over a box that still works. Losing it must not take the
            // settings window down with it.
            Log.Error(GalleryScope, "the picture picker could not be shown", ex);
        }
    }

    /// <summary>
    /// Say what is wrong, or confirm what will actually be shown. Both are worth saying: a box that
    /// looks accepted but silently reads somewhere else is the failure the whole check exists to
    /// prevent.
    /// </summary>
    private static void ShowGalleryVerdict(TextBlock verdict, string current, bool blank, FolderPathResult result, bool pickFile, bool refusedType)
    {
        if (refusedType)
        {
            FluentLayout.Refuse(verdict, Str.WidgetsB_Settings_Gallery_NotAPicture.Text());
            return;
        }

        if (blank)
        {
            FluentLayout.Confirm(verdict, pickFile ? Str.WidgetsB_Settings_Gallery_NoPictureYet.Text() : Str.WidgetsB_Settings_Gallery_NoFolderYet.Text());
            return;
        }

        if (result.IsValid)
        {
            FluentLayout.Confirm(verdict, Str.WidgetsB_Settings_Gallery_Showing.Format(("name", FolderPath.DisplayName(current))));
            return;
        }

        // Refused, and the box keeps what was typed so it can be corrected rather than retyped.
        FluentLayout.Refuse(verdict, FolderFormatting.PathMessage(result.Problem));
    }
}
