using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// The vocabulary every settings page is written in, drawn exactly as Windows 11's Settings app
/// draws it. [S-1540]
/// <para>
/// A row is the Community Toolkit's <see cref="SettingsCard"/> — the control Settings itself is
/// built from — with the header icon, title, secondary-text description and control on the right,
/// the card fill and stroke, hover and press, and the wrap under the title when the window is
/// narrow. Groups of related rows are a <see cref="SettingsExpander"/>. Section headings are
/// BodyStrong, 30 above and 6 below; cards sit 4 apart.
/// </para>
/// <para>
/// This is the whole vocabulary a settings page gets: a <see cref="Header"/>, a <see cref="Row"/>
/// and the handful of controls that go in one. Everything a settings page wants to draw is here
/// rather than in the page, because pages each inventing their own refusal message and their own
/// confirmation dialog is how they drifted apart in the first place — and because restyling this
/// file restyles every page at once, including pages written after it. A page that needs a shape
/// that is not here adds it here. A widget has no page: its options are its properties menu's
/// ([S-6536]).
/// </para>
/// <para>
/// Every row is also registered for the window's "Find a setting" box (see <see cref="SettingRow"/>),
/// so anything built through these helpers is searchable without being listed anywhere.
/// </para>
/// </summary>
public static class FluentLayout
{
    private const string Scope = "layout";

    /// <summary>
    /// The narrowest a drop-down on a settings card is, so a column of them lines up on the card
    /// edge instead of each being as wide as whatever it happens to show.
    /// </summary>
    public const double ChoiceMinWidth = 200;

    /// <summary>
    /// A settings row: icon, title, optional description, and a control on the right.
    /// </summary>
    /// <param name="keywords">
    /// Words that should find this row in search without being on it ("dark mode" for a theme
    /// row). A drop-down's own options are added automatically.
    /// </param>
    public static Border Row(string glyph, string title, string? description, FrameworkElement control, params string[] keywords)
        => Wrap(Card(GlyphIcon(glyph), title, description, control), title, description, control, keywords);

    /// <summary>
    /// A settings row whose icon is a picture rather than a glyph — a company's mark on the About
    /// page. The picture takes the glyph's place and size, so the row lines up with its neighbours.
    /// </summary>
    public static Border Row(FrameworkElement icon, string title, string? description, FrameworkElement control)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return Wrap(Card(PictureIcon(icon), title, description, control), title, description, control, []);
    }

    /// <summary>
    /// A row that is itself the button and leaves this window — "open the config file", "open the
    /// website" — with Windows' open-in-new-window mark on the right.
    /// </summary>
    public static Border ClickRow(string glyph, string title, string? description, Action onClick)
        => ActionRow(glyph, title, description, onClick, UiLanguage.Mirrored(new FontIcon { Glyph = Glyphs.OpenExternal, FontSize = 14 }));

    private static Border ActionRow(string glyph, string title, string? description, Action onClick, IconElement? actionIcon)
    {
        ArgumentNullException.ThrowIfNull(onClick);

        var card = Card(GlyphIcon(glyph), title, description, control: null);
        card.IsClickEnabled = true;

        if (actionIcon is not null)
        {
            card.ActionIcon = actionIcon;
        }

        card.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                // A row's click runs the page's code; nothing it throws may reach the window.
                Log.Error(Scope, $"'{title}' failed", ex);
            }
        };

        return Wrap(card, title, description, null, []);
    }

    /// <summary>
    /// A group of related rows that fold under one header, as Settings folds "Choose your mode" and
    /// its friends. The header row may carry its own control (a master switch); the folded rows are
    /// built with <see cref="Row"/> as usual and are searched like any other row.
    /// </summary>
    /// <param name="expandedChanged">Told when the reader opens or closes the group ([S-1833]).</param>
    public static Border Expander(
        string glyph,
        string title,
        string? description,
        FrameworkElement? control,
        IReadOnlyList<Border> rows,
        bool expanded = false,
        Action<bool>? expandedChanged = null)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var expander = new SettingsExpander
        {
            Header = title,
            HeaderIcon = GlyphIcon(glyph),
            IsExpanded = expanded,
        };

        if (expandedChanged is not null)
        {
            expander.RegisterPropertyChangedCallback(SettingsExpander.IsExpandedProperty, (sender, _) =>
            {
                try
                {
                    expandedChanged(((SettingsExpander)sender).IsExpanded);
                }
                catch (Exception ex)
                {
                    // A XAML callback must not throw; the group still opens or closes.
                    Log.Error(Scope, $"'{title}' could not record its open state", ex);
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            expander.Description = description;
        }

        if (control is not null)
        {
            expander.Content = control;
        }

        var nested = new List<SettingRow>();

        foreach (var row in rows)
        {
            // The expander lays out cards, not frames: take the card out of its frame and keep the
            // frame's record of it for search.
            if (SettingRow.Of(row) is { } setting && row.Child is SettingsCard card)
            {
                row.Child = null;
                setting.Parent = expander;
                nested.Add(setting);
                expander.Items.Add(card);
            }
            else
            {
                expander.Items.Add(row);
            }
        }

        return new Border { Child = expander, Tag = new SettingRow(title, description, control) { Card = expander, Nested = nested } };
    }

    /// <summary>
    /// The button of a row that destroys something (Sign out): its label in the theme's
    /// critical colour, so it reads as the one to be careful with. Always the last row of its group.
    /// </summary>
    public static Button DangerButton(string text)
    {
        var label = new TextBlock { Text = text, Foreground = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red) };
        var button = new Button { Content = label };
        AutomationProperties.SetName(button, text);
        return button;
    }

    /// <summary>The card itself. Its own method so every row is built the same way.</summary>
    private static SettingsCard Card(IconElement icon, string title, string? description, FrameworkElement? control)
    {
        var card = new SettingsCard
        {
            Header = title,
            HeaderIcon = icon,
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            card.Description = description;
        }

        if (control is not null)
        {
            card.Content = control;

            // A switch reads its row's title, so a screen reader says "Start with Windows, on"
            // rather than "toggle switch, on".
            if (string.IsNullOrEmpty(AutomationProperties.GetName(control)))
            {
                AutomationProperties.SetName(control, title);
            }
        }

        return card;
    }

    private static Border Wrap(Control card, string title, string? description, FrameworkElement? control, string[] keywords)
        => new() { Child = card, Tag = new SettingRow(title, description, control) { Card = card, Keywords = keywords } };

    private static FontIcon GlyphIcon(string glyph) => UiLanguage.Mirrored(new FontIcon { Glyph = glyph });

    /// <summary>A picture in the icon slot: an image becomes an image icon, an icon stays itself.</summary>
    private static IconElement PictureIcon(FrameworkElement picture) => picture switch
    {
        IconElement icon => icon,
        Image { Source: { } source } image => new ImageIcon { Source = source, Width = image.Width, Height = image.Height },
        _ => new FontIcon { Glyph = Glyphs.Info },
    };

    /// <summary>
    /// A card that is not a settings row: a list item, a sign-in flow, a preview. The same fill,
    /// stroke, radius and height as the rows around it, from the theme, so it follows light and dark.
    /// </summary>
    public static Border Card(FrameworkElement child)
    {
        var border = new Border { Child = child };
        ApplyStyle(border, "SettingsCardBorderStyle");
        return border;
    }

    /// <summary>
    /// A section heading above a run of rows: BodyStrong, 30 above and 6 below, as Settings spaces
    /// them. Marked, so search knows which section a row sits under.
    /// </summary>
    public static TextBlock Header(string text)
    {
        var heading = new TextBlock { Text = text, Tag = SettingsIndex.SectionMark };
        ApplyStyle(heading, "SettingsSectionHeaderTextBlockStyle");
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level2);
        return heading;
    }

    /// <summary>Whether an element is a section heading built by <see cref="Header"/>.</summary>
    public static bool IsHeader(FrameworkElement element)
        => element is TextBlock { Tag: string mark } && mark == SettingsIndex.SectionMark;

    /// <summary>A plain line of explanation between rows, in the secondary text colour.</summary>
    public static TextBlock Note(string text)
    {
        var note = new TextBlock { Text = text };
        ApplyStyle(note, "SettingsNoteTextBlockStyle");
        return note;
    }

    /// <summary>
    /// A named style from App.xaml. Styles carry ThemeResource brushes that follow the window's own
    /// theme; a missing one is a typo worth a log line, and the element keeps the defaults.
    /// </summary>
    public static void ApplyStyle(FrameworkElement element, string key)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Style style)
        {
            element.Style = style;
            return;
        }

        Log.Warn(Scope, $"style '{key}' missing from App.xaml");
    }

    public static ComboBox Choice<T>(IReadOnlyList<(T Value, string Label)> options, T selected, Action<T> onChanged)
        where T : struct
    {
        var combo = new ComboBox { MinWidth = ChoiceMinWidth };

        foreach (var (value, label) in options)
        {
            combo.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }

        for (var i = 0; i < options.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(options[i].Value, selected))
            {
                combo.SelectedIndex = i;
                break;
            }
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: T value })
            {
                onChanged(value);
            }
        };

        return combo;
    }

    // ---------------------------------------------------------------------------------------
    // Fields that can be refused
    // ---------------------------------------------------------------------------------------

    /// <summary>The line under a field, before anything has been said on it.</summary>
    public static TextBlock Verdict() => new()
    {
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed,
        HorizontalAlignment = HorizontalAlignment.Right,
        TextAlignment = TextAlignment.Right,
        MaxWidth = 300,
    };

    /// <summary>Say why the value was refused. The field keeps what was typed.</summary>
    public static void Refuse(TextBlock verdict, string message)
        => Say(verdict, message, ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red));

    /// <summary>
    /// Confirm what the value resolved to, or say what an empty field falls back to. Worth as much
    /// as a refusal: a field that looks accepted but points somewhere else is the same failure.
    /// </summary>
    public static void Confirm(TextBlock verdict, string message)
        => Say(verdict, message, ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray));

    private static void Say(TextBlock verdict, string message, Brush colour)
    {
        verdict.Text = message;
        verdict.Foreground = colour;
        verdict.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------------------------------------------------------------------------------------
    // Destroying something
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// A destructive row: a button that asks first, the same way everywhere.
    /// </summary>
    /// <param name="describe">
    /// What the dialog says, or <see langword="null"/> when there is nothing to destroy. A button
    /// whose only possible outcome is to report that it had nothing to do is a button that should
    /// not have been offered, so it is disabled instead.
    /// </param>
    public static Border ConfirmRow(
        string glyph,
        string title,
        string description,
        string action,
        string question,
        Func<string?> describe,
        Action confirmed)
    {
        var button = new Button { Content = action, IsEnabled = describe() is not null };
        button.Click += (_, _) => _ = ConfirmClickAsync(button, action, question, describe, confirmed);

        return Row(glyph, title, description, button);
    }

    /// <summary>
    /// Ask, then do it. Awaited rather than fired and forgotten, so a dialog that could not be shown
    /// is never mistaken for the user saying yes.
    /// </summary>
    public static async Task<bool> ConfirmAsync(
        FrameworkElement source,
        string question,
        string detail,
        string action)
    {
        var root = source.XamlRoot;

        if (root is null)
        {
            // No window to put a dialog in. Refusing beats destroying something unasked.
            Log.Warn(Scope, $"no XamlRoot for the '{question}' confirmation; nothing was changed");
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            RequestedTheme = source.ActualTheme,
            Title = question,
            Content = detail,
            PrimaryButtonText = action,
            CloseButtonText = Str.Common_Cancel.Text(),
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            // ShowAsync throws when another dialog already owns this window. Say so and change
            // nothing.
            Log.Error(Scope, $"could not show the '{question}' confirmation", ex);
            return false;
        }
    }

    /// <summary>
    /// The body of a <see cref="ConfirmRow"/> click. Its own method rather than an async lambda on
    /// the event: an exception out of an async void handler takes the process with it, and this one
    /// runs the caller's code.
    /// </summary>
    private static async Task ConfirmClickAsync(
        Button button,
        string action,
        string question,
        Func<string?> describe,
        Action confirmed)
    {
        if (describe() is not { } detail)
        {
            return;
        }

        if (!await ConfirmAsync(button, question, detail, action).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            confirmed();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"'{question}' could not be carried out", ex);
            return;
        }

        button.IsEnabled = describe() is not null;
    }

    /// <summary>A compact icon command on a card (a view's move up, move down, remove).</summary>
    public static Button IconCommand(string glyph, string tooltip)
    {
        var button = new Button
        {
            Content = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize }),
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            VerticalAlignment = VerticalAlignment.Center,
        };

        ToolTipService.SetToolTip(button, tooltip);

        // [S-1979] An icon has nothing for a screen reader to read: the tooltip is its name.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tooltip);
        return button;
    }
}
