using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12703] The weather graphs' one part, on the Chart block ([S-6717]): the top line (the kind's glyph leading the
/// reading now, primary or its band's Caution/Critical, a chosen measure after it, secondary; the stale glyph at the
/// line's end, [S-7042]), the plot, the wind's arrow line and the time line ("Now" and the window's end, Caption tertiary).
/// The plot draws the shaded spans (Subtle), the days' borders (Track hairlines), the series as a line, a line
/// with its 16 % wash, or round columns, each piece in its band's role, the second series tertiary, the moment
/// now (a hairline and a dot on the line) and the figures at the marked slots. Geometry is <see cref="HourGraphRules"/>'s;
/// every kind draws through this one part so they read as one family. Time runs left to right in every language.
/// <para>
/// [S-12800] It keeps the family's inset at the sides and the foot only: the card gives its top ([S-5700]), the same
/// joined or alone ([S-12483]).
/// </para>
/// </summary>
public sealed partial class HourGraph : Grid
{
    private readonly FontIcon _glyph = Family.Glyph(string.Empty, FamilyShape.Icon, ColourRole.TextSecondary);
    private readonly TextBlock _reading = Family.Text(FamilyType.BodyStrong);
    private readonly TextBlock _label = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly FontIcon _problem = Family.Glyph(Shell.Glyphs.Warning, FamilyShape.Icon, ColourRole.Caution);
    private readonly Grid _plot = new() { IsHitTestVisible = false };
    private readonly Canvas _arrows = new() { IsHitTestVisible = false, Height = FamilyShape.Icon };
    private readonly TextBlock _start = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly TextBlock _end = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private HourGraphModel? _model;

    public HourGraph()
    {
        FlowDirection = FlowDirection.LeftToRight;
        Margin = Family.SideInset with { Bottom = FamilySpace.Inset };
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // [S-12801] The glyph says what the reading is; the measure only where the kind offers a choice of them.
        var top = new Grid { Margin = new Thickness(0, 0, 0, FamilySpace.S2), ColumnSpacing = FamilySpace.GlyphGap };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        _label.TextTrimming = TextTrimming.CharacterEllipsis;
        _label.TextWrapping = TextWrapping.NoWrap;
        _problem.VerticalAlignment = VerticalAlignment.Center;
        _problem.Visibility = Visibility.Collapsed;
        SetColumn(_reading, 1);
        SetColumn(_label, 2);
        SetColumn(_problem, 3);
        top.Children.Add(_glyph);
        top.Children.Add(_reading);
        top.Children.Add(_label);
        top.Children.Add(_problem);
        Children.Add(top);

        SetRow(_plot, 1);
        Children.Add(_plot);

        _arrows.Margin = new Thickness(0, FamilySpace.S1, 0, 0);
        _arrows.Visibility = Visibility.Collapsed;
        SetRow(_arrows, 2);
        Children.Add(_arrows);

        var times = new Grid { Margin = new Thickness(0, FamilySpace.S1, 0, 0) };
        _end.HorizontalAlignment = HorizontalAlignment.Right;
        times.Children.Add(_start);
        times.Children.Add(_end);
        SetRow(times, 3);
        Children.Add(times);

        _plot.SizeChanged += (_, _) => Draw();
    }

    /// <summary>Draw <paramref name="model"/>; the plot is drawn again whenever its box changes.</summary>
    public void Show(HourGraphModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _glyph.Glyph = model.Glyph;
        _glyph.Visibility = model.Glyph.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _label.Text = model.Label;
        _label.Visibility = model.Label.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _reading.Text = model.Reading;
        _reading.Foreground = Family.Brush(model.ReadingRole);
        _start.Text = model.StartLabel;
        _end.Text = model.EndLabel;
        _arrows.Visibility = model.Arrows ? Visibility.Visible : Visibility.Collapsed;
        Draw();
    }

    /// <summary>[S-7042] The stale rule's lines: a Caution glyph after the reading, its tooltip the lines; none hides it.</summary>
    public void Problems(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _problem.Visibility = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_problem, lines.Count > 0 ? string.Join(Environment.NewLine, lines) : null);
    }

    private void Draw()
    {
        _plot.Children.Clear();
        _arrows.Children.Clear();
        if (_model is not { Slots.Count: > 0 } model)
        {
            return;
        }

        var (w, h) = (_plot.ActualWidth, _plot.ActualHeight);
        if (!(w > 0) || !(h > 0))
        {
            return;
        }

        var slots = model.Slots;
        var n = slots.Count;
        var first = slots[0].At;
        var canvas = new Canvas();

        // Behind everything: the shaded spans, then the days' borders.
        foreach (var span in model.Shade)
        {
            var from = Math.Clamp(HourGraphRules.X(span.Rise, first, model.Step, n, w), 0, w);
            var to = Math.Clamp(HourGraphRules.X(span.Set, first, model.Step, n, w), 0, w);
            if (to - from > 0)
            {
                var shade = new Rectangle { Width = to - from, Height = h, Fill = Family.Brush(ColourRole.Subtle), RadiusX = FamilyShape.Radius, RadiusY = FamilyShape.Radius };
                Canvas.SetLeft(shade, from);
                canvas.Children.Add(shade);
            }
        }

        foreach (var midnight in HourGraphRules.Midnights(first, slots[^1].At, TimeZoneInfo.Local))
        {
            Rule(canvas, HourGraphRules.X(midnight, first, model.Step, n, w), h, ColourRole.Track);
        }

        var values = slots.Select(s => s.Value).ToList();
        if (model.Look == HourGraphLook.Bars)
        {
            Bars(canvas, slots, w, h);
        }
        else
        {
            Lines(canvas, slots, values, w, h, wash: model.Look == HourGraphLook.Area);
        }

        Second(canvas, slots, w, h);
        Now(canvas, model, values, w, h);
        Figures(canvas, model, w, h);
        Arrows(slots, w);
        _plot.Children.Add(canvas);
    }

    private static Point Point(IReadOnlyList<HourSlot> slots, int i, double w, double h)
        => new(HourGraphRules.X(i, slots.Count, w), HourGraphRules.Y(slots[i].Value ?? 0, h));

    /// <summary>Each run's pieces in the worse role of their two ends; a lone slot is a dot. With a wash, the area under each piece.</summary>
    private static void Lines(Canvas canvas, IReadOnlyList<HourSlot> slots, List<double?> values, double w, double h, bool wash)
    {
        var foot = HourGraphRules.Y(0, h);
        foreach (var (firstSlot, lastSlot) in HourGraphRules.Runs(values))
        {
            if (firstSlot == lastSlot)
            {
                Dot(canvas, Point(slots, firstSlot, w, h), slots[firstSlot].Role);
                continue;
            }

            // A run is drawn as stretches of one role, so the stroke joins smoothly inside each.
            var start = firstSlot;
            while (start < lastSlot)
            {
                var role = HourGraphRules.Worse(slots[start].Role, slots[start + 1].Role);
                var end = start + 1;
                while (end < lastSlot && HourGraphRules.Worse(slots[end].Role, slots[end + 1].Role) == role)
                {
                    end++;
                }

                var points = Enumerable.Range(start, end - start + 1).Select(i => Point(slots, i, w, h)).ToList();
                var brush = Family.Brush(role);
                if (wash)
                {
                    canvas.Children.Add(new Path { Data = Figure(points, closedAt: foot), Fill = brush, Opacity = FamilyShape.Wash });
                }

                canvas.Children.Add(new Path { Data = Figure(points, closedAt: null), Stroke = brush, StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
                start = end;
            }
        }
    }

    /// <summary>A round column per slot with a value over nothing, filled from the plot's foot in its role.</summary>
    private static void Bars(Canvas canvas, IReadOnlyList<HourSlot> slots, double w, double h)
    {
        var cell = w / slots.Count;
        var width = HourGraphRules.BarWidth(cell);
        var foot = h;
        var room = Math.Max(0, h - FamilyType.Caption.LineHeight);
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].Value is not { } share || !(share > 0))
            {
                continue;
            }

            // A wet slot never under the track, so a drizzle is a low pill, not a dot as tall as a downpour.
            var height = Math.Max(Math.Min(width, FamilyShape.Track), share * room);
            var bar = new Border
            {
                Width = width,
                Height = height,
                Background = Family.Brush(slots[i].Role),
                CornerRadius = Family.Round(width, height),
            };
            Canvas.SetLeft(bar, HourGraphRules.X(i, slots.Count, w) - (width / 2));
            Canvas.SetTop(bar, foot - height);
            canvas.Children.Add(bar);
        }
    }

    /// <summary>The second series (feels like, gusts, the chance of rain): tertiary, on the same frame.</summary>
    private static void Second(Canvas canvas, IReadOnlyList<HourSlot> slots, double w, double h)
    {
        var seconds = slots.Select(s => s.Second).ToList();
        foreach (var (firstSlot, lastSlot) in HourGraphRules.Runs(seconds))
        {
            var points = Enumerable.Range(firstSlot, lastSlot - firstSlot + 1)
                .Select(i => new Point(HourGraphRules.X(i, slots.Count, w), HourGraphRules.Y(seconds[i]!.Value, h)))
                .ToList();
            if (points.Count < 2)
            {
                continue;
            }

            canvas.Children.Add(new Path { Data = Figure(points, closedAt: null), Stroke = Family.Brush(ColourRole.TextTertiary), StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round });
        }
    }

    /// <summary>The moment now: a secondary hairline down the plot and, on a line, a dot where the line is.</summary>
    private static void Now(Canvas canvas, HourGraphModel model, List<double?> values, double w, double h)
    {
        var slots = model.Slots;
        var x = HourGraphRules.X(model.Now, slots[0].At, model.Step, slots.Count, w);
        if (x < 0 || x > w)
        {
            return;
        }

        Rule(canvas, x, h, ColourRole.TextSecondary);
        if (model.Look == HourGraphLook.Bars)
        {
            return;
        }

        // The line's height at now, between the two slots around it.
        var at = (x * slots.Count / w) - 0.5;
        var i = Math.Clamp((int)Math.Floor(at), 0, slots.Count - 1);
        var j = Math.Min(i + 1, slots.Count - 1);
        if (values[i] is not { } a)
        {
            return;
        }

        var b = values[j] ?? a;
        var t = Math.Clamp(at - i, 0, 1);
        Dot(canvas, new Point(x, HourGraphRules.Y(a + ((b - a) * t), h)), slots[i].Role);
    }

    /// <summary>The figures at the marked slots: over a point in primary, under it in secondary, kept inside the plot.</summary>
    private static void Figures(Canvas canvas, HourGraphModel model, double w, double h)
    {
        var line = FamilyType.Caption.LineHeight;
        foreach (var mark in model.Marks)
        {
            if (mark.Slot < 0 || mark.Slot >= model.Slots.Count || model.Slots[mark.Slot].Value is not { } share)
            {
                continue;
            }

            var text = Family.Text(FamilyType.Caption, mark.Under ? ColourRole.TextSecondary : ColourRole.TextPrimary, mark.Text);
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = text.DesiredSize.Width;
            var x = Math.Clamp(HourGraphRules.X(mark.Slot, model.Slots.Count, w) - (width / 2), 0, Math.Max(0, w - width));
            var y = model.Look == HourGraphLook.Bars
                ? h - Math.Max(Math.Min(HourGraphRules.BarWidth(w / model.Slots.Count), FamilyShape.Track), share * Math.Max(0, h - line)) - line
                : HourGraphRules.Y(share, h) + (mark.Under ? FamilyShape.Stroke : -FamilyShape.Stroke - line);
            Canvas.SetLeft(text, x);
            Canvas.SetTop(text, Math.Clamp(y, 0, Math.Max(0, h - line)));
            canvas.Children.Add(text);
        }
    }

    /// <summary>[S-12703] The wind's arrows: where it blows to, as many as stand a glyph and a gap apart, the first slot's first.</summary>
    private void Arrows(IReadOnlyList<HourSlot> slots, double w)
    {
        if (_model is not { Arrows: true })
        {
            return;
        }

        var cell = w / slots.Count;
        var every = Math.Max(1, (int)Math.Ceiling((FamilyShape.Icon + FamilySpace.S1) / cell));
        for (var i = 0; i < slots.Count; i += every)
        {
            if (slots[i].Direction is not { } from)
            {
                continue;
            }

            var arrow = Family.Glyph(Shell.Glyphs.MoveUp, FamilyShape.Icon, ColourRole.TextSecondary);
            arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            arrow.RenderTransform = new RotateTransform { Angle = (from + 180) % 360 };
            Canvas.SetLeft(arrow, HourGraphRules.X(i, slots.Count, w) - (FamilyShape.Icon / 2));
            _arrows.Children.Add(arrow);
        }
    }

    private static void Rule(Canvas canvas, double x, double h, ColourRole role)
    {
        var rule = new Rectangle { Width = FamilyShape.Hairline, Height = h, Fill = Family.Brush(role) };
        Canvas.SetLeft(rule, x - (FamilyShape.Hairline / 2));
        canvas.Children.Add(rule);
    }

    private static void Dot(Canvas canvas, Point at, ColourRole role)
    {
        var size = FamilyShape.Track * 2;
        var dot = new Ellipse { Width = size, Height = size, Fill = Family.Brush(role) };
        Canvas.SetLeft(dot, at.X - (size / 2));
        Canvas.SetTop(dot, at.Y - (size / 2));
        canvas.Children.Add(dot);
    }

    private static PathGeometry Figure(IReadOnlyList<Point> points, double? closedAt)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = closedAt is not null, IsFilled = closedAt is not null };
        var segment = new PolyLineSegment();
        foreach (var p in points.Skip(1))
        {
            segment.Points.Add(p);
        }

        if (closedAt is { } foot)
        {
            segment.Points.Add(new Point(points[^1].X, foot));
            segment.Points.Add(new Point(points[0].X, foot));
        }

        figure.Segments.Add(segment);
        return new PathGeometry { Figures = { figure } };
    }
}
