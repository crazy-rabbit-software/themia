using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Run = Microsoft.UI.Xaml.Documents.Run;
using Typography = Microsoft.UI.Xaml.Documents.Typography;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-667] The hero readout: the one big value a widget is about — a temperature, a price — with
/// its unit, the short token that belongs beside it (a change, a session), an optional leading
/// glyph, and caption lines that stand beside the value where they fit and under it where not.
/// <para>
/// One control so that Weather and Stocks (and every widget that adopts it) draw a big number the
/// same way. The value, the unit and the token are <b>runs of one text block</b>: text of different
/// sizes in one block shares a baseline, which three bottom-aligned blocks of different line
/// heights never did (the change sat a few pixels under the price). The unit is smaller and
/// regular (<see cref="HeroLayout.UnitStep"/>), in the secondary colour, as Windows writes "45 GB".
/// Lines under the value start at the readout's own left edge, not under the number, so they
/// share the inset with every other line on the card.
/// </para>
/// <para>
/// The control only draws. Which step, whether the glyph shows and where the lines go are the
/// caller's ladder's decisions (<see cref="Lay"/>), measured with <see cref="HeroLayout"/>.
/// </para>
/// <para>
/// [S-6583] The value's row (glyph, value, lines beside) is a grid of its own over the lines
/// under, so <see cref="Align"/> can set it at the start, the middle or the end of the readout's
/// width and the lines under it with it, whichever of the two is wider.
/// </para>
/// </summary>
public sealed partial class HeroReadout : Grid
{
    private readonly TextBlock _line = new()
    {
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly Run _value = new();
    private readonly Run _unit = new();
    private readonly TextBlock _trailingLine;
    private readonly StackPanel _lines = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _top = new();
    private readonly StackPanel _under = new();
    private readonly List<Run> _trailing = [];
    private bool _unitSpaced;
    private string _unitText = string.Empty;
    private ContentAlign _align = ContentAlign.Left;
    private bool _linesBeside;

    public HeroReadout()
    {
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Icon = Fluent.Icon(Glyphs.Info, "TextFillColorSecondaryBrush", HeroLayout.IconSize(FluentType.Subtitle));
        Icon.Margin = new Thickness(0, 0, HeroLayout.IconGap, 0);
        Icon.Visibility = Visibility.Collapsed;

        _line.Foreground = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
        _unit.Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);
        _unit.FontWeight = FontWeights.Normal;
        _line.Inlines.Add(_value);
        _line.Inlines.Add(_unit);

        // Tabular figures, so a price ticking from 419.99 to 420.01 does not shuffle what follows it.
        Typography.SetNumeralAlignment(_line, FontNumeralAlignment.Tabular);

        _trailingLine = Fluent.Text(FluentType.Body);
        _trailingLine.Visibility = Visibility.Collapsed;
        _under.Children.Add(_trailingLine);

        SetColumn(Icon, 0);
        SetColumn(_line, 1);
        SetRow(_under, 1);
        _top.Children.Add(Icon);
        _top.Children.Add(_line);
        _top.Children.Add(_lines);
        Children.Add(_top);
        Children.Add(_under);
    }

    /// <summary>The leading glyph (hidden until <see cref="Lay"/> shows it). The caller sets its glyph.</summary>
    public FontIcon Icon { get; }

    /// <summary>The value line itself, for a tooltip.</summary>
    public TextBlock ValueLine => _line;

    /// <summary>The value and its unit: "14" and "°C" (no space), "420.27" and "USD" (<paramref name="spaced"/>).</summary>
    public void SetValue(string value, string unit, bool spaced)
    {
        _value.Text = value;
        _unitText = unit;
        _unitSpaced = spaced;
        _unit.Text = unit.Length == 0 ? string.Empty : (spaced ? " " : string.Empty) + unit;
    }

    /// <summary>Show or hide the unit without forgetting it (a narrow tile sheds the currency).</summary>
    public void ShowUnit(bool show)
        => _unit.Text = !show || _unitText.Length == 0 ? string.Empty : (_unitSpaced ? " " : string.Empty) + _unitText;

    /// <summary>
    /// The token beside the value, as runs the caller styles (a coloured change, a session glyph
    /// and its words). Replaces the previous token; an empty list is none.
    /// </summary>
    public void SetTrailing(IReadOnlyList<Run> runs)
    {
        Detach(_trailing);
        _trailing.Clear();
        _trailing.AddRange(runs);
    }

    /// <summary>
    /// [S-6583] Where the value's row and the lines under it sit across the readout: the start edge
    /// (the default), the middle or the end. Lines beside the value still read from it outwards.
    /// </summary>
    public void Align(ContentAlign align)
    {
        _align = align;
        _top.HorizontalAlignment = align == ContentAlign.Left ? HorizontalAlignment.Stretch : Aligned.Horizontal(align);
        AlignLines();
    }

    private void AlignLines()
    {
        var under = Aligned.Text(_align);
        _trailingLine.TextAlignment = under;
        foreach (var line in _lines.Children.OfType<TextBlock>())
        {
            line.TextAlignment = _linesBeside ? TextAlignment.Left : under;
        }
    }

    /// <summary>A caption line, beside or under the value per <see cref="Lay"/>, in the order added.</summary>
    public TextBlock AddLine(TypeStep step, string colorKey = "TextFillColorSecondaryBrush")
    {
        var line = Fluent.Text(step, colorKey);
        line.TextAlignment = _linesBeside ? TextAlignment.Left : Aligned.Text(_align);
        _lines.Children.Add(line);
        return line;
    }

    /// <summary>
    /// Draw at <paramref name="step"/>. The glyph grows with the value; the token stands on the
    /// value's baseline or on a line of its own under it, at <see cref="HeroLayout.TrailingStep"/>
    /// unless the caller's style gives it a step of its own (<paramref name="trailing"/>, [S-6166]);
    /// the lines stand beside the value (centred on it) or under it.
    /// </summary>
    public void Lay(TypeStep step, bool icon, bool trailingBeside, bool linesBeside, TypeStep? trailing = null)
    {
        _line.LineHeight = step.LineHeight;
        _value.FontSize = step.FontSize;
        _value.FontWeight = step.SemiBold ? FontWeights.SemiBold : FontWeights.Normal;
        _unit.FontSize = HeroLayout.UnitStep(step).FontSize;

        Icon.Visibility = icon ? Visibility.Visible : Visibility.Collapsed;
        Icon.FontSize = HeroLayout.IconSize(step);

        // The token: on the value's baseline, or the first line under it.
        Detach(_trailing);
        var trailingStep = trailing ?? HeroLayout.TrailingStep(step);

        if (trailingBeside)
        {
            foreach (var run in _trailing)
            {
                run.FontSize = trailingStep.FontSize;
                _line.Inlines.Add(run);
            }

            _trailingLine.Visibility = Visibility.Collapsed;
        }
        else
        {
            _trailingLine.Apply(trailingStep);

            foreach (var run in _trailing)
            {
                run.FontSize = trailingStep.FontSize;
                _trailingLine.Inlines.Add(run);
            }

            _trailingLine.Visibility = _trailing.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // The lines: a column beside the value, or the rest of the stack under it.
        // Out of both holders by hand: Parent is not reliable before the readout is in the live
        // tree (a meter lays out while it attaches), and adding a child twice throws.
        _top.Children.Remove(_lines);
        _under.Children.Remove(_lines);

        if (linesBeside)
        {
            _lines.Margin = new Thickness(HeroLayout.BesideGap, 0, 0, 0);
            _lines.VerticalAlignment = VerticalAlignment.Center;
            SetRow(_lines, 0);
            SetColumn(_lines, 2);
            SetColumnSpan(_lines, 1);
            _top.Children.Add(_lines);
        }
        else
        {
            _lines.Margin = new Thickness(0);
            _lines.VerticalAlignment = VerticalAlignment.Top;
            _under.Children.Add(_lines);
        }

        _under.Visibility = _trailingLine.Visibility == Visibility.Visible || !linesBeside
            ? Visibility.Visible
            : Visibility.Collapsed;

        _linesBeside = linesBeside;
        AlignLines();
    }

    /// <summary>Take the token's runs out of whichever block holds them, so they can move.</summary>
    private void Detach(List<Run> runs)
    {
        foreach (var run in runs)
        {
            _line.Inlines.Remove(run);
            _trailingLine.Inlines.Remove(run);
        }
    }
}

/// <summary>
/// [S-668] [S-669] The other parts widgets share: a stat line, a stat cell, a figure. Sized by
/// <see cref="PartMetrics"/>, so the ladder that budgets for a part and the part agree.
/// </summary>
public static class WidgetParts
{
    /// <summary>A run in a colour, for <see cref="HeroReadout.SetTrailing"/>.</summary>
    public static Run Run(string text, Brush brush) => new() { Text = text, Foreground = brush };

    /// <summary>A glyph as a run, so it shares the text's baseline (a session's sun or moon).</summary>
    public static Run GlyphRun(string glyph, Brush brush) => new()
    {
        Text = glyph,
        Foreground = brush,
        FontFamily = SymbolFont(),
    };

    /// <summary>
    /// [S-668] One reading on one line: a 16px glyph, the label in the secondary colour, the value
    /// at the end in the primary — the shape of a Settings info row, at <see cref="PartMetrics.StatLine"/>.
    /// </summary>
    public static Grid StatLine(string glyph, string label, string value) => new StatPart(FactStyle.Line, glyph).Show(label, value).Root;

    /// <summary>
    /// [S-668] One reading as a cell: a 12px glyph and the Caption label over the value in Body
    /// Strong, no box — the Windows Weather widget's detail cell, at <see cref="PartMetrics.StatCell"/>.
    /// </summary>
    public static Grid StatCell(string glyph, string label, string value) => new StatPart(FactStyle.Cell, glyph).Show(label, value).Root;

    /// <summary>
    /// [S-669] A number in a list column: right-aligned, tabular, at least <see cref="PartMetrics.FigureWidth"/>
    /// wide, so a column of temperatures lines up down the rows.
    /// </summary>
    public static TextBlock Figure(string text, TypeStep step, string colorKey = "TextFillColorPrimaryBrush")
    {
        var block = Fluent.Text(step, colorKey);
        block.Text = text;
        block.TextAlignment = TextAlignment.Right;
        block.MinWidth = PartMetrics.FigureWidth;
        Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
        return block;
    }

    private static FontFamily? _symbols;

    private static FontFamily SymbolFont()
        => _symbols ??= Application.Current.Resources.TryGetValue("SymbolThemeFontFamily", out var font) && font is FontFamily family
            ? family
            : new FontFamily("Segoe Fluent Icons");
}

/// <summary>
/// [S-668] [S-3862] A reading as a stat line or a stat cell: the one implementation of both shapes,
/// kept and restyled in place by a widget that rewrites its readings every tick (the detail
/// widgets), built once by one that does not (<see cref="WidgetParts.StatLine"/>). Without a glyph
/// the label starts at the part's own edge.
/// </summary>
public sealed class StatPart
{
    private readonly FontIcon? _icon;
    private readonly TextBlock _name = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _figure = Fluent.Text(FluentType.Body);
    private FactStyle? _style;

    public StatPart(FactStyle style, string? glyph = null)
    {
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (!string.IsNullOrEmpty(glyph))
        {
            _icon = Fluent.Icon(glyph, "TextFillColorSecondaryBrush", PartMetrics.LineIcon);
            _icon.VerticalAlignment = VerticalAlignment.Center;
            Root.Children.Add(_icon);
        }

        Typography.SetNumeralAlignment(_figure, FontNumeralAlignment.Tabular);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(_name, 1);
        Root.Children.Add(_name);
        Root.Children.Add(_figure);
        Restyle(style);
    }

    public Grid Root { get; } = new();

    /// <summary>The value's text block, for a caller that colours a reading that is not available.</summary>
    public TextBlock Figure => _figure;

    /// <summary>Draw as a line or as a cell; nothing is touched when the shape is already this one.</summary>
    public StatPart Restyle(FactStyle style)
    {
        if (_style == style)
        {
            return this;
        }

        _style = style;
        var line = style == FactStyle.Line;
        Root.Height = line ? PartMetrics.StatLine : PartMetrics.StatCell;

        if (_icon is not null)
        {
            _icon.FontSize = line ? PartMetrics.LineIcon : PartMetrics.CaptionIcon;
            _icon.Margin = new Thickness(0, 0, line ? Fluent.Space2 : Fluent.Space1, 0);
        }

        _name.Apply(line ? FluentType.Body : FluentType.Caption);
        _name.VerticalAlignment = line ? VerticalAlignment.Center : VerticalAlignment.Top;
        _figure.Apply(line ? FluentType.Body : FluentType.BodyStrong);
        _figure.VerticalAlignment = line ? VerticalAlignment.Center : VerticalAlignment.Top;
        _figure.Margin = line ? new Thickness(Fluent.Space3, 0, 0, 0) : new Thickness(0);
        Grid.SetRow(_figure, line ? 0 : 1);
        Grid.SetColumn(_figure, line ? 2 : 0);
        Grid.SetColumnSpan(_figure, line ? 1 : 3);
        return this;
    }

    /// <summary>The label and the value, written only when they changed.</summary>
    public StatPart Show(string label, string value)
    {
        if (_name.Text != label)
        {
            _name.Text = label;
        }

        if (_figure.Text != value)
        {
            _figure.Text = value;
        }

        return this;
    }
}
