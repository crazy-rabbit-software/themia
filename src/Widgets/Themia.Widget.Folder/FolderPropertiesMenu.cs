using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Folder;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1337] The Folder widget's options, all on its properties menu ([S-3719]): under Style what the
/// look draws ([S-4121]: row or tile size, the details columns, the extensions, the order); then
/// Appearance ([S-3723]), the folder and hidden items, and how items and folders open. It has no
/// settings page. (Its item and background menus are <see cref="FolderMenu"/>.)
/// </summary>
internal static class FolderPropertiesMenu
{
    public static void Lines(WidgetMenuLines menu, FolderConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        StyleLines(menu, config);

        // [S-1337] After the look, each group behind a divider: the folder and what of it is
        // listed; then how its items and its folders open.
        menu.PlaceCard();
        var lines = menu.Lines;
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(FolderLine(menu, config));
        lines.Add(menu.Toggle(Glyphs.Hidden, FolderStr.WidgetsB_Folder_ShowHiddenItems.Text(), config.ShowHidden, on => config.ShowHidden = on));
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.Choice(Glyphs.Open, FolderStr.WidgetsB_Settings_Folder_OpenWith_Label.Text(),
            [(ItemClickMode.Double, FolderStr.WidgetsB_Settings_Folder_DoubleClick.Text()), (ItemClickMode.Single, FolderStr.WidgetsB_Settings_Folder_SingleClick.Text())],
            config.ClickMode, value => config.ClickMode = value));

        // "Open in File Explorer" is on the widget's own menu; this does not repeat it ([S-3795]).
        lines.Add(menu.Choice(Glyphs.Back, FolderStr.WidgetsB_Settings_Folder_OpenFolders_Label.Text(),
            [(FolderNavigation.Explorer, FolderStr.WidgetsB_Settings_Folder_InExplorer.Text()), (FolderNavigation.Navigate, FolderStr.WidgetsB_Settings_Folder_InsideWidget.Text())],
            config.Navigation, value => config.Navigation = value));
    }

    /// <summary>[S-4121] Only what the style draws ([S-6074]); another style builds the menu again without the rest.</summary>
    private static void StyleLines(WidgetMenuLines menu, FolderConfig config)
    {
        var look = menu.StyleLines;
        if (config.Offers(FolderSetting.RowSize))
        {
            look.Add(menu.Choice(Glyphs.TextSize, FolderStr.WidgetsB_Settings_Folder_RowSize_Label.Text(),
                [(FolderItemSize.ExtraSmall, FolderStr.WidgetsB_Settings_Folder_ExtraSmall.Text()), (FolderItemSize.Small, Str.Folder_Small.Text()), (FolderItemSize.Medium, Str.Folder_Medium.Text()), (FolderItemSize.Large, Str.Folder_Large.Text())],
                config.ItemSize, value => config.ItemSize = value));
        }

        if (config.Offers(FolderSetting.TileSize))
        {
            look.Add(menu.Choice(Glyphs.GridLayout, FolderStr.WidgetsB_Settings_Folder_TileSize_Label.Text(),
                [(FolderTileSize.Small, Str.Folder_Small.Text()), (FolderTileSize.Medium, Str.Folder_Medium.Text()), (FolderTileSize.Large, Str.Folder_Large.Text())],
                config.TileSize, value => config.TileSize = value));
        }

        if (config.Offers(FolderSetting.Columns))
        {
            look.Add(WidgetMenuLines.Submenu(Glyphs.ColumnsLayout, FolderStr.WidgetsB_Settings_Folder_Columns_Label.Text(),
                [.. FolderColumns.All.Select(column => menu.Toggle(null, FolderColumns.Title(column), config.Columns.Contains(column),
                    _ => config.Columns = FolderColumns.Toggle(config.Columns, column)))]));
        }

        if (config.Offers(FolderSetting.Extensions))
        {
            look.Add(menu.Toggle(Glyphs.File, Str.Folder_ShowExtMenu.Text(), config.ShowExtensions, on => config.ShowExtensions = on));
        }

        // Details sorts from its column headers; a sort line would say the same thing twice.
        if (config.Offers(FolderSetting.Sort))
        {
            // [S-3795] The order folds under its key.
            var sort = menu.Choice(Glyphs.Sort, FolderStr.WidgetsB_Settings_Folder_SortBy_Label.Text(),
                [(FolderSort.Name, Str.Folder_ColName.Text()), (FolderSort.Modified, FolderStr.WidgetsB_Settings_Folder_DateModified.Text()), (FolderSort.Size, FolderStr.Folder_ColSize.Text())],
                config.Sort, value => config.Sort = value);
            look.Add(sort with
            {
                Children = [.. sort.Children!, ItemMenuEntry.Separator, menu.Toggle(null, FolderStr.WidgetsB_Settings_Folder_Reverse_Label.Text(), config.Descending, on => config.Descending = on)],
            });
        }
    }

    /// <summary>
    /// [S-1337] The folder: Windows' folder picker ([S-3730]), or a path typed beside the menu
    /// ([S-3721]) for one the picker cannot reach. A typed path is the trust boundary: whatever is
    /// typed goes on to a filesystem call, so it is checked by <see cref="FolderPath"/> as it is typed
    /// and refused with the reason, never corrected into a working one (a relative path is a request
    /// to type a full one). Empty is a deliberate "my user folder", which an empty setting means.
    /// </summary>
    private static ItemMenuEntry FolderLine(WidgetMenuLines menu, FolderConfig config)
    {
        var current = config.Path;

        void Store(string path)
        {
            config.Path = path;
            menu.Save();
        }

        var lines = new List<ItemMenuEntry>
        {
            menu.PickFolder(Glyphs.Folder, Str.WidgetsB_Settings_Folder_Browse.Text(), current, picked =>
            {
                // The picker's answer is still checked: it reaches the same filesystem calls.
                if (FolderPath.Validate(picked) is { IsValid: true } result)
                {
                    config.Path = result.Path;
                }
            }) with { Value = null },
            new(Glyphs.Edit, FolderStr.Folder_Menu_TypePath.Text(), () => menu.Ask(new WidgetTextAsk(
                FolderStr.Folder_Label_Label.Text(),
                ProfileFolder(),
                Str.Common_Save.Text(),
                PathRefusal,
                typed =>
                {
                    var path = typed.Trim().Length == 0 ? string.Empty : FolderPath.Validate(typed).Path;
                    if (!string.Equals(path, current, StringComparison.OrdinalIgnoreCase))
                    {
                        Store(path);
                    }
                },
                Initial: current,
                AllowEmpty: true))),
        };

        var shown = WidgetMenuLines.FolderName(current) ?? WidgetMenuLines.FolderName(ProfileFolder());
        return WidgetMenuLines.Submenu(Glyphs.Folder, FolderStr.Folder_Label_Label.Text(), lines) with { Value = shown };
    }

    /// <summary>[S-1337] Why a typed path cannot be listed, or null when it can (empty: the user folder).</summary>
    internal static string? PathRefusal(string typed)
    {
        if ((typed ?? string.Empty).Trim().Length == 0)
        {
            return null;
        }

        var result = FolderPath.Validate(typed!);
        return result.IsValid ? null : FolderFormatting.PathMessage(result.Problem);
    }

    /// <summary>
    /// The folder an empty setting means. Shown rather than written into the config, so the setting
    /// keeps meaning "my user folder" if the profile ever moves.
    /// </summary>
    private static string ProfileFolder() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
