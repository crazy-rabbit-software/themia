using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-15101] An atom's line of text: one line in its card's box less the universal spacing (<see cref="AtomSpace.Pad"/>) on
/// every side, its capitals filling that room's height and the line standing on its baseline at the room's foot, its
/// descenders hanging into the spacing under it (<see cref="AtomRules.FontSize"/>), whatever the text says and however
/// long it is (owner, 2026-11-21: "the text should grow in height to fill up to the universal spacing"; "the text should
/// rather overflow ... than to shrink"; "the text feels as if it has too much space at the bottom"). Its first
/// ink stands on the room's start, its last on the room's end when it sits there; a text longer than the room keeps its
/// start and is cut at the room's end with its ellipsis. The panel takes its whole box.
/// </summary>
public sealed partial class AtomText : Panel
{
    private readonly TextBlock _text;
    private readonly bool _strong;
    private ContentAlign _align = ContentAlign.Left;
    private bool _figures;
    private double _font;

    /// <summary>The room's height the line was last fitted to, and the line's room over its ink at that fit.</summary>
    private double _fitted = double.NaN;
    private double _lift;

    /// <param name="role">The role its line is drawn in.</param>
    /// <param name="strong">Whether it is set strong (a title), not regular (a detail).</param>
    public AtomText(ColourRole role, bool strong)
    {
        _strong = strong;
        _text = Family.Text(Step(AtomRules.FontSize(AtomRules.TextRoom)), role);
        _text.MaxLines = 1;

        // The line is placed on its ink, to the fraction: rounding its box would move its capitals off the room's top.
        _text.UseLayoutRounding = false;
        Children.Add(_text);
    }

    /// <summary>What it says.</summary>
    public string Text
    {
        get => _text.Text;
        set
        {
            if (!string.Equals(_text.Text, value, StringComparison.Ordinal))
            {
                _text.Text = value ?? string.Empty;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The brush its line is drawn in.</summary>
    public Brush Foreground
    {
        get => _text.Foreground;
        set => _text.Foreground = value;
    }

    /// <summary>[S-15105] Where its line sits across the room: at the start (a new one), the middle or the end.</summary>
    public ContentAlign Align
    {
        get => _align;
        set
        {
            if (value != _align)
            {
                _align = value;
                _text.TextAlignment = Aligned.Text(value);
                InvalidateArrange();
            }
        }
    }

    /// <summary>
    /// [S-14319] Its text is a reading that runs (a time): its figures are tabular and it stands on its advance (less a
    /// figure's own side room), not its first figure's ink, so it never moves as it runs (WR-15).
    /// </summary>
    public void Figures()
    {
        _figures = true;
        Family.Figures(_text);
    }

    /// <summary>The font size it is drawn at now (0 before its first layout).</summary>
    public double FontSize => _font;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        Lay(width, height);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (x, width) = Lay(finalSize.Width, finalSize.Height);
        _text.Arrange(new Rect(x, AtomSpace.Pad - _lift, width, AtomRules.LineHeight(_font)));
        return finalSize;
    }

    /// <summary>
    /// Set the line on the size the box's height gives it and measure it in the room's width; where its box starts and
    /// how wide it is, so that its ink (not its advance) runs from the room's start to the room's end.
    /// </summary>
    private (double X, double Width) Lay(double width, double height)
    {
        var room = AtomRules.Room(width, height);
        if (!room.Height.Equals(_fitted))
        {
            _fitted = room.Height;
            Fit(room.Height);
        }

        var (start, end) = Bearings();
        var box = room.Width + start + end;
        _text.Measure(new Size(box, double.PositiveInfinity));
        return (AtomSpace.Pad - start, box);
    }

    /// <summary>
    /// [S-15101] Set the line on the size at which the face's capitals (<see cref="AtomRules.Capital"/>) are exactly a
    /// room <paramref name="height"/> tall, as the meter measures them at that size (the face's optical size changes its
    /// proportions a little), held so that its descenders stay in the card (<see cref="AtomRules.Held"/>); no meter: the
    /// model's proportions.
    /// </summary>
    private void Fit(double height)
    {
        var font = Math.Max(1, AtomRules.FontSize(height));
        var lift = AtomRules.Lift(font);

        // The capitals at the model's size correct the size; the descenders at that size hold it; the capitals at the
        // size it then has place the line.
        if (Capitals(font) is { } first)
        {
            font = Math.Max(1, AtomRules.Refit(font, first.Height, height));
            if (Capitals(font) is { } caps)
            {
                var deep = Measure(AtomRules.Deep, font);
                font = Math.Max(1, AtomRules.Held(font, caps.Height, deep is { } d ? caps.Under - d.Under : 0, height));
                lift = Capitals(font)?.Over ?? AtomRules.Lift(font);
            }
        }

        _lift = lift;
        if (!font.Equals(_font))
        {
            _font = font;
            Family.Step(_text, Step(font));
        }
    }

    /// <summary>The line's room over its capitals and under its baseline at <paramref name="font"/>, and how tall they are; no meter: none.</summary>
    private (double Over, double Under, double Height)? Capitals(double font)
        => Measure(AtomRules.Capital, font) is { } ink ? (ink.Over, ink.Under, AtomRules.LineHeight(font) - ink.Over - ink.Under) : null;

    private LineInk? Measure(string letters, double font)
        => InkMeter.Measure(new InkText(letters, Ink.FontOf(_text.FontFamily), font, Weight(font), AtomRules.LineHeight(font)));

    /// <summary>The weight the family draws a line of <paramref name="font"/> in: set it on the step, read it back.</summary>
    private int Weight(double font)
    {
        Family.Step(_text, Step(font));
        _font = font;
        return _text.FontWeight.Weight;
    }

    /// <summary>
    /// The room its first letter leaves before its ink and its last after it. A running reading stands on its advance,
    /// which never moves as its figures change; it is set back by the room a tabular figure leaves either side (a "0"'s,
    /// whatever it shows), so its figures too stand on the room's start and end.
    /// </summary>
    private (double Start, double End) Bearings()
    {
        var words = _figures ? "0" : _text.Text;
        if (string.IsNullOrEmpty(_text.Text))
        {
            return (0, 0);
        }

        var ink = InkMeter.Room(new InkText(words, Ink.FontOf(_text.FontFamily), _font, _text.FontWeight.Weight, 0, Tracking: _text.CharacterSpacing, Tabular: _figures));
        if (ink is not { } room)
        {
            return (0, 0);
        }

        var rtl = _text.FlowDirection == FlowDirection.RightToLeft;
        static double Held(double bearing) => double.IsFinite(bearing) ? Math.Max(0, bearing) : 0;
        return (Held(room.Start(rtl)), Held(rtl ? room.Left : room.Right));
    }

    private TypeStep Step(double font) => new(font, AtomRules.LineHeight(font), _strong);
}
