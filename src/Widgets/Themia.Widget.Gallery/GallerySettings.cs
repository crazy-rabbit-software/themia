using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.System;
using Themia.Widget.Gallery;

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
internal static class GallerySettings
{
    private const string GalleryScope = "gallery-settings";

    public static void Rows(List<FrameworkElement> rows, GalleryConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        // [S-6074] Only the rows the source, the layout and the outline use, by the config's one
        // rule; a source, a layout or the outline switch changed builds the page again ([S-6071]).
        rows.Add(FluentLayout.Header(GalleryStr.WidgetsB_Settings_Gallery_Gallery.Text()));

        // [S-760] The source first: every other row depends on it.
        rows.Add(FluentLayout.Row(GalleryModule.Glyph, GalleryStr.Gallery_SourceLabel.Text(), GalleryStr.WidgetsB_Settings_Gallery_Source_Desc.Text(),
            FluentLayout.Choice(
                [(GallerySource.Folder, GalleryStr.WidgetsB_Settings_Gallery_SourceFolder.Text()), (GallerySource.File, GalleryStr.WidgetsB_Settings_Gallery_SourceFile.Text()), (GallerySource.Bing, GalleryStr.WidgetsB_Settings_Gallery_SourceBing.Text())],
                config.Source,
                value => { config.Source = value; save(); })));

        if (config.Offers(GallerySetting.Folder))
        {
            rows.Add(GalleryPathRow(config, save, pickFile: false));
        }

        if (config.Offers(GallerySetting.File))
        {
            rows.Add(GalleryPathRow(config, save, pickFile: true));
        }

        if (config.Offers(GallerySetting.Recursive))
        {
            rows.Add(FluentLayout.Row(Glyphs.Folder, GalleryStr.Gallery_RecursiveLabel.Text(),
                GalleryStr.WidgetsB_Settings_Gallery_Recursive_Desc.Text(),
                FluentLayout.Toggle(config.Recursive, value => { config.Recursive = value; save(); })));
        }

        // [S-6402] The layout is the gallery's style, picked on the properties panel ([S-6536]).
        if (config.Offers(GallerySetting.SlideSeconds))
        {
            rows.Add(FluentLayout.Row(Glyphs.Timer, GalleryStr.WidgetsB_Settings_Gallery_Slide_Label.Text(), GalleryStr.WidgetsB_Settings_Gallery_Slide_Desc.Text(),
                FluentLayout.Slider(config.EffectiveSlideshowSeconds, GalleryConfig.MinSlideSeconds, 120, 1, "s",
                    value => { config.SlideshowSeconds = (int)Math.Round(value); save(); })));
        }

        if (config.Offers(GallerySetting.Fit))
        {
            rows.Add(FluentLayout.Row(Glyphs.Scale, GalleryStr.WidgetsB_Settings_Gallery_Fit_Label.Text(),
                GalleryStr.WidgetsB_Settings_Gallery_Fit_Desc.Text(),
                FluentLayout.Choice(
                    [(GalleryFit.Fill, GalleryStr.WidgetsB_Settings_Gallery_Fill.Text()), (GalleryFit.Fit, GalleryStr.WidgetsB_Settings_Gallery_FitChoice.Text())],
                    config.Fit,
                    value => { config.Fit = value; save(); })));
        }

        // [S-774] Turned by any angle, the whole picture still inside the widget.
        if (config.Offers(GallerySetting.Rotation))
        {
            rows.Add(FluentLayout.Row(Glyphs.Rotate, GalleryStr.WidgetsB_Settings_Gallery_Rotation_Label.Text(), GalleryStr.WidgetsB_Settings_Gallery_Rotation_Desc.Text(),
                FluentLayout.Slider(config.EffectiveRotation, -180, 180, 1, "°",
                    value => { config.Rotation = (int)Math.Round(value); save(); })));
        }

        // [S-775] The outline along the picture's own shape, its colour and its width.
        if (config.Offers(GallerySetting.Outline))
        {
            rows.Add(FluentLayout.Row(GalleryModule.Glyph, GalleryStr.WidgetsB_Settings_Gallery_Outline_Label.Text(), GalleryStr.WidgetsB_Settings_Gallery_Outline_Desc.Text(),
                FluentLayout.Toggle(config.Outline, value => { config.Outline = value; save(); })));
        }

        if (config.Offers(GallerySetting.OutlineColor))
        {
            rows.Add(FluentLayout.ColorPickerRow(Glyphs.Palette, GalleryStr.WidgetsB_Settings_Gallery_OutlineColour_Label.Text(), null,
                config.EffectiveOutlineColor, value => { config.OutlineColor = value; save(); }));
        }

        if (config.Offers(GallerySetting.OutlineWidth))
        {
            rows.Add(FluentLayout.Row(Glyphs.Scale, GalleryStr.WidgetsB_Settings_Gallery_OutlineWidth_Label.Text(), null,
                FluentLayout.Slider(config.EffectiveOutlineWidth, StickerRules.MinOutline, StickerRules.MaxOutline, 1, "px",
                    value => { config.OutlineWidth = (int)Math.Round(value); save(); })));
        }

        if (config.Offers(GallerySetting.Count))
        {
            rows.Add(FluentLayout.Row(Glyphs.Count, GalleryStr.WidgetsB_Settings_Gallery_Count_Label.Text(), GalleryStr.WidgetsB_Settings_Gallery_Count_Desc.Text(),
                FluentLayout.Slider(
                    config.EffectiveMaxImages,
                    1,
                    GalleryConfig.MaxImageCount,
                    1,
                    string.Empty,
                    value => { config.MaxImages = (int)Math.Round(value); save(); })));
        }

        if (config.Offers(GallerySetting.Shuffle))
        {
            rows.Add(FluentLayout.Row(Glyphs.Shuffle, GalleryStr.WidgetsB_Settings_Gallery_Shuffle_Label.Text(),
                GalleryStr.WidgetsB_Settings_Gallery_Shuffle_Desc.Text(),
                FluentLayout.Toggle(config.Shuffle, value => { config.Shuffle = value; save(); })));
        }

        if (!config.Offers(GallerySetting.OpenFolder))
        {
            return;
        }

        var open = new Button { Content = Str.Folder_OpenInExplorer.Text() };

        open.Click += (_, _) =>
        {
            var result = FolderPath.Validate(config.Path);

            if (result.IsValid)
            {
                _ = SettingsHost.Launcher.Open(result.Path);
            }
        };

        // The widget will not move, rename or delete anything, so this is the way out to the place
        // that will.
        rows.Add(FluentLayout.Row(Glyphs.Open, GalleryStr.WidgetsB_Settings_Gallery_Files_Label.Text(), GalleryStr.WidgetsB_Settings_Gallery_Files_Desc.Text(), open));
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
            PlaceholderText = pickFile ? GalleryStr.WidgetsB_Settings_Gallery_NoPicture.Text() : GalleryStr.WidgetsB_Settings_Gallery_NoFolder.Text(),
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
        browse.Click += (_, _) => _ = pickFile ? BrowsePictureAsync(box, Commit) : FolderBrowse.IntoAsync(box, Commit);

        var current = Current();
        ShowGalleryVerdict(verdict, current, current.Trim().Length == 0, FolderPath.Validate(current), pickFile, refusedType: false);

        return FluentLayout.FieldRow(pickFile ? Glyphs.File : Glyphs.Folder, pickFile ? GalleryStr.Gallery_ImageLabel.Text() : GalleryStr.Gallery_FolderLabel.Text(),
            pickFile ? GalleryStr.WidgetsB_Settings_Gallery_Picture_Desc.Text() : GalleryStr.WidgetsB_Settings_Gallery_Folder_Desc.Text(),
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

            // The picker needs an owner window: the settings window the click came from.
            SettingsHost.OwnPicker(picker);

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
            FluentLayout.Refuse(verdict, GalleryStr.WidgetsB_Settings_Gallery_NotAPicture.Text());
            return;
        }

        if (blank)
        {
            FluentLayout.Confirm(verdict, pickFile ? GalleryStr.WidgetsB_Settings_Gallery_NoPictureYet.Text() : GalleryStr.WidgetsB_Settings_Gallery_NoFolderYet.Text());
            return;
        }

        if (result.IsValid)
        {
            FluentLayout.Confirm(verdict, GalleryStr.WidgetsB_Settings_Gallery_Showing.Format(("name", FolderPath.DisplayName(current))));
            return;
        }

        // Refused, and the box keeps what was typed so it can be corrected rather than retyped.
        FluentLayout.Refuse(verdict, FolderFormatting.PathMessage(result.Problem));
    }
}
