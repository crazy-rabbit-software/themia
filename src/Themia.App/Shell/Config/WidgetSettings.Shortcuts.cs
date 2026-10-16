using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell.Config;

/// <summary>
/// The Shortcuts widget's settings rows ([S-1388]): how it looks first, then what to add, then the
/// list itself last (original S-61).
/// <para>
/// The target box is the trust boundary on this page. Whatever is typed becomes something the
/// desktop will open on a click, so it is checked by <see cref="LaunchTargets"/> before it is
/// written and the reason it was refused is shown under the box rather than swallowed. Nothing is
/// corrected: a bare word is refused rather than turned into <c>https://word</c>, and a relative
/// path is refused rather than resolved against wherever the app happens to have been started.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    private const string LauncherScope = "shortcut-settings";

    // Built per page, not cached: the labels follow the language the page is built in.
    private static (ShortcutItemSize, string)[] ShortcutSizes() =>
    [
        (ShortcutItemSize.Small, Str.Folder_Small.Text()),
        (ShortcutItemSize.Medium, Str.Folder_Medium.Text()),
        (ShortcutItemSize.Large, Str.Folder_Large.Text()),
    ];

    private static (ItemClickMode, string)[] ShortcutClicks() =>
    [
        (ItemClickMode.Double, Str.WidgetsC_Shortcuts_Settings_OpenWith_Double.Text()),
        (ItemClickMode.Single, Str.WidgetsC_Shortcuts_Settings_OpenWith_Single.Text()),
    ];

    public static void Shortcuts(List<FrameworkElement> rows, ShortcutsConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));

        // [S-6401] The view is the widget's style (on the properties panel); only what that style draws
        // with is offered here, and picking another style rebuilds the page ([S-6071], [S-6074]).
        if (config.Offers(ShortcutsSetting.TileSize))
        {
            rows.Add(FluentLayout.Row(Glyphs.Scale, Str.WidgetsC_Shortcuts_Settings_TileSize_Label.Text(),
                Str.WidgetsC_Shortcuts_Settings_TileSize_Desc.Text(),
                FluentLayout.Choice(ShortcutSizes(), config.GridSize, value => { config.GridSize = value; save(); })));
        }

        if (config.Offers(ShortcutsSetting.IconOnly))
        {
            rows.Add(FluentLayout.Row(Glyphs.Preview, Str.CtxMenu_IconOnly.Text(),
                Str.WidgetsC_Shortcuts_Settings_IconsOnly_Desc.Text(),
                FluentLayout.Toggle(config.GridIconOnly, value => { config.GridIconOnly = value; save(); })));
        }

        if (config.Offers(ShortcutsSetting.Stretch))
        {
            rows.Add(FluentLayout.Row(Glyphs.Fit, Str.WidgetsC_Shortcuts_Settings_Stretch_Label.Text(),
                Str.WidgetsC_Shortcuts_Settings_Stretch_Desc.Text(),
                FluentLayout.Toggle(config.GridStretchHeight, value => { config.GridStretchHeight = value; save(); })));
        }

        if (config.Offers(ShortcutsSetting.RowSize))
        {
            rows.Add(FluentLayout.Row(Glyphs.TextSize, Str.WidgetsC_Shortcuts_Settings_RowSize_Label.Text(),
                Str.WidgetsC_Shortcuts_Settings_RowSize_Desc.Text(),
                FluentLayout.Choice(ShortcutSizes(), config.ListSize, value => { config.ListSize = value; save(); })));
        }

        if (config.Offers(ShortcutsSetting.Extensions))
        {
            rows.Add(FluentLayout.Row(Glyphs.File, Str.Folder_ShowExtMenu.Text(),
                Str.WidgetsC_Shortcuts_Settings_ShowExtensions_Desc.Text(),
                FluentLayout.Toggle(config.ShowFileExtension, value => { config.ShowFileExtension = value; save(); })));
        }

        rows.Add(FluentLayout.Row(Glyphs.Open, Str.WidgetsC_Shortcuts_Settings_OpenWith_Label.Text(),
            Str.WidgetsC_Shortcuts_Settings_OpenWith_Desc.Text(),
            FluentLayout.Choice(ShortcutClicks(), config.ClickMode, value => { config.ClickMode = value; save(); })));

        // The list editor is built last (it sits at the bottom) but the location switches above it
        // refresh it, and it refreshes them: each reflects what the other changed.
        ListEditorHandle<ShortcutItem>? list = null;
        var switches = new List<(string Name, ToggleSwitch Toggle)>();

        void SaveAndSync()
        {
            save();

            foreach (var (name, toggle) in switches)
            {
                var on = ShortcutRules.HasLocation(config.Items, name);
                if (toggle.IsOn != on)
                {
                    toggle.IsOn = on;
                }
            }
        }

        rows.Add(FluentLayout.Header(Str.Common_Add.Text()));

        var file = new Button { Content = Str.WidgetsC_Shortcuts_Settings_Pick_File.Text() };
        var folder = new Button { Content = Str.WidgetsC_Shortcuts_Settings_Pick_Folder.Text() };

        file.Click += (_, _) => _ = PickFileAsync(() => list);
        folder.Click += (_, _) => _ = PickFolderAsync(() => list);

        rows.Add(FluentLayout.Row(Glyphs.Folder, Str.WidgetsC_Shortcuts_Settings_Pick_Label.Text(),
            Str.WidgetsC_Shortcuts_Settings_Pick_Desc.Text(),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2,
                Children = { file, folder },
            }));

        // [S-1411] An app layout: added, then straight into its editor (original S-55).
        var layout = new Button { Content = Str.Shortcut_NewLayout.Text() };
        layout.IsEnabled = AppServices.EditAppLayout is not null;
        layout.Click += (_, _) =>
        {
            var item = ShortcutItem.CreateLayout();

            if (list is not null)
            {
                list.Add(item);
                AppServices.EditAppLayout?.Invoke(item.Id);
            }
        };

        rows.Add(FluentLayout.Row(Glyphs.AppLayout, ShortcutRules.LayoutKindLabel(),
            Str.WidgetsC_Shortcuts_Settings_AppLayout_Desc.Text(),
            layout));

        // [S-1388] One switch per Windows location, with the icon Explorer shows for it: on adds it,
        // off removes every shortcut that points there, so the switch always says what the widget
        // holds (original S-16). The shell: URI is the tooltip, never the text. [S-3795] Folded
        // under one group: a dozen switches would otherwise bury the list below them.
        var locations = new List<Border>();
        foreach (var name in LaunchTargets.WellKnownShellNames)
        {
            var uri = LaunchTargets.ShellTarget(name);
            var toggle = FluentLayout.Toggle(ShortcutRules.HasLocation(config.Items, name), on =>
            {
                if (ShortcutRules.SetLocation(config.Items, name, on))
                {
                    save();
                    list?.Refresh();
                }
            });

            switches.Add((name, toggle));

            var row = FluentLayout.Row(ShellIcon(uri), LaunchTargets.ShellDisplayName(uri), null, toggle);
            ToolTipService.SetToolTip(row.Child, uri);
            locations.Add(row);
        }

        rows.Add(FluentLayout.Expander(Glyphs.Folder, Str.WidgetsC_Shortcuts_Settings_Locations.Text(), null, null, locations));

        rows.Add(FluentLayout.Header(Str.WidgetsC_Shortcuts_Settings_List_Header.Text()));

        list = FluentLayout.ListEditor(rows, new ListEditorOptions<ShortcutItem>
        {
            Items = config.Items,
            Save = SaveAndSync,
            EmptyText = Str.WidgetsC_Shortcuts_Settings_List_Empty.Text(),
            AddTitle = Str.Shortcut_AddTitle.Text(),
            AddDescription = Str.WidgetsC_Shortcuts_Settings_List_AddDesc.Text(),
            AddAction = Str.Common_Add.Text(),
            RemoveTooltip = Str.WidgetsC_Shortcuts_Settings_List_Remove.Text(),
            Create = () => ShortcutItem.Create(string.Empty, string.Empty),
            Body = item => ShortcutBody(item, save),
        });
    }

    /// <summary>
    /// A Windows location's real icon for its settings row: an <see cref="ImageIcon"/> (the row's
    /// icon slot takes an icon element) filled in when the shell answers.
    /// </summary>
    private static ImageIcon ShellIcon(string uri)
    {
        var icon = new ImageIcon { Width = Fluent.IconSize, Height = Fluent.IconSize };
        _ = LoadShellIconAsync(uri, icon);
        return icon;
    }

    private static async Task LoadShellIconAsync(string uri, ImageIcon icon)
    {
        try
        {
            icon.Source = await AppIcons.GetAsync(uri, (int)(Fluent.IconSize * 2), AppIcons.ThemeOf(icon)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(LauncherScope, "could not load a Windows location's icon", ex);
        }
    }

    /// <summary>
    /// One shortcut's editor: what it is called, where it points, and whether that is allowed. The
    /// ordering and the removal belong to the shared list editor.
    /// </summary>
    private static FrameworkElement ShortcutBody(ShortcutItem item, Action save)
    {
        if (item.IsLayout)
        {
            return LayoutBody(item, save);
        }

        var label = new TextBox
        {
            Text = item.Label,
            PlaceholderText = NameHint(item.Target),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var target = new TextBox
        {
            Text = item.Target,
            PlaceholderText = @"https://example.com  ·  C:\Users\…  ·  shell:Downloads",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, Fluent.Space2, 0, 0),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };

        // The verdict sits under the field it judges, the same way it does in every other settings
        // field on every other page. Left-aligned here only because the field it belongs to is.
        var verdict = FluentLayout.Verdict();
        verdict.HorizontalAlignment = HorizontalAlignment.Left;
        verdict.TextAlignment = TextAlignment.Left;
        verdict.Margin = new Thickness(0, Fluent.Space2, 0, 0);

        void ShowVerdict()
        {
            if (item.Target.Trim().Length == 0)
            {
                FluentLayout.Confirm(verdict, Str.WidgetsC_Shortcuts_Settings_Verdict_None.Text());
                return;
            }

            var classified = LaunchTargets.Classify(item.Target);

            if (!classified.IsValid)
            {
                FluentLayout.Refuse(verdict, LaunchTargets.RefusalMessage(classified.Refusal));
                return;
            }

            // Confirming what will actually open is worth as much as refusing: a row that looks
            // accepted but opens something else is the failure this check exists to prevent.
            FluentLayout.Confirm(verdict, classified.Kind switch
            {
                LaunchKind.Web => Str.WidgetsC_Shortcuts_Settings_Verdict_Web.Format(("target", classified.Value)),
                LaunchKind.ShellNamespace => Str.WidgetsC_Shortcuts_Settings_Verdict_Open.Format(("target", LaunchTargets.ShellDisplayName(classified.Value))),
                _ => Str.WidgetsC_Shortcuts_Settings_Verdict_Open.Format(("target", classified.Value)),
            });
        }

        // On commit, not per keystroke: a path or a URL is only ever valid once it has been fully
        // typed, so validating each character would spend the whole time saying it is wrong.
        void CommitTarget()
        {
            var typed = target.Text.Trim();

            if (typed == item.Target)
            {
                ShowVerdict();
                return;
            }

            // Stored whether or not it passes, and the box keeps what was typed: a wrong target is
            // corrected by editing it, not by having it wiped. The widget refuses to open it, which
            // is where refusing belongs.
            item.Target = typed;
            label.PlaceholderText = NameHint(item.Target);

            save();
            ShowVerdict();
        }

        void CommitLabel()
        {
            var typed = label.Text.Trim();

            if (typed == item.Label)
            {
                return;
            }

            item.Label = typed;
            save();
        }

        target.LostFocus += (_, _) => CommitTarget();
        target.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                args.Handled = true;
                CommitTarget();
            }
        };

        label.LostFocus += (_, _) => CommitLabel();
        label.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                args.Handled = true;
                CommitLabel();
            }
        };

        ShowVerdict();

        return new StackPanel { Children = { label, target, verdict } };
    }

    /// <summary>
    /// [S-1411] An app layout's row (original S-57): its name, how many apps it opens instead of a
    /// target, and the button that opens its editor.
    /// </summary>
    private static FrameworkElement LayoutBody(ShortcutItem item, Action save)
    {
        var label = new TextBox
        {
            Text = item.Label,
            PlaceholderText = ShortcutRules.LayoutKindLabel(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        void CommitLabel()
        {
            var typed = label.Text.Trim();

            if (typed != item.Label)
            {
                item.Label = typed;
                save();
            }
        }

        label.LostFocus += (_, _) => CommitLabel();
        label.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                args.Handled = true;
                CommitLabel();
            }
        };

        var summary = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        summary.Text = Str.WidgetsC_Shortcuts_AppLayout_Summary.Format(("apps", LayoutPlan.Summary(item.Layout)));
        summary.VerticalAlignment = VerticalAlignment.Center;

        var edit = new Button { Content = Str.Shortcut_EditLayoutMenu.Text(), IsEnabled = AppServices.EditAppLayout is not null };
        edit.Click += (_, _) => AppServices.EditAppLayout?.Invoke(item.Id);

        var line = new Grid { Margin = new Thickness(0, Fluent.Space2, 0, 0) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(edit, 1);
        line.Children.Add(summary);
        line.Children.Add(edit);

        return new StackPanel { Children = { label, line } };
    }

    /// <summary>What the name box suggests when it is empty: whatever the target is called.</summary>
    private static string NameHint(string target)
        => LaunchTargets.DisplayName(null, target) is { Length: > 0 } derived ? derived : Str.Connect_Field_Name.Text();


    /// <summary>
    /// The shell's own file picker, so nobody has to type a path they could point at. It writes
    /// through the same add as everything else, so there is one way a shortcut comes into being.
    /// </summary>
    private static async Task PickFileAsync(Func<ListEditorHandle<ShortcutItem>?> list)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();

            // Required: PickSingleFileAsync throws without at least one filter.
            picker.FileTypeFilter.Add("*");

            // The picker needs an owner window, and the click that opened it came from the settings
            // window, so the foreground window is that window at this instant.
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Win32.GetForegroundWindow());

            var file = await picker.PickSingleFileAsync();

            if (file is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            list()?.Add(ShortcutItem.Create(file.Path, string.Empty));
        }
        catch (Exception ex)
        {
            // The dialog is a convenience over a box that still works. Losing it must not take the
            // settings window down with it.
            Log.Error(LauncherScope, "the file picker could not be shown", ex);
        }
    }

    private static async Task PickFolderAsync(Func<ListEditorHandle<ShortcutItem>?> list)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");

            WinRT.Interop.InitializeWithWindow.Initialize(picker, Win32.GetForegroundWindow());

            var folder = await picker.PickSingleFolderAsync();

            if (folder is null)
            {
                return; // ignore: the user cancelled the dialog.
            }

            list()?.Add(ShortcutItem.Create(folder.Path, string.Empty));
        }
        catch (Exception ex)
        {
            Log.Error(LauncherScope, "the folder picker could not be shown", ex);
        }
    }
}
