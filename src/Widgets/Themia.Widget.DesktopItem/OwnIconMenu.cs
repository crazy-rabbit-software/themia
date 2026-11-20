using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-9509] An own icon's options, all on its properties page ([S-3719]): what it opens (Target), its
/// name, its icon, then how many lines its name has and how it opens. A desktop item has the name
/// lines alone (<see cref="DesktopItemModule.MenuLines"/>).
/// <para>
/// The typed address is the trust boundary here. Whatever is given becomes something the desktop
/// opens on a click, so it is checked by <see cref="LaunchTargets"/> as it is typed and cannot be
/// set while it is refused, with the reason under the box. Nothing is corrected: a bare word is
/// refused rather than turned into <c>https://word</c>, and a relative path rather than resolved
/// against wherever the app happens to have been started.
/// </para>
/// </summary>
internal static class OwnIconMenu
{
    /// <summary>What a typed target may look like, under the box.</summary>
    private const string TargetExamples = @"https://example.com  ·  C:\Users\…  ·  shell:Downloads  ·  ms-settings:display";

    /// <param name="layouts">The app-layout editor a new layout opens in (the host's, <see cref="SettingsHost.AppLayouts"/>).</param>
    public static void Lines(WidgetMenuLines menu, DesktopItemConfig config, IAppLayouts layouts)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(layouts);

        // [S-1862] What it opens is what it waits for: the Set up button opens the page here.
        menu.Lines.Add(WidgetMenuLines.SetsUp(Target(menu, config, layouts)));
        menu.Lines.Add(Name(menu, config));
        menu.Lines.Add(Icon(menu, config));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(DesktopItemModule.NameLines(menu, config));
        menu.Lines.Add(menu.Choice(
            Glyphs.Open,
            DesktopItemStr.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text(),
            [
                (ItemClickMode.Double, DesktopItemStr.WidgetsC_Shortcuts_Settings_OpenWith_Double.Text()),
                (ItemClickMode.Single, DesktopItemStr.WidgetsC_Shortcuts_Settings_OpenWith_Single.Text()),
            ],
            config.ClickMode,
            value => config.ClickMode = value));
    }

    /// <summary>
    /// [S-9509] The Target line, ending with what kind of thing the shortcut opens now: a file or a folder
    /// (Windows' pickers), a typed web address, path, Windows location or Settings page (asked beside the
    /// menu, [S-3721]), a Windows location by name, or an app layout (made, then its editor, [S-1411]; an
    /// existing one opens in its editor).
    /// </summary>
    private static ItemMenuEntry Target(WidgetMenuLines menu, DesktopItemConfig config, IAppLayouts layouts)
    {
        void Set(string target)
        {
            if (config.IsLayout || !string.Equals(target, config.Target, StringComparison.Ordinal))
            {
                config.SetTarget(target);
                menu.Save();
            }
        }

        var lines = new List<ItemMenuEntry>
        {
            new(Glyphs.File, DesktopItemStr.WidgetsC_Shortcuts_Settings_Pick_File.Text(), () => _ = FolderBrowse.PickFileAsync(["*"], Set)),
            new(Glyphs.Folder, DesktopItemStr.WidgetsC_Shortcuts_Settings_Pick_Folder.Text(), () => _ = FolderBrowse.PickAsync(Set)),
            WidgetMenuLines.Ask(Glyphs.Link, DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLink.Text(), new WidgetTextAsk(
                DesktopItemStr.WidgetsC_Shortcuts_Menu_ChangeTarget.Text().TrimEnd('…'),
                TargetExamples,
                Str.Common_Save.Text(),
                Refusal,
                Set,
                Initial: config.IsLayout ? null : config.Target)),
            Locations(config, Set),
            ItemMenuEntry.Separator,
            config.IsLayout
                ? new(Glyphs.Edit, Str.Shortcut_EditLayoutMenu.Text(), () => layouts.Edit(config.ItemId), Disabled: !layouts.CanEdit)

                // [S-1411] An app layout: made, then straight into its editor.
                : new(Glyphs.AppLayout, DesktopItemStr.WidgetsC_Shortcuts_Menu_AddLayout.Text(), () =>
                {
                    config.MakeLayout();
                    menu.Save();
                    layouts.Edit(config.ItemId);
                }, Disabled: !layouts.CanEdit),
        };

        return WidgetMenuLines.Submenu(Glyphs.Link, DesktopItemStr.WidgetsC_Shortcuts_Column_Target.Text(), lines) with { Value = TargetValue(config) };
    }

    /// <summary>What the Target line says the shortcut opens now; nothing while it opens nothing.</summary>
    private static string? TargetValue(DesktopItemConfig config)
        => config.IsLayout ? OwnIconRules.LayoutKindLabel()
            : string.IsNullOrWhiteSpace(config.Target) ? null
            : OwnIconRules.KindLabel(LaunchTargets.Classify(config.Target), TargetHealth.Unknown);

    /// <summary>[S-9509] The Windows locations by name, each with Explorer's icon: a choice of one, the current one marked.</summary>
    private static ItemMenuEntry Locations(DesktopItemConfig config, Action<string> set)
    {
        var text = DesktopItemStr.WidgetsC_Shortcuts_Settings_Locations.Text();
        var lines = new List<ItemMenuEntry>();
        foreach (var name in LaunchTargets.WellKnownShellNames)
        {
            var uri = LaunchTargets.ShellTarget(name);
            var current = !config.IsLayout && LaunchTargets.SameTarget(config.Target, uri);
            lines.Add(new(null, LaunchTargets.ShellDisplayName(uri), () =>
            {
                if (!current)
                {
                    set(uri);
                }
            }, Checked: current, IconFile: uri, Group: text));
        }

        return WidgetMenuLines.Submenu(Glyphs.Folder, text, lines);
    }

    /// <summary>[S-9507] The Name line: asked in place in its card ([S-2351]); the target's own name (or nothing) clears the label.</summary>
    private static ItemMenuEntry Name(WidgetMenuLines menu, DesktopItemConfig config)
        => WidgetMenuLines.Ask(Glyphs.Rename, Str.Folder_ColName.Text(), new WidgetTextAsk(
            Str.Folder_ColName.Text(),
            OwnIconRules.DerivedName(config),
            Str.Common_Save.Text(),
            _ => null,
            typed =>
            {
                if (OwnIconRules.Rename(config, typed))
                {
                    menu.Save();
                }
            },
            Initial: OwnIconRules.Name(config),
            AllowEmpty: true), OwnIconRules.Name(config));

    /// <summary>
    /// [S-9503] The Icon submenu: Picture… (the shell's picker over the picture types), Emoji… (one typed,
    /// or from Windows' emoji panel, Win + .), and Automatic icon back to what Windows gives the target.
    /// Its line ends with what is chosen: the emoji, or the picture's file name.
    /// </summary>
    internal static ItemMenuEntry Icon(WidgetMenuLines menu, DesktopItemConfig config)
    {
        var lines = new List<ItemMenuEntry>
        {
            // The chosen file is checked again by SetPicture: a picker can hand back a network path.
            new(Glyphs.Photo, DesktopItemStr.WidgetsC_Shortcuts_Icon_Picture.Text(), () => _ = FolderBrowse.PickFileAsync(OwnIcons.PictureExtensions, path =>
            {
                if (OwnIcons.SetPicture(config, path))
                {
                    menu.Save();
                }
            })),
            WidgetMenuLines.Ask(Glyphs.Emoji, DesktopItemStr.WidgetsC_Shortcuts_Icon_Emoji.Text(), new WidgetTextAsk(
                DesktopItemStr.WidgetsC_Shortcuts_Icon_Emoji.Text().TrimEnd('…'),
                DesktopItemStr.WidgetsC_Shortcuts_Icon_EmojiPlaceholder.Text(),
                Str.Common_Save.Text(),
                EmojiRefusal,
                typed =>
                {
                    if (OwnIcons.SetEmoji(config, typed))
                    {
                        menu.Save();
                    }
                },
                Initial: config.IconEmoji)),
            ItemMenuEntry.Separator,
            new(Glyphs.Reset, DesktopItemStr.WidgetsC_Shortcuts_Icon_Automatic.Text(), () =>
            {
                if (OwnIcons.Reset(config))
                {
                    menu.Save();
                }
            }, Disabled: !OwnIcons.HasOwn(config)),
        };

        var value = config.IconEmoji ?? (config.IconPicture is { } picture ? Path.GetFileName(picture) : null);
        return WidgetMenuLines.Submenu(Glyphs.Photo, DesktopItemStr.WidgetsC_Shortcuts_Icon_Label.Text(), lines) with { Value = value };
    }

    /// <summary>[S-9503] Why a typed emoji cannot be the icon, or null when it can.</summary>
    internal static string? EmojiRefusal(string typed)
        => OwnIcons.Emoji(typed) is null ? DesktopItemStr.WidgetsC_Shortcuts_Icon_EmojiRefused.Text() : null;

    /// <summary>Why a typed target cannot be a shortcut's, or null when it can.</summary>
    internal static string? Refusal(string typed)
    {
        var classified = LaunchTargets.Classify(typed);
        return classified.IsValid ? null : LaunchTargets.RefusalMessage(classified.Refusal);
    }
}
