using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>[S-3716] [S-3734] How a level's rows are drawn, Karpo's (Explorer's) metrics, and how a change of value is written into them.</summary>
internal partial class MenuPanel
{
    /// <summary>[S-3734] Inside a row's fill: the room before its icon (the icon then stands 16 DIPs in from the menu's edge).</summary>
    private const double PadStart = 11;

    /// <summary>[S-3734] Inside a row's fill: the room after the end slot (the chevron's box ends 13 DIPs in from the menu's edge).</summary>
    private const double PadEnd = 8;

    /// <summary>[S-3734] The icon's column: the 16-DIP icon and 12 to the words, which start 44 DIPs in.</summary>
    private const double LeadColumn = Fluent.IconSize + Fluent.Space3;

    /// <summary>[S-3734] The end slot (the chevron, the tick, a link's mark) and the gap before it, in which a value stands right-aligned.</summary>
    private const double SlotGap = Fluent.Space2;

    /// <summary>[S-3734] A row with no end slot ends its words 11 DIPs in from its fill's end, as one with ends its slot 8 in.</summary>
    private const double NoSlotEnd = PadStart - PadEnd;

    /// <summary>[S-3734] Between a row's words and its value.</summary>
    private const double WordsGap = Fluent.Space2;

    /// <summary>[S-3741] Windows' toggle switch, at a row's end.</summary>
    private const double SwitchWidth = 40;

    private const double SwitchHeight = 20;

    private const double KnobSize = 12;

    /// <summary>[S-3731] A slider row's compact track, and the least it gives way to long words; its value's column.</summary>
    private const double SliderWidth = 96;

    private const double SliderMinWidth = 64;

    private const double SliderValueWidth = 40;

    /// <summary>[S-3740] A heading row's height.</summary>
    private const double HeadingHeight = 32;

    /// <summary>[S-3734] A rule: one hairline with 4 DIPs over and under it.</summary>
    private const double RuleGap = 4;

    /// <summary>The chevron of a row with a level of its own: Windows' thin ChevronRight, at the menu's 12.</summary>
    private const double SmallGlyph = 12;

    /// <summary>[S-3556] What a password row shows while its form holds one: never its letters.</summary>
    protected const string MaskedValue = "●●●●●●●●";

    /// <summary>A drawn row: its node now, and the parts a change of value is written into.</summary>
    private sealed class LineView(MenuNode node, Button button, Border lead, Border end, Border held, TextBlock words, TextBlock value, Border valueIcon, double room)
    {
        /// <summary>[S-3734] What the words and the value share of the row: less its icon, grip, mark and end.</summary>
        public double Room { get; } = room;

        public MenuNode Node { get; set; } = node;

        public string IconKey { get; set; } = node.IconKey;

        public string ValueIconKey { get; set; } = node.ValueIconKey;

        public Button Button { get; } = button;

        public FrameworkElement Root => Button;

        public Border Lead { get; } = lead;

        /// <summary>[S-3716] The end: the chevron, the tick of the option that holds, a link's mark, or a switch.</summary>
        public Border End { get; } = end;

        /// <summary>[S-3541] Before the chevron: the tick of a row with a level of its own that stands for what holds (the view shown here).</summary>
        public Border Held { get; } = held;

        public TextBlock Words { get; } = words;

        public TextBlock Value { get; } = value;

        public Border ValueIcon { get; } = valueIcon;
    }

    /// <summary>[S-3731] A drawn slider row: its node now, and the parts a change of value is written into.</summary>
    private sealed class SliderView(MenuNode node, Slider slider, TextBlock words, TextBlock value, double room)
    {
        public MenuNode Node { get; set; } = node;

        /// <summary>[S-3734] What the words and the track share of the row.</summary>
        public double Room { get; } = room;

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

    /// <summary>The rows of the level shown (or of the foot): the panel they are drawn in and what each drawn row is.</summary>
    private sealed class Level(StackPanel lines, Canvas overlay)
    {
        public StackPanel Lines { get; } = lines;

        /// <summary>[S-3732] Over the rows: where a dragged row's drop line is drawn.</summary>
        public Canvas Overlay { get; } = overlay;

        /// <summary>[S-3731] The slider rows, by their place in <see cref="Nodes"/>.</summary>
        public Dictionary<int, SliderView> Sliders { get; } = [];

        /// <summary>[S-3561] The notes, by their place: their words are written in place.</summary>
        public Dictionary<int, NoteView> Notes { get; } = [];

        /// <summary>[S-3736] A text level's field.</summary>
        public FieldView? Field { get; set; }

        public List<LineView?> Views { get; } = [];

        public List<MenuNode> Nodes { get; set; } = [];

        public string Shape { get; set; } = string.Empty;

        /// <summary>[S-3734] Some row has a chevron, a tick or a link's mark: every row keeps the end slot, so the values stand in one column.</summary>
        public bool Slot { get; set; }

        public bool Icons { get; set; }
    }

    /// <summary>[S-3743] A level's rows: their panel takes a dragged row's pointer, and the arrows move between them.</summary>
    private static StackPanel NewLines() => new()
    {
        Background = new SolidColorBrush(Colors.Transparent),
        XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled,
    };

    /// <summary>[S-3732] The layer over a level's rows a dragged row's drop line is drawn on.</summary>
    private static Canvas Overlay() => new() { IsHitTestVisible = false };

    /// <summary>Which rows there are, as one string: values (switches, ticks, words at the end, icons) are not in it.</summary>
    /// <remarks>
    /// [S-3561] Whether a row is greyed is written in place too (a form's Add while its fields are
    /// typed in), but for a danger row, whose colour is chosen as it is built.
    /// </remarks>
    private static string Shape(IReadOnlyList<MenuNode> nodes)
        => string.Join(";", nodes.Select(n => n.Key + "|" + (int)n.Kind + "|" + (n.Danger && n.Disabled ? 'd' : 'e') + "|"
            + (n.Opens ? 's' : '-') + (n.Switch ? 'w' : '-') + (n.Checked is null ? '-' : 'c') + (n.Icon is null ? '-' : 'i') + (n.ValueIcon is null ? '-' : 'v')
            + (n.Danger && n.Kind != MenuNodeKind.Note ? '!' : '-') + (n.Order is null ? '-' : 'o') + (n.Link is null ? '-' : 'l') + (n.Tools is null ? "-" : string.Join(",", n.Tools.Select(t => t.Key + (t.Disabled ? "~" : string.Empty))))));

    /// <summary>[S-3742] Whether a level of <paramref name="rows"/> is a long list a filter field heads: more than ten rows to pick and no field of its own.</summary>
    internal static bool Searchable(IReadOnlyList<MenuNode> rows)
        => rows.All(r => r.Kind is not (MenuNodeKind.Field or MenuNodeKind.Custom)) && rows.Count(Listed) > FilterFrom;

    /// <summary>[S-3742] A row a filter finds: one to pick, with words of its own.</summary>
    private static bool Listed(MenuNode row) => row.Kind is MenuNodeKind.Line or MenuNodeKind.Slider && row.Text.Length > 0;

    /// <summary>Draw <paramref name="nodes"/> into <paramref name="level"/>, replacing what it held.</summary>
    private void FillLevel(Level level, IReadOnlyList<MenuNode> nodes)
    {
        level.Lines.Children.Clear();
        level.Views.Clear();
        level.Sliders.Clear();
        level.Notes.Clear();
        level.Field = null;
        level.Nodes = [.. nodes];
        level.Shape = Shape(nodes);
        level.Slot = nodes.Any(n => n.Kind == MenuNodeKind.Line && (n.Opens || n.Link is not null || (n.Checked is not null && n.Radio)));
        level.Icons = nodes.Any(n => n.Kind is MenuNodeKind.Line or MenuNodeKind.Slider && n.Icon is not null);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            switch (node.Kind)
            {
                case MenuNodeKind.Separator:
                    level.Lines.Children.Add(Divider(full: false));
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Heading:
                    level.Lines.Children.Add(HeadingRow(node));
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Note:
                    var note = NoteLine(node, level.Icons);
                    level.Lines.Children.Add(note.Root);
                    level.Notes[i] = note;
                    level.Views.Add(null);
                    break;
                case MenuNodeKind.Field:
                    var field = MakeField(node);
                    level.Lines.Children.Add(field.Root);
                    level.Field = field;
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

        // Leading, trailing and doubled rules go, as the card menu drops them ([S-1742]).
        HideStrayDividers(level.Lines);

        // [S-3748] A keyboard move drew the list again: the moved row takes the keyboard back.
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

    /// <summary>Write the values of <paramref name="nodes"/> (same shape) into the rows drawn for the ones before.</summary>
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
        view.Value.Foreground = ThemeBrush.Of(node.Disabled ? "TextFillColorDisabledBrush" : "TextFillColorSecondaryBrush", Colors.Gray);

        if (!string.Equals(view.IconKey, node.IconKey, StringComparison.Ordinal))
        {
            view.IconKey = node.IconKey;
            view.Lead.Child = node.Icon?.Invoke();
        }

        if (!string.Equals(view.ValueIconKey, node.ValueIconKey, StringComparison.Ordinal))
        {
            view.ValueIconKey = node.ValueIconKey;
            view.ValueIcon.Child = node.ValueIcon?.Invoke();
        }

        view.ValueIcon.Visibility = view.ValueIcon.Child is null ? Visibility.Collapsed : Visibility.Visible;
        Fit(view);
        TipWhenTrimmed(view.Words);
        TipWhenTrimmed(view.Value);

        // [S-3741] A switch's state; [S-3718] the tick of the option that holds at the far end;
        // [S-3541] a row with a level of its own that holds has its tick before the chevron.
        if (node.Switch)
        {
            view.End.Child = SwitchArt(node.Checked == true, !node.Disabled);
        }
        else if (!node.Opens && node.Link is null)
        {
            view.End.Child = node.Checked == true ? Tick() : null;
        }
        else if (node.Opens)
        {
            view.Held.Child = node.Checked == true ? Tick() : null;
        }

        AutomationProperties.SetName(view.Button, node.Value is { Length: > 0 } value ? node.Text + ", " + value : node.Text);
        if (node.Checked is { } on)
        {
            AutomationProperties.SetItemStatus(view.Button, node.Switch ? (on ? Str.Toggle_On.Text() : Str.Toggle_Off.Text()) : on ? "✓" : string.Empty);
        }
    }

    /// <summary>
    /// [S-3734] A row's words and its value share its room (Karpo's rule): both whole where both fit;
    /// else the shorter keeps all it needs where that is within its share (the value two fifths, the
    /// words the rest) and the other has what is left; else each its share, both trimmed.
    /// </summary>
    private static void Fit(LineView view)
    {
        view.Words.MaxWidth = double.PositiveInfinity;
        view.Value.MaxWidth = double.PositiveInfinity;
        var valueIcon = view.ValueIcon.Child is null ? 0 : Fluent.IconSize + WordsGap;
        var room = view.Room - valueIcon;
        if (view.Value.Text.Length == 0)
        {
            view.Words.MaxWidth = Math.Max(0, room);
            view.Value.Margin = new Thickness(0);
            return;
        }

        view.Words.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        view.Value.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (words, value) = MenuShare.Share(room - WordsGap, Math.Ceiling(view.Words.DesiredSize.Width), Math.Ceiling(view.Value.DesiredSize.Width));
        view.Words.MaxWidth = words;
        view.Value.MaxWidth = value;
        view.Value.Margin = new Thickness(WordsGap, 0, 0, 0);
    }

    /// <summary>[S-3734] Words cut short by the menu's one width show in full as their tooltip; words that fit have none.</summary>
    private static void TipWhenTrimmed(TextBlock text)
    {
        if (text.Tag is null)
        {
            text.Tag = Scope;
            text.IsTextTrimmedChanged += (sender, _) => ToolTipService.SetToolTip(sender, sender.IsTextTrimmed ? sender.Text : null);
        }

        ToolTipService.SetToolTip(text, text.IsTextTrimmed ? text.Text : null);
    }

    /// <summary>[S-3718] The tick of the option that holds, at a row's far end, as Windows draws it.</summary>
    private static FontIcon Tick() => new() { Glyph = Glyphs.Done, FontSize = SmallGlyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// [S-3741] Windows' toggle switch, drawn: a 40 × 20 track with a 12-DIP knob, the accent filled
    /// while on. Art only: the whole row is the switch, so a click anywhere on it flips it.
    /// </summary>
    private static Grid SwitchArt(bool on, bool enabled)
    {
        string track, stroke, knob;
        if (on)
        {
            track = enabled ? "AccentFillColorDefaultBrush" : "AccentFillColorDisabledBrush";
            stroke = track;
            knob = enabled ? "TextOnAccentFillColorPrimaryBrush" : "TextOnAccentFillColorDisabledBrush";
        }
        else
        {
            track = enabled ? "ControlAltFillColorSecondaryBrush" : "ControlAltFillColorDisabledBrush";
            stroke = enabled ? "ControlStrongStrokeColorDefaultBrush" : "ControlStrongStrokeColorDisabledBrush";
            knob = enabled ? "TextFillColorSecondaryBrush" : "TextFillColorDisabledBrush";
        }

        return new Grid
        {
            Width = SwitchWidth,
            Height = SwitchHeight,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Children =
            {
                new Border
                {
                    CornerRadius = new CornerRadius(SwitchHeight / 2),
                    Background = ThemeBrush.Of(track, Colors.Gray),
                    BorderBrush = ThemeBrush.Of(stroke, Colors.Gray),
                    BorderThickness = new Thickness(Fluent.Hairline),
                },
                new Ellipse
                {
                    Width = KnobSize,
                    Height = KnobSize,
                    Fill = ThemeBrush.Of(knob, Colors.White),
                    HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness((SwitchHeight - KnobSize) / 2, 0, (SwitchHeight - KnobSize) / 2, 0),
                },
            },
        };
    }

    /// <summary>
    /// [S-3716] [S-3734] One row: its icon (where the level has icons), its words, its value in the
    /// secondary colour right-aligned 8 DIPs before the end slot, and at the end the chevron of a row
    /// with a level of its own, the tick of the option that holds, a link's "open in new window"
    /// mark ([S-3749]) or a switch ([S-3741]). A danger row's words and icon are the critical colour;
    /// [S-3732] a row of an ordered list carries a grip before its end and is dragged.
    /// </summary>
    private LineView MakeLine(Level level, MenuNode node)
    {
        var lead = new Border { Width = Fluent.IconSize, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Child = node.Icon?.Invoke() };
        var end = new Border { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        if (node.Opens)
        {
            end.Child = new FontIcon
            {
                Glyph = Glyphs.ChevronRight,
                FontSize = SmallGlyph,
                MirroredWhenRightToLeft = true,
                Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else if (node.Link is not null)
        {
            end.Child = new FontIcon
            {
                Glyph = Glyphs.OpenExternal,
                FontSize = SmallGlyph,
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
            HorizontalAlignment = HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        var value = new TextBlock
        {
            FontSize = FluentType.Body.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        var valueIcon = new Border { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(WordsGap, 0, 0, 0) };

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
            FontSize = SmallGlyph,
            Foreground = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Fluent.Space2, 0, 0, 0),
        };

        // [S-3549] A thing's tools, before its grip: 24-DIP subtle squares, their names the tooltips.
        LineView? made = null;
        var tools = node.Tools is { Count: > 0 } own ? ToolsOf(() => made?.Node ?? node, own) : null;

        // [S-3541] The tick's place is kept on every row with a level of its own that can hold, so the grips stand in one column.
        var held = new Border
        {
            VerticalAlignment = VerticalAlignment.Center,
            Width = node.Opens && node.Checked is not null ? Fluent.IconSize : 0,
            Margin = new Thickness(node.Opens && node.Checked is not null ? SlotGap : 0, 0, 0, 0),
        };

        // What the row ends with: the switch, the slot (kept where the level has one), or the room a row with no slot keeps.
        double endWidth;
        if (node.Switch)
        {
            end.Width = SwitchWidth;
            end.Margin = new Thickness(SlotGap, 0, 0, 0);
            endWidth = SwitchWidth + SlotGap;
        }
        else if (level.Slot)
        {
            end.Width = Fluent.IconSize;
            end.Margin = new Thickness(SlotGap, 0, 0, 0);
            endWidth = Fluent.IconSize + SlotGap;
        }
        else
        {
            end.Width = 0;
            end.Margin = new Thickness(0, 0, NoSlotEnd, 0);
            endWidth = NoSlotEnd;
        }

        var button = LineButton(level.Icons ? lead : null, words, value, valueIcon, tools, grip, held, end);
        button.IsEnabled = !node.Disabled;

        // [S-3734] What the words and the value share: the fill less its padding, icon, grip, mark and end.
        var room = OneWidth - (2 * (RowInset + Fluent.Hairline)) - PadStart - PadEnd - (level.Icons ? LeadColumn : 0) - endWidth
            - (grip is null ? 0 : Fluent.Space2 + SmallGlyph) - (held.Width > 0 ? Fluent.IconSize + SlotGap : 0)
            - (tools is null ? 0 : Fluent.Space2 + (node.Tools!.Count * (ToolSize + ToolGap)));
        var view = new LineView(node, button, lead, end, held, words, value, valueIcon, room);
        made = view;
        button.Tag = view;
        Apply(view, node);

        if (node.Link is { } owner)
        {
            var hint = Str.Menu_OpensIn.Format(("name", owner));
            ToolTipService.SetToolTip(button, hint);
            AutomationProperties.SetHelpText(button, hint);
        }

        button.Click += (_, _) => OnLine(view);

        if (node.Order is not null)
        {
            Orderable(level, view);
        }

        return view;
    }

    /// <summary>[S-3549] A tool's square inside a row's 28-DIP fill, and the room between two.</summary>
    private const double ToolSize = 24;

    private const double ToolGap = 2;

    /// <summary>
    /// [S-3549] A thing's tools: subtle 24-DIP squares, each its glyph at 12 with its name (and key) as
    /// its tooltip and what Narrator reads; a click runs it with the row in hand, so what it asks opens
    /// as the row's level, as the row holds it now (a rename written in place). Not tab stops: the
    /// row's keys (F2, Delete) reach them.
    /// </summary>
    private StackPanel ToolsOf(Func<MenuNode> current, IReadOnlyList<MenuTool> tools)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ToolGap, Margin = new Thickness(Fluent.Space2, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < tools.Count; i++)
        {
            var tool = tools[i];
            var at = i;
            var button = new Button
            {
                Content = UiLanguage.Mirrored(new FontIcon { Glyph = tool.Glyph, FontSize = SmallGlyph }),
                IsEnabled = !tool.Disabled,
                IsTabStop = false,
            };
            EditBar.Subtle(button);
            button.Width = ToolSize;
            button.Height = ToolSize;
            button.MinHeight = ToolSize;
            button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            var tip = tool.Shortcut is { } key ? Str.Shell_Menu_LabelWithShortcut.Format(("label", tool.Name), ("shortcut", key == Windows.System.VirtualKey.F2 ? "F2" : KeyChordText.Format(KeyChord.Plain(ChordKey.Delete)))) : tool.Name;
            ToolTipService.SetToolTip(button, tip);
            AutomationProperties.SetName(button, tool.Name);
            button.Click += (_, _) =>
            {
                var row = current();
                if (row.Tools is { } now && at < now.Count && !now[at].Disabled)
                {
                    _askKeys = false;
                    Ask(row, now[at].Run);
                }
            };
            panel.Children.Add(button);
        }

        return panel;
    }

    /// <summary>[S-3734] The button of a row: a 28-DIP fill 2 DIPs inside its 32, inset 5 from the menu's sides, its parts in their columns.</summary>
    private static Button LineButton(UIElement? lead, TextBlock words, TextBlock value, UIElement valueIcon, UIElement? tools, UIElement? grip, UIElement held, UIElement end)
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
        Add(value, GridLength.Auto);
        Add(valueIcon, GridLength.Auto);
        if (tools is not null)
        {
            Add(tools, GridLength.Auto);
        }

        if (grip is not null)
        {
            Add(grip, GridLength.Auto);
        }

        Add(held, GridLength.Auto);
        Add(end, GridLength.Auto);

        var line = new Button
        {
            Content = parts,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(RowInset, (LineHeight - FillHeight) / 2, RowInset, (LineHeight - FillHeight) / 2),
        };
        EditBar.Subtle(line);
        line.Height = FillHeight;
        line.MinHeight = FillHeight;
        line.Padding = new Thickness(PadStart, 0, PadEnd, 0);
        return line;
    }

    /// <summary>
    /// [S-3718] A level's head, an ordinary row: the back arrow (mirrored right to left) and the
    /// level's name in the semibold; a click, Enter or Space on it goes back. "Back" is its tooltip and
    /// what Narrator calls it.
    /// </summary>
    private Button HeadRow(Step step)
    {
        var words = new TextBlock
        {
            Text = step.Title,
            FontSize = FluentType.Body.FontSize,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        var lead = new Border
        {
            Width = Fluent.IconSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FontIcon { Glyph = Glyphs.Back, FontSize = Fluent.IconSize, MirroredWhenRightToLeft = true },
        };
        var button = LineButton(lead, words, new TextBlock(), new Border(), null, null, new Border(), new Border { Margin = new Thickness(0, 0, NoSlotEnd, 0) });
        var view = new LineView(HeadNode, button, lead, new Border(), new Border(), words, new TextBlock(), new Border(), 0);
        button.Tag = view;
        var back = Str.Common_Back.Text();
        ToolTipService.SetToolTip(button, back);
        AutomationProperties.SetName(button, back);
        AutomationProperties.SetHelpText(button, step.Title);
        button.Click += (_, _) => OnLine(view);
        return button;
    }

    /// <summary>[S-3742] [S-3722] A field pinned under a level's head: it fills a row's 28-DIP fill.</summary>
    private static Border BoxRow(Control box)
    {
        box.Height = FillHeight;
        box.MinHeight = FillHeight;
        box.Padding = new Thickness(Fluent.Space2 + 2, 3, Fluent.Space1 + 2, 3);
        box.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Child = box,
            Margin = new Thickness(RowInset, (LineHeight - FillHeight) / 2, RowInset, (LineHeight - FillHeight) / 2),
        };
    }

    /// <summary>[S-3740] A heading over a group: 12-DIP semibold words in the secondary colour, 32 DIPs tall; an untitled one is a rule.</summary>
    private static FrameworkElement HeadingRow(MenuNode node)
    {
        if (node.Text.Length == 0)
        {
            return Divider(full: false);
        }

        var words = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        words.FontWeight = FontWeights.SemiBold;
        words.Text = node.Text;
        words.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Height = HeadingHeight,
            Padding = new Thickness(RowInset + PadStart, 0, RowInset + PadStart, 0),
            Child = words,
        };
    }

    /// <summary>
    /// [S-3731] A slider's row, one row as every row (user request 2026-10-26: "every row one entry
    /// high"): the icon (where the level has icons), its words, a compact track (96 DIPs, giving way to
    /// long words down to 64; words longer still are trimmed, the whole their tooltip) and its value
    /// right-aligned in the values' column. A drag shows the value (and the widgets, where the row
    /// previews them) and stores once on the release; a key steps it and stores the step; Up and Down
    /// move between the rows as on any other.
    /// </summary>
    private (FrameworkElement Root, SliderView View) MakeSlider(Level level, MenuNode node)
    {
        var rule = node.Slider!;
        var parts = new Grid
        {
            Height = FillHeight,
            Margin = new Thickness(RowInset, (LineHeight - FillHeight) / 2, RowInset, (LineHeight - FillHeight) / 2),
            Padding = new Thickness(PadStart, 0, PadEnd, 0),
            Background = new SolidColorBrush(Colors.Transparent),
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
            HorizontalAlignment = HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
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
            Margin = new Thickness(WordsGap, 0, 0, 0),
        };
        Add(slider, GridLength.Auto);

        var value = new TextBlock
        {
            FontSize = FluentType.Body.FontSize,
            Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Add(value, new GridLength(SliderValueWidth + WordsGap));

        // The value stands in the values' column: the end slot kept where the level has one.
        var endWidth = level.Slot ? Fluent.IconSize + SlotGap : NoSlotEnd;
        Add(new Border(), new GridLength(endWidth));

        // [S-3734] What the words and the track share: the fill less its padding, icon, value and end.
        var room = OneWidth - (2 * (RowInset + Fluent.Hairline)) - PadStart - PadEnd - (level.Icons ? LeadColumn : 0) - (SliderValueWidth + WordsGap) - endWidth - WordsGap;
        var view = new SliderView(node, slider, words, value, room);
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
                // [S-3707] Up and Down move between the rows here too; the slider steps on the others.
                case Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down:
                    e.Handled = FocusManager.TryMoveFocus(
                        e.Key == Windows.System.VirtualKey.Up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down,
                        new FindNextElementOptions { SearchRoot = _body });
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

    /// <summary>[S-3731] Write a slider row's node in: its words, its value and where its thumb stands; never while it is dragged.</summary>
    private static void ApplySlider(SliderView view, MenuNode node)
    {
        view.Node = node;
        view.Words.Text = node.Text;
        view.Words.Foreground = ThemeBrush.Of(node.Disabled ? "TextFillColorDisabledBrush" : "TextFillColorPrimaryBrush", Colors.White);
        TipWhenTrimmed(view.Words);

        // [S-3734] The words keep their room: the track gives way to long words, down to its least;
        // words longer than that are trimmed, the row never grows.
        view.Words.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        view.Slider.Width = Math.Clamp(view.Room - Math.Ceiling(view.Words.DesiredSize.Width), SliderMinWidth, SliderWidth);
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

    /// <summary>[S-3561] A drawn note: the parts its node is written into.</summary>
    private sealed class NoteView(Border root, TextBlock words, ProgressRing ring, bool icons)
    {
        public Border Root { get; } = root;

        public TextBlock Words { get; } = words;

        public ProgressRing Ring { get; } = ring;

        /// <summary>Its level has an icon column: its words start where a row's do.</summary>
        public bool Icons { get; } = icons;
    }

    /// <summary>
    /// [S-3728] A note: words only, in the tertiary colour and the 12-DIP size, where a row's words
    /// start, wrapped in full within the menu's width; never lit, never picked, skipped by the keys.
    /// [S-3561] What went wrong is in the critical colour; something being done has a ring before its
    /// words; a note without words takes no room. [S-3737] A question's own words are the text's colour
    /// and size. Its words are written in place, so a note under a form's row changes as the form is
    /// typed in without the form being drawn again.
    /// </summary>
    private static NoteView NoteLine(MenuNode node, bool icons)
    {
        var words = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        words.TextWrapping = TextWrapping.WrapWholeWords;
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
        var start = RowInset + PadStart + (view.Icons ? LeadColumn : 0);
        var room = OneWidth - (2 * Fluent.Hairline) - start - (RowInset + PadStart);

        view.Words.Text = text;
        view.Words.MaxWidth = busy ? room - Fluent.IconSize - Fluent.Space2 : room;
        if (node.Strong)
        {
            view.Words.Apply(FluentType.Body);
        }

        view.Words.Foreground = node.Danger
            ? ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed)
            : ThemeBrush.Of(node.Strong ? "TextFillColorPrimaryBrush" : "TextFillColorTertiaryBrush", Colors.Gray);
        view.Words.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        view.Ring.IsActive = busy;
        view.Ring.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        view.Root.Padding = empty
            ? new Thickness(0)
            : new Thickness(start, Fluent.Space1, RowInset + PadStart, Fluent.Space1 + 2);
        AutomationProperties.SetName(view.Words, text);

        // What went wrong, or how a try is going, is said to a screen reader as it changes.
        AutomationProperties.SetLiveSetting(view.Words, node.Danger || node.Busy ? Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Assertive : Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Off);
    }

    /// <summary>
    /// [S-3734] A rule between groups: a hairline with 4 DIPs over and under it, inset 5 from the
    /// menu's sides; under the menu's top row the menu's full width.
    /// </summary>
    protected static Rectangle Divider(bool full) => new()
    {
        Height = Fluent.Hairline,
        Fill = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
        Margin = full ? new Thickness(0) : new Thickness(RowInset, RuleGap, RowInset, RuleGap),
        IsHitTestVisible = false,
    };

    protected static IconElement Glyph(string glyph) => UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize });

    /// <summary>One option of a choice of one: the tick at the far end while it holds.</summary>
    protected static MenuNode Option(string key, string text, string iconKey, Func<UIElement?>? icon, bool holds, Action pick)
        => new(key) { Text = text, IconKey = iconKey, Icon = icon, Checked = holds, Radio = true, Run = pick };

    /// <summary>A percentage in the language's own way of writing one ("42 %" where it spaces it).</summary>
    protected static string Percent(double fraction) => LocaleFormat.Percent(Strings.Current, (int)Math.Round(fraction * 100));
}
