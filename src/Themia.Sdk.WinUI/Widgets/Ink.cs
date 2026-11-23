using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12807] [S-12808] [S-12833] [S-14200] The one ink truth for drawing: what a measured part draws, read once in one
/// walk of its tree (<see cref="Of"/>, a <see cref="PartBox"/>): where its ink starts and ends inside its box over and under
/// (a family text's room over its first ink and under its last, a row's <see cref="IInked"/>, a panel's part that reaches
/// furthest up and down as it stands in it), before and after across ([S-14301]: a text's side bearings, a column's least
/// either side, a row's ends), the smallest text and glyph it draws (the legibility floor, <see cref="ScaleFloor"/>) and
/// how much wider its cut lines would ask to stand whole ([S-14791], <see cref="CutLine"/>). A text and a glyph are
/// measured as they are drawn (<see cref="InkMeter"/>: a text's letters over its capitals and its descenders, a glyph's
/// room in its em box); the family's model of capitals on the baseline (<see cref="LineInk.Of(TypeStep)"/>) stands only
/// where nothing is measured (no meter) and for pure sizing. A chart, a bar, anything else inks its whole box; a horizontal
/// slider its thumb, a toggle switch its track. The same walk hands a floor to every block whose pieces give way
/// (<see cref="Keep"/>, <see cref="IFacePieces"/>).
/// </summary>
public static class Ink
{
    /// <summary>
    /// What <paramref name="element"/> draws as it was last measured (its margin included): its box, its ink's room over,
    /// under and either side, its smallest text and glyph, its cut lines' whole width. [S-12813] A placeholder ("--") as it
    /// is drawn, as any text. Collapsed: nothing.
    /// </summary>
    public static PartBox Of(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Walk(element).Box(element);
    }

    /// <summary>[S-12833] Hand <paramref name="floor"/> to every block under <paramref name="element"/> whose pieces give way (shown or not).</summary>
    public static void Keep(UIElement element, double floor)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is IFacePieces pieces)
        {
            pieces.Keep(floor);
            return;
        }

        foreach (var child in Inside(element))
        {
            Keep(child, floor);
        }
    }

    /// <summary>What a node draws, as the walk reads it: its ink's room over, under and either side, its smallest text and glyph, its cut lines' more.</summary>
    private readonly record struct Read(LineInk Line, double Left, double Right, double Text, double Glyph, double Uncut)
    {
        public (double Left, double Right) Across => (Left, Right);

        public static Read Nothing => new(LineInk.None, 0, 0, double.PositiveInfinity, double.PositiveInfinity, 0);

        public PartBox Box(UIElement element)
            => element.Visibility != Visibility.Visible
                ? new PartBox(0, 0)
                : new PartBox(element.DesiredSize.Width, element.DesiredSize.Height, Line, Left, Right, Uncut: Uncut, Text: Text, Glyph: Glyph);
    }

    /// <summary>The children the walk reads under a node: a panel's, a border's, a content control's content, a viewbox's child.</summary>
    private static IEnumerable<UIElement> Inside(UIElement node) => node switch
    {
        Panel panel => panel.Children,
        Border { Child: { } child } => [child],
        ContentControl { Content: UIElement content } => [content],
        Viewbox { Child: { } child } => [child],
        _ => [],
    };

    /// <summary>The one walk: each shown node read once, its children's reads combined by the node's own rule.</summary>
    private static Read Walk(UIElement node)
    {
        if (node.Visibility != Visibility.Visible)
        {
            return Read.Nothing;
        }

        var kids = new Dictionary<UIElement, Read>();
        double text = double.PositiveInfinity, glyph = double.PositiveInfinity, uncut = 0;
        foreach (var child in Inside(node))
        {
            var read = Walk(child);
            kids[child] = read;
            text = Math.Min(text, read.Text);
            glyph = Math.Min(glyph, read.Glyph);
            uncut += read.Uncut;
        }

        Read Kid(UIElement child) => kids.TryGetValue(child, out var read) ? read : Read.Nothing;

        // [S-12833] The smallest text and glyph drawn: a text's (its runs' too), a glyph's, else its children's.
        switch (node)
        {
            case TextBlock block when block.Text.Length > 0 || block.Inlines.Count > 0:
                text = Math.Min(text, block.FontSize);
                foreach (var inline in block.Inlines)
                {
                    if (inline is Run { Text.Length: > 0 } run)
                    {
                        text = Math.Min(text, run.FontSize);
                    }
                }

                break;
            case FontIcon icon when icon.Glyph.Length > 0:
                glyph = Math.Min(glyph, icon.FontSize);
                break;
        }

        // [S-14791] A cut line's whole width over what it asks; a panel adds its parts' (side by side, the most it can be),
        // a border its child's; nothing else holds one.
        uncut = node switch
        {
            CutLine cut => Math.Max(0, cut.Whole - cut.DesiredSize.Width),
            Panel or Border => uncut,
            _ => 0,
        };

        var margin = node is FrameworkElement fe ? fe.Margin : default;
        var line = node switch
        {
            IInked part => part.Ink,
            Slider { Orientation: Orientation.Horizontal } slider => Thumb(slider),
            ToggleSwitch toggle => Track(toggle),
            TextBlock block => Text(block),
            FontIcon icon => Glyph(icon),
            Border border when border.Child is { } child => Add(Kid(child).Line, border.Padding.Top + border.BorderThickness.Top, border.Padding.Bottom + border.BorderThickness.Bottom),
            StackPanel { Orientation: Orientation.Vertical } stack => Add(Stacked(stack.Children, Kid), stack.Padding.Top, stack.Padding.Bottom),
            SpreadStack { Orientation: Orientation.Vertical } spread => Stacked(spread.Children, Kid),
            Panel panel => Beside(panel, Kid),
            _ => LineInk.None,
        };

        var (left, right) = node switch
        {
            IInked => (0.0, 0.0),
            TextBlock block => TextAcross(block),
            Border { Child: { } child } border => Pad(Kid(child).Across, border.Padding.Left + border.BorderThickness.Left, border.Padding.Right + border.BorderThickness.Right),
            StackPanel { Orientation: Orientation.Vertical } stack => Pad(Column(stack.Children, stack.DesiredSize.Width - stack.Margin.Left - stack.Margin.Right - stack.Padding.Left - stack.Padding.Right, Kid), stack.Padding.Left, stack.Padding.Right),
            SpreadStack { Orientation: Orientation.Vertical } spread => Column(spread.Children, spread.DesiredSize.Width - spread.Margin.Left - spread.Margin.Right, Kid),
            StackPanel { Orientation: Orientation.Horizontal } row => Pad(Row(row.Children, Kid), row.Padding.Left, row.Padding.Right),
            FontIcon icon => GlyphAcross(icon),
            Grid grid => Pad(Columns(grid, Kid), grid.Padding.Left, grid.Padding.Right),
            _ => (0.0, 0.0),
        };

        return new Read(Add(line, margin.Top, margin.Bottom), Math.Max(0, left + margin.Left), Math.Max(0, right + margin.Right), text, glyph, uncut);
    }

    /// <summary>A vertical stack: its first shown part's room over and its last's under.</summary>
    private static LineInk Stacked(UIElementCollection children, Func<UIElement, Read> kid)
    {
        var shown = children.Where(c => c.Visibility == Visibility.Visible).ToList();
        return shown.Count == 0 ? LineInk.None : new LineInk(kid(shown[0]).Line.Over, kid(shown[^1]).Line.Under);
    }

    /// <summary>Parts side by side (a grid, a row): the least room over and under any of them, where each stands in the panel's height.</summary>
    private static LineInk Beside(Panel panel, Func<UIElement, Read> kid)
    {
        // Its own box: what it measured less its own margin (each part's slot holds the part's).
        var own = panel is FrameworkElement self ? self.Margin.Top + self.Margin.Bottom : 0;
        var height = Math.Max(0, panel.DesiredSize.Height - own);
        var rows = panel is Grid grid ? Math.Max(1, grid.RowDefinitions.Count) : 1;
        double over = double.PositiveInfinity, under = double.PositiveInfinity;

        // A picture beside a text (an image, an icon slot, [S-6631], an item's lead, [S-16637]) draws inside its box (its own margins): the text says where the ink is. [S-14307] A glyph
        // the meter measures says where its own ink is (the Header's glyph stands over its title's capitals); one it cannot
        // measure is left to the text, as a picture.
        var shown = panel.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        var pictures = shown.Where(c => c is Image or Themia.Sdk.WinUI.IconSlot or Themia.Sdk.WinUI.Items.ItemLeadSlot { ShowsPicture: true } || (c is FontIcon icon && !Measured(icon))).ToList();
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
            var ink = kid(part).Line;
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

    /// <summary>Whether the meter measures <paramref name="icon"/>'s glyph.</summary>
    private static bool Measured(FontIcon icon)
        => InkMeter.Measure(new InkText(icon.Glyph, FontOf(icon.FontFamily), icon.FontSize, icon.FontWeight.Weight, 0)) is not null;

    /// <summary>
    /// [S-14283] A horizontal slider: the room its box holds over and under its thumb, centred on the rail. The thumb, its
    /// ring included, is the slider's first and last ink, read from its template as drawn (its <c>HorizontalThumb</c>'s
    /// height and its border standing outside it, a negative margin); before the template stands, the family's
    /// <see cref="ControlsRules.SliderThumb"/>.
    /// </summary>
    private static LineInk Thumb(Slider slider)
    {
        var thumb = ControlsRules.SliderThumb;
        if (Named(slider, "HorizontalThumb") is { Height: > 0 } part && double.IsFinite(part.Height))
        {
            var ring = VisualTreeHelper.GetChildrenCount(part) > 0 && VisualTreeHelper.GetChild(part, 0) is FrameworkElement root
                ? Math.Max(0, -(root.Margin.Top + root.Margin.Bottom))
                : 0;
            thumb = part.Height + ring;
        }

        var box = slider.DesiredSize.Height - slider.Margin.Top - slider.Margin.Bottom;
        var room = Math.Max(0, (box - thumb) / 2);
        return new LineInk(room, room);
    }

    /// <summary>
    /// [S-14283] A toggle switch: the room its template's box holds over and under its track, centred in it: the track read
    /// from its template as drawn (its <c>OuterBorder</c>'s height); before the template stands, <see cref="ControlsRules.SwitchTrack"/>.
    /// </summary>
    private static LineInk Track(ToggleSwitch toggle)
    {
        var track = Named(toggle, "OuterBorder") is { Height: > 0 } part && double.IsFinite(part.Height) ? part.Height : ControlsRules.SwitchTrack;
        var box = toggle.DesiredSize.Height - toggle.Margin.Top - toggle.Margin.Bottom;
        var room = Math.Max(0, (box - track) / 2);
        return new LineInk(room, room);
    }

    /// <summary>The part of a control's template named <paramref name="name"/>, as it stands in its visual tree; null before the template stands.</summary>
    private static FrameworkElement? Named(DependencyObject control, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(control);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(control, i);
            if (child is FrameworkElement { Name: var named } element && named == name)
            {
                return element;
            }

            if (Named(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static LineInk Add(LineInk ink, double over, double under) => new(ink.Over + over, ink.Under + under);

    /// <summary>
    /// [S-14301] Stand <paramref name="text"/>'s first ink, not its advance, where its box starts (WR-3): its margin at the
    /// start reaches back over the room its first letter leaves there, as the meter measures it (a block scaled 2.5 times
    /// grew a title's "D" to 4.7 past the start inset). The start does not move when the text is cut at its end.
    /// Unmeasured (no meter, no text): no room.
    /// </summary>
    public static void InkStart(TextBlock text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var room = string.IsNullOrEmpty(text.Text) || !(text.FontSize > 0) ? null
            : InkMeter.Room(new InkText(text.Text, FontOf(text.FontFamily), text.FontSize, text.FontWeight.Weight, 0, Tracking: text.CharacterSpacing));
        var rtl = text.FlowDirection == FlowDirection.RightToLeft;
        var start = room is { } r && double.IsFinite(r.Start(rtl)) ? Math.Max(0, r.Start(rtl)) : 0;
        var margin = rtl ? text.Margin with { Right = -start } : text.Margin with { Left = -start };
        if (text.Margin != margin)
        {
            text.Margin = margin;
        }
    }

    private static (double Left, double Right) Pad((double Left, double Right) room, double left, double right) => (room.Left + left, room.Right + right);

    /// <summary>
    /// A text's side bearings: one line as it measures unbounded; [S-15674] a text that wraps onto several lines as its lines
    /// stand at the width it wraps at, each where its alignment puts it (a centred sentence's widest line from its box's start
    /// to its end, its letters' bearings either side; it had been read with no room at its end, standing a centred word
    /// clock 4.7 DIP off centre at 900 × 300). Unmeasured: none.
    /// </summary>
    private static (double Left, double Right) TextAcross(TextBlock text)
    {
        var words = text.Text;
        if (string.IsNullOrEmpty(words) || !(text.FontSize > 0))
        {
            return (0, 0);
        }

        // A wrapping text that stands on one line ends as one line does.
        var line = text.LineHeight > 0 ? text.LineHeight : text.FontSize * 1.4;
        var wraps = text.TextWrapping != TextWrapping.NoWrap && text.DesiredSize.Height - text.Margin.Top - text.Margin.Bottom > line * 1.5;
        var rtl = text.FlowDirection == FlowDirection.RightToLeft;
        var ink = new InkText(words, FontOf(text.FontFamily), text.FontSize, text.FontWeight.Weight, 0, Tracking: text.CharacterSpacing);
        var room = InkMeter.Room(wraps ? ink with { Width = WrapWidth(text), Align = AlignOf(text.TextAlignment, rtl) } : ink);
        if (room is not { } r)
        {
            return (0, 0);
        }

        // The meter lays the lines left to right: right to left, its start is its right.
        var (start, end) = InkRoom.Ends(r, r, rtl, led: false);
        return rtl ? (end, start) : (start, end);
    }

    /// <summary>
    /// [S-15674] Where XAML stands a text's wrapped lines, as the meter lays them left to right (XAML's Left is its Start and
    /// its Right its End: right to left, the start is the right).
    /// </summary>
    private static InkAlign AlignOf(TextAlignment alignment, bool rtl) => alignment switch
    {
        TextAlignment.Center => InkAlign.Center,
        TextAlignment.End => rtl ? InkAlign.Start : InkAlign.End,
        _ => rtl ? InkAlign.End : InkAlign.Start,
    };

    /// <summary>A column's parts as they stand in its <paramref name="width"/>: the least room either side over them.</summary>
    private static (double Left, double Right) Column(UIElementCollection children, double width, Func<UIElement, Read> kid)
    {
        double left = double.PositiveInfinity, right = double.PositiveInfinity;
        foreach (var child in children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible))
        {
            // A part that stretches (a slider's row, a bar) is laid across the whole column, whatever it asked for; a text
            // stretched draws from its start, as wide as it asked.
            var w = child.HorizontalAlignment == HorizontalAlignment.Stretch && child is not TextBlock ? width : child.DesiredSize.Width;
            var x = child.HorizontalAlignment switch
            {
                HorizontalAlignment.Right => width - w,
                HorizontalAlignment.Center => (width - w) / 2,
                _ => 0,
            };
            var (l, r) = kid(child).Across;
            left = Math.Min(left, x + l);
            right = Math.Min(right, width - x - w + r);
        }

        return double.IsFinite(left) && double.IsFinite(right) ? (left, right) : (0, 0);
    }

    /// <summary>[S-14306] A glyph's room in its em box either side (its em box centred in the icon's box), unmeasured none.</summary>
    private static (double Left, double Right) GlyphAcross(FontIcon icon)
    {
        var size = icon.FontSize;
        if (InkMeter.Room(new InkText(icon.Glyph, FontOf(icon.FontFamily), size, icon.FontWeight.Weight, 0)) is not { } room)
        {
            return (0, 0);
        }

        var box = double.IsFinite(icon.Width) && icon.Width > 0 ? icon.Width : size;
        var em = Math.Max(0, (box - size) / 2);
        return (Math.Max(0, room.Left) + em, Math.Max(0, room.Right) + em);
    }

    /// <summary>
    /// [S-14306] A grid's sides: one column, its parts as they stand in it (a glyph centred in its mark); several, the parts
    /// standing at the first column's start and at the last column's end (a part centred in a column between says nothing).
    /// </summary>
    private static (double Left, double Right) Columns(Grid grid, Func<UIElement, Read> kid)
    {
        var width = grid.DesiredSize.Width - grid.Margin.Left - grid.Margin.Right - grid.Padding.Left - grid.Padding.Right;
        var columns = Math.Max(1, grid.ColumnDefinitions.Count);
        if (columns == 1)
        {
            return Column(grid.Children, width, kid);
        }

        // The first and last columns that hold a shown part (a collapsed glyph's column takes no room).
        var shown = grid.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        if (shown.Count == 0)
        {
            return (0, 0);
        }

        var first = shown.Min(Grid.GetColumn);
        var last = shown.Max(c => Grid.GetColumn(c) + Grid.GetColumnSpan(c));
        double left = double.PositiveInfinity, right = double.PositiveInfinity;
        foreach (var child in shown)
        {
            var (l, r) = kid(child).Across;
            var column = Grid.GetColumn(child);
            if (column == first && child.HorizontalAlignment is HorizontalAlignment.Left or HorizontalAlignment.Stretch)
            {
                left = Math.Min(left, l);
            }

            if (column + Grid.GetColumnSpan(child) >= last && child.HorizontalAlignment is HorizontalAlignment.Right or HorizontalAlignment.Stretch)
            {
                right = Math.Min(right, r);
            }
        }

        return (double.IsFinite(left) ? left : 0, double.IsFinite(right) ? right : 0);
    }

    /// <summary>A row's first shown part's start and its last's end.</summary>
    private static (double Left, double Right) Row(UIElementCollection children, Func<UIElement, Read> kid)
    {
        var shown = children.Where(c => c.Visibility == Visibility.Visible).ToList();
        return shown.Count == 0 ? (0, 0) : (kid(shown[0]).Left, kid(shown[^1]).Right);
    }

    /// <summary>
    /// [S-14200] A family text (its block line height): the room over its first ink and under its last as it is drawn,
    /// wrapped where it wraps and cut where its lines run out; a planned line saying nothing yet is the model's capitals on the
    /// baseline. A text on the font's own line: none.
    /// </summary>
    private static LineInk Text(TextBlock text)
    {
        if (!(text.LineHeight > 0))
        {
            return LineInk.None;
        }

        var step = new TypeStep(text.FontSize, text.LineHeight, false);
        return InkMeter.Line(step, text.Text, FontOf(text.FontFamily), text.FontWeight.Weight, WrapWidth(text), text.CharacterSpacing, text.MaxLines);
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
    /// broke), with a device pixel's slack for layout rounding (<see cref="LayoutMath.Pixel"/>); a text that does not wrap is one line.
    /// </summary>
    private static double WrapWidth(TextBlock text)
    {
        if (text.TextWrapping == TextWrapping.NoWrap)
        {
            return double.PositiveInfinity;
        }

        // [S-15761] A text XAML laid on one line is one line to the meter too: the meter's face (Segoe UI Variable at its nearest
        // optical size) sets a line a little wider than XAML's continuous optical size, so at the drawn width it wrapped the
        // last word under ("Could not sign out: this account is not allowed to" at Title, its "to" alone on a second line)
        // and read that line's foot, the "g" of "sign" unseen: the tile stood 4.7 DIP low, its line in the inset foot.
        var height = text.DesiredSize.Height - text.Margin.Top - text.Margin.Bottom;
        if (text.LineHeight > 0 && height > 0 && height < (2 * text.LineHeight) - LayoutMath.Tolerance)
        {
            return double.PositiveInfinity;
        }

        var width = text.DesiredSize.Width - text.Margin.Left - text.Margin.Right;
        return width > 0 ? width + LayoutMath.Pixel(text.XamlRoot?.RasterizationScale ?? 0) : double.PositiveInfinity;
    }
}
