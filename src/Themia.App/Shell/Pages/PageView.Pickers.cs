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
using CP = Themia.Core.Settings.ColourPicker;
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
    /// <summary>
    /// [S-2308] A card that opens: its head (icon, name, what holds, a chevron) and, open, what it opens. [S-2303]
    /// <paramref name="inside"/> an opened card (Karpo's picker head in its expander, §682 <c>PanelRow.Folded</c>) it is
    /// folded until its head is clicked, its body unfolding under its line; the card's other rows stay as they are.
    /// [S-2351] A line that asks opens what it asks in place, the same way; a card asked at (a tool's question, a
    /// question after a step of its own) shows the question as its inner level until it is answered or closed.
    /// </summary>
    private StackPanel Expander(PageRow row, bool inside = false)
    {
        var asked = AskOf(row);
        var open = asked is not null || (row.Kind != SettingsRowKind.Ask && _nav.IsOpen(row.Key, inside));
        var head = Plain(new Button
        {
            Content = Lay(row, HeadEnd(row, open, inside), row.Caption, lead: row.Order is not null ? Grip(row) : null),
            IsEnabled = !row.Disabled,
            Tag = row.Key + "#head",
        });
        AutomationProperties.SetName(head, Said(row));
        head.Click += (_, _) =>
        {
            if (row.Kind == SettingsRowKind.Ask)
            {
                ToggleAsk(row.Key, row.Key, inside);
                return;
            }

            // [S-2351] A card asked at: its head closes the question, keeping nothing.
            if (asked is not null)
            {
                CloseAsk();
                return;
            }

            // [S-2303] Inside an opened card it unfolds or folds; on the page it is the one card open, and [S-2351] the section
            // asked closes (one open at a time).
            // ignore: whether it is open now; the drawing says it.
            _ = _nav.ToggleCard(row.Key, inside);
            if (!inside)
            {
                _question = null;
            }

            // [S-2347] The card with what it opened, scrolled only as far as it takes to stand whole in sight.
            Redraw(() =>
            {
                if (!open)
                {
                    Reveal(row.Key);
                }
            });
        };
        ThingKeys(head, row, inside);

        var card = new StackPanel { Children = { head } };
        if (open)
        {
            card.Children.Add(Hairline());
            card.Children.Add(asked is { } section ? AskSection(section, row.Text, inside) : Body(row, inside));
        }

        return card;
    }

    /// <summary>
    /// [S-2308] [S-2317] A picker's head's end, as Karpo's <c>PickerHead</c>: what holds in Body and the secondary
    /// colour (a colour's swatch or a choice's picture 8 before its words), 12 before the chevron's room; the
    /// chevron, up while open. [S-2310] An expander's (a thing's) head: what it holds in Caption while shut (open,
    /// its rows say it: Karpo's §638), 12 before its icon buttons, those 4 apart before the chevron's room. [S-2351] A
    /// line that asks says what it holds as a picker's head does.
    /// </summary>
    private StackPanel HeadEnd(PageRow row, bool open, bool inside)
    {
        var end = new StackPanel { Orientation = Orientation.Horizontal };
        var expander = row.Kind == SettingsRowKind.Expander;
        var tools = row.Tools ?? [];
        TextBlock? said = null;
        if (row.Colour is { } colour)
        {
            var swatch = Swatch(colour, colour.Mixed ? null : colour.Chosen, held: false, fill: colour.Fill);
            swatch.Margin = new Thickness(0, 0, Fluent.Space2, 0);
            end.Children.Add(swatch);
            said = Said(colour.Mixed ? Str.Groups_Page_Mixed.Text() : colour.IsFill ? ColorChoice.DescribeFill(colour.Fill!, colour.Options) : ColorChoice.Describe(colour.Chosen, colour.Options), FamilyType.Body);
        }
        else if (row.Value is { Length: > 0 } value && !(open && expander))
        {
            said = Said(value, expander ? FamilyType.Caption : FamilyType.Body);
        }

        // [S-14322] What the picker holds pictured (an icon picker's chosen icon), as a choice's head says its choice.
        if (!(open && expander) && row.Shows?.Invoke() is FrameworkElement shows)
        {
            shows.VerticalAlignment = VerticalAlignment.Center;
            shows.Margin = said is not null ? new Thickness(0, 0, Fluent.Space2, 0) : tools.Count > 0 ? new Thickness(0, 0, P.Label, 0) : ToChevron;
            end.Children.Add(shows);
        }

        if (said is not null)
        {
            said.Margin = tools.Count > 0 ? new Thickness(0, 0, P.Label, 0) : ToChevron;
            end.Children.Add(said);
        }

        for (var i = 0; i < tools.Count; i++)
        {
            var button = ToolButton(row, tools[i], inside);
            button.Margin = i == tools.Count - 1 ? ToChevronRoom : new Thickness(0, 0, Fluent.Space1, 0);
            end.Children.Add(button);
        }

        end.Children.Add(ChevronOf(open ? Glyphs.ChevronUp : Glyphs.ChevronDown));
        return end;
    }

    private static Rectangle Hairline() => new()
    {
        Height = FamilyShape.Hairline,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Fill = ShellBrush.Of(ShellRole.CardStroke),
    };

    /// <summary>[S-2308] What an opened card holds; <paramref name="inside"/> a card, a picker's body square at its foot (more rows follow).</summary>
    private FrameworkElement Body(PageRow row, bool inside = false)
    {
        if (row.Kind == SettingsRowKind.Expander)
        {
            // [S-2303] An expander's rows on its own card, one level in, no deeper.
            var rows = new StackPanel();
            var inner = row.Rows ?? [];

            // [S-2349] Under a group's head (a heading with a glyph) its rows a step in, to the next heading or divider.
            var step = 0.0;
            for (var i = 0; i < inner.Count; i++)
            {
                if (inner[i].Kind == SettingsRowKind.Break)
                {
                    rows.Children.Add(Hairline());
                    step = 0;
                    continue;
                }

                if (inner[i].Kind == SettingsRowKind.Heading)
                {
                    step = inner[i].Icon is null ? 0 : P.IndentStep;
                }

                // [S-2308] Options standing open in the card: radio rows, more than six in columns.
                if (inner[i].Kind == SettingsRowKind.Radio)
                {
                    var run = inner.Skip(i).TakeWhile(r => r.Kind == SettingsRowKind.Radio).ToList();
                    rows.Children.Add(new Border { Padding = new Thickness(P.IconAt + step, 0, P.EndInset, 0), Child = Options(run, row.Key + "#" + i) });
                    i += run.Count - 1;
                    continue;
                }

                var shown = RowOf(inner[i], inside: true, first: i == 0 || inner[i - 1].Kind == SettingsRowKind.Break);
                if (step > 0 && inner[i].Kind != SettingsRowKind.Heading)
                {
                    shown.Margin = new Thickness(step, 0, 0, 0);
                }

                rows.Children.Add(shown);
            }

            return rows;
        }

        FrameworkElement picker = row.Kind switch
        {
            SettingsRowKind.Choice => Options(row.Rows ?? [], row.Key),
            SettingsRowKind.Lookup => Lookup(row),
            SettingsRowKind.Colour => ColourPicker(row),
            _ => Picked(row),
        };

        return new Border
        {
            Background = ShellBrush.Of(ShellRole.CardSecondary),
            Padding = new Thickness(I.Pad),
            CornerRadius = inside ? new CornerRadius(0) : new CornerRadius(0, 0, P.CardRadius, P.CardRadius),
            Child = picker,
        };
    }

    /// <summary>
    /// [S-2308] A choice's options: radio rows 36 tall; more than six in up to three columns, each at least 180 wide.
    /// Their radio group is the choice's own (<paramref name="group"/>, its row's key): one name shared by every
    /// choice on the page made them one group, so the page showed one option of all its choices checked.
    /// </summary>
    private FrameworkElement Options(IReadOnlyList<PageRow> options, string group)
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
            radio.GroupName = group;
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
    /// [S-2308] [S-13606]..[S-13609] A lookup (a language, a place, a symbol, a currency, a zone, a device): the one
    /// search box over at most eight results and the one status line, the card the same height whatever is typed,
    /// so typing moves nothing. Its own options are filtered as typed; a lookup that asks (a place, a symbol) asks
    /// as typing settles, its fixed lines after the answers. Every result is the same option row, whatever the
    /// search: a radio row 36 tall, its words alone, the one in force dotted; the keys are <see cref="SearchKeys"/>'.
    /// </summary>
    private FrameworkElement Lookup(PageRow row)
    {
        var typed = _typed.GetValueOrDefault(row.Key) ?? string.Empty;
        var box = new SearchBox(row.Search?.Placeholder ?? row.Filter ?? Str.Menu_Filter.Text())
        {
            Text = typed,
            Key = row.Key + "#find",
            Said = row.Text,
        };

        List<PageRow> results;
        string? note = null;
        var asking = false;
        if (row.Search is { } search)
        {
            if (_found.For(row.Key, typed) is { } answer)
            {
                results = PageRows.Of(answer.Lines, row.Key + "/found");
                note = answer.Note;
            }
            else
            {
                results = [];
                asking = true;
                if (_found.Wants(row.Key, typed))
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
            var found = Found(result);
            if (found is RadioButton r)
            {
                r.Margin = new Thickness(0);
                r.GroupName = row.Key + "#found";
            }

            list.Children.Add(found);
        }

        var status = SearchBox.Status(LookupStatus.Line(typed, asking, results.Count, note));
        // [S-16409] Only the user's words, in the box shown now: a box drawn anew and the one it replaced raise their words
        // later, which taken as typing flipped what was typed between the two for ever, asking again on every flip.
        _boxes[row.Key] = box;
        box.Typed += text =>
        {
            if (LookupTyping.Types(ReferenceEquals(_boxes.GetValueOrDefault(row.Key), box), text, _typed.GetValueOrDefault(row.Key)))
            {
                _typed[row.Key] = text;
                Redraw();
            }
        };
        box.Count = () => shown.Count;
        box.PickBest = () => Do(shown[0].Run);
        box.ToFirst = () => list.Children.OfType<Control>().FirstOrDefault()?.Focus(FocusState.Keyboard);
        box.Results(list, source => source is DependencyObject node && Ancestor(node, list) is { } child ? list.Children.IndexOf(child) : null);

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

    /// <summary>
    /// [S-13606] One result of a lookup, whatever its search made it: the option row (radio, 36, its words alone, the
    /// one in force dotted); a line that is not a pick (a note) stands as its own row.
    /// </summary>
    private FrameworkElement Found(PageRow result) => result.Kind switch
    {
        SettingsRowKind.Radio => RadioRow(result with { Icon = null }),
        SettingsRowKind.Action or SettingsRowKind.Item => RadioRow(result with { Kind = SettingsRowKind.Radio, Icon = null, Holds = false }),
        _ => RowOf(result, inside: true, first: false),
    };

    /// <summary>The child of <paramref name="parent"/> that <paramref name="node"/> stands in, or null.</summary>
    private static UIElement? Ancestor(DependencyObject node, Panel parent)
    {
        for (var at = node; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is UIElement element && ReferenceEquals(VisualTreeHelper.GetParent(at), parent))
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-3722] [S-13609] Ask a lookup's search for what is typed once it settles; the page is drawn again with its
    /// answer, unless something newer was typed since (<see cref="LookupAnswers{T}"/>: the latest wins).
    /// </summary>
    private async void Find(string key, SettingsSearch search, string typed)
    {
        // [S-16409] One question out per lookup: a lookup's newer question cancels its own older one, never another lookup's.
        if (_finding.Remove(key, out var older))
        {
            older.Cancel();
            older.Dispose();
        }

        var source = new CancellationTokenSource();
        _finding[key] = source;
        var token = source.Token;
        _found.Asking(key, typed);
        _searches++;
        try
        {
            if (search.Settle > TimeSpan.Zero)
            {
                _typing++;
                try
                {
                    await Task.Delay(search.Settle, token).ConfigureAwait(true);
                }
                finally
                {
                    _typing--;
                }
            }

            var answer = await search.Find(typed, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                _found.Drop(key, typed);
            }
            else if (_found.Answered(key, typed, answer))
            {
                _answered[key] = _answered.GetValueOrDefault(key) + 1;
                Redraw();
            }
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer search took its place. [S-16409] Unless the page cut it short (another lookup asked, the page
            // went): then the question is forgotten, so the lookup asks it again when it is drawn, never "Searching…" for good.
            _found.Drop(key, typed);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a page's lookup failed", ex);
            if (_found.Answered(key, typed, new SettingsSearchAnswer([], Str.Common_NoResults.Text())))
            {
                _answered[key] = _answered.GetValueOrDefault(key) + 1;
                Redraw();
            }
        }
        finally
        {
            _searches--;
            if (_finding.TryGetValue(key, out var mine) && ReferenceEquals(mine, source))
            {
                _finding.Remove(key);
                source.Dispose();
            }
        }
    }

    /// <summary>The lookups' searches still running (waiting for the typing to settle, or asked).</summary>
    private int _searches;

    /// <summary>The lookups' searches still waiting for the typing to settle (not asked yet).</summary>
    private int _typing;

#if DEBUG
    /// <summary>[S-15900] Debug renders: a lookup's search still running, which a render's settle waits for; null when none.</summary>
    internal string? DebugSearching => _searches > 0 ? "a lookup's search" : null;

    /// <summary>[S-15900] Debug renders: a lookup's search not asked yet (the typing settling); null once it is asked.</summary>
    internal string? DebugTyping => _typing > 0 ? "a lookup's typing settling" : null;
#endif

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
    /// [S-2308] [S-3724] [S-1574]..[S-1577] A colour, as Karpo's picker (its §670, §682, §690): the swatches
    /// in their column — Default (and a fill's Transparent, slashed) with the recent colours on the first
    /// line, two lines of presets under it, the one that holds ringed — and beside them the mixer (a box
    /// spectrum of saturation and value 96 tall, the hue's slider, a fill's opacity slider), then the hex
    /// box's row under them with the eyedropper and a swatch of what the box says. The layout is
    /// <see cref="Themia.Core.Settings.ColourPicker.Lay"/>'s. A drag on the mixer moves it; letting go keeps the colour, as a
    /// hex typed and kept (Enter, or leaving the box) does; Escape in the box puts back what holds.
    /// </summary>
    private FrameworkElement ColourPicker(PageRow row)
    {
        var colour = row.Colour!;
        var options = colour.Options;

        Button Pick(Border swatch, string name, bool holds, Action choose)
        {
            // [S-1574] The one that holds ringed 2 in the primary text colour, 2 off it, as Karpo's and Windows' grid.
            var ring = new Border
            {
                Child = swatch,
                Padding = new Thickness(holds ? I.Ring : 0),
                BorderThickness = new Thickness(holds ? I.Ring : 0),
                BorderBrush = holds ? ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White) : null,
                CornerRadius = new CornerRadius(I.RingRadius),
            };
            var button = Plain(new Button
            {
                Content = ring,
                Width = CP.Swatch,
                Height = CP.Swatch,
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

        // The first line: the "no colour" choices, then the recent colours.
        var first = new List<Button>();
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

            first.Add(Pick(Framed(paint), defaultLabel, colour.DefaultHolds, colour.ChooseDefault));
        }

        if (colour.IsFill)
        {
            var clear = new Border();
            ColorChoice.PaintFill(clear, FillChoice.Transparent(), options);
            first.Add(Pick(Slashed(Framed(clear)), Str.Shell_Fill_Transparent.Text(), colour.TransparentHolds, colour.ChooseTransparent));
        }

        var holding = !colour.Mixed && !colour.DefaultHolds && !colour.TransparentHolds ? colour.Chosen : string.Empty;
        foreach (var hex in ColorChoice.Recent)
        {
            var paint = new Border();
            ColorChoice.PaintSwatch(paint, hex, options);
            first.Add(Pick(Framed(paint), ColorPresets.Find(options.Presets, hex)?.Name ?? Str.Shell_Color_Recent.Text() + " " + hex, holding == hex, () => colour.Choose(hex)));
        }

        var presets = new List<Button>();
        foreach (var preset in CP.Presets(options.Presets))
        {
            var hex = HexColor.Normalise(preset.Hex);
            var paint = new Border();
            ColorChoice.PaintSwatch(paint, hex, options);
            presets.Add(Pick(Framed(paint), preset.Label.Text(), holding == hex, () => colour.Choose(hex)));
        }

        var width = ShellMetrics.Flyout.Cards - P.NameAt - P.EndInset;
        var sliders = options.AllowCustom ? (colour.IsFill ? 2 : 1) : 0;
        var plan = CP.Lay(width, first.Count, presets.Count, sliders);
        var body = new Canvas { Width = width, Height = options.AllowCustom ? plan.Height : plan.Swatches.Count == 0 ? 0 : plan.Swatches.Max(s => s.Y) + CP.Swatch };
        var buttons = first.Take(plan.FirstShown).Concat(presets).ToList();
        foreach (var spot in plan.Swatches)
        {
            Canvas.SetLeft(buttons[spot.Index], spot.X);
            Canvas.SetTop(buttons[spot.Index], spot.Y);
            body.Children.Add(buttons[spot.Index]);
        }

        var stack = new StackPanel { Spacing = CP.Pad, Margin = new Thickness(P.NameAt - I.Pad, 0, P.EndInset - I.Pad, 0), Children = { body } };
        if (options.AllowCustom)
        {
            var mixer = Mixer(row.Key, colour, plan.MixerWidth, out var hexRow);
            Canvas.SetLeft(mixer, plan.MixerX);
            Canvas.SetTop(mixer, plan.MixerY);
            body.Children.Add(mixer);
            stack.Children.Add(hexRow);
        }

        return stack;
    }

    /// <summary>Transparent's swatch: a hairline slash across it, as Karpo draws None.</summary>
    private static Border Slashed(Border swatch)
    {
        var slash = new Line
        {
            X1 = CP.Swatch - (2 * I.SwatchRadius),
            Y1 = 2 * I.SwatchRadius,
            X2 = 2 * I.SwatchRadius,
            Y2 = CP.Swatch - (2 * I.SwatchRadius),
            Stroke = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed),
            StrokeThickness = FamilyShape.Stroke,
            IsHitTestVisible = false,
        };
        return new Border { Child = new Grid { Children = { swatch, slash } } };
    }

    /// <summary>
    /// [S-1574] [S-1577] The mixer (Karpo's §523 as §670 draws it): the spectrum of saturation across and
    /// value up at the hue that holds, the hue's slider over the rainbow, a fill's opacity slider, each a
    /// 12 track with a 20 thumb; and its hex row (built here, laid under the whole body by the caller):
    /// the eyedropper, a swatch of what the box says, the box.
    /// </summary>
    private StackPanel Mixer(string key, MenuColour colour, double width, out FrameworkElement hexRow)
    {
        Windows.UI.Color start;
        if (HexColor.TryParse(colour.Chosen, out var r, out var g, out var b))
        {
            start = HexBrush.Colour(new RgbColor(r, g, b));
        }
        else
        {
            start = colour.Options.DefaultColor?.Invoke() ?? ShellColours.WindowsAccent;
        }

        var spectrum = new SpectrumBox(width, CP.Spectrum) { Tag = key + "#spectrum" };
        var (h0, s0, v0) = CP.ToHsv(start.R, start.G, start.B);
        spectrum.Show(h0, s0, v0);
        AutomationProperties.SetName(spectrum, Str.Shell_Color_Spectrum.Text());

        var hue = Track(width, 0, 359, h0, key + "#hue", Str.Shell_Color_Hue.Text());
        var rainbow = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0.5), EndPoint = new Windows.Foundation.Point(1, 0.5) };
        for (var stop = 0; stop <= 6; stop++)
        {
            var (hr, hg, hb) = CP.FromHsv(stop * 60, 1, 1);
            rainbow.GradientStops.Add(new GradientStop { Offset = stop / 6.0, Color = HexBrush.Colour(new RgbColor(hr, hg, hb)) });
        }

        hue.Background = rainbow;

        Slider? opacity = null;
        if (colour.IsFill)
        {
            opacity = Track(width, MenuColour.OpacityMin * 100, MenuColour.OpacityMax * 100, colour.Opacity * 100, key + "#opacity", Str.Shell_Fill_Opacity.Text());
        }

        var hex = new TextBox
        {
            Text = HexColor.From(start.R, start.G, start.B),
            Width = P.ControlWide,
            MinHeight = P.Control,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsSpellCheckEnabled = false,
            MaxLength = 7,
            Tag = key + "#hex",
        };
        AutomationProperties.SetName(hex, Str.Shell_Color_Hex.Text());
        var said = Framed(new Border { Width = P.Control, Height = P.Control });
        var eyedropper = Plain(new Button
        {
            Content = new FontIcon { Glyph = Glyphs.Eyedropper, FontSize = Fluent.IconSize },
            Width = P.Control,
            Height = P.Control,
            Padding = new Thickness(0),
            Tag = key + "#screen",
        });
        ToolTipService.SetToolTip(eyedropper, Str.Shell_Color_PickFromScreen.Text());
        AutomationProperties.SetName(eyedropper, Str.Shell_Color_PickFromScreen.Text());
        eyedropper.Click += (_, _) => ColorChoice.PickFromScreen(picked =>
        {
            ColorChoice.Remember(picked);
            colour.Choose(picked);
        });

        string Shown()
        {
            var (cr, cg, cb) = spectrum.Colour;
            return HexColor.From(cr, cg, cb);
        }

        Windows.UI.Color Now()
        {
            var (cr, cg, cb) = spectrum.Colour;
            return HexBrush.Colour(new RgbColor(cr, cg, cb), opacity is null ? 1 : FillChoice.Alpha(opacity.Value / 100) / 255.0);
        }

        void Paint()
        {
            var c = Now();
            said.Background = new SolidColorBrush(c);
            if (opacity is not null)
            {
                var track = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0.5), EndPoint = new Windows.Foundation.Point(1, 0.5) };
                track.GradientStops.Add(new GradientStop { Offset = 0, Color = HexBrush.Colour(new RgbColor(c.R, c.G, c.B), 0) });
                track.GradientStops.Add(new GradientStop { Offset = 1, Color = HexBrush.Colour(new RgbColor(c.R, c.G, c.B)) });
                opacity.Background = track;
            }
        }

        void Keep()
        {
            var c = Now();
            var picked = HexColor.From(c.R, c.G, c.B);
            if (picked != colour.Chosen || (colour.IsFill && Math.Abs((c.A / 255.0) - colour.Opacity) > 0.01))
            {
                ColorChoice.Remember(picked);
                Do(() => colour.ChooseCustom(picked, colour.IsFill ? c.A / 255.0 : 1));
            }
        }

        void Mixed()
        {
            hex.Text = Shown();
            Paint();
        }

        spectrum.Changed += Mixed;
        spectrum.Kept += Keep;
        hue.ValueChanged += (_, e) =>
        {
            spectrum.Show(e.NewValue, spectrum.Saturation, spectrum.Value);
            Mixed();
        };
        if (opacity is not null)
        {
            opacity.ValueChanged += (_, _) => Paint();
        }

        // Letting go of a slider keeps the colour (the pointer's release, or the keys leaving it).
        foreach (var part in new Control?[] { hue, opacity })
        {
            if (part is null)
            {
                continue;
            }

            part.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => Keep()), handledEventsToo: true);
            part.AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, _) => Keep()), handledEventsToo: true);
            part.LostFocus += (_, _) => Keep();
        }

        void KeepTyped()
        {
            if (CP.Typed(hex.Text) is not { } typed)
            {
                // Not a colour: what holds is put back.
                hex.Text = Shown();
                return;
            }

            if (typed != Shown() && HexColor.TryParse(typed, out var tr, out var tg, out var tb))
            {
                var (th, ts, tv) = CP.ToHsv(tr, tg, tb, spectrum.Hue);
                spectrum.Show(th, ts, tv);
                hue.Value = th;
                hex.Text = typed;
                Paint();
                Keep();
            }
        }

        hex.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                KeepTyped();
            }
            else if (e.Key == VirtualKey.Escape && hex.Text != Shown())
            {
                e.Handled = true;
                hex.Text = Shown();
            }
        };
        hex.LostFocus += (_, _) => KeepTyped();
        Paint();

        var words = Fluent.Text(FamilyType.Body, "TextFillColorPrimaryBrush");
        words.Text = Str.Shell_Color_Hex.Text();
        words.VerticalAlignment = VerticalAlignment.Center;
        var end = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, HorizontalAlignment = HorizontalAlignment.Right, Children = { eyedropper, said, hex } };
        var line = new Grid { MinHeight = P.Control, Children = { words, end } };
        hexRow = line;

        var mixer = new StackPanel { Width = width, Spacing = 0, Children = { spectrum } };
        mixer.Children.Add(new Border { Height = CP.SpectrumGap });
        mixer.Children.Add(hue);
        if (opacity is not null)
        {
            mixer.Children.Add(opacity);
        }

        return mixer;
    }

    /// <summary>A mixer's slider: a 12 track round at its ends, a 20 thumb, in a 28 row; its track painted by the caller.</summary>
    private static Slider Track(double width, double min, double max, double value, string tag, string name)
    {
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Width = width,
            Height = CP.Slider,
            IsThumbToolTipEnabled = false,
            Foreground = Fluent.Clear(),
            Tag = tag,
        };

        // The template's own track (4 tall) is the mixer's 12, its thumb Karpo's 20.
        slider.Resources["SliderTrackThemeHeight"] = TrackHeight;
        slider.Resources["SliderHorizontalThumbWidth"] = ThumbSide;
        slider.Resources["SliderHorizontalThumbHeight"] = ThumbSide;
        slider.Resources["SliderTrackFillPointerOver"] = slider.Foreground;
        slider.Resources["SliderTrackFillPressed"] = slider.Foreground;
        slider.Resources["SliderTrackValueFill"] = slider.Foreground;
        slider.Resources["SliderTrackValueFillPointerOver"] = slider.Foreground;
        slider.Resources["SliderTrackValueFillPressed"] = slider.Foreground;
        AutomationProperties.SetName(slider, name);
        return slider;
    }

    private const double TrackHeight = 12;

    private const double ThumbSide = 20;


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
}
