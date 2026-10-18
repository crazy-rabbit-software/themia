using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// The Folder widget's settings rows.
/// <para>
/// The path box is the one control here that is a trust boundary: whatever is typed goes on to a
/// filesystem call, so it is checked by <see cref="FolderPath"/> before it is written, and the
/// reason it was refused is shown under the box rather than swallowed. A refused value is not
/// corrected into a working one — a relative path becomes a request to type a full one, never a
/// silent jump to whatever the process's working directory happens to be.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    public static void Folder(List<FrameworkElement> rows, FolderConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Folder_Label_Label.Text()));

        var verdict = FluentLayout.Verdict();

        var box = new TextBox
        {
            // Read once to seed the control, then never mirrored: the config stays the only copy of
            // the value, and the box writes to it rather than shadowing it.
            Text = config.Path,
            Width = 260,
            PlaceholderText = ProfilePlaceholder(),
        };

        var browse = new Button { Content = Str.WidgetsB_Settings_Folder_Browse.Text() };

        void Commit()
        {
            var typed = box.Text;

            // An emptied box is a deliberate "use my profile folder", which is what the config
            // documents a blank path to mean. It is not an error.
            var result = FolderPath.Resolve(typed, string.Empty);

            if (typed.Trim().Length == 0 || result.IsValid)
            {
                config.Path = result.IsValid ? result.Path : string.Empty;
                box.Text = config.Path;
                save();
            }

            ShowFolderVerdict(verdict, config, typed, result);
        }

        // On commit, not per keystroke: a path is only ever valid once it has been fully typed, so
        // validating each character would spend the whole time telling the user they are wrong.
        box.LostFocus += (_, _) => Commit();

        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                Commit();
            }
        };

        browse.Click += (_, _) => _ = FolderBrowse.IntoAsync(box, Commit);

        ShowFolderVerdict(verdict, config, config.Path, FolderPath.Resolve(config.Path, string.Empty));

        rows.Add(FluentLayout.FieldRow(Glyphs.Folder, Str.Folder_Label_Label.Text(),
            Str.WidgetsB_Settings_Folder_Folder_Desc.Text(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2,
                Children = { box, browse },
            },
            verdict));

        rows.Add(FluentLayout.Header(Str.Folder_View_Label.Text()));

        // [S-4121] Only what the style draws; picking another style rebuilds the page ([S-6071]).
        if (config.Offers(FolderSetting.RowSize))
        {
            rows.Add(RowSizeRow(config.ItemSize, value => { config.ItemSize = value; save(); }));
        }

        if (config.Offers(FolderSetting.TileSize))
        {
            rows.Add(TileSizeRow(config.TileSize, value => { config.TileSize = value; save(); }));
        }

        if (config.Offers(FolderSetting.Columns))
        {
            rows.Add(FluentLayout.Expander(Glyphs.ColumnsLayout, Str.WidgetsB_Settings_Folder_Columns_Label.Text(),
                Str.WidgetsB_Settings_Folder_Columns_Desc.Text(),
                null,
                [.. FolderColumns.All.Select(column => FluentLayout.Row(Glyphs.ColumnsLayout, FolderColumns.Title(column), ColumnHint(column),
                    FluentLayout.Toggle(config.Columns.Contains(column), value =>
                    {
                        if (value != config.Columns.Contains(column))
                        {
                            config.Columns = FolderColumns.Toggle(config.Columns, column);
                            save();
                        }
                    })))]));
        }

        if (config.Offers(FolderSetting.Extensions))
        {
            rows.Add(ExtensionsRow(config.ShowExtensions, value => { config.ShowExtensions = value; save(); }));
        }

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Folder_Contents.Text()));

        // Details sorts from its column headers; a sort row would say the same thing twice.
        if (config.Offers(FolderSetting.Sort))
        {
            // [S-3795] The order folds under its key.
            rows.Add(FluentLayout.Expander(Glyphs.Sort, Str.WidgetsB_Settings_Folder_SortBy_Label.Text(), null,
                FluentLayout.Choice(
                    [(FolderSort.Name, Str.Folder_ColName.Text()), (FolderSort.Modified, Str.WidgetsB_Settings_Folder_DateModified.Text()), (FolderSort.Size, Str.Folder_ColSize.Text())],
                    config.Sort,
                    value => { config.Sort = value; save(); }),
                [
                    FluentLayout.Row(Glyphs.MoveDown, Str.WidgetsB_Settings_Folder_Reverse_Label.Text(), null,
                        FluentLayout.Toggle(config.Descending, value => { config.Descending = value; save(); })),
                ]));
        }

        rows.Add(FluentLayout.Row(Glyphs.Hidden, Str.WidgetsB_Folder_ShowHiddenItems.Text(), null,
            FluentLayout.Toggle(config.ShowHidden, value => { config.ShowHidden = value; save(); }),
            "hidden files", "system files"));

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Folder_Opening.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Open, Str.WidgetsB_Settings_Folder_OpenWith_Label.Text(), null,
            FluentLayout.Choice(
                [(ItemClickMode.Double, Str.WidgetsB_Settings_Folder_DoubleClick.Text()), (ItemClickMode.Single, Str.WidgetsB_Settings_Folder_SingleClick.Text())],
                config.ClickMode,
                value => { config.ClickMode = value; save(); }),
            "click mode"));

        // "Open in File Explorer" is on the widget's own menu; the page does not repeat it ([S-3795]).
        rows.Add(FluentLayout.Row(Glyphs.Back, Str.WidgetsB_Settings_Folder_OpenFolders_Label.Text(), null,
            FluentLayout.Choice(
                [(FolderNavigation.Explorer, Str.WidgetsB_Settings_Folder_InExplorer.Text()), (FolderNavigation.Navigate, Str.WidgetsB_Settings_Folder_InsideWidget.Text())],
                config.Navigation,
                value => { config.Navigation = value; save(); }),
            "navigation", "browse"));
    }

    private static FrameworkElement RowSizeRow(FolderItemSize current, Action<FolderItemSize> set)
        => FluentLayout.Row(Glyphs.TextSize, Str.WidgetsB_Settings_Folder_RowSize_Label.Text(), Str.WidgetsB_Settings_Folder_RowSize_Desc.Text(),
            FluentLayout.Choice(
                [(FolderItemSize.ExtraSmall, Str.WidgetsB_Settings_Folder_ExtraSmall.Text()), (FolderItemSize.Small, Str.Folder_Small.Text()), (FolderItemSize.Medium, Str.Folder_Medium.Text()), (FolderItemSize.Large, Str.Folder_Large.Text())],
                current,
                set),
            "density", "item size");

    private static FrameworkElement TileSizeRow(FolderTileSize current, Action<FolderTileSize> set)
        => FluentLayout.Row(Glyphs.GridLayout, Str.WidgetsB_Settings_Folder_TileSize_Label.Text(), Str.WidgetsB_Settings_Folder_TileSize_Desc.Text(),
            FluentLayout.Choice(
                [(FolderTileSize.Small, Str.Folder_Small.Text()), (FolderTileSize.Medium, Str.Folder_Medium.Text()), (FolderTileSize.Large, Str.Folder_Large.Text())],
                current,
                set),
            "tile size", "icon size");

    private static FrameworkElement ExtensionsRow(bool current, Action<bool> set)
        => FluentLayout.Row(Glyphs.File, Str.Folder_ShowExtMenu.Text(), null, FluentLayout.Toggle(current, set), "file extensions");

    private static string ColumnHint(FolderColumn column) => column switch
    {
        FolderColumn.Size => Str.WidgetsB_Settings_Folder_ColSize.Text(),
        FolderColumn.Modified => Str.WidgetsB_Settings_Folder_ColModified.Text(),
        FolderColumn.Type => Str.WidgetsB_Settings_Folder_ColType.Text(),
        FolderColumn.Cloud => Str.WidgetsB_Settings_Folder_ColCloud.Text(),
        FolderColumn.Git => Str.WidgetsB_Settings_Folder_ColGit.Text(),
        FolderColumn.ItemCount => Str.WidgetsB_Settings_Folder_ColItems.Text(),
        _ => string.Empty,
    };

    private static void ShowFolderVerdict(TextBlock verdict, FolderConfig config, string typed, FolderPathResult result)
    {
        if (typed.Trim().Length == 0)
        {
            FluentLayout.Confirm(verdict, Str.WidgetsB_Settings_Folder_ListingDefault.Format(("folder", ProfilePlaceholder())));
            return;
        }

        if (result.IsValid)
        {
            FluentLayout.Confirm(verdict, Str.WidgetsB_Settings_Folder_Listing.Format(("folder", FolderPath.DisplayName(config.Path))));
            return;
        }

        // Refused, and the box keeps what was typed so it can be corrected rather than retyped.
        FluentLayout.Refuse(verdict, FolderFormatting.PathMessage(result.Problem));
    }

    /// <summary>
    /// The folder an empty setting means. Shown rather than written into the config, so the setting
    /// keeps meaning "my user folder" if the profile ever moves.
    /// </summary>
    private static string ProfilePlaceholder() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
