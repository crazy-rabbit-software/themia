using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-3719] [S-3724] The menu vocabulary's lines as the engine's rows: entries, and the one colour level.</summary>
internal partial class MenuPanel
{
    /// <summary>[S-3723] A swatch of a colour level's grid, in its 32-DIP cell, eight to a line (Karpo's).</summary>
    private const double SwatchSize = 24;

    private const double SwatchCell = 32;

    /// <summary>
    /// [S-3719] A widget's own lines (and the card's colours) as the menu's rows: its levels,
    /// searches, switches, choices, commands, the lines that ask ([S-3736], [S-3737]), links
    /// ([S-3749]), colours ([S-3724]) and notes ([S-3728]).
    /// </summary>
    protected List<MenuNode> FromEntries(IReadOnlyList<ItemMenuEntry> entries, string prefix)
    {
        var nodes = new List<MenuNode>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.IsSeparator)
            {
                nodes.Add(MenuNode.Separator(prefix + "/-" + i));
                continue;
            }

            var key = prefix + "/" + entry.Text;
            if (entry.Colour is { } colour)
            {
                nodes.Add(ColourNode(key, entry.Text, colour, entry.Disabled));
                continue;
            }

            if (entry.Note)
            {
                nodes.Add(new MenuNode(key, MenuNodeKind.Note) { Text = entry.Text });
                continue;
            }

            if (entry.Slider is { } slider)
            {
                nodes.Add(new MenuNode(key, MenuNodeKind.Slider)
                {
                    Text = entry.Text,
                    Value = entry.Value,
                    IconKey = entry.Glyph ?? string.Empty,
                    Icon = entry.Glyph is null ? null : () => ItemMenu.IconFor(entry),
                    Disabled = entry.Disabled,
                    Slider = slider,
                });
                continue;
            }

            var value = entry.Value ?? (entry.Shortcut is { } chord ? KeyChordText.Format(chord) : entry.Search?.Value);
            List<MenuNode>? children = null;
            if (entry.Children is { } kids)
            {
                children = FromEntries(kids, key);

                // [S-3718] A choice of one shows the option that holds, as the card's own choices do.
                var chosen = kids.FirstOrDefault(k => k is { Checked: true, Group: not null });
                if (chosen.Group is not null)
                {
                    value ??= chosen.Text;
                }
            }

            nodes.Add(new MenuNode(key)
            {
                Text = entry.Text,
                Value = value,
                IconKey = entry.IconFile ?? entry.Glyph ?? string.Empty,
                Icon = entry.Glyph is null && entry.IconFile is null ? null : () => ItemMenu.IconFor(entry),
                Run = entry.Action,
                Checked = entry.Checked,
                Radio = entry.Group is not null,
                Disabled = entry.Disabled,
                Danger = entry.Danger,
                Children = children,
                Search = entry.Search,
                SetsUp = entry.SetsUp,
                Order = entry.Order,
                Asks = entry.Asks && entry.Children is null && entry.Search is null,
                Link = entry.LinkTo,
            });
        }

        return nodes;
    }

    /// <summary>
    /// [S-3724] A colour row and its level, the one path every colour of the menu takes (the card's, a
    /// widget's own and the app's): Default (where the setting has one) and a fill's Transparent, ticked
    /// while they hold; under a rule the swatches, 24 DIPs in 32-DIP cells, eight to a line, the one that
    /// holds ringed in the accent and ticked; a fill's Opacity, a one-row slider ([S-3731], a colour only);
    /// under a rule Custom colour… (the picker's dialog) and Pick a colour from the screen (the
    /// eyedropper), those two where custom colours are allowed. The row shows the colour's swatch and
    /// its words; a mixed selection reads as a dash.
    /// </summary>
    protected MenuNode ColourNode(string key, string text, MenuColour colour, bool disabled)
    {
        var options = colour.Options;
        var chosen = colour.Chosen;

        // Every part of the value, so the swatches are drawn again (with a pick's opacity) when any changes.
        var held = colour.Mixed ? "mixed" : colour.IsFill ? colour.Fill!.Kind + "|" + chosen + "|" + colour.Opacity : "c|" + chosen;

        void Paint(Border swatch)
        {
            if (colour.Mixed)
            {
                return;
            }

            if (colour.IsFill)
            {
                ColorChoice.PaintFill(swatch, colour.Fill!, options);
            }
            else
            {
                ColorChoice.PaintSwatch(swatch, chosen, options);
            }
        }

        var children = new List<MenuNode>();
        if (options.DefaultLabel is { } defaultLabel)
        {
            children.Add(Option(key + "/default", defaultLabel, "default", () => Swatch(b =>
            {
                if (colour.IsFill)
                {
                    ColorChoice.PaintFill(b, FillChoice.Default(), options);
                }
                else
                {
                    ColorChoice.PaintSwatch(b, string.Empty, options);
                }
            }), colour.DefaultHolds, colour.ChooseDefault));
        }

        if (colour.IsFill)
        {
            children.Add(Option(key + "/transparent", Str.Shell_Fill_Transparent.Text(), "transparent", () => Swatch(b => ColorChoice.PaintFill(b, FillChoice.Transparent(), options)),
                colour.TransparentHolds, colour.ChooseTransparent));
        }

        children.Add(MenuNode.Separator(key + "/-swatches"));
        children.Add(new MenuNode(key + "/swatches", MenuNodeKind.Custom)
        {
            ValueKey = held,
            Build = () => Swatches(options.Presets, chosen, colour.Choose),
        });

        // [S-3731] A fill's opacity: a one-row slider, greyed while no colour holds.
        if (colour.OpacitySlider(Percent) is { } opacity)
        {
            children.Add(new MenuNode(key + "/opacity", MenuNodeKind.Slider)
            {
                Text = Str.Shell_Fill_Opacity.Text(),
                Value = colour.HasOpacity ? Percent(colour.Opacity) : null,
                Disabled = !colour.HasOpacity,
                Slider = opacity,
            });
        }

        if (options.AllowCustom)
        {
            children.Add(MenuNode.Separator(key + "/-more"));
            children.Add(new MenuNode(key + "/custom")
            {
                Text = Str.Shell_Color_Custom.Text(),
                IconKey = "custom",
                Icon = () => Glyph(Glyphs.Palette),
                Run = () => Custom(key, chosen, options, colour.IsFill ? colour.Opacity : null, colour.ChooseCustom),
            });
            children.Add(new MenuNode(key + "/screen")
            {
                Text = Str.Shell_Color_PickFromScreen.Text(),
                IconKey = "screen",
                Icon = () => Glyph(Glyphs.Eyedropper),
                Run = () => ColorChoice.PickFromScreen(colour.Choose),
            });
        }

        return new MenuNode(key)
        {
            Text = text,
            Value = colour.Mixed ? Mixed : colour.IsFill ? ColorChoice.DescribeFill(colour.Fill!, options) : ColorChoice.Describe(chosen, options),
            IconKey = held,
            Icon = () => Swatch(Paint),
            Disabled = disabled,
            Children = children,
        };
    }

    /// <summary>What a mixed selection's choice reads: a dash, as Explorer's properties show differing values.</summary>
    protected const string Mixed = "—";

    /// <summary>[S-3723] The custom colour dialog, from the row that asked for it.</summary>
    private void Custom(string key, string current, ColorChoiceOptions options, double? opacity, Action<string, double> choose)
    {
        _ = ColorChoice.CustomAsync(AskAnchor, current, options, opacity, choose);
        Log.Debug(Scope, $"custom colour asked from {key}");
    }

    /// <summary>
    /// [S-3723] A colour level's swatches, 24 DIPs in 32-DIP cells, eight to a line, the first under a
    /// row's icon: the chosen one with a tick in a colour that reads on it and the accent ring, each
    /// one's name as its tooltip. A click picks it, and the level stays open on the pick.
    /// </summary>
    private static FrameworkElement Swatches(IReadOnlyList<ColorPreset> presets, string current, Action<string> pick)
    {
        var grid = new Grid
        {
            Margin = new Thickness(RowInset + PadStart - ((SwatchCell - SwatchSize) / 2), Fluent.Space1, 0, Fluent.Space1),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        for (var c = 0; c < ColorPresets.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SwatchCell) });
        }

        for (var r = 0; r < (presets.Count + ColorPresets.Columns - 1) / ColorPresets.Columns; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SwatchCell) });
        }

        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var hex = HexColor.Normalise(preset.Hex);
            HexColor.TryParse(hex, out var r, out var g, out var b);
            var chosen = hex == current;

            var swatch = new Button
            {
                Width = SwatchSize,
                Height = SwatchSize,
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b)),
                BorderThickness = new Thickness(chosen ? 2 : Fluent.Hairline),
                BorderBrush = chosen ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue) : ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray),
                Content = chosen
                    ? new FontIcon
                    {
                        Glyph = Glyphs.Done,
                        FontSize = SmallGlyph,

                        // The tick is drawn ON the colour, so its colour is the colour's contrast, not a theme brush.
                        Foreground = new SolidColorBrush(ScreenPick.WantsDarkInk(r, g, b) ? Colors.Black : Colors.White),
                    }
                    : null,
            };

            // The colour stays itself under the pointer: only the ring says it is pointed at.
            swatch.Resources["ButtonBackgroundPointerOver"] = swatch.Background;
            swatch.Resources["ButtonBackgroundPressed"] = swatch.Background;
            swatch.Resources["ButtonBorderBrushPointerOver"] = ThemeBrush.Of("ControlStrokeColorSecondaryBrush", Colors.Gray);

            ToolTipService.SetToolTip(swatch, preset.Name);
            AutomationProperties.SetName(swatch, preset.Name);
            swatch.Click += (_, _) => Run(() => pick(hex));

            Grid.SetRow(swatch, i / ColorPresets.Columns);
            Grid.SetColumn(swatch, i % ColorPresets.Columns);
            grid.Children.Add(swatch);
        }

        return grid;
    }

    /// <summary>A colour's swatch the size of a menu icon, framed as Settings frames its swatches.</summary>
    protected static Border Swatch(Action<Border> paint)
    {
        var swatch = new Border
        {
            Width = Fluent.IconSize,
            Height = Fluent.IconSize,
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            BorderBrush = ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        paint(swatch);
        return swatch;
    }
}
