using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>[S-3716] The menu's levels and how their lines are drawn and written into.</summary>
internal partial class MenuPanel
{
    /// <summary>[S-3731] An inline slider's track, and the column its value stands in at the line's end.</summary>
    private const double SliderWidth = 100;

    /// <summary>[S-3734] The least a slider's track gives way to long words.</summary>
    private const double SliderMinWidth = 64;

    private const double SliderValueWidth = 40;

    /// <summary>The chevron of a line with a submenu: Windows' "ChevronRightMed", at the menu's size.</summary>
    private static readonly string Chevron = char.ConvertFromUtf32(0xE974);

    /// <summary>[S-3734] The widest a line's value is; it gives way to the line's words down to <see cref="ValueMinWidth"/>.</summary>
    private const double ValueMaxWidth = 160;

    /// <summary>[S-3734] The least a line's value keeps beside long words: a few letters and the ellipsis.</summary>
    private const double ValueMinWidth = 48;

    /// <summary>[S-3728] The most lines a note wraps to; the rest is trimmed, the whole in its tooltip.</summary>
    private const int NoteLines = 5;

    /// <summary>A drawn line: its node now, and the parts a change of value is written into.</summary>
    private sealed class LineView(MenuNode node, Button button, FrameworkElement root, Border lead, Border trail, Border held, TextBlock words, TextBlock value, double room)
    {
        /// <summary>[S-3734] What the words and the value share of the line: less its icon, grip, mark and end columns.</summary>
        public double Room { get; } = room;

        public MenuNode Node { get; set; } = node;

        public string IconKey { get; set; } = node.IconKey;

        public Button Button { get; } = button;

        public FrameworkElement Root { get; } = root;

        public Border Lead { get; } = lead;

        /// <summary>[S-3716] The end column: the tick or the dot, or the chevron of a line that opens a submenu.</summary>
        public Border Trail { get; } = trail;

        /// <summary>[S-3541] Before the chevron: the dot of a line that opens a submenu and stands for the item that holds.</summary>
        public Border Held { get; } = held;

        public TextBlock Words { get; } = words;

        public TextBlock Value { get; } = value;
    }

    /// <summary>[S-3731] A drawn slider line: its node now, and the parts a change of value is written into.</summary>
    private sealed class SliderView(MenuNode node, Slider slider, TextBlock words, TextBlock value, double room, int wordsColumn)
    {
        public MenuNode Node { get; set; } = node;

        /// <summary>[S-3734] What the words and the track share of the line.</summary>
        public double Room { get; } = room;

        /// <summary>[S-3734] The column the words stand in: a track under them starts there.</summary>
        public int WordsColumn { get; } = wordsColumn;

        public Slider Slider { get; } = slider;

        public TextBlock Words { get; } = words;

        public TextBlock Value { get; } = value;

        /// <summary>The value is being written in from the menu's state: not a move of the user's.</summary>
        public bool Writing { get; set; }

        /// <summary>A key is stepping it: the step is stored at once.</summary>
        public bool Keyed { get; set; }

        /// <summary>The pointer moved it: stored on the release, previewed until then.</summary>
        public bool Pending { get; set; }
    }

    /// <summary>The menu's list or one open submenu: its lines, the surface they are on, and the line that opened it.</summary>
    private sealed class Level(StackPanel lines, FrameworkElement root, Canvas overlay)
    {
        public StackPanel Lines { get; } = lines;

        public FrameworkElement Root { get; } = root;

        /// <summary>[S-3732] Over the lines: where a dragged line's drop line is drawn.</summary>
        public Canvas Overlay { get; } = overlay;

        /// <summary>[S-3731] The slider lines, by their place in <see cref="Nodes"/>.</summary>
        public Dictionary<int, SliderView> Sliders { get; } = [];

        /// <summary>[S-3561] The notes, by their place: their words are written in place.</summary>
        public Dictionary<int, NoteView> Notes { get; } = [];

        /// <summary>[S-3561] The field lines, by their place.</summary>
        public Dictionary<int, FieldView> Fields { get; } = [];

        public List<LineView?> Views { get; } = [];

        public List<MenuNode> Nodes { get; set; } = [];

        public string Shape { get; set; } = string.Empty;

        public LineView? Anchor { get; set; }

        /// <summary>[S-3716] Some line has a mark or a chevron: every line keeps the end column, so the values stand in one column.</summary>
        public bool Trail { get; set; }

        public bool Icons { get; set; }

        /// <summary>[S-3722] A search's box, what it was asked and found, and the question still out.</summary>
        public TextBox? Box { get; set; }

        public MenuSearch? Search { get; set; }

        public List<MenuNode> Results { get; set; } = [];

        /// <summary>[S-3725] Show all was clicked: every answer shows until the next question.</summary>
        public bool ShowAll { get; set; }

        public CancellationTokenSource? Pending { get; set; }

        public Microsoft.UI.Dispatching.DispatcherQueueTimer? Settle { get; set; }
    }

    /// <summary>Which lines there are, as one string: values (ticks, words at the end, icons) are not in it.</summary>
    /// <remarks>
    /// [S-3561] Whether a line is greyed is written in place too (a form's Add while its fields are
    /// typed in), but for a danger line, whose colour is chosen as it is built.
    /// </remarks>
    private static string Shape(IReadOnlyList<MenuNode> nodes)
        => string.Join(";", nodes.Select(n => n.Key + "|" + (int)n.Kind + "|" + (n.Danger && n.Disabled ? 'd' : 'e') + "|"
            + (n.Opens ? 's' : '-') + "|" + (n.Checked is null ? '-' : 'c') + "|" + (n.Icon is null ? '-' : 'i') + "|" + (n.Danger && n.Kind != MenuNodeKind.Note ? '!' : '-')
            + "|" + (n.Order is null ? '-' : 'o')));

    /// <summary>[S-3732] The layer over a level's lines a dragged line's drop line is drawn on.</summary>
    private static Canvas Overlay() => new() { IsHitTestVisible = false };

    /// <summary>Draw <paramref name="nodes"/> into <paramref name="level"/>, replacing what it held.</summary>
    private void FillLevel(Level level, IReadOnlyList<MenuNode> nodes)
    {
        if (_drag is { } drag && ReferenceEquals(drag.Level, level))
        {
            EndDrag(drop: false);
        }

        // [S-3561] The field being typed in keeps the keyboard when its level is drawn again.
        var typing = FocusedField(level);

        level.Lines.Children.Clear();
        level.Views.Clear();
        level.Sliders.Clear();
        level.Notes.Clear();
        level.Fields.Clear();
        level.Nodes = [.. nodes];
        level.Shape = Shape(nodes);
        level.Trail = nodes.Any(n => n.Kind == MenuNodeKind.Line && (n.Checked is not null || n.Opens));
        level.Icons |= nodes.Any(n => n.Kind is MenuNodeKind.Line or MenuNodeKind.Slider && n.Icon is not null);

        // [S-3561] A form's notes stand where its fields do (a field has no icon column), not where a
        // line's words do: the note under Port or Password is about that field.
        var noteLead = level.Icons && !nodes.Any(n => n.Kind == MenuNodeKind.Field);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            switch (node.Kind)
            {
                case MenuNodeKind.Separator:
                    level.Lines.Children.Add(Divider(full: false));
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Note:
                    var note = NoteLine(node, noteLead);
                    level.Lines.Children.Add(note.Root);
                    level.Notes[i] = note;
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Field:
                    var field = MakeField(node);
                    level.Lines.Children.Add(field.Root);
                    level.Fields[i] = field;
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Spacer:
                    level.Lines.Children.Add(new Border { Height = node.Height, IsHitTestVisible = false });
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Custom:
                    level.Lines.Children.Add(node.Build!());
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Slider:
                    var slider = MakeSlider(level, node);
                    level.Lines.Children.Add(slider.Root);
                    level.Sliders[i] = slider.View;
                    level.Views.Add(null);
                    break;
                default:
                    var view = MakeLine(level, node);
                    level.Lines.Children.Add(view.Root);
                    level.Views.Add(view);
                    break;
            }
        }

        // Leading, trailing and doubled dividers go, as the card menu drops them ([S-1742]).
        HideStrayDividers(level.Lines);
        Refocus(level, typing);

        // [S-3732] A keyboard move drew the list again: the moved line takes the focus back.
        if (_refocus is { } refocus && level.Views.FirstOrDefault(v => v is not null && v.Node.Key == refocus) is { } moved)
        {
            _refocus = null;
            _ = DispatcherQueue?.TryEnqueue(() => moved.Button.Focus(FocusState.Keyboard));
        }
    }

    private static void HideStrayDividers(StackPanel lines)
    {
        var last = -1;
        var previousWasDivider = true;
        for (var i = 0; i < lines.Children.Count; i++)
        {
            var isDivider = lines.Children[i] is Rectangle;
            lines.Children[i].Visibility = Visibility.Visible;
            if (isDivider && previousWasDivider)
            {
                lines.Children[i].Visibility = Visibility.Collapsed;
                continue;
            }

            previousWasDivider = isDivider;
            if (!isDivider)
            {
                last = i;
            }
        }

        for (var i = last + 1; i < lines.Children.Count; i++)
        {
            lines.Children[i].Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Write the values of <paramref name="nodes"/> (same shape) into the lines drawn for the ones before.</summary>
    private static void Update(Level level, IReadOnlyList<MenuNode> nodes)
    {
        for (var i = 0; i < nodes.Count && i < level.Views.Count; i++)
        {
            var node = nodes[i];
            if (level.Views[i] is { } view)
            {
                Apply(view, node);
            }
            else if (level.Sliders.TryGetValue(i, out var slider))
            {
                ApplySlider(slider, node);
            }
            else if (level.Notes.TryGetValue(i, out var note))
            {
                ApplyNote(note, node);
            }
            else if (level.Fields.TryGetValue(i, out var field))
            {
                ApplyField(field, node);
            }
            else if (node.Kind is MenuNodeKind.Custom && i < level.Nodes.Count
                && !string.Equals(level.Nodes[i].ValueKey, node.ValueKey, StringComparison.Ordinal))
            {
                level.Lines.Children[i] = node.Build!();
            }
        }

        level.Nodes = [.. nodes];
    }

    private static void Apply(LineView view, MenuNode node)
    {
        view.Node = node;
        view.Button.IsEnabled = !node.Disabled;
        view.Words.Text = node.Text;
        view.Value.Text = node.Value ?? string.Empty;
        FitValue(view);
        TipWhenTrimmed(view.Words);
        TipWhenTrimmed(view.Value);

        if (!string.Equals(view.IconKey, node.IconKey, StringComparison.Ordinal))
        {
            view.IconKey = node.IconKey;
            view.Lead.Child = node.Icon?.Invoke();
        }

        // [S-3716] The mark at the end of the line, where a line that opens a submenu has its chevron;
        // [S-3541] such a line that holds (the view shown here) has the mark before its chevron.
        if (!node.Opens)
        {
            view.Trail.Child = node.Checked == true ? Mark(node.Radio) : null;
        }
        else
        {
            view.Held.Child = node.Checked == true ? Mark(node.Radio) : null;
        }

        AutomationProperties.SetName(view.Button, node.Value is { Length: > 0 } value ? node.Text + ", " + value : node.Text);
        if (node.Checked is { } on)
        {
            AutomationProperties.SetItemStatus(view.Button, on ? "✓" : string.Empty);
        }
    }

    /// <summary>
    /// [S-3734] The words keep their room before a long value: the value gives way to them, down to
    /// its least, and is trimmed; only words longer than the line less that are trimmed themselves
    /// ("Output d…" beside a device's long name was the words giving way instead).
    /// Measured on its own, the words come out a few DIPs short of what the line draws: a gap's worth more.
    /// </summary>
    private static void FitValue(LineView view)
    {
        if (view.Value.Text.Length == 0)
        {
            view.Value.MaxWidth = ValueMaxWidth;
            return;
        }

        view.Words.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var right = view.Value.Margin.Right;
        var beside = view.Room - view.Words.DesiredSize.Width - Fluent.Space2 - right;

        // The gap before the value narrows before either is trimmed.
        var gap = beside - Fluent.Space6 >= ValueMinWidth ? Fluent.Space6 : Fluent.Space3;
        view.Value.Margin = new Thickness(gap, 0, right, 0);
        view.Value.MaxWidth = Math.Clamp(beside - gap, ValueMinWidth, ValueMaxWidth);
    }

    /// <summary>
    /// [S-3734] Words cut short by the menu's one width show in full as their tooltip; words that fit
    /// have none (a line's own tooltip, a list's reorder hint, shows there).
    /// </summary>
    private static void TipWhenTrimmed(TextBlock text)
    {
        if (text.Tag is null)
        {
            text.Tag = Scope;
            text.IsTextTrimmedChanged += (sender, _) => ToolTipService.SetToolTip(sender, sender.IsTextTrimmed ? sender.Text : null);
        }

        ToolTipService.SetToolTip(text, text.IsTextTrimmed ? text.Text : null);
    }

    /// <summary>A tick for a switch that is on, the small dot for the option that holds, as Windows draws them.</summary>
    private static FrameworkElement Mark(bool radio) => radio
        ? new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        }
        : new FontIcon { Glyph = Glyphs.Done, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// [S-3716] One line: an icon column (where the level has icons), its words, a value (or a
    /// shortcut) in the secondary colour where a menu writes a shortcut, and at the end, right-aligned
    /// in its own column, the tick or the dot of a switch or a choice, or the chevron of a line that
    /// opens a submenu; [S-3541] a line that opens a submenu and holds has the mark just before its
    /// chevron. A danger line's words and icon are the critical colour.
    /// [S-3732] A line of an ordered list carries a grip before its end column and is dragged.
    /// </summary>
    private LineView MakeLine(Level level, MenuNode node)
    {
        var lead = new Border { Width = Fluent.IconSize, HorizontalAlignment = HorizontalAlignment.Left, Child = node.Icon?.Invoke() };
        var trail = new Border { Width = Fluent.IconSize, HorizontalAlignment = HorizontalAlignment.Right };
        if (node.Opens)
        {
            trail.Child = new FontIcon
            {
                Glyph = Chevron,
                FontSize = 12,
                MirroredWhenRightToLeft = true,
                Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var words = new TextBlock
        {
            FontSize = FluentType.Body.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var value = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");

        if (node.Danger && !node.Disabled)
        {
            var critical = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
            words.Foreground = critical;
            if (lead.Child is IconElement icon)
            {
                icon.Foreground = critical;
            }
        }

        var grip = node.Order is null ? null : new FontIcon
        {
            Glyph = Glyphs.GripperHorizontal,
            FontSize = 12,
            Foreground = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0),
        };

        // [S-3541] The mark's place is kept on every line that can hold, so the grips stand in one column.
        var held = new Border { VerticalAlignment = VerticalAlignment.Center, Width = node.Checked is null ? 0 : Fluent.Space3, Margin = new Thickness(0, 0, node.Checked is null ? 0 : Fluent.Space2, 0) };
        var button = LineButton(level.Icons ? lead : null, words, value, grip, node.Opens ? held : null, level.Trail ? trail : null);
        button.IsEnabled = !node.Disabled;

        // [S-3734] What the words and the value share: the line less its icon, grip, mark and end columns.
        var room = MenuWidth - (2 * (Fluent.Space1 + Fluent.Space3)) - (2 * Fluent.Hairline) - (level.Icons ? LeadColumn : 0)
            - (level.Trail ? Fluent.IconSize : 0) - (node.Order is null ? 0 : Fluent.Space2 + 12 + Fluent.Space2)
            - (node.Opens && node.Checked is not null ? Fluent.Space3 + Fluent.Space2 : 0);
        var view = new LineView(node, button, button, lead, trail, held, words, value, room);
        Apply(view, node);

        button.Click += (_, _) => OnLine(level, view);
        button.KeyDown += (_, e) =>
        {
            var rtl = UiLanguage.Flow == FlowDirection.RightToLeft;
            var towards = rtl ? Windows.System.VirtualKey.Left : Windows.System.VirtualKey.Right;
            var away = rtl ? Windows.System.VirtualKey.Right : Windows.System.VirtualKey.Left;
            if (e.Key == towards && view.Node.Opens)
            {
                CloseFrom(_levels.IndexOf(level) + 1);
                OpenSubmenu(view, focus: true);
                e.Handled = true;
            }
            else if (e.Key == away && _levels.IndexOf(level) is var at && at > 0)
            {
                var anchor = level.Anchor;
                CloseFrom(at);
                anchor?.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };

        if (node.Order is not null)
        {
            Orderable(level, view);
        }

        return view;
    }

    /// <summary>The button of a line, its parts in their columns (the ones given), 32 DIPs, inset 4.</summary>
    private static Button LineButton(UIElement? lead, TextBlock words, TextBlock value, UIElement? grip, UIElement? held, UIElement? trail)
    {
        var parts = new Grid();
        var column = 0;

        void Add(UIElement element, GridLength width)
        {
            parts.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            Grid.SetColumn((FrameworkElement)element, column++);
            parts.Children.Add(element);
        }

        if (lead is not null)
        {
            Add(lead, new GridLength(LeadColumn));
        }

        Add(words, new GridLength(1, GridUnitType.Star));

        // The value and the end column never meet: the value keeps a gap before the mark or chevron.
        value.VerticalAlignment = VerticalAlignment.Center;
        value.Margin = new Thickness(Fluent.Space6, 0, trail is null ? 0 : Fluent.Space2, 0);
        value.MaxWidth = ValueMaxWidth;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        Add(value, GridLength.Auto);

        if (grip is not null)
        {
            Add(grip, GridLength.Auto);
        }

        if (held is not null)
        {
            Add(held, GridLength.Auto);
        }

        if (trail is not null)
        {
            Add(trail, new GridLength(Fluent.IconSize));
        }

        var line = new Button
        {
            Content = parts,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
        };
        EditBar.Subtle(line);
        line.Height = LineHeight;
        line.Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0);
        return line;
    }

    /// <summary>
    /// [S-3731] An inline slider's line, 32 DIPs as every line: the icon column (where the level has
    /// icons), its words, the slider and its value, then the end column kept empty where the level has
    /// one, so its value stands in the values' column. [S-3734] The words keep their room: the track
    /// (100 DIPs) gives way to long words down to 64; words longer than that ("Gap between widgets")
    /// keep the line to themselves and their value, and the track runs under them, from where the
    /// words start to the value's end; only words longer than the whole line are trimmed. A drag shows
    /// the value (and the widgets, where the line previews them) and stores once on the release; a key
    /// steps it and stores the step; Up and Down move between the lines as on any other line.
    /// </summary>
    private (FrameworkElement Root, SliderView View) MakeSlider(Level level, MenuNode node)
    {
        var rule = node.Slider!;
        var parts = new Grid
        {
            Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0),
            Background = new SolidColorBrush(Colors.Transparent),
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(LineHeight) },

                // [S-3734] The track's row under long words; empty (no height) while it stands inline.
                new RowDefinition { Height = GridLength.Auto },
            },
        };
        var column = 0;

        void Add(FrameworkElement element, GridLength width)
        {
            parts.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            Grid.SetColumn(element, column++);
            parts.Children.Add(element);
        }

        if (level.Icons)
        {
            Add(new Border { Width = Fluent.IconSize, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Child = node.Icon?.Invoke() }, new GridLength(LeadColumn));
        }

        var words = new TextBlock
        {
            FontSize = FluentType.Body.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var wordsColumn = column;
        Add(words, new GridLength(1, GridUnitType.Star));

        var slider = new Slider
        {
            Width = SliderWidth,
            Minimum = rule.Min,
            Maximum = rule.Max,
            StepFrequency = rule.Step,
            SmallChange = rule.Step,
            LargeChange = rule.Step * 5,
            IsThumbToolTipEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Fluent.Space2, 0, 0, 0),
        };
        Add(slider, GridLength.Auto);

        var value = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        value.VerticalAlignment = VerticalAlignment.Center;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.TextAlignment = TextAlignment.Right;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        Add(value, new GridLength(SliderValueWidth));

        if (level.Trail)
        {
            Add(new Border { Width = Fluent.IconSize + Fluent.Space2 }, GridLength.Auto);
        }

        // [S-3734] What the words and the track share: the line less its icon, value and end columns.
        var room = MenuWidth - (2 * (Fluent.Space1 + Fluent.Space3)) - (2 * Fluent.Hairline) - (level.Icons ? LeadColumn : 0)
            - SliderValueWidth - (level.Trail ? Fluent.IconSize + Fluent.Space2 : 0) - Fluent.Space2;
        var view = new SliderView(node, slider, words, value, room, wordsColumn);
        ApplySlider(view, node);

        slider.ValueChanged += (_, e) =>
        {
            if (view.Writing)
            {
                return;
            }

            var held = view.Node.Slider!;
            view.Value.Text = held.Format(e.NewValue);
            if (view.Keyed)
            {
                view.Keyed = false;
                Run(() => held.Commit(e.NewValue));
                return;
            }

            view.Pending = true;
            Run(() => held.Preview(e.NewValue));
        };

        // A pointer's move is stored once, when it lets go; the slider marks those events handled.
        void Release()
        {
            if (view.Pending)
            {
                view.Pending = false;
                var held = view.Node.Slider!;
                Run(() => held.Commit(slider.Value));
            }
        }

        slider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => Release()), handledEventsToo: true);
        slider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, _) => Release()), handledEventsToo: true);

        slider.PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                // [S-3707] Up and Down move between the lines here too; the slider steps on the others.
                case Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down:
                    e.Handled = FocusManager.TryMoveFocus(
                        e.Key == Windows.System.VirtualKey.Up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down,
                        new FindNextElementOptions { SearchRoot = level.Root });
                    break;
                case Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Home
                    or Windows.System.VirtualKey.End or Windows.System.VirtualKey.PageUp or Windows.System.VirtualKey.PageDown:
                    view.Keyed = true;
                    break;
            }
        };
        slider.KeyUp += (_, _) => view.Keyed = false;

        return (parts, view);
    }

    /// <summary>[S-3731] Write a slider line's node in: its words, its value and where its thumb stands; never while it is dragged.</summary>
    private static void ApplySlider(SliderView view, MenuNode node)
    {
        view.Node = node;
        view.Words.Text = node.Text;
        view.Words.Foreground = ThemeBrush.Of(node.Disabled ? "TextFillColorDisabledBrush" : "TextFillColorPrimaryBrush", Colors.White);
        TipWhenTrimmed(view.Words);

        // [S-3734] The words keep their room: the track gives way to long words, down to its least;
        // words longer than that take the line, and the track goes under them.
        // Measured on its own, the words come out a few DIPs short of what the line draws: a gap's worth more.
        view.Words.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var beside = view.Room - view.Words.DesiredSize.Width - Fluent.Space2;
        if (beside >= SliderMinWidth)
        {
            Grid.SetRow(view.Slider, 0);
            Grid.SetColumn(view.Slider, view.WordsColumn + 1);
            Grid.SetColumnSpan(view.Slider, 1);
            view.Slider.Width = Math.Min(beside, SliderWidth);
            view.Slider.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        }
        else
        {
            Grid.SetRow(view.Slider, 1);
            Grid.SetColumn(view.Slider, view.WordsColumn);
            Grid.SetColumnSpan(view.Slider, 3);
            view.Slider.Width = double.NaN;
            view.Slider.HorizontalAlignment = HorizontalAlignment.Stretch;

            // The track sits close under its words, as one line of two rows, not two lines.
            view.Slider.Margin = new Thickness(0, -Fluent.Space2, 0, Fluent.Space1);
        }

        view.Slider.IsEnabled = !node.Disabled;
        AutomationProperties.SetName(view.Slider, node.Text);

        if (view.Pending)
        {
            return;
        }

        var rule = node.Slider!;
        view.Writing = true;
        try
        {
            view.Slider.Minimum = rule.Min;
            view.Slider.Maximum = rule.Max;
            view.Slider.StepFrequency = rule.Step;
            view.Slider.Value = rule.Value ?? rule.Min;
        }
        finally
        {
            view.Writing = false;
        }

        view.Value.Text = node.Value ?? string.Empty;
    }

    /// <summary>While its submenu is open a line stays lit, as Windows keeps it.</summary>
    private static void Lit(LineView view, bool on)
    {
        if (on)
        {
            view.Button.Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        }
        else
        {
            view.Button.ClearValue(Control.BackgroundProperty);
        }
    }

    /// <summary>
    /// [S-3728] A note: words only in the disabled colour, where a line's words start, wrapped to a
    /// few lines within the menu's width and trimmed past them, the whole as its tooltip. [S-3561]
    /// What went wrong is in the critical colour; something being done has a ring before its words;
    /// a note without words takes no room. Its words are written in place, so a note under a form's
    /// line changes as the form is typed in without the form being drawn again.
    /// </summary>
    private static NoteView NoteLine(MenuNode node, bool icons)
    {
        var words = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        words.TextWrapping = TextWrapping.WrapWholeWords;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.MaxLines = NoteLines;
        words.HorizontalAlignment = HorizontalAlignment.Left;
        words.VerticalAlignment = VerticalAlignment.Center;

        var ring = new ProgressRing
        {
            Width = Fluent.IconSize,
            Height = Fluent.IconSize,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, Fluent.Space2, 0),
        };

        var root = new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { ring, words } },
            Background = new SolidColorBrush(Colors.Transparent),
        };

        var view = new NoteView(root, words, ring, icons);
        ApplyNote(view, node);
        return view;
    }

    /// <summary>[S-3561] Write a note's node in: its words, its colour, its ring; no room at all without words.</summary>
    private static void ApplyNote(NoteView view, MenuNode node)
    {
        var text = node.Text;
        var empty = text.Length == 0;
        var busy = node.Busy && !empty;
        // Inside the menu's frame, whose border takes a hairline each side (a word at the end was cut).
        var room = MenuWidth - (2 * (Fluent.Space1 + Fluent.Space3)) - (2 * Fluent.Hairline) - (view.Icons ? LeadColumn : 0);

        view.Words.Text = text;
        view.Words.MaxWidth = busy ? room - Fluent.IconSize - Fluent.Space2 : room;
        view.Words.Foreground = node.Danger
            ? ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed)
            : ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);
        view.Words.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        view.Ring.IsActive = busy;
        view.Ring.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        view.Root.Padding = empty
            ? new Thickness(0)
            : new Thickness(Fluent.Space1 + Fluent.Space3 + (view.Icons ? LeadColumn : 0), Fluent.Space2, Fluent.Space1 + Fluent.Space3, Fluent.Space2);
        ToolTipService.SetToolTip(view.Words, empty ? null : text);
        AutomationProperties.SetName(view.Words, text);

        // What went wrong, or how a try is going, is said to a screen reader as it changes.
        AutomationProperties.SetLiveSetting(view.Words, node.Danger || node.Busy ? Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Assertive : Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Off);
    }

    /// <summary>A divider between groups: across the menu under an icon row, inset in the list.</summary>
    protected static Rectangle Divider(bool full) => new()
    {
        Height = Fluent.Hairline,
        Fill = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
        Margin = full ? new Thickness(0) : new Thickness(Fluent.Space1, Fluent.Space1, Fluent.Space1, Fluent.Space1),
        IsHitTestVisible = false,
    };

    protected static IconElement Glyph(string glyph) => UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize });

    /// <summary>One option of a choice of one: the dot while it holds.</summary>
    protected static MenuNode Option(string key, string text, string iconKey, Func<UIElement?>? icon, bool holds, Action pick)
        => new(key) { Text = text, IconKey = iconKey, Icon = icon, Checked = holds, Radio = true, Run = pick };

    /// <summary>A percentage in the language's own way of writing one ("42 %" where it spaces it).</summary>
    protected static string Percent(double fraction) => LocaleFormat.Percent(Strings.Current, (int)Math.Round(fraction * 100));
}
