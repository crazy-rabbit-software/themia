using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Shortcuts;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1388] The Shortcuts widget's options, all on its properties menu ([S-3719]): under Style what
/// the look draws with, then how a shortcut opens, then Add (a file, a folder, a web address or path,
/// an app layout, and the Windows locations as switches). It has no settings page: the shortcuts
/// themselves are added, ordered, renamed and removed on the widget ([S-1387], [S-1410]).
/// <para>
/// The typed address is the trust boundary here. Whatever is given becomes something the desktop
/// opens on a click, so it is checked by <see cref="LaunchTargets"/> as it is typed and cannot be
/// added while it is refused, with the reason under the box. Nothing is corrected: a bare word is
/// refused rather than turned into <c>https://word</c>, and a relative path rather than resolved
/// against wherever the app happens to have been started.
/// </para>
/// </summary>
internal static class ShortcutsMenu
{
    private const string Scope = "shortcut-menu";

    // Built per menu, not cached: the labels follow the language the menu is built in.
    private static (ShortcutItemSize, string)[] Sizes() =>
    [
        (ShortcutItemSize.Small, Str.Folder_Small.Text()),
        (ShortcutItemSize.Medium, Str.Folder_Medium.Text()),
        (ShortcutItemSize.Large, Str.Folder_Large.Text()),
    ];

    private static (ItemClickMode, string)[] Clicks() =>
    [
        (ItemClickMode.Double, ShortcutsStr.WidgetsC_Shortcuts_Settings_OpenWith_Double.Text()),
        (ItemClickMode.Single, ShortcutsStr.WidgetsC_Shortcuts_Settings_OpenWith_Single.Text()),
    ];

    /// <param name="layouts">The app-layout editor a new layout opens in (the host's, <see cref="SettingsHost.AppLayouts"/>).</param>
    public static void Lines(WidgetMenuLines menu, ShortcutsConfig config, IAppLayouts layouts)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(layouts);

        // [S-6401] What only the current look draws with, under Style ([S-6070]): picking another
        // style builds the menu again with its own.
        var look = menu.StyleLines;
        if (config.Offers(ShortcutsSetting.TileSize))
        {
            look.Add(menu.Choice(Glyphs.Scale, ShortcutsStr.WidgetsC_Shortcuts_Settings_TileSize_Label.Text(), Sizes(), config.GridSize, value => config.GridSize = value));
        }

        if (config.Offers(ShortcutsSetting.RowSize))
        {
            look.Add(menu.Choice(Glyphs.TextSize, ShortcutsStr.WidgetsC_Shortcuts_Settings_RowSize_Label.Text(), Sizes(), config.ListSize, value => config.ListSize = value));
        }

        if (config.Offers(ShortcutsSetting.IconOnly))
        {
            look.Add(menu.Toggle(null, ShortcutsStr.CtxMenu_IconOnly.Text(), config.GridIconOnly, on => config.GridIconOnly = on));
        }

        if (config.Offers(ShortcutsSetting.Stretch))
        {
            look.Add(menu.Toggle(null, ShortcutsStr.WidgetsC_Shortcuts_Settings_Stretch_Label.Text(), config.GridStretchHeight, on => config.GridStretchHeight = on));
        }

        if (config.Offers(ShortcutsSetting.Extensions))
        {
            look.Add(menu.Toggle(null, Str.Folder_ShowExtMenu.Text(), config.ShowFileExtension, on => config.ShowFileExtension = on));
        }

        menu.Lines.Add(menu.Choice(Glyphs.Open, ShortcutsStr.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text(), Clicks(), config.ClickMode, value => config.ClickMode = value));
        menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.Add, Str.Common_Add.Text(), Add(menu, config, layouts)));
    }

    /// <summary>[S-1388] The Add submenu: the pickers, a typed address, a new app layout, and the Windows locations.</summary>
    private static List<ItemMenuEntry> Add(WidgetMenuLines menu, ShortcutsConfig config, IAppLayouts layouts)
    {
        void Append(ShortcutItem item)
        {
            config.Items.Add(item);
            menu.Save();
        }

        var lines = new List<ItemMenuEntry>
        {
            new(Glyphs.File, ShortcutsStr.WidgetsC_Shortcuts_Settings_Pick_File.Text(), () => _ = PickFileAsync(Append)),
            new(Glyphs.Folder, ShortcutsStr.WidgetsC_Shortcuts_Settings_Pick_Folder.Text(), () => _ = PickFolderAsync(Append)),
            new(Glyphs.Link, ShortcutsStr.WidgetsC_Shortcuts_Menu_AddLink.Text(), () => menu.Ask(new WidgetTextAsk(
                ShortcutsStr.Shortcut_AddTitle.Text(),
                @"https://example.com  ·  C:\Users\…  ·  shell:Downloads",
                Str.Common_Add.Text(),
                Refusal,
                typed => Append(ShortcutItem.Create(typed, string.Empty))))),

            // [S-1411] An app layout: added, then straight into its editor (original S-55).
            new(Glyphs.AppLayout, ShortcutsStr.WidgetsC_Shortcuts_Menu_AddLayout.Text(), () =>
            {
                var item = ShortcutItem.CreateLayout();
                Append(item);
                layouts.Edit(item.Id);
            }, Disabled: !layouts.CanEdit),
            ItemMenuEntry.Separator,
        };

        // [S-1388] One switch per Windows location, with the icon Explorer shows for it: on adds it,
        // off removes every shortcut that points there, so the switch always says what the widget
        // holds (original S-16). [S-3795] Folded one level down: a dozen switches would bury the rest.
        var locations = new List<ItemMenuEntry>();
        foreach (var name in LaunchTargets.WellKnownShellNames)
        {
            var uri = LaunchTargets.ShellTarget(name);
            var on = ShortcutRules.HasLocation(config.Items, name);
            locations.Add(new(null, LaunchTargets.ShellDisplayName(uri), () =>
            {
                if (ShortcutRules.SetLocation(config.Items, name, !on))
                {
                    menu.Save();
                }
            }, Checked: on, IconFile: uri));
        }

        lines.Add(WidgetMenuLines.Submenu(Glyphs.Folder, ShortcutsStr.WidgetsC_Shortcuts_Settings_Locations.Text(), locations));
        return lines;
    }

    /// <summary>Why a typed target cannot be a shortcut, or null when it can.</summary>
    internal static string? Refusal(string typed)
    {
        var classified = LaunchTargets.Classify(typed);
        return classified.IsValid ? null : LaunchTargets.RefusalMessage(classified.Refusal);
    }

    /// <summary>
    /// The shell's own file picker, so nobody has to type a path they could point at. It adds
    /// through the same add as everything else, so there is one way a shortcut comes into being.
    /// </summary>
    private static async Task PickFileAsync(Action<ShortcutItem> add)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();

            // Required: PickSingleFileAsync throws without at least one filter.
            picker.FileTypeFilter.Add("*");

            // The picker needs an owner window, and the click that opened it came from the desktop
            // in edit mode, so the foreground window is that one at this instant.
            SettingsHost.OwnPicker(picker);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            add(ShortcutItem.Create(file.Path, string.Empty));
        }
        catch (Exception ex)
        {
            // The dialog is a convenience over a drop or a typed path that still work.
            Log.Error(Scope, "the file picker could not be shown", ex);
        }
    }

    private static async Task PickFolderAsync(Action<ShortcutItem> add)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");

            SettingsHost.OwnPicker(picker);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            add(ShortcutItem.Create(folder.Path, string.Empty));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the folder picker could not be shown", ex);
        }
    }
}
