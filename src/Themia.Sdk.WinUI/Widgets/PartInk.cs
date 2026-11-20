using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12807] [S-12808] Where a measured part's ink starts and ends inside its box (<see cref="LineInk"/>): a family
/// text's room over its first ink and under its last, a row's (<see cref="IInked"/>), and for a panel
/// the part of it that reaches furthest up and down, as it stands in the panel. [S-14200] A text and a glyph are measured
/// as they are drawn (<see cref="InkMeter"/>: a text's letters over its capitals and its descenders, a glyph's room in its
/// em box). A chart, a bar, anything else inks its whole box; a horizontal slider its thumb. What a
/// <see cref="SpreadStack"/> stands on the insets.
/// </summary>
public static class PartInk
{
    /// <summary>The room in <paramref name="element"/>'s box (its margin included) over its first ink and under its last; measured parts only.</summary>
    public static LineInk Of(UIElement element) => Of(element, held: false);

    /// <summary>
    /// [S-12813] The ink <paramref name="element"/> is sized by: <see cref="Of(UIElement)"/>, but a part showing a
    /// placeholder reads as what it stands for (<see cref="IInked.Held"/>).
    /// </summary>
    public static LineInk Held(UIElement element) => Of(element, held: true);

    private static LineInk Of(UIElement element, bool held)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Visibility != Visibility.Visible)
        {
            return LineInk.None;
        }

        var margin = element is FrameworkElement fe ? fe.Margin : default;
        var inner = element switch
        {
            IInked part => held ? part.Held : part.Ink,
            Slider { Orientation: Orientation.Horizontal } slider => Thumb(slider),
            ToggleSwitch toggle => Track(toggle),
            TextBlock text => Text(text, held),
            FontIcon icon => Glyph(icon),
            Border border when border.Child is { } child => Add(Of(child, held), border.Padding.Top + border.BorderThickness.Top, border.Padding.Bottom + border.BorderThickness.Bottom),
            StackPanel { Orientation: Orientation.Vertical } stack => Add(Stacked(stack.Children, held), stack.Padding.Top, stack.Padding.Bottom),
            SpreadStack { Orientation: Orientation.Vertical } spread => Stacked(spread.Children, held),
            Panel panel => Beside(panel, held),
            _ => LineInk.None,
        };

        return Add(inner, margin.Top, margin.Bottom);
    }

    /// <summary>A vertical stack: its first shown part's room over and its last's under.</summary>
    private static LineInk Stacked(UIElementCollection children, bool held)
    {
        var shown = children.Where(c => c.Visibility == Visibility.Visible).ToList();
        return shown.Count == 0 ? LineInk.None : new LineInk(Of(shown[0], held).Over, Of(shown[^1], held).Under);
    }

    /// <summary>Parts side by side (a grid, a row): the least room over and under any of them, where each stands in the panel's height.</summary>
    private static LineInk Beside(Panel panel, bool held)
    {
        // Its own box: what it measured less its own margin (each part's slot holds the part's).
        var own = panel is FrameworkElement self ? self.Margin.Top + self.Margin.Bottom : 0;
        var height = Math.Max(0, panel.DesiredSize.Height - own);
        var rows = panel is Grid grid ? Math.Max(1, grid.RowDefinitions.Count) : 1;
        double over = double.PositiveInfinity, under = double.PositiveInfinity;

        // A picture or a glyph beside a text draws inside its box (its own margins): the text says where the ink is.
        var shown = panel.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        var pictures = shown.Where(c => c is Image or FontIcon).ToList();
        if (pictures.Count < shown.Count)
        {
            shown = [.. shown.Except(pictures)];
        }

        foreach (var part in shown)
        {

            // In a grid of rows only the first row's parts can ink its top and only the last row's its foot.
            var row = panel is Grid ? Grid.GetRow(part) : 0;
            var span = panel is Grid ? Grid.GetRowSpan(part) : 1;
            // Only a text's or a glyph's place in the panel is read, and [S-14245] a part of a height of its own (a Controls
            // row's 28 icon button beside its 32 slider, centred: its foot 2 over the row's); any other part (a ring, a canvas
            // the panel sizes) is taken to ink the panel's whole height.
            var texty = part is IInked or TextBlock or Panel or Border or FontIcon || double.IsFinite(part.Height);
            var room = texty && part.DesiredSize.Height > 0 ? Math.Max(0, height - part.DesiredSize.Height) : 0;
            // A part shorter than its slot stands centred when it stretches (its own height holds it).
            var top = part.VerticalAlignment switch
            {
                VerticalAlignment.Top => 0,
                VerticalAlignment.Bottom => room,
                _ => room / 2,
            };
            var ink = Of(part, held);
            if (row == 0)
            {
                over = Math.Min(over, top + ink.Over);
            }

            if (row + span >= rows)
            {
                under = Math.Min(under, room - top + ink.Under);
            }
        }

        return double.IsFinite(over) && double.IsFinite(under) ? new LineInk(over, under) : LineInk.None;
    }

    /// <summary>
    /// The thumb of a horizontal slider, its ring included, measured on the renders: the template's box is taller
    /// than it (32), and the thumb is the slider's first and last ink.
    /// </summary>
    public const double SliderThumb = 22;

    /// <summary>A horizontal slider: the room its box holds over and under its thumb, centred on the rail.</summary>
    private static LineInk Thumb(Slider slider)
    {
        var box = slider.DesiredSize.Height - slider.Margin.Top - slider.Margin.Bottom;
        var room = Math.Max(0, (box - SliderThumb) / 2);
        return new LineInk(room, room);
    }

    /// <summary>[S-14283] A toggle switch: the room its template's box holds over and under its track, centred in it.</summary>
    private static LineInk Track(ToggleSwitch toggle)
    {
        var box = toggle.DesiredSize.Height - toggle.Margin.Top - toggle.Margin.Bottom;
        var room = Math.Max(0, (box - ControlsRules.SwitchTrack) / 2);
        return new LineInk(room, room);
    }

    private static LineInk Add(LineInk ink, double over, double under) => new(ink.Over + over, ink.Under + under);

    /// <summary>
    /// [S-14200] A family text (its block line height): the room over its first ink and under its last as it is drawn,
    /// wrapped where it wraps; a planned line saying nothing yet is the model's capitals on the baseline, and so is a
    /// placeholder read as what it stands for (<paramref name="held"/>, [S-12813]). A text on the font's own line: none.
    /// </summary>
    private static LineInk Text(TextBlock text, bool held)
    {
        if (!(text.LineHeight > 0))
        {
            return LineInk.None;
        }

        var step = new TypeStep(text.FontSize, text.LineHeight, false);
        return held && LineInk.Capless(text.Text)
            ? LineInk.Of(step)
            : InkMeter.Line(step, text.Text, FontOf(text.FontFamily), text.FontWeight.Weight, WrapWidth(text), text.CharacterSpacing);
    }

    /// <summary>
    /// [S-14200] A glyph: its ink's room in its em box (the font's own line), the em box centred in the icon's box. A
    /// glyph the meter cannot measure inks its whole box.
    /// </summary>
    private static LineInk Glyph(FontIcon icon)
    {
        var size = icon.FontSize;
        var box = double.IsFinite(icon.Height) && icon.Height > 0 ? icon.Height : size;
        if (InkMeter.Measure(new InkText(icon.Glyph, FontOf(icon.FontFamily), size, icon.FontWeight.Weight, 0)) is not { } ink)
        {
            return LineInk.None;
        }

        var room = Math.Max(0, (box - size) / 2);
        return new LineInk(ink.Over + room, ink.Under + room);
    }

    /// <summary>The family a text or a glyph is drawn in, as the meter names it (the first of a list); empty: the meter's own default.</summary>
    internal static string FontOf(FontFamily? family) => family?.Source ?? string.Empty;

    /// <summary>
    /// The width a wrapping text wraps at as it is laid out: its widest line (a greedy wrap at that width breaks where it
    /// broke), with a pixel's slack for layout rounding; a text that does not wrap is one line.
    /// </summary>
    private static double WrapWidth(TextBlock text)
    {
        if (text.TextWrapping == TextWrapping.NoWrap)
        {
            return double.PositiveInfinity;
        }

        var width = text.DesiredSize.Width - text.Margin.Left - text.Margin.Right;
        return width > 0 ? width + 1 : double.PositiveInfinity;
    }
}
