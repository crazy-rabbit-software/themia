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
/// The rows of the app-layout editor's window ([S-1411]), the one window Themia still opens, drawn
/// as Windows 11's Settings draws a page. [S-1540]
/// <para>
/// A row is the Community Toolkit's <see cref="SettingsCard"/>, the control Settings itself is built
/// from: the header icon, title, secondary-text description and control on the right, the card fill
/// and stroke, hover and press, and the wrap under the title when the window is narrow. Section
/// headings are BodyStrong, 30 above and 6 below. The app's own settings are the edit bar's menus
/// ([S-3520]) and a widget's its properties menu ([S-6536]), both drawn by the menu engine; nothing
/// here is a settings page.
/// </para>
/// </summary>
public static class FluentLayout
{
    private const string Scope = "layout";

    /// <summary>
    /// The narrowest a drop-down on a card is, so a column of them lines up on the card edge instead
    /// of each being as wide as whatever it happens to show.
    /// </summary>
    public const double ChoiceMinWidth = 200;

    /// <summary>A row: icon, title, optional description, and a control on the right.</summary>
    public static Border Row(string glyph, string title, string? description, FrameworkElement control)
        => new() { Child = Card(GlyphIcon(glyph), title, description, control) };

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

            // A control reads its row's title, so a screen reader says "Name, edit" rather than "edit".
            if (string.IsNullOrEmpty(AutomationProperties.GetName(control)))
            {
                AutomationProperties.SetName(control, title);
            }
        }

        return card;
    }

    private static FontIcon GlyphIcon(string glyph) => UiLanguage.Mirrored(new FontIcon { Glyph = glyph });

    /// <summary>
    /// A card that is not a row: a canvas, a preview. The same fill, stroke, radius and height as the
    /// rows around it, from the theme, so it follows light and dark.
    /// </summary>
    public static Border Card(FrameworkElement child)
    {
        var border = new Border { Child = child };
        ApplyStyle(border, "SettingsCardBorderStyle");
        return border;
    }

    /// <summary>A section heading above a run of rows: BodyStrong, 30 above and 6 below, as Settings spaces them.</summary>
    public static TextBlock Header(string text)
    {
        var heading = new TextBlock { Text = text };
        ApplyStyle(heading, "SettingsSectionHeaderTextBlockStyle");
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level2);
        return heading;
    }

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

    private static void Say(TextBlock verdict, string message, Brush colour)
    {
        verdict.Text = message;
        verdict.Foreground = colour;
        verdict.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>A compact icon command: a notification's dismiss, the video stream's pause, a layout cell's remove.</summary>
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
