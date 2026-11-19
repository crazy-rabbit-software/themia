using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>[S-2302] [S-2305] A page's rows, one builder per kind.</summary>
internal sealed partial class PageView
{
    /// <summary>The thin chevron at a row's end.</summary>
    private const double Chevron = ShellMetrics.Chevron;

    /// <summary>A row: its body in a border that carries its key (a deep link lights it).</summary>
    private FrameworkElement RowOf(PageRow row, bool inside, bool first)
    {
        // [S-2317] A card's own lines carry no glyph of their setting, as Karpo's (§644): their names stand where every
        // row's does; a mark or a picture of their own stays, and a group's head keeps its glyph.
        if (inside && !row.Marked && row.Kind is not (SettingsRowKind.Heading or SettingsRowKind.Radio) && !Foot(row))
        {
            row = row with { Icon = null };
        }

        FrameworkElement body = row.Kind switch
        {
            SettingsRowKind.Toggle => ToggleRow(row),
            SettingsRowKind.Number => Lay(row, NumberBox(row), row.Caption),
            SettingsRowKind.Text => Lay(row, TextBox(row), _refused.GetValueOrDefault(row.Key) ?? row.Caption, critical: _refused.ContainsKey(row.Key)),
            SettingsRowKind.Action => Foot(row) ? FootButton(row, inside) : ActionRow(row),
            SettingsRowKind.Link => LinkRow(row),
            SettingsRowKind.Item => ItemRow(row),
            SettingsRowKind.Note => NoteRow(row, first),
            SettingsRowKind.Heading => HeadingRow(row),
            SettingsRowKind.Radio => RadioRow(row),
            SettingsRowKind.SubPage => SubPageRow(row),
            SettingsRowKind.Choice or SettingsRowKind.Lookup or SettingsRowKind.Colour or SettingsRowKind.Font or SettingsRowKind.Icon or SettingsRowKind.Expander
                => Expander(row, inside),
            SettingsRowKind.Preview => PreviewRow(row),
            _ => new Border(),
        };

        return new Border { Child = body, Tag = row.Key + "#row", CornerRadius = new CornerRadius(P.CardRadius) };
    }

    /// <summary>
    /// [S-2303] [S-11755] A sub-page's row: the whole row opens it; its icon buttons stand before the chevron, their keys working while the row has the keyboard (Delete).
    /// </summary>
    private Button SubPageRow(PageRow row)
    {
        var button = ClickRow(row, SubPageEnd(row), () => Push(row));
        if (row.Tools is { Count: > 0 } tools)
        {
            button.KeyDown += (_, e) =>
            {
                if (tools.FirstOrDefault(t => t.Shortcut == e.Key && !t.Disabled) is { } keyed)
                {
                    Do(keyed.Run);
                    e.Handled = true;
                }
            };
        }

        return button;
    }

    /// <summary>[S-2317] A sub-page's end: its icon buttons 4 apart, its value in Caption, the chevron (a link's).</summary>
    private StackPanel? SubPageEnd(PageRow row)
    {
        var end = End(row.Value, Glyphs.ChevronRight)!;
        var at = 0;
        foreach (var tool in row.Tools ?? [])
        {
            var button = ToolButton(row, tool);
            button.Margin = new Thickness(0, 0, Fluent.Space1, 0);
            end.Children.Insert(at++, button);
        }

        return end;
    }

    /// <summary>[S-3549] An icon button at a row's end: its words the tooltip and its name.</summary>
    private static Button ToolButton(PageRow row, PageTool tool)
    {
        var button = Plain(new Button
        {
            Content = UiLanguage.Mirrored(new FontIcon { Glyph = tool.Glyph, FontSize = Fluent.IconSize }),
            Width = P.Control,
            Height = P.Control,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = !tool.Disabled,
            Tag = row.Key + "#" + tool.Key,
        });
        button.HorizontalAlignment = HorizontalAlignment.Right;
        ToolTipService.SetToolTip(button, tool.Name);
        AutomationProperties.SetName(button, tool.Name);
        var run = tool.Run;
        button.Click += (_, e) => Do(run);
        return button;
    }

    /// <summary>[S-2307] A page of its own under this one: the keyboard on its first control.</summary>
    private void Push(PageRow row)
    {
        _steps.Add(new SubStep(row.Key));
        _open = null;
        _unfolded.Clear();
        Show();
        FocusFirst();
    }

    /// <summary>
    /// [S-2305] A row's three columns: its icon at 16, its name at 48 (a caption under it makes the row
    /// 60 tall), its control ending 16 from the card's end; [S-2317] a thing put in order its handle
    /// (<paramref name="lead"/>) 4 in at its start, its icon and name 12 further in (Karpo's Â§551).
    /// </summary>
    private static Grid Lay(PageRow row, UIElement? end, string? caption, bool critical = false, string? text = null, FrameworkElement? lead = null)
    {
        var further = lead is null ? 0 : P.HandleLead;
        var grid = new Grid
        {
            MinHeight = SettingsRows.Height(caption is { Length: > 0 }),
            Padding = new Thickness(P.IconAt + further, 0, P.EndInset, 0),
            ColumnSpacing = 0,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(P.NameAt - P.IconAt) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        if (lead is not null)
        {
            lead.HorizontalAlignment = HorizontalAlignment.Left;
            lead.VerticalAlignment = VerticalAlignment.Center;
            lead.Margin = new Thickness(P.HandleIn - P.IconAt - further, 0, 0, 0);
            grid.Children.Add(lead);
        }

        if (row.Icon?.Invoke() is { } icon)
        {
            if (icon is FrameworkElement shaped)
            {
                shaped.VerticalAlignment = VerticalAlignment.Center;
                shaped.HorizontalAlignment = HorizontalAlignment.Left;
            }

            if (icon is IconElement glyph && row.Danger)
            {
                glyph.Foreground = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
            }

            grid.Children.Add(new Border { Width = Fluent.IconSize, Height = Fluent.IconSize, Child = icon, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left });
        }

        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Fluent.Space3, 0) };
        var name = BodyText(text ?? row.Text, row.Danger);
        if (row.Disabled)
        {
            // Greyed as Windows greys a setting that cannot be changed now.
            name.Foreground = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray);
        }

        words.Children.Add(name);
        if (caption is { Length: > 0 })
        {
            words.Children.Add(CaptionText(caption, critical));
        }

        Grid.SetColumn(words, 1);
        grid.Children.Add(words);

        if (end is FrameworkElement control)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(control, 2);
            grid.Children.Add(control);
        }


        return grid;
    }

    /// <summary>
    /// [S-2317] What stands at a row's end, as Karpo's painter sets it: its value in Caption and the secondary
    /// colour (a link's, a reading's, a shut expander's), 12 before the chevron's room; the chevron's middle 20
    /// in from the control's end.
    /// </summary>
    private static StackPanel? End(string? value, string? glyph)
    {
        if (value is null && glyph is null)
        {
            return null;
        }

        var end = new StackPanel { Orientation = Orientation.Horizontal };
        if (value is { Length: > 0 })
        {
            var words = Said(value, FamilyType.Caption);
            if (glyph is not null)
            {
                words.Margin = ToChevron;
            }

            end.Children.Add(words);
        }

        if (glyph is not null)
        {
            end.Children.Add(ChevronOf(glyph));
        }

        return end;
    }

    /// <summary>[S-2317] A value said at a row's end in <paramref name="type"/>, the secondary colour, cut short past the value column.</summary>
    private static TextBlock Said(string value, Themia.Core.Widgets.TypeStep type)
    {
        var words = Fluent.Text(type, "TextFillColorSecondaryBrush");
        words.Text = value;
        words.MaxWidth = P.ControlWide;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.VerticalAlignment = VerticalAlignment.Center;
        return words;
    }

    /// <summary>
    /// [S-2317] A chevron whose middle stands 20 in from its control's end (Karpo's <c>ChevronIn</c>): a 12 glyph,
    /// 14 off the end.
    /// </summary>
    private static FrameworkElement ChevronOf(string glyph)
    {
        var chevron = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Chevron, VerticalAlignment = VerticalAlignment.Center });
        chevron.Margin = new Thickness(0, 0, P.ChevronOff, 0);
        return chevron;
    }

    /// <summary>[S-2317] What stands before a chevron's room (a value, a thing's last button) ends 12 before it: Karpo's <c>Label</c> before <c>ChevronRoom</c>.</summary>
    private static Thickness ToChevron => new(0, 0, P.ChevronRoom + P.Label - P.ChevronOff - Chevron, 0);

    /// <summary>[S-2317] The last of a thing's buttons ends where the chevron's room starts.</summary>
    private static Thickness ToChevronRoom => new(0, 0, P.ChevronRoom - P.ChevronOff - Chevron, 0);

    /// <summary>
    /// A row that is one button (an action, a link, a sub-page, a picker's head): no fill at rest, the
    /// subtle fills under the pointer and pressed, the whole row the target.
    /// </summary>
    private static T Plain<T>(T button)
        where T : ButtonBase
    {
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Stretch;
        button.Padding = new Thickness(0);
        button.BorderThickness = new Thickness(0);
        button.CornerRadius = new CornerRadius(P.CardRadius);
        button.MinHeight = 0;

        var clear = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        var hover = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        var pressed = ThemeBrush.Of("SubtleFillColorTertiaryBrush", Colors.DimGray);
        var text = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
        var name = button is ToggleButton ? "ToggleButton" : "Button";
        foreach (var state in new[] { string.Empty, "Checked" })
        {
            if (state.Length > 0 && button is not ToggleButton)
            {
                continue;
            }

            button.Resources[name + "Background" + state] = clear;
            button.Resources[name + "Background" + state + "PointerOver"] = hover;
            button.Resources[name + "Background" + state + "Pressed"] = pressed;
            button.Resources[name + "Background" + state + "Disabled"] = clear;
            button.Resources[name + nameof(Control.BorderBrush) + state] = clear;
            button.Resources[name + nameof(Control.BorderBrush) + state + "PointerOver"] = clear;
            button.Resources[name + nameof(Control.BorderBrush) + state + "Pressed"] = clear;
            button.Resources[name + nameof(Control.BorderBrush) + state + "Disabled"] = clear;
            button.Resources[name + "Foreground" + state] = text;
            button.Resources[name + "Foreground" + state + "PointerOver"] = text;
            button.Resources[name + "Foreground" + state + "Pressed"] = text;
        }

        return button;
    }

    /// <summary>[S-2302] A row that is a click: an action, a sub-page; a danger's words in the critical colour.</summary>
    private Button ClickRow(PageRow row, UIElement? end, Action? run)
    {
        var button = Plain(new Button { Content = Lay(row, end, row.Caption), IsEnabled = !row.Disabled, Tag = row.Key + "#head" });
        AutomationProperties.SetName(button, Said(row));
        button.Click += (_, _) => Do(run);
        return button;
    }

    /// <summary>
    /// [S-2318] A row that only acts with nothing to say before its button (Karpo's <c>PanelRow.Foot</c>, Â§578): its
    /// button alone, no card round it.
    /// </summary>
    private static bool Foot(PageRow row) => row.Kind == SettingsRowKind.Action && row.Caption is not { Length: > 0 };

    /// <summary>
    /// [S-2318] A row that says what it does (Karpo's Â§521): a card, its icon and what it does in Body before a
    /// standard button at its end whose words are the row's (12 either side, 120 to 240 wide; a chevron after
    /// them where its click asks first), its value in Caption 12 before the button.
    /// </summary>
    private FrameworkElement ActionRow(PageRow row)
    {
        var end = new StackPanel { Orientation = Orientation.Horizontal };
        if (row.Value is { Length: > 0 } value)
        {
            var said = Said(value, FamilyType.Caption);
            said.Margin = new Thickness(0, 0, P.Label, 0);
            end.Children.Add(said);
        }

        end.Children.Add(RowButton(row, row.Text, row.Asks ? Glyphs.ChevronRight : null, row.Run, row.Key + "#head"));
        return Lay(row with { Caption = null }, end, null, text: row.Caption);
    }

    /// <summary>[S-2318] A standard button of words (and a chevron after them), 120 to 240 wide, its words the critical colour's for a danger.</summary>
    private static Button RowButton(PageRow row, string words, string? chevron, Action? run, string tag)
    {
        var said = BodyText(words, row.Danger);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(said);
        if (chevron is not null)
        {
            content.Children.Add(UiLanguage.Mirrored(new FontIcon { Glyph = chevron, FontSize = Chevron, VerticalAlignment = VerticalAlignment.Center }));
        }

        var button = new Button
        {
            Content = content,
            MinWidth = P.ActionLeast,
            MaxWidth = P.ButtonMost,
            Height = P.Control,
            Padding = new Thickness(P.Label, 0, P.Label, 0),
            IsEnabled = !row.Disabled,
            Tag = tag,
        };
        AutomationProperties.SetName(button, words);
        ToolTipService.SetToolTip(button, words);
        button.Click += (_, _) => Do(run);
        return button;
    }

    /// <summary>
    /// [S-2318] A row that only acts, its button alone (Karpo's Â§578 foot): no card, a hyperlink button's look â€”
    /// no fill nor rim until pointed at, its glyph and its words in the accent's text colour (the critical colour
    /// for a danger), a chevron after its words where its click asks first; on a page from the cards' start, in
    /// a card's opened level its glyph where its rows' names start. Its value is its tooltip.
    /// </summary>
    private Button FootButton(PageRow row, bool inside)
    {
        var ink = row.Danger ? ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed) : ShellBrush.Of(ShellRole.AccentText);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
        if (row.Icon?.Invoke() is { } icon)
        {
            if (icon is IconElement glyph)
            {
                glyph.Foreground = ink;
            }

            content.Children.Add(new Border { Width = Fluent.IconSize, Height = Fluent.IconSize, Child = icon, VerticalAlignment = VerticalAlignment.Center });
        }

        var words = Fluent.Text(FamilyType.Body);
        words.Foreground = ink;
        words.Text = row.Text;
        words.VerticalAlignment = VerticalAlignment.Center;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        content.Children.Add(words);
        if (row.Asks)
        {
            var chevron = new FontIcon { Glyph = Glyphs.ChevronRight, FontSize = Chevron, VerticalAlignment = VerticalAlignment.Center, Foreground = ink };
            content.Children.Add(UiLanguage.Mirrored(chevron));
        }

        var button = Plain(new Button
        {
            Content = content,
            Height = P.Control,
            Padding = new Thickness(P.Label, 0, P.Label, 0),
            IsEnabled = !row.Disabled,
            Tag = row.Key + "#head",
        });
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Margin = inside ? new Thickness(P.NameAt - P.Label, Fluent.Space1, 0, Fluent.Space1) : new Thickness(0);
        button.Resources["ButtonForegroundDisabled"] = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray);
        AutomationProperties.SetName(button, Said(row));
        if (row.Value is { Length: > 0 } value)
        {
            ToolTipService.SetToolTip(button, value);
        }

        if (row.Disabled)
        {
            words.Foreground = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray);
        }

        button.Click += (_, _) => Do(row.Run);
        return button;
    }

    /// <summary>
    /// [S-2318] A reading or a note that has a button standing in its line (Karpo's Â§638 <c>Beside</c>: Lometics'
    /// site beside its name): the button at the line's end.
    /// </summary>
    private FrameworkElement BesideRow(PageRow row, PageRow beside)
    {
        var button = RowButton(beside, beside.Text, null, beside.Run, row.Key + "#beside");
        if (beside.Icon?.Invoke() is { } icon)
        {
            ((StackPanel)button.Content).Children.Insert(0, new Border { Width = Fluent.IconSize, Height = Fluent.IconSize, Child = icon, VerticalAlignment = VerticalAlignment.Center });
        }

        return Lay(row, button, row.Caption);
    }

    /// <summary>[S-3749] A link row: the owner's setting, what it is set to in Caption and a chevron at its end; its tooltip names the owner.</summary>
    private Button LinkRow(PageRow row)
    {
        var button = ClickRow(row, End(row.Value, Glyphs.ChevronRight), row.Run);
        if (row.Owner is { } owner)
        {
            ToolTipService.SetToolTip(button, Str.Menu_OpensIn.Format(("name", owner)));
        }

        return button;
    }

    /// <summary>[S-2302] A switch: On or Off before it, the whole row the switch.</summary>
    private ToggleButton ToggleRow(PageRow row)
    {
        var end = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space3 };
        var state = Fluent.Text(FamilyType.Body);
        state.Text = row.On ? Str.Toggle_On.Text() : Str.Toggle_Off.Text();
        state.VerticalAlignment = VerticalAlignment.Center;
        state.MinWidth = P.SwitchWords;
        state.TextAlignment = TextAlignment.Right;
        end.Children.Add(state);
        end.Children.Add(SwitchArt(row.On, !row.Disabled));

        var toggle = Plain(new ToggleButton { Content = Lay(row, end, row.Caption), IsChecked = row.On, IsEnabled = !row.Disabled, Tag = row.Key + "#head" });
        AutomationProperties.SetName(toggle, row.Text);
        toggle.Click += (_, _) => Do(() => row.Toggle?.Invoke(!row.On));
        return toggle;
    }

    /// <summary>[S-3741] Windows' toggle switch, drawn: a 40 Ã— 20 track with a 12 knob, the accent filled while on.</summary>
    private static Grid SwitchArt(bool on, bool enabled)
    {
        Brush track, stroke, knob;
        if (on)
        {
            track = enabled ? Family.Brush(ColourRole.Accent) : ShellBrush.Of(ShellRole.AccentDisabled);
            stroke = track;
            knob = enabled ? Family.Brush(ColourRole.TextOnAccent) : ShellBrush.Of(ShellRole.TextOnAccentDisabled);
        }
        else
        {
            track = ShellBrush.Of(enabled ? ShellRole.SwitchTrack : ShellRole.SwitchTrackDisabled);
            stroke = ShellBrush.Of(enabled ? ShellRole.SwitchRim : ShellRole.SwitchRimDisabled);
            knob = Family.Brush(enabled ? ColourRole.TextSecondary : ColourRole.TextDisabled);
        }

        return new Grid
        {
            Width = P.SwitchWidth,
            Height = P.SwitchHeight,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Children =
            {
                new Border
                {
                    CornerRadius = Family.Round(P.SwitchWidth, P.SwitchHeight),
                    Background = track,
                    BorderBrush = stroke,
                    BorderThickness = new Thickness(Fluent.Hairline),
                },
                new Ellipse
                {
                    Width = P.Knob,
                    Height = P.Knob,
                    Fill = knob,
                    HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(P.KnobInset, 0, P.KnobInset, 0),
                },
            },
        };
    }

    /// <summary>
    /// [S-2302] [S-2309] A number box: 240 Ã— 32, its value as the line writes it, âˆ’ and + inside; a
    /// typed value read in the box's units and held to the range and the grain on Enter or leaving it,
    /// Escape restoring it; Up and Down step it.
    /// </summary>
    private FrameworkElement NumberBox(PageRow row)
    {
        var slider = row.Number!;
        var shown = slider.Value is { } v ? slider.Format(v) : "â€”";
        var box = new TextBox
        {
            Text = shown,
            Width = P.ControlWide,
            MinHeight = P.Control,
            Padding = new Thickness(Fluent.Space3, Fluent.Space1, P.SpinsRoom, Fluent.Space1),
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = !row.Disabled,
            Tag = row.Key + "#box",
        };
        AutomationProperties.SetName(box, row.Text);

        void Keep()
        {
            if (box.Text == shown)
            {
                return;
            }

            if (NumberEntry.Value(box.Text, slider.Max != 0 ? slider.Max : slider.Min, slider.Format) is { } typed)
            {
                Do(() => slider.Commit(typed));
            }
            else
            {
                box.Text = shown;
            }
        }

        void Step(int by) => Do(() => slider.Commit((slider.Value ?? slider.Min) + (by * slider.Step)));

        box.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case VirtualKey.Enter:
                    Keep();
                    e.Handled = true;
                    break;
                case VirtualKey.Escape when box.Text != shown:
                    box.Text = shown;
                    e.Handled = true;
                    break;
                case VirtualKey.Up:
                    Step(1);
                    e.Handled = true;
                    break;
                case VirtualKey.Down:
                    Step(-1);
                    e.Handled = true;
                    break;
            }
        };
        box.LostFocus += (_, _) => Keep();

        // [S-2370] The wheel over a number box steps it, as Karpo's (its §502); the page scrolls under the pointer elsewhere.
        box.PointerWheelChanged += (_, e) =>
        {
            var delta = e.GetCurrentPoint(box).Properties.MouseWheelDelta;
            if (row.Disabled || delta == 0)
            {
                return;
            }

            Step(delta > 0 ? 1 : -1);
            e.Handled = true;
        };

        Button Spin(string glyph, int by, Str name)
        {
            var spin = Plain(new Button
            {
                Content = new FontIcon { Glyph = glyph, FontSize = Fluent.Space3 },
                Width = P.Spin,
                Height = P.Spin,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = !row.Disabled && (by < 0 ? (slider.Value ?? slider.Min) > slider.Min : (slider.Value ?? slider.Min) < slider.Max),
                IsTabStop = false,
                Tag = row.Key + "#spin" + by,
            });
            spin.HorizontalAlignment = HorizontalAlignment.Right;
            AutomationProperties.SetName(spin, name.Text());
            spin.Click += (_, _) => Step(by);
            return spin;
        }

        var spins = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, P.SpinInset, 0),
            Children = { Spin(Glyphs.Minus, -1, Str.Common_Remove), Spin(Glyphs.Add, 1, Str.Common_Add) },
        };

        return new Grid { Width = P.ControlWide, Children = { box, spins } };
    }

    /// <summary>
    /// [S-2302] [S-3736] A text box: 240 Ã— 32; Enter or leaving it keeps what is typed (unless it cannot
    /// be kept: why, under the name, in the critical colour), Escape restores it; a form's field hands on
    /// every letter; a password's box never shows what it holds.
    /// </summary>
    private Control TextBox(PageRow row)
    {
        var edit = row.Edit!;
        if (edit.Masked)
        {
            var secret = new PasswordBox
            {
                Width = P.ControlWide,
                MinHeight = P.Control,
                PlaceholderText = edit.Placeholder,
                Password = edit.Fill?.Invoke() ?? string.Empty,
                IsEnabled = !row.Disabled,
                Tag = row.Key + "#box",
            };
            AutomationProperties.SetName(secret, row.Text);
            secret.PasswordChanged += (_, _) => Do(() => edit.Live?.Invoke(secret.Password));
            return secret;
        }

        var box = new TextBox
        {
            Text = edit.Initial,
            Width = P.ControlWide,
            MinHeight = P.Control,
            PlaceholderText = edit.Placeholder,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = !row.Disabled,
            InputScope = edit.Numeric ? new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } } : null,
            Tag = row.Key + "#box",
        };
        AutomationProperties.SetName(box, row.Text);
        if (edit.Live is { } live)
        {
            box.TextChanged += (_, _) => Do(() => live(box.Text));
            return box;
        }

        void Keep()
        {
            var typed = box.Text.Trim();
            if (typed == edit.Initial)
            {
                return;
            }

            if (edit.Reason(typed) is { } why)
            {
                _refused[row.Key] = why;
                Show();
                return;
            }

            _refused.Remove(row.Key);
            Do(() => edit.Commit(typed));
        }

        box.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case VirtualKey.Enter:
                    Keep();
                    e.Handled = true;
                    break;
                case VirtualKey.Escape when box.Text != edit.Initial:
                    box.Text = edit.Initial;
                    _refused.Remove(row.Key);
                    e.Handled = true;
                    break;
            }
        };
        box.LostFocus += (_, _) => Keep();
        return box;
    }

    /// <summary>[S-3728] [S-3561] Words only: under the row they follow (in at its name), or a card of their own.</summary>
    private FrameworkElement NoteRow(PageRow row, bool first)
    {
        if (row.Text.Length == 0)
        {
            return new Border();
        }

        if (row.Beside is { } beside)
        {
            return BesideRow(row, beside);
        }

        if (row.Value is not null || row.Icon is not null)
        {
            // [S-3561] A reading: words (over its line, Karpo's note card, Â§579) and a value in Caption at its end.
            return Lay(row, End(row.Value, null), row.Caption);
        }

        var words = first ? Fluent.Text(FamilyType.Body, row.Danger ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush") : CaptionText(row.Text, row.Danger);
        words.Text = row.Text;
        words.TextWrapping = TextWrapping.Wrap;
        words.IsTextSelectionEnabled = true;
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Padding = first ? new Thickness(P.IconAt, Fluent.Space3, P.EndInset, Fluent.Space3) : new Thickness(P.NameAt, 0, P.EndInset, Fluent.Space3),
        };
        if (row.Busy)
        {
            panel.Children.Add(new ProgressRing { IsActive = true, Width = Fluent.IconSize, Height = Fluent.IconSize });
        }

        panel.Children.Add(words);
        words.MaxWidth = ShellMetrics.Flyout.Cards - P.NameAt - P.EndInset;
        return panel;
    }

    /// <summary>
    /// A heading over the rows of its card (a group of a sub-page); [S-2349] one with a glyph a group's head (Karpo's
    /// §685): a line of its own, its glyph in the icon column and its words semibold where the rows' names start.
    /// </summary>
    private static FrameworkElement HeadingRow(PageRow row)
    {
        var words = Fluent.Text(FamilyType.BodyStrong);
        words.Text = row.Text;
        if (row.Icon is null)
        {
            words.Margin = new Thickness(P.IconAt, Fluent.Space3, P.EndInset, Fluent.Space1);
            return words;
        }

        var head = Lay(row, null, null);
        if (head.Children.OfType<StackPanel>().FirstOrDefault()?.Children.FirstOrDefault() is TextBlock name)
        {
            name.FontWeight = words.FontWeight;
        }

        return head;
    }

    /// <summary>[S-2308] An option standing open: a radio row 36 tall, its icon (a view's mark) before its words.</summary>
    private RadioButton RadioRow(PageRow row)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
        if (row.Icon?.Invoke() is { } icon)
        {
            content.Children.Add(icon);
        }

        content.Children.Add(BodyText(row.Text));
        var radio = new RadioButton
        {
            Content = content,
            IsChecked = row.Holds,
            IsEnabled = !row.Disabled,
            MinHeight = ShellMetrics.Inline.Option,
            MinWidth = 0,
            Padding = new Thickness(Fluent.Space2, 0, 0, 0),
            Margin = new Thickness(P.IconAt, 0, P.EndInset, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Tag = row.Key + "#head",

            // A group of its own unless its choice names one (Options): never a group shared with another choice's.
            GroupName = row.Key,
        };
        radio.Click += (_, _) => Do(row.Run);
        return radio;
    }

    /// <summary>
    /// [S-3549] [S-3732] One of the user's own things: its icon and name, its icon buttons at its end (their
    /// keys work while it has the keyboard: F2, Delete), dragged up or down, or Ctrl+Up / Down, to reorder.
    /// </summary>
    private ContentControl ItemRow(PageRow row)
    {
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1 };
        foreach (var tool in row.Tools ?? [])
        {
            tools.Children.Add(ToolButton(row, tool));
        }

        var item = new ContentControl
        {
            Content = Lay(row, tools, row.Caption),
            IsTabStop = true,
            UseSystemFocusVisuals = true,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = row.Key + "#head",
        };
        AutomationProperties.SetName(item, row.Text);
        item.KeyDown += (_, e) =>
        {
            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (ctrl && row.Order is { } order && e.Key is VirtualKey.Up or VirtualKey.Down)
            {
                var to = order.Index + (e.Key == VirtualKey.Up ? -1 : 1);
                if (to >= 0 && to < order.Count)
                {
                    Do(() => order.Move(order.Index, to));
                    Refocus(new FocusTag(row.Key + "#head", 0));
                }

                e.Handled = true;
                return;
            }

            if (row.Tools?.FirstOrDefault(t => t.Shortcut == e.Key && !t.Disabled) is { } keyed)
            {
                Do(keyed.Run);
                e.Handled = true;
            }
        };

        if (row.Order is { } list)
        {
            Draggable(item, item, list, 0);
        }

        return item;
    }

    /// <summary>
    /// [S-3732] [S-2310] One of a list's own dragged up or down among the list's: <paramref name="moved"/> (an item's row,
    /// a thing's whole card) follows the pointer pressed on <paramref name="handle"/> (the row itself, a card's grip);
    /// where it is let go, it is moved to, once, a step being its height and <paramref name="gap"/>.
    /// </summary>
    private void Draggable(FrameworkElement moved, UIElement handle, MenuOrder order, double gap)
    {
        double? start = null;
        var moving = false;
        var shift = new TranslateTransform();
        moved.RenderTransform = shift;

        handle.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(moved, handle) && e.OriginalSource is DependencyObject source && Within<Button>(source, moved))
            {
                return;
            }

            start = e.GetCurrentPoint(this).Position.Y;
            moving = false;
            handle.CapturePointer(e.Pointer);

            // A card's grip stands in its head's button: the press is the grip's, never the head's click.
            e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (start is not { } from)
            {
                return;
            }

            var dy = e.GetCurrentPoint(this).Position.Y - from;
            moving |= Math.Abs(dy) > Fluent.Space1;
            if (moving)
            {
                shift.Y = dy;
                Canvas.SetZIndex(moved, 1);
            }
        };
        handle.PointerReleased += (_, e) =>
        {
            if (start is not { } from)
            {
                return;
            }

            start = null;
            handle.ReleasePointerCapture(e.Pointer);
            shift.Y = 0;
            e.Handled = true;
            if (!moving)
            {
                return;
            }

            var height = Math.Max(1, moved.ActualHeight + gap);
            var steps = (int)Math.Round((e.GetCurrentPoint(this).Position.Y - from) / height);
            var to = Math.Clamp(order.Index + steps, 0, order.Count - 1);
            if (to != order.Index)
            {
                Do(() => order.Move(order.Index, to));
            }
        };
        handle.PointerCaptureLost += (_, _) =>
        {
            start = null;
            shift.Y = 0;
        };
    }

    /// <summary>Whether <paramref name="source"/> is inside a <typeparamref name="T"/> within <paramref name="root"/>.</summary>
    private static bool Within<T>(DependencyObject source, DependencyObject root)
        where T : DependencyObject
    {
        for (var node = source; node is not null && !ReferenceEquals(node, root); node = VisualTreeHelper.GetParent(node))
        {
            if (node is T)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>[S-2307] A sub-page's head: 32 tall, the back arrow and its title in BodyStrong; the whole of it goes back.</summary>
    private Button BackHead(string title)
    {
        var words = Fluent.Text(FamilyType.BodyStrong);
        words.Text = title;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.VerticalAlignment = VerticalAlignment.Center;
        var head = Plain(new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2,
                Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0),
                Children = { UiLanguage.Mirrored(new FontIcon { Glyph = Glyphs.Back, FontSize = Fluent.IconSize }), words },
            },
            Height = P.BackHead,
            Margin = new Thickness(0, 0, 0, P.HeadingGap),
            Tag = "back#head",
        });
        AutomationProperties.SetName(head, Str.Common_Back.Text());
        head.Click += (_, _) => Back();
        return head;
    }

    /// <summary>[S-3736] A line of text asked on a page of its own: its box, the button that keeps it, why not under it.</summary>
    private Border AskCard(PageEdit edit)
    {
        var box = new TextBox
        {
            Text = edit.Initial,
            PlaceholderText = edit.Placeholder,
            MinHeight = P.Control,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = "ask#box",
        };
        var why = CaptionText(string.Empty, critical: true);
        var accept = new Button { Content = edit.Accept ?? Str.Common_Done.Text(), MinWidth = P.ActionLeast, Height = P.Control, Tag = "ask#accept" };
        FluentLayout.ApplyStyle(accept, "AccentButtonStyle");

        void Keep()
        {
            var typed = box.Text.Trim();
            if (edit.Reason(typed) is { } reason)
            {
                why.Text = reason;
                return;
            }

            _steps.RemoveAll(s => s is AskStep a && ReferenceEquals(a.Edit, edit));
            Do(() => edit.Commit(typed));
            Show();
        }

        accept.Click += (_, _) => Keep();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                Keep();
                e.Handled = true;
            }
        };
        box.Loaded += (_, _) => box.SelectAll();

        return new Border
        {
            Background = ShellBrush.Of(ShellRole.CardFill),
            BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = new CornerRadius(P.CardRadius),
            Padding = new Thickness(P.IconAt),
            Child = new StackPanel { Spacing = Fluent.Space2, Children = { box, why, accept } },
        };
    }

    /// <summary>[S-3737] An "are you sure?": the question and what goes with it, the button that does it (the critical colour for a danger) and Cancel.</summary>
    private Border QuestionCard(PageQuestion question)
    {
        var accept = new Button { Content = question.Accept, MinWidth = P.ActionLeast, Height = P.Control, Tag = "question#accept" };
        FluentLayout.ApplyStyle(accept, "AccentButtonStyle");
        if (question.Danger)
        {
            var critical = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
            accept.Resources["AccentButtonBackground"] = critical;
            accept.Resources["AccentButtonBackgroundPointerOver"] = critical;
            accept.Resources["AccentButtonBackgroundPressed"] = critical;
        }

        var cancel = new Button { Content = Str.Common_Cancel.Text(), MinWidth = P.ActionLeast, Height = P.Control, Tag = "question#cancel" };
        accept.Click += (_, _) =>
        {
            _steps.RemoveAll(s => s is QuestionStep q && ReferenceEquals(q.Question, question));
            Do(question.Done);
            Show();
        };
        cancel.Click += (_, _) => Back();

        // The question is the page's head; its card says what goes with it.
        var words = new StackPanel { Spacing = Fluent.Space2 };
        if (question.Detail is { Length: > 0 } detail)
        {
            words.Children.Add(CaptionText(detail));
        }

        words.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Margin = new Thickness(0, Fluent.Space2, 0, 0), Children = { accept, cancel } });
        return new Border
        {
            Background = ShellBrush.Of(ShellRole.CardFill),
            BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = new CornerRadius(P.CardRadius),
            Padding = new Thickness(P.IconAt),
            Child = words,
        };
    }

    /// <summary>
    /// [S-2340] The selection page's header card: 80 tall, a 48 tile with the kind's glyph, the name in
    /// Subtitle over a Caption line, its icon buttons (Cut, Copy, Remove) at its end.
    /// </summary>
    private Border HeaderCard(PageHeader header)
    {
        var tile = new Border
        {
            Width = P.HeaderTile,
            Height = P.HeaderTile,
            CornerRadius = new CornerRadius(P.CardRadius),
            Background = ShellBrush.Of(ShellRole.CardSecondary),
            Child = new FontIcon { Glyph = header.Glyph, FontSize = P.HeaderGlyph, Foreground = ShellBrush.Of(ShellRole.AccentText) },
        };
        var name = Fluent.Text(FamilyType.Title);
        name.Text = header.Name;
        name.TextTrimming = TextTrimming.CharacterEllipsis;

        var caption = CaptionText(header.Caption);
        caption.TextWrapping = TextWrapping.NoWrap;
        caption.TextTrimming = TextTrimming.CharacterEllipsis;
        ToolTipService.SetToolTip(caption, header.Caption);
        var named = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(P.IconAt, 0, Fluent.Space3, 0), Children = { name, caption } };

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, VerticalAlignment = VerticalAlignment.Center };
        foreach (var tool in header.Tools)
        {
            var button = Plain(new Button
            {
                Content = new FontIcon { Glyph = tool.Glyph, FontSize = Fluent.IconSize },
                Width = P.Control,
                Height = P.Control,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = !tool.Disabled,
                Tag = "header#" + tool.Key,
            });
            ToolTipService.SetToolTip(button, tool.Name);
            AutomationProperties.SetName(button, tool.Name);
            var run = tool.Run;
            button.Click += (_, _) => Do(run);
            tools.Children.Add(button);
        }

        var grid = new Grid
        {
            Height = P.HeaderCard,
            Padding = new Thickness(P.IconAt, 0, P.EndInset, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { tile, named, tools },
        };
        tile.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(named, 1);
        Grid.SetColumn(tools, 2);
        return new Border
        {
            Child = grid,
            Background = ShellBrush.Of(ShellRole.CardFill),
            BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = new CornerRadius(P.CardRadius),
            Margin = new Thickness(0, 0, 0, P.CardGap),
            Tag = "header#card",
        };
    }

    /// <summary>Run what a row does: an exception is logged, never raised out of a click.</summary>
    private static void Do(Action? run)
    {
        try
        {
            run?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a page's row failed", ex);
        }
    }
}
