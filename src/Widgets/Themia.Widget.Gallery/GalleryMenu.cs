using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Gallery;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-778] The Gallery's options, all on its properties menu ([S-3719]): under Style what the
/// layout draws (the slide time, the picture size); then Appearance ([S-3723]), the source and only
/// the lines it uses ([S-6074]: a folder's, a picture file's, none of either for the photo of the
/// day). It has no settings page.
/// <para>
/// The folder and the picture are trust boundaries: a typed path goes on to the filesystem, so it is
/// checked by <see cref="FolderPath"/> as it is typed and refused with the reason, never corrected
/// into a working one. An empty one means "nothing chosen", which the widget says out loud: a photo
/// frame showing pictures nobody pointed it at is a different kind of mistake from a file list
/// showing the wrong folder.
/// </para>
/// </summary>
internal static class GalleryMenu
{
    private const string Scope = "gallery-menu";

    public static void Lines(WidgetMenuLines menu, GalleryConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        if (config.Offers(GallerySetting.SlideSeconds))
        {
            menu.StyleLines.Add(menu.Number(Glyphs.Timer, GalleryStr.WidgetsB_Settings_Gallery_Slide_Label.Text(), config.EffectiveSlideshowSeconds,
                GalleryConfig.MinSlideSeconds, GalleryConfig.MaxSlideSeconds, 1, Seconds, value => config.SlideshowSeconds = value));
        }

        if (config.Offers(GallerySetting.Fit))
        {
            menu.StyleLines.Add(menu.Choice(Glyphs.Scale, GalleryStr.WidgetsB_Settings_Gallery_Fit_Label.Text(),
                [(GalleryFit.Fill, GalleryStr.WidgetsB_Settings_Gallery_Fill.Text()), (GalleryFit.Fit, GalleryStr.WidgetsB_Settings_Gallery_FitChoice.Text())],
                config.Fit, value => config.Fit = value));
        }

        menu.PlaceCard();
        var lines = menu.Lines;

        // [S-760] The source first: every other line depends on it.
        lines.Add(menu.Choice(GalleryModule.Glyph, GalleryStr.Gallery_SourceLabel.Text(),
            [(GallerySource.Folder, GalleryStr.WidgetsB_Settings_Gallery_SourceFolder.Text()), (GallerySource.File, GalleryStr.WidgetsB_Settings_Gallery_SourceFile.Text()), (GallerySource.Bing, GalleryStr.WidgetsB_Settings_Gallery_SourceBing.Text())],
            config.Source, value => config.Source = value));

        if (config.Offers(GallerySetting.Folder))
        {
            lines.Add(WidgetMenuLines.SetsUp(PathLine(menu, config, picture: false)));
        }

        if (config.Offers(GallerySetting.File))
        {
            lines.Add(WidgetMenuLines.SetsUp(PathLine(menu, config, picture: true)));
        }

        if (config.Offers(GallerySetting.Recursive))
        {
            lines.Add(menu.Toggle(Glyphs.Folder, GalleryStr.Gallery_RecursiveLabel.Text(), config.Recursive, on => config.Recursive = on));
        }

        if (config.Offers(GallerySetting.Count))
        {
            lines.Add(menu.Number(Glyphs.Count, GalleryStr.WidgetsB_Settings_Gallery_Count_Label.Text(), config.EffectiveMaxImages,
                1, GalleryConfig.MaxImageCount, 1, Whole, value => config.MaxImages = value));
        }

        if (config.Offers(GallerySetting.Shuffle))
        {
            lines.Add(menu.Toggle(Glyphs.Shuffle, GalleryStr.WidgetsB_Settings_Gallery_Shuffle_Label.Text(), config.Shuffle, on => config.Shuffle = on));
        }

        // [S-774] Turned by any angle, the whole picture still inside the widget.
        if (config.Offers(GallerySetting.Rotation))
        {
            lines.Add(menu.Number(Glyphs.Rotate, GalleryStr.WidgetsB_Settings_Gallery_Rotation_Label.Text(), config.EffectiveRotation,
                -180, 180, 1, value => Whole(value) + "°", value => config.Rotation = value));
        }

        // [S-775] The outline along the picture's own shape, its colour and its width.
        if (config.Offers(GallerySetting.Outline))
        {
            lines.Add(menu.Toggle(GalleryModule.Glyph, GalleryStr.WidgetsB_Settings_Gallery_Outline_Label.Text(), config.Outline, on => config.Outline = on));
        }

        if (config.Offers(GallerySetting.OutlineColor))
        {
            lines.Add(menu.Colour(GalleryStr.WidgetsB_Settings_Gallery_OutlineColour_Label.Text(), config.EffectiveOutlineColor,
                value => config.OutlineColor = value, new ColorChoiceOptions { DefaultLabel = null, Presets = ColorPresets.Surfaces }));
        }

        if (config.Offers(GallerySetting.OutlineWidth))
        {
            lines.Add(menu.Number(Glyphs.Scale, GalleryStr.WidgetsB_Settings_Gallery_OutlineWidth_Label.Text(), config.EffectiveOutlineWidth,
                StickerRules.MinOutline, StickerRules.MaxOutline, 1, value => Whole(value) + " px", value => config.OutlineWidth = value));
        }

        // The widget will not move, rename or delete anything, so this is the way out to the place that will.
        if (config.Offers(GallerySetting.OpenFolder))
        {
            var folder = FolderPath.Validate(config.Path);
            lines.Add(ItemMenuEntry.Separator);
            lines.Add(new ItemMenuEntry(Glyphs.Open, Str.Folder_OpenInExplorer.Text(), () => _ = SettingsHost.Launcher.Open(folder.Path), Disabled: !folder.IsValid));
        }
    }

    /// <summary>
    /// [S-778] The folder or the picture: Browse… (Windows' picker: the folder picker, [S-3730], or a
    /// picture picker offering only what the widget draws, [S-761]) or Type a path… ([S-3721]), its
    /// name at the line's end or "No folder chosen". One shape for both, so the two cannot drift on
    /// what a valid path is.
    /// </summary>
    private static ItemMenuEntry PathLine(WidgetMenuLines menu, GalleryConfig config, bool picture)
    {
        var current = picture ? config.FilePath : config.Path;

        void Store(string path)
        {
            if (string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (picture)
            {
                config.FilePath = path;
            }
            else
            {
                config.Path = path;
            }

            menu.Save();
        }

        var browse = picture
            ? new ItemMenuEntry(Glyphs.File, Str.WidgetsB_Settings_Folder_Browse.Text(), () => _ = BrowsePictureAsync(chosen =>
            {
                if (PathRefusal(chosen, picture: true) is null)
                {
                    Store(FolderPath.Validate(chosen).Path);
                }
            }))
            : menu.PickFolder(Glyphs.Folder, Str.WidgetsB_Settings_Folder_Browse.Text(), current, picked =>
            {
                // The picker's answer is still checked: it reaches the same filesystem calls.
                if (FolderPath.Validate(picked) is { IsValid: true } result)
                {
                    config.Path = result.Path;
                }
            }) with { Value = null };

        var title = picture ? GalleryStr.Gallery_ImageLabel.Text() : GalleryStr.Gallery_FolderLabel.Text();
        var none = picture ? GalleryStr.WidgetsB_Settings_Gallery_NoPicture.Text() : GalleryStr.WidgetsB_Settings_Gallery_NoFolder.Text();
        var typePath = new ItemMenuEntry(Glyphs.Edit, GalleryStr.Gallery_Menu_TypePath.Text(), () => menu.Ask(new WidgetTextAsk(
            title,
            none,
            Str.Common_Save.Text(),
            typed => PathRefusal(typed, picture),
            typed => Store(typed.Trim().Length == 0 ? string.Empty : FolderPath.Validate(typed).Path),
            Initial: current,
            AllowEmpty: true)));

        var shown = current.Trim().Length == 0 ? none : FolderPath.DisplayName(current);
        return WidgetMenuLines.Submenu(picture ? Glyphs.File : Glyphs.Folder, title, [browse, typePath]) with { Value = shown };
    }

    /// <summary>
    /// [S-778] Why a typed folder or picture cannot be used, or null when it can (empty: nothing
    /// chosen). [S-761] A picture also refuses a name that is not a picture: it would only come back
    /// from the widget as "not a picture this app can show".
    /// </summary>
    internal static string? PathRefusal(string typed, bool picture)
    {
        var text = typed ?? string.Empty;
        if (text.Trim().Length == 0)
        {
            return null;
        }

        var result = FolderPath.Validate(text);
        if (!result.IsValid)
        {
            return FolderFormatting.PathMessage(result.Problem);
        }

        return picture && !ImageFiles.IsImage(result.Path) ? GalleryStr.WidgetsB_Settings_Gallery_NotAPicture.Text() : null;
    }

    /// <summary>[S-761] Pick one picture, offering only the types the widget can draw.</summary>
    private static async Task BrowsePictureAsync(Action<string> chosen)
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

            // The picker needs an owner window, as the folder picker does.
            SettingsHost.OwnPicker(picker);

            var file = await picker.PickSingleFileAsync();

            if (file is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            chosen(file.Path);
        }
        catch (Exception ex)
        {
            // The dialog is a convenience over a typed path that still works. Losing it must not
            // take the window it was opened from down with it.
            Log.Error(Scope, "the picture picker could not be shown", ex);
        }
    }

    private static string Whole(int value) => LocaleFormat.Number(value, 0);

    private static string Seconds(int value) => Whole(value) + " " + Str.Shell_Refresh_SecondsUnit.Text();
}
