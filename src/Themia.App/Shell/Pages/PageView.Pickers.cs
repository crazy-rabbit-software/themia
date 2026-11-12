using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using I = Themia.Sdk.Kit.ShellMetrics.Inline;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-2308] A card opened inside: an expander's rows, and the inline pickers (Karpo's §670..§676): a
/// choice's radio rows, a lookup's box over its results, a colour's swatches and custom picker, a font's
/// and an icon's picker. Never a popup or a drop-down: the picker stands in its card under a hairline on
/// the secondary card fill, its head showing what holds and a chevron.
/// </summary>
internal sealed partial class PageView
{
    /// <summary>[S-2308] A card that opens: its head (icon, name, what holds, a chevron) and, open, what it opens.</summary>
    private StackPanel Expander(PageRow row)
    {
        var open = _open == row.Key;
        var head = Plain(new Button
        {
            Content = Lay(row, HeadEnd(row, open), row.Caption),
            IsEnabled = !row.Disabled,
            Tag = row.Key + "#head",
        });
        AutomationProperties.SetName(head, Said(row));
        head.Click += (_, _) =>
        {
            _open = open ? null : row.Key;
            Show();
            if (!open && Tagged(this, row.Key + "#head") is { } shown)
            {
                BringIntoView?.Invoke(shown);
            }
        };

        var card = new StackPanel { Children = { head } };
        if (open)
        {
            card.Children.Add(Hairline());
            card.Children.Add(Body(row));
        }

        return card;
    }

    /// <summary>[S-2303] Inside an expander a picker stands open: its name over it (a card has no third level).</summary>
    private FrameworkElement StandingOpen(PageRow row)
    {
        var card = new StackPanel();
        card.Children.Add(Lay(row, End(row.Kind == SettingsRowKind.Expander ? null : row.Value, null), row.Caption));
        card.Children.Add(Body(row));
        return card;
    }

    /// <summary>[S-2308] A picker's head's end: what holds (a colour's swatch and words, a choice's words) and the chevron, up while open.</summary>
    private static StackPanel HeadEnd(PageRow row, bool open)
    {
        var end = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
        if (row.Colour is { } colour)
        {
            end.Children.Add(Swatch(colour, colour.Mixed ? null : colour.Chosen, held: false, fill: colour.Fill));
            end.Children.Add(Secondary(colour.Mixed ? "—" : colour.IsFill ? ColorChoice.DescribeFill(colour.Fill!, colour.Options) : ColorChoice.Describe(colour.Chosen, colour.Options)));
        }
        else if (row.Value is { Length: > 0 } value)
        {
            end.Children.Add(Secondary(value));
        }

        end.Children.Add(new FontIcon { Glyph = open ? Glyphs.ChevronUp : Glyphs.ChevronDown, FontSize = Chevron, VerticalAlignment = VerticalAlignment.Center });
        return end;
    }

    private static TextBlock Secondary(string text)
    {
        var words = Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush");
        words.Text = text;
        words.MaxWidth = P.ControlWide;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.VerticalAlignment = VerticalAlignment.Center;
        return words;
    }

    private static Rectangle Hairline() => new()
    {
        Height = FamilyShape.Hairline,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Fill = ShellBrush.Of(ShellRole.CardStroke),
    };

    /// <summary>[S-2308] What an opened card holds.</summary>
    private FrameworkElement Body(PageRow row)
    {
        if (row.Kind == SettingsRowKind.Expander)
        {
            // [S-2303] An expander's rows on its own card, one level in, no deeper.
            var rows = new StackPanel();
            var inner = row.Rows ?? [];
            for (var i = 0; i < inner.Count; i++)
            {
                if (inner[i].Kind == SettingsRowKind.Break)
                {
                    rows.Children.Add(Hairline());
                    continue;
                }

                // [S-2308] Options standing open in the card: radio rows, more than six in columns.
                if (inner[i].Kind == SettingsRowKind.Radio)
                {
                    var run = inner.Skip(i).TakeWhile(r => r.Kind == SettingsRowKind.Radio).ToList();
                    rows.Children.Add(new Border { Padding = new Thickness(P.IconAt, 0, P.EndInset, 0), Child = Options(run) });
                    i += run.Count - 1;
                    continue;
                }

                rows.Children.Add(RowOf(inner[i], inside: true, first: i == 0 || inner[i - 1].Kind == SettingsRowKind.Break));
            }

            return rows;
        }

        FrameworkElement picker = row.Kind switch
        {
            SettingsRowKind.Choice => Options(row.Rows ?? []),
            SettingsRowKind.Lookup => Lookup(row),
            SettingsRowKind.Colour => ColourPicker(row),
            _ => Picked(row),
        };

        return new Border
        {
            Background = ShellBrush.Of(ShellRole.CardSecondary),
            Padding = new Thickness(I.Pad),
            CornerRadius = new CornerRadius(0, 0, P.CardRadius, P.CardRadius),
            Child = picker,
        };
    }

    /// <summary>[S-2308] A choice's options: radio rows 36 tall; more than six in up to three columns, each at least 180 wide.</summary>
    private FrameworkElement Options(IReadOnlyList<PageRow> options)
    {
        var radios = options.Where(o => o.Kind == SettingsRowKind.Radio).ToList();
        var columns = radios.Count <= I.InColumn ? 1 : Math.Min(I.Columns, (int)Math.Floor((ShellMetrics.Flyout.Cards - (2 * I.Pad)) / I.OptionLeast));
        columns = Math.Max(1, Math.Min(columns, (radios.Count + I.InColumn - 1) / I.InColumn));
        var perColumn = (radios.Count + columns - 1) / Math.Max(1, columns);

        var grid = new Grid();
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < perColumn; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < radios.Count; i++)
        {
            var radio = RadioRow(radios[i]);
            radio.Margin = new Thickness(0);
            radio.GroupName = "options";
            Grid.SetColumn(radio, i / perColumn);
            Grid.SetRow(radio, i % perColumn);
            grid.Children.Add(radio);
        }

        var rest = options.Where(o => o.Kind != SettingsRowKind.Radio && o.Kind != SettingsRowKind.Break).ToList();
        if (rest.Count == 0)
        {
            return grid;
        }

        var stack = new StackPanel { Children = { grid } };
        foreach (var other in rest)
        {
            stack.Children.Add(RowOf(other, inside: true, first: false));
        }

        return stack;
    }

    /// <summary>
    /// [S-2308] A lookup (a language, a place): a search box over at most eight results and a status line,
    /// the card the same height whatever is typed, so typing moves nothing. Its own options are filtered
    /// as typed; a lookup that asks (a place) asks as typing settles, its fixed lines after the answers.
    /// </summary>
    private FrameworkElement Lookup(PageRow row)
    {
        var typed = _typed.GetValueOrDefault(row.Key) ?? string.Empty;
        var box = new TextBox
        {
            Text = typed,
            PlaceholderText = row.Search?.Placeholder ?? row.Filter ?? Str.Menu_Filter.Text(),
            MinHeight = P.Control,
            Tag = row.Key + "#find",
        };
        AutomationProperties.SetName(box, row.Text);

        List<PageRow> results;
        string? note = null;
        if (row.Search is { } search)
        {
            if (_found.TryGetValue(row.Key, out var found) && found.Typed == typed && found.Answer is { } answer)
            {
                results = PageRows.Of(answer.Lines, row.Key + "/found");
                note = answer.Note;
            }
            else
            {
                results = [];
                note = Str.Shell_Place_Searching.Text();
                if (!_found.TryGetValue(row.Key, out var asked) || asked.Typed != typed)
                {
                    Find(row.Key, search, typed);
                }
            }
        }
        else
        {
            results = [.. (row.Rows ?? []).Where(o => o.Kind == SettingsRowKind.Radio && SettingsMenu.Matches(o.Text, o.Keywords, typed))];
        }

        var shown = results.Take(I.Found).ToList();
        var list = new StackPanel { Height = I.Found * I.Option };
        foreach (var result in shown)
        {
            var radio = result.Kind == SettingsRowKind.Radio ? RadioRow(result) : (FrameworkElement)RowOf(result, inside: true, first: false);
            if (radio is RadioButton r)
            {
                r.Margin = new Thickness(0);
                r.GroupName = "found";
            }

            list.Children.Add(radio);
        }

        var status = CaptionText(note ?? FoundWords(results.Count));
        box.TextChanged += (_, _) =>
        {
            _typed[row.Key] = box.Text;
            Show();
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter && shown.FirstOrDefault() is { } best)
            {
                Do(best.Run);
                e.Handled = true;
            }
        };

        var stack = new StackPanel { Spacing = Fluent.Space2, Children = { box, list, status } };
        if (row.Search is not null)
        {
            foreach (var fixedRow in row.Rows ?? [])
            {
                stack.Children.Add(RowOf(fixedRow, inside: true, first: false));
            }
        }

        return stack;
    }

    /// <summary>[S-3722] Ask a lookup's search for what is typed; the page is drawn again with its answer.</summary>
    private async void Find(string key, MenuSearch search, string typed)
    {
        Cancel();
        _finding = new CancellationTokenSource();
        var token = _finding.Token;
        _found[key] = (typed, null);
        try
        {
            if (search.Settle > TimeSpan.Zero)
            {
                await Task.Delay(search.Settle, token).ConfigureAwait(true);
            }

            var answer = await search.Find(typed, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            _found[key] = (typed, answer);
            Show();
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer search took its place.
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a page's lookup failed", ex);
            _found[key] = (typed, new MenuSearchAnswer([], Str.Common_NoResults.Text()));
            Show();
        }
    }

    /// <summary>A font's or an icon's picker, with the rows that go with it (Add fonts).</summary>
    private FrameworkElement Picked(PageRow row)
    {
        var stack = new StackPanel { Spacing = Fluent.Space2 };
        if (row.Picker is { } build)
        {
            try
            {
                stack.Children.Add(build());
            }
            catch (Exception ex)
            {
                Log.Error(Scope, $"the picker of '{row.Key}' could not be built", ex);
            }
        }

        foreach (var more in row.Rows ?? [])
        {
            stack.Children.Add(RowOf(more, inside: true, first: false));
        }

        return stack;
    }

    /// <summary>
    /// [S-2308] [S-3724] A colour: 36 swatches 4 apart (Default first, a fill's Transparent next), the one
    /// that holds ringed; then the custom picker (spectrum, hue, a fill's opacity, the hex box: a drag
    /// shows the colour as it goes, letting go keeps it) and the eyedropper.
    /// </summary>
    private FrameworkElement ColourPicker(PageRow row)
    {
        var colour = row.Colour!;
        var options = colour.Options;
        var swatches = new VariableSizedWrapGrid
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = I.Swatch + I.SwatchGap,
            ItemHeight = I.Swatch + I.SwatchGap,
        };

        Button Pick(Border swatch, string name, bool holds, Action choose)
        {
            // The one that holds ringed in the accent, 2 round it.
            var ring = new Border
            {
                Child = swatch,
                Padding = new Thickness(holds ? I.Ring : 0),
                BorderThickness = new Thickness(holds ? I.Ring : 0),
                BorderBrush = holds ? ShellBrush.Of(ShellRole.AccentText) : null,
                CornerRadius = new CornerRadius(I.RingRadius),
            };
            var button = Plain(new Button
            {
                Content = ring,
                Width = I.Swatch,
                Height = I.Swatch,
                Padding = new Thickness(0),
                Tag = row.Key + "#" + name,
            });
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Stretch;
            ToolTipService.SetToolTip(button, name);
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => Do(choose);
            return button;
        }

        if (options.DefaultLabel is { } defaultLabel)
        {
            var paint = new Border();
            if (colour.IsFill)
            {
                ColorChoice.PaintFill(paint, FillChoice.Default(), options);
            }
            else
            {
                ColorChoice.PaintSwatch(paint, string.Empty, options);
            }

            swatches.Children.Add(Pick(Framed(paint), defaultLabel, colour.DefaultHolds, colour.ChooseDefault));
        }

        if (colour.IsFill)
        {
            var clear = new Border();
            ColorChoice.PaintFill(clear, FillChoice.Transparent(), options);
            swatches.Children.Add(Pick(Framed(clear), Str.Shell_Fill_Transparent.Text(), colour.TransparentHolds, colour.ChooseTransparent));
        }

        foreach (var preset in options.Presets)
        {
            var hex = HexColor.Normalise(preset.Hex);
            var paint = new Border();
            ColorChoice.PaintSwatch(paint, hex, options);
            swatches.Children.Add(Pick(Framed(paint), preset.Label.Text(), !colour.Mixed && colour.Chosen == hex && !colour.DefaultHolds, () => colour.Choose(hex)));
        }

        var stack = new StackPanel { Spacing = Fluent.Space3, Children = { swatches } };
        if (colour.OpacitySlider(v => LocaleFormat.Percent(Strings.Current, (int)Math.Round(v * 100))) is { } opacity)
        {
            stack.Children.Add(Lay(new PageRow(row.Key + "/opacity", SettingsRowKind.Number) { Text = Str.Shell_Fill_Opacity.Text(), Number = opacity, Disabled = !colour.HasOpacity }, NumberBox(new PageRow(row.Key + "/opacity", SettingsRowKind.Number) { Number = opacity, Text = Str.Shell_Fill_Opacity.Text(), Disabled = !colour.HasOpacity }), null));
        }

        if (options.AllowCustom)
        {
            stack.Children.Add(CustomColour(row.Key, colour));
            var eyedropper = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Fluent.Space2,
                    Children = { new FontIcon { Glyph = Glyphs.Eyedropper, FontSize = Fluent.IconSize }, new TextBlock { Text = Str.Shell_Color_PickFromScreen.Text() } },
                },
                MinWidth = P.ActionLeast,
                Height = P.Control,
                Tag = row.Key + "#screen",
            };
            eyedropper.Click += (_, _) => ColorChoice.PickFromScreen(colour.Choose);
            stack.Children.Add(eyedropper);
        }

        return stack;
    }

    /// <summary>A swatch's paint in its 36 square, radius 2.</summary>
    private static Border Framed(Border paint)
    {
        paint.CornerRadius = new CornerRadius(I.SwatchRadius);
        paint.HorizontalAlignment = HorizontalAlignment.Stretch;
        paint.VerticalAlignment = VerticalAlignment.Stretch;
        paint.BorderBrush = ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray);
        paint.BorderThickness = new Thickness(FamilyShape.Hairline);
        return paint;
    }

    /// <summary>A small swatch for a picker's head.</summary>
    private static Border Swatch(MenuColour colour, string? hex, bool held, FillChoice? fill)
    {
        var swatch = new Border { Width = Fluent.IconSize + Fluent.Space1, Height = Fluent.IconSize + Fluent.Space1, VerticalAlignment = VerticalAlignment.Center };
        if (colour.IsFill && fill is not null)
        {
            ColorChoice.PaintFill(swatch, fill, colour.Options);
        }
        else if (hex is not null)
        {
            ColorChoice.PaintSwatch(swatch, hex, colour.Options);
        }

        _ = held;
        return Framed(swatch);
    }

    /// <summary>
    /// [S-2308] The custom colour inline: Windows' colour picker (its spectrum, its hue and, for a fill, its
    /// opacity slider, the hex box) on the card. A drag only moves the picker; letting go keeps the colour,
    /// as a typed hex does.
    /// </summary>
    private FrameworkElement CustomColour(string key, MenuColour colour)
    {
        Windows.UI.Color start;
        if (HexColor.TryParse(colour.Chosen, out var r, out var g, out var b))
        {
            start = HexBrush.Colour(new RgbColor(r, g, b));
            start.A = FillChoice.Alpha(colour.Opacity);
        }
        else
        {
            start = colour.Options.DefaultColor?.Invoke() ?? ShellColours.WindowsAccent;
        }

        var picker = new ColorPicker
        {
            Color = start,
            IsAlphaEnabled = colour.IsFill,
            IsAlphaTextInputVisible = false,
            IsHexInputVisible = true,
            IsColorChannelTextInputVisible = false,
            IsMoreButtonVisible = false,
            IsColorSliderVisible = true,
            IsColorPreviewVisible = false,
            ColorSpectrumShape = ColorSpectrumShape.Box,
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Tag = key + "#custom",
        };
        AutomationProperties.SetName(picker, Str.Shell_Color_Custom.Text());

        var pressed = false;
        void Keep()
        {
            var c = picker.Color;
            var hex = HexColor.From(c.R, c.G, c.B);
            if (hex != colour.Chosen || (colour.IsFill && Math.Abs((c.A / 255.0) - colour.Opacity) > 0.01))
            {
                Do(() => colour.ChooseCustom(hex, colour.IsFill ? c.A / 255.0 : 1));
            }
        }

        picker.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => pressed = true), handledEventsToo: true);
        picker.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) =>
        {
            pressed = false;
            Keep();
        }), handledEventsToo: true);
        picker.LostFocus += (_, _) =>
        {
            if (!pressed)
            {
                Keep();
            }
        };

        var words = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
        words.Text = Str.Shell_Color_Custom.Text();
        return new StackPanel { Spacing = Fluent.Space1, Children = { words, picker } };
    }
}
