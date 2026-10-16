using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-827] / [S-1723] The one font picker: a button naming the current font that opens a searchable
/// list (<see cref="FontPicker"/>) of the presets and every installed family, each entry drawn in the
/// font it names, and beside it "Add fonts", which opens Windows Settings' Fonts page ([S-6443]).
/// Every font setting uses this control rather than a list of names.
/// <para>
/// The installed list arrives off the UI thread; until it does the picker lists the presets and the
/// current family, so it shows what is set. It is read when the picker opens and again whenever
/// Windows says the installed fonts changed, so a font added from the Fonts page is there.
/// </para>
/// </summary>
internal static class FontChoice
{
    private const string Scope = "font-choice";

    /// <summary>The Fonts page of Windows Settings (Personalization › Fonts): where fonts are added.</summary>
    private static readonly Uri FontsPage = new("ms-settings:fonts");

    /// <summary>
    /// The picker and its "Add fonts" button for <paramref name="current"/> (a preset or a family
    /// name). <paramref name="onChanged"/> gets the chosen value when the user picks another one.
    /// </summary>
    public static FrameworkElement Create(string current, Action<string> onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);

        var installed = InstalledFonts.None;
        var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 180, VerticalAlignment = VerticalAlignment.Center };
        var button = new DropDownButton
        {
            Content = label,
            MinWidth = FluentLayout.ChoiceMinWidth,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(button, Str.Settings_Font_FamilyLabel.Text());

        IReadOnlyList<FontOption> Options() => FontSearch.Options(Presets(), installed, current);

        // The closed button names the font in the UI's own font ([S-1723]), as Settings' pickers do.
        void Show()
        {
            var options = Options();
            var chosen = FontSearch.Chosen(options, current);
            label.Text = options.First(o => o.Value == chosen).Label;
            ToolTipService.SetToolTip(button, label.Text);
        }

        var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight, ShouldConstrainToRootBounds = true };
        button.Flyout = flyout;

        void Choose(string family)
        {
            flyout.Hide();
            if (string.Equals(family, current, StringComparison.Ordinal))
            {
                return;
            }

            current = family;
            Show();

            try
            {
                onChanged(family);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "saving the font failed", ex);
            }
        }

        var queue = DispatcherQueue.GetForCurrentThread();

        void Apply(InstalledFonts families)
        {
            installed = families;
            Show();
            (flyout.Content as FontPicker)?.SetOptions(Options());
        }

        // Read (or re-read after a font change) the installed list; the open picker follows it. Read
        // once it is cached, so a picker opened a second time lists everything at once.
        void Load()
        {
            var loading = SystemFonts.LoadAsync();
            if (loading.IsCompletedSuccessfully)
            {
                Apply(loading.Result);
                return;
            }

            _ = loading.ContinueWith(
                task =>
                {
                    if (!task.IsCompletedSuccessfully)
                    {
                        Log.Warn(Scope, "the installed font list could not be read; only the presets are offered", task.Exception);
                        return;
                    }

                    if (queue is null || !queue.TryEnqueue(() => Apply(task.Result)))
                    {
                        Log.Warn(Scope, "could not list the installed fonts on the settings thread");
                    }
                },
                TaskScheduler.Default);
        }

        // Built when it opens, so the tick sits on the font chosen now and a closed picker costs nothing.
        flyout.Opening += (_, _) =>
        {
            var options = Options();
            flyout.Content = new FontPicker(options, FontSearch.Chosen(options, current), Choose);
            SystemFonts.Changed += Load;
            Load();
        };

        // Typing searches at once: the caret is in the box as soon as the picker is open.
        flyout.Opened += (_, _) => (flyout.Content as FontPicker)?.FocusSearch();
        flyout.Closed += (_, _) =>
        {
            SystemFonts.Changed -= Load;
            flyout.Content = null;
        };

        Show();

        var add = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2,
                Children = { Fluent.Icon(Glyphs.Add, "TextFillColorPrimaryBrush"), new TextBlock { Text = Str.Shell_Font_Add.Text() } },
            },
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(add, Str.Shell_Font_Add.Text());
        ToolTipService.SetToolTip(add, Str.Shell_Font_AddTip.Text());
        add.Click += (_, _) => _ = ShellLauncher.OpenWindowsSettings(FontsPage);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Children = { add, button },
        };

#if DEBUG
        // The settings render draws the picker's panel in the page: a flyout on an off-screen,
        // never-activated window does not render.
        DebugPanels.AddOrUpdate(row, new Func<string?, FontPicker>(query =>
        {
            var picker = new FontPicker(Options(), FontSearch.Chosen(Options(), current), _ => { });
            if (!string.IsNullOrEmpty(query))
            {
                picker.SetQuery(query);
            }

            // Follows the list as it is read, as the real one does.
            _ = SystemFonts.LoadAsync().ContinueWith(
                task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        queue?.TryEnqueue(() => picker.SetOptions(FontSearch.Options(Presets(), task.Result, current)));
                    }
                },
                TaskScheduler.Default);
            return picker;
        }));
#endif

        return row;
    }

#if DEBUG
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, Func<string?, FontPicker>> DebugPanels = [];

    /// <summary>Debug renders only: the panel this choice's flyout would show, with <paramref name="query"/> typed.</summary>
    internal static FrameworkElement? DebugPanel(FrameworkElement choice, string? query)
        => DebugPanels.TryGetValue(choice, out var build) ? build(query) : null;
#endif

    /// <summary>The four presets ([S-826]) under their names in the UI's language.</summary>
    private static FontOption[] Presets() =>
    [
        new(DesignLook.SystemFont, Str.Shell_Appearance_SystemFont.Text()),
        new(DesignLook.SansFont, Str.Shell_Appearance_SansFont.Text()),
        new(DesignLook.SerifFont, Str.Settings_Font_Serif.Text()),
        new(DesignLook.MonoFont, Str.Settings_Font_Mono.Text()),
    ];
}
