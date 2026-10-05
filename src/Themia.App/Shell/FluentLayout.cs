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
using Themia.Core.Widgets;

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
/// and the handful of controls that go in one. Everything a widget's settings page wants to draw is
/// here rather than in the page, because thirty pages each inventing their own list editor, their
/// own refusal message and their own confirmation dialog is how they drifted apart in the first
/// place — and because restyling this file restyles every page at once, including pages written
/// after it. A page that needs a shape that is not here adds it here.
/// </para>
/// <para>
/// Every row is also registered for the window's "Find a setting" box (see <see cref="SettingRow"/>),
/// so anything built through these helpers is searchable without being listed anywhere.
/// </para>
/// </summary>
internal static class FluentLayout
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
        => ActionRow(glyph, title, description, onClick, new FontIcon { Glyph = Glyphs.OpenExternal, FontSize = 14 });

    /// <summary>
    /// A row that is itself the button and goes to another page of this window — a view's widget,
    /// the accounts page — with the chevron Settings puts on a card that drills in.
    /// </summary>
    public static Border NavRow(string glyph, string title, string? description, Action onClick)
        => ActionRow(glyph, title, description, onClick, actionIcon: null);

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
    /// <param name="item">
    /// [S-1832] On a list page (Displays, Views), the display or view this group is: search offers
    /// its rows under the item's name and opens this group when it lands on one.
    /// </param>
    /// <param name="expandedChanged">Told when the reader opens or closes the group ([S-1833]).</param>
    public static Border Expander(
        string glyph,
        string title,
        string? description,
        FrameworkElement? control,
        IReadOnlyList<Border> rows,
        bool expanded = false,
        string? item = null,
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
                setting.Item = item;
                nested.Add(setting);
                expander.Items.Add(card);
            }
            else
            {
                expander.Items.Add(row);
            }
        }

        return new Border { Child = expander, Tag = new SettingRow(title, description, control) { Card = expander, Nested = nested, Item = item } };
    }

    /// <summary>
    /// [S-1832] The button of a row that destroys something (Delete view): its label in the theme's
    /// critical colour, so it reads as the one to be careful with. Always the last row of its group.
    /// </summary>
    public static Button DangerButton(string text)
    {
        var label = new TextBlock { Text = text, Foreground = Themed("SystemFillColorCriticalBrush", Colors.Red) };
        var button = new Button { Content = label };
        AutomationProperties.SetName(button, text);
        return button;
    }

    /// <summary>
    /// [S-1549] A colour setting: the shared swatch button and its Windows 11-style picker. Every
    /// colour in the settings is one of these — never a hex box. <paramref name="onChanged"/> gets
    /// <c>#RRGGBB</c>, or empty for the default when <see cref="ColorChoiceOptions.DefaultLabel"/> is set.
    /// </summary>
    public static Border ColorPickerRow(
        string glyph,
        string title,
        string? description,
        string? value,
        Action<string> onChanged,
        ColorChoiceOptions? options = null)
        => Row(glyph, title, description, ColorChoice.Create(value, onChanged, options, title), "colour", "color picker");

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

    private static FontIcon GlyphIcon(string glyph) => new() { Glyph = glyph };

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

    /// <summary>
    /// A switch, drawn as Settings draws it inside a card: "On" / "Off" to the left of the knob,
    /// the whole thing flush with the card's right edge (the toolkit card's own switch style).
    /// </summary>
    public static ToggleSwitch Toggle(bool value, Action<bool> onChanged)
    {
        // On / Off in the app's language: the control's own default follows Windows' display
        // language, which is not the one the user picked in Themia.
        var toggle = new ToggleSwitch { IsOn = value, OnContent = Str.Toggle_On.Text(), OffContent = Str.Toggle_Off.Text() };
        toggle.Toggled += (_, _) => onChanged(toggle.IsOn);
        return toggle;
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

    /// <summary>A slider with a live numeric readout, used for the gap and corner sizes.</summary>
    public static StackPanel Slider(double value, double min, double max, double step, string unit, Action<double> onChanged)
    {
        // The number in the language's own digits grouping, and "42 %" where the language spaces it.
        string Readout(double v) => unit == "%"
            ? LocaleFormat.Percent(Strings.Current, Math.Round(v))
            : $"{LocaleFormat.Integer((long)Math.Round(v))}{unit}";

        var readout = new TextBlock
        {
            Text = Readout(value),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 44,
            TextAlignment = TextAlignment.Right,
        };

        ApplyStyle(readout, "SettingsDescriptionTextBlockStyle");

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            StepFrequency = step,
            Value = value,
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
        };

        slider.ValueChanged += (_, args) =>
        {
            readout.Text = Readout(args.NewValue);
            onChanged(args.NewValue);
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Children = { readout, slider },
        };
    }

    /// <summary>
    /// A whole-number box with up/down buttons and no upper bound, for a count a slider would cap
    /// ([S-1276]). Typed text that is not a number is overwritten with the last good value; a number
    /// under <paramref name="min"/> becomes <paramref name="min"/>. The buttons step by
    /// <paramref name="step"/>.
    /// </summary>
    public static NumberBox Stepper(int value, int min, int step, Action<int> onChanged)
    {
        var box = new NumberBox
        {
            Value = Math.Max(min, value),
            Minimum = min,
            SmallChange = step,
            LargeChange = step * 5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
            MinWidth = 140,
            VerticalAlignment = VerticalAlignment.Center,
        };

        box.ValueChanged += (_, args) =>
        {
            if (double.IsNaN(args.NewValue))
            {
                // Cleared: the last good value goes back in, rather than a count of nothing.
                box.Value = double.IsNaN(args.OldValue) ? min : args.OldValue;
                return;
            }

            var whole = (int)Math.Clamp(Math.Round(args.NewValue), min, int.MaxValue);
            if (whole != args.NewValue)
            {
                box.Value = whole;
                return;
            }

            onChanged(whole);
        };

        return box;
    }

    // ---------------------------------------------------------------------------------------
    // Fields that can be refused
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// A row whose control is a field with its verdict on the line underneath it.
    /// <para>
    /// Every text field on a settings page that can be wrong — a host, a feed address, a shortcut
    /// target, a folder, a coordinate, a search template — reports through this one shape: the value
    /// stays exactly as it was typed, and a single line under the field says why it was refused or
    /// confirms what it actually resolved to. Nothing is ever silently corrected into something that
    /// works, because a field that looks accepted and does something else is the failure all of this
    /// checking exists to prevent.
    /// </para>
    /// </summary>
    public static Border FieldRow(
        string glyph,
        string title,
        string? description,
        FrameworkElement field,
        TextBlock verdict)
        => Row(glyph, title, description, new StackPanel
        {
            Spacing = Fluent.Space1,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { field, verdict },
        });

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
        => Say(verdict, message, Themed("SystemFillColorCriticalBrush", Colors.Red));

    /// <summary>
    /// Confirm what the value resolved to, or say what an empty field falls back to. Worth as much
    /// as a refusal: a field that looks accepted but points somewhere else is the same failure.
    /// </summary>
    public static void Confirm(TextBlock verdict, string message)
        => Say(verdict, message, Themed("TextFillColorTertiaryBrush", Colors.Gray));

    private static void Say(TextBlock verdict, string message, Brush colour)
    {
        verdict.Text = message;
        verdict.Foreground = colour;
        verdict.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------------------------------------------------------------------------------------
    // How often a widget updates
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The refresh row for a widget that reads this machine: seconds, because a local read costs a
    /// wake-up and nothing else.
    /// </summary>
    /// <param name="description">
    /// The row's description as one whole sentence in the current language — "How often the
    /// devices are enumerated" — never a fragment this method completes: word order differs per
    /// language, so each caller owns its own key.
    /// </param>
    public static void RefreshSeconds(
        List<FrameworkElement> rows,
        string description,
        double current,
        double min,
        double max,
        double step,
        Action<int> onChanged)
    {
        rows.Add(Header(Str.Shell_Refresh_Header.Text()));

        rows.Add(Row(Glyphs.Timer, Str.Common_Refresh.Text(), description,
            Slider(current, min, max, step, Str.Shell_Refresh_SecondsUnit.Text(), value => onChanged((int)Math.Round(value)))));
    }

    /// <summary>
    /// The refresh row for a widget that asks a service: a fixed set of cadences in minutes, and
    /// nothing under a minute anywhere.
    /// <para>
    /// The other refresh row is a slider in seconds, and the difference is deliberate rather than
    /// drift. Every service behind these widgets is free, shared and unauthenticated, and a slider
    /// that goes to "every second" is an invitation to get the whole app blocked. The row reads the
    /// same either way — same glyph, same title, same place on the page — so only the granularity
    /// differs, which is the part that is genuinely not the same.
    /// </para>
    /// </summary>
    public static void RefreshChoice(
        List<FrameworkElement> rows,
        int current,
        Action<int> onChanged,
        bool fast = false)
    {
        (int Value, string Label)[] options = fast
            ? [(1, Str.Shell_Refresh_EveryMinute.Text()), (5, Str.Shell_Refresh_EveryMinutes.Plural(5)), (15, Str.Shell_Refresh_EveryMinutes.Plural(15)), (30, Str.Shell_Refresh_EveryMinutes.Plural(30))]
            : [(5, Str.Shell_Refresh_EveryMinutes.Plural(5)), (15, Str.Shell_Refresh_EveryMinutes.Plural(15)), (30, Str.Shell_Refresh_EveryMinutes.Plural(30)), (60, Str.Shell_Refresh_EveryHour.Text())];

        // A persisted value outside the set — hand-edited, or left over from an older build —
        // would otherwise leave the box blank and the setting pointing at nothing visible.
        var selected = current;
        var known = false;

        foreach (var (value, _) in options)
        {
            known |= value == selected;
        }

        if (!known)
        {
            // The second entry either way: five minutes on a fast widget, fifteen on a slow one.
            selected = options[1].Value;
            onChanged(selected);
        }

        rows.Add(Header(Str.Shell_Refresh_Header.Text()));

        rows.Add(Row(Glyphs.Timer, Str.Common_Refresh.Text(), Str.Shell_Refresh_ServiceDesc.Text(),
            Choice(options, selected, onChanged)));
    }

    // ---------------------------------------------------------------------------------------
    // Destroying something
    // ---------------------------------------------------------------------------------------

    /// <summary>A row whose action cannot be undone. The page puts every one of these last.</summary>
    public static Border DangerRow(string glyph, string title, string? description, FrameworkElement control)
    {
        var card = Card(GlyphIcon(glyph), title, description, control);
        return new Border { Child = card, Tag = new SettingRow(title, description, control) { Card = card, IsDanger = true } };
    }

    /// <summary>Whether a row belongs in the page's danger section.</summary>
    public static bool IsDanger(FrameworkElement element) => SettingRow.Of(element) is { IsDanger: true };

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

        return DangerRow(glyph, title, description, button);
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

    // ---------------------------------------------------------------------------------------
    // Editable lists
    // ---------------------------------------------------------------------------------------

    /// <summary>A compact icon command on a list card: move up, move down, remove.</summary>
    public static Button IconCommand(string glyph, string tooltip)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize },
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            VerticalAlignment = VerticalAlignment.Center,
        };

        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    /// <summary>
    /// The one editable list on a settings page: a card per item with the item's own editor on the
    /// left and move-up, move-down and remove on the right, a line saying so when there is nothing
    /// in it, and an add row underneath.
    /// <para>
    /// Five widgets — timers, shortcuts, swatches, quotes and habits — had each grown their own
    /// version of this, and no two agreed on where the commands sat, what the empty list said, or
    /// whether the ends of the list could move past themselves. The differences were all accidents
    /// of who wrote which page. What genuinely varies is the body of one item, which is the one
    /// thing the caller supplies.
    /// </para>
    /// </summary>
    /// <returns>
    /// A handle for the pages that can add an item from somewhere other than the add button — the
    /// shortcuts page adds from a file picker and from the Windows locations list, and both must go
    /// through the same append so there is one way an item comes into being.
    /// </returns>
    public static ListEditorHandle<T> ListEditor<T>(List<FrameworkElement> rows, ListEditorOptions<T> options)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);

        // One host whose children are rebuilt in place, because adding, removing and reordering all
        // change the shape of this section while the page around it is built once.
        var host = new StackPanel { Spacing = Fluent.Space1 };
        rows.Add(host);

        var add = new Button { Content = options.AddAction };

        void Rebuild()
        {
            host.Children.Clear();

            if (options.Items.Count == 0)
            {
                host.Children.Add(Note(options.EmptyText));
            }

            for (var index = 0; index < options.Items.Count; index++)
            {
                host.Children.Add(ItemCard(options, index, Rebuild));
            }

            add.IsEnabled = options.Items.Count < options.Capacity;
        }

        void Append(T item)
        {
            if (options.Items.Count >= options.Capacity)
            {
                return; // ignore: the add button is already disabled at the cap.
            }

            options.Items.Add(item);
            options.Save();
            Rebuild();
        }

        add.Click += (_, _) => Append(options.Create());

        Rebuild();

        rows.Add(Row(Glyphs.Add, options.AddTitle, options.AddDescription, add));

        return new ListEditorHandle<T>(Append, Rebuild);
    }

    private static FrameworkElement ItemCard<T>(ListEditorOptions<T> options, int index, Action rebuild)
        where T : class
    {
        var item = options.Items[index];

        var body = options.Body(item);
        body.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(body, 0);

        var up = IconCommand(Glyphs.MoveUp, Str.ChildList_MoveUp.Text());
        var down = IconCommand(Glyphs.MoveDown, Str.ChildList_MoveDown.Text());
        var remove = IconCommand(Glyphs.Delete, options.RemoveTooltip);

        // One swap per click, and the ends cannot move past themselves.
        up.IsEnabled = index > 0;
        down.IsEnabled = index < options.Items.Count - 1;

        up.Click += (_, _) => Reorder(options, index, -1, rebuild);
        down.Click += (_, _) => Reorder(options, index, 1, rebuild);
        remove.Click += (_, _) => _ = RemoveAsync(options, item, remove, rebuild);

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space1,
            Margin = new Thickness(Fluent.Space3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { up, down, remove },
        };

        Grid.SetColumn(commands, 1);

        return Card(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { body, commands },
        });
    }

    private static void Reorder<T>(ListEditorOptions<T> options, int index, int delta, Action rebuild)
        where T : class
    {
        if (!DeskListOrder.Move(options.Items, index, delta))
        {
            return; // ignore: the arrow at the end of the list is pressable and does nothing.
        }

        options.Save();
        rebuild();
    }

    private static async Task RemoveAsync<T>(
        ListEditorOptions<T> options,
        T item,
        FrameworkElement source,
        Action rebuild)
        where T : class
    {
        if (options.ConfirmRemove is { } ask && !await ask(item, source).ConfigureAwait(true))
        {
            return;
        }

        // Removed by identity rather than by the index the card was built with: the list may have
        // been reordered while a confirmation was up.
        if (!options.Items.Remove(item))
        {
            return; // ignore: something else already took it out.
        }

        options.Save();
        rebuild();
    }

    /// <summary>
    /// A stock Fluent brush by resource key, with a fallback colour for the case where the key is
    /// missing.
    /// <para>
    /// Public because every settings file and every widget view wants it, and four private copies of
    /// the same four lines is four places for the fallback to drift. Looking the brush up by key is
    /// what makes a surface follow the theme; a literal colour is a surface that does not.
    /// </para>
    /// <para>
    /// [S-822] The brush comes from the appearance engine: it is Themia's theme (not the app's start-up
    /// theme) and it changes colour in place when the theme or the accent does, so nothing built from
    /// it has to be rebuilt.
    /// </para>
    /// </summary>
    public static Brush Themed(string key, Windows.UI.Color fallback) => DesignApplier.Brush(key, fallback);
}

/// <summary>What one <see cref="FluentLayout.ListEditor{T}"/> needs to know about its list.</summary>
internal sealed class ListEditorOptions<T>
    where T : class
{
    /// <summary>The live list out of the widget's config. Edited in place, never copied.</summary>
    public required IList<T> Items { get; init; }

    /// <summary>
    /// One item's own editor — the only part of a list that genuinely differs between widgets. The
    /// move and remove commands are added beside whatever this returns.
    /// </summary>
    public required Func<T, FrameworkElement> Body { get; init; }

    public required Action Save { get; init; }

    /// <summary>What the section says when the list is empty, in the widget's own words.</summary>
    public required string EmptyText { get; init; }

    /// <summary>The title of the add row, e.g. "Add a shortcut".</summary>
    public required string AddTitle { get; init; }

    /// <summary>The label on the add button, e.g. "Add".</summary>
    public required string AddAction { get; init; }

    /// <summary>A new, empty item. Nothing about it is filled in from anywhere else.</summary>
    public required Func<T> Create { get; init; }

    public string? AddDescription { get; init; }

    /// <summary>The most the list may hold. The add button is disabled at the cap.</summary>
    public int Capacity { get; init; } = int.MaxValue;

    public string RemoveTooltip { get; init; } = Str.Common_Remove.Text();

    /// <summary>
    /// Ask before removing, for a list whose items carry something the user cannot get back. Left
    /// unset, removing is immediate — which is right for a row that holds only what is on screen.
    /// </summary>
    public Func<T, FrameworkElement, Task<bool>>? ConfirmRemove { get; init; }
}

/// <summary>A way back into a built list editor, for a page that can add an item from elsewhere.</summary>
internal sealed class ListEditorHandle<T>
    where T : class
{
    private readonly Action<T> _append;
    private readonly Action _rebuild;

    internal ListEditorHandle(Action<T> append, Action rebuild)
    {
        _append = append;
        _rebuild = rebuild;
    }

    /// <summary>Append an item through the same path the add button uses.</summary>
    public void Add(T item) => _append(item);

    /// <summary>Redraw the cards from the list as it stands.</summary>
    public void Refresh() => _rebuild();
}
