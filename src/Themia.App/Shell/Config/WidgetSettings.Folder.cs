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
    private const string FolderScope = "folder-settings";

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

        browse.Click += (_, _) => _ = BrowseAsync(box, Commit);

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

        rows.Add(FluentLayout.Row(Glyphs.View, Str.WidgetsB_Settings_Folder_Layout_Label.Text(), null,
            FluentLayout.Choice(
                [(FolderViewMode.List, Str.CtxMenu_List.Text()), (FolderViewMode.Details, Str.CtxMenu_Details.Text()), (FolderViewMode.Grid, Str.CtxMenu_Grid.Text()), (FolderViewMode.Icons, Str.WidgetsB_Settings_Folder_Icons.Text())],
                config.View,
                value => { config.View = value; save(); }),
            "view mode", "grid", "details", "icons"));

        rows.Add(FluentLayout.Row(Glyphs.TextSize, Str.WidgetsB_Settings_Folder_RowSize_Label.Text(), Str.WidgetsB_Settings_Folder_RowSize_Desc.Text(),
            FluentLayout.Choice(
                [(FolderItemSize.ExtraSmall, Str.WidgetsB_Settings_Folder_ExtraSmall.Text()), (FolderItemSize.Small, Str.Folder_Small.Text()), (FolderItemSize.Medium, Str.Folder_Medium.Text()), (FolderItemSize.Large, Str.Folder_Large.Text())],
                config.ItemSize,
                value => { config.ItemSize = value; save(); }),
            "density", "item size"));

        rows.Add(FluentLayout.Expander(Glyphs.GridLayout, Str.WidgetsB_Settings_Folder_TileSize_Label.Text(), Str.WidgetsB_Settings_Folder_TileSize_Desc.Text(),
            FluentLayout.Choice(
                [(FolderTileSize.Small, Str.Folder_Small.Text()), (FolderTileSize.Medium, Str.Folder_Medium.Text()), (FolderTileSize.Large, Str.Folder_Large.Text())],
                config.TileSize,
                value => { config.TileSize = value; save(); }),
            [
                FluentLayout.Row(Glyphs.Fit, Str.WidgetsB_Settings_Folder_Stretch_Label.Text(),
                    Str.WidgetsB_Settings_Folder_Stretch_Desc.Text(),
                    FluentLayout.Toggle(config.StretchTiles, value => { config.StretchTiles = value; save(); })),
            ]));

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

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Folder_Contents.Text()));

        // [S-3795] The order folds under its key; the filters under one group.
        rows.Add(FluentLayout.Expander(Glyphs.Sort, Str.WidgetsB_Settings_Folder_SortBy_Label.Text(), null,
            FluentLayout.Choice(
                [(FolderSort.Name, Str.Folder_ColName.Text()), (FolderSort.Modified, Str.WidgetsB_Settings_Folder_DateModified.Text()), (FolderSort.Size, Str.Folder_ColSize.Text())],
                config.Sort,
                value => { config.Sort = value; save(); }),
            [
                FluentLayout.Row(Glyphs.MoveDown, Str.WidgetsB_Settings_Folder_Reverse_Label.Text(), null,
                    FluentLayout.Toggle(config.Descending, value => { config.Descending = value; save(); })),
                FluentLayout.Row(Glyphs.Folder, Str.WidgetsB_Settings_Folder_FoldersFirst_Label.Text(), null,
                    FluentLayout.Toggle(config.FoldersFirst, value => { config.FoldersFirst = value; save(); })),
            ]));

        var filter = new TextBox { Text = config.Filter, Width = 260, PlaceholderText = Str.Folder_FilterPlaceholder.Text() };
        filter.LostFocus += (_, _) =>
        {
            if (filter.Text != config.Filter)
            {
                config.Filter = filter.Text;
                save();
            }
        };

        rows.Add(FluentLayout.Expander(Glyphs.Search, Str.Folder_Filter_Label.Text(), Str.WidgetsB_Settings_Folder_Filter_Desc.Text(), filter,
            [
                FluentLayout.Row(Glyphs.Hidden, Str.WidgetsB_Folder_ShowHiddenItems.Text(), null,
                    FluentLayout.Toggle(config.ShowHidden, value => { config.ShowHidden = value; save(); })),
                FluentLayout.Row(Glyphs.File, Str.Folder_ShowExtMenu.Text(), null,
                    FluentLayout.Toggle(config.ShowExtensions, value => { config.ShowExtensions = value; save(); })),
            ]));

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

    /// <summary>
    /// The shell's own folder picker, so nobody has to type a path they could point at.
    /// <para>
    /// The picker needs an owner window, and the click that opened it came from the settings
    /// window, so the foreground window <i>is</i> that window at this instant. It writes into the
    /// same box the user types into and goes through the same commit, so there is one path to the
    /// setting rather than two.
    /// </para>
    /// </summary>
    private static async Task BrowseAsync(TextBox box, Action commit)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();

            // Required: PickSingleFolderAsync throws without at least one filter.
            picker.FileTypeFilter.Add("*");

            WinRT.Interop.InitializeWithWindow.Initialize(picker, Win32.GetForegroundWindow());

            var folder = await picker.PickSingleFolderAsync();

            if (folder is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            box.Text = folder.Path;
            commit();
        }
        catch (Exception ex)
        {
            // The dialog is a convenience over a box that still works. Losing it must not take the
            // settings window down with it.
            Log.Error(FolderScope, "the folder picker could not be shown", ex);
        }
    }
}
