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
/// [S-8115] The cycle as one sectioned bar: the Chart's 4 track cut into a section per phase
/// (<see cref="PomodoroSections.Bar"/>), each round, its fill in its kind's role from its start. It asks for no length
/// of its own: it runs the length it is laid at and is as thick as its box's other side (the track at the face's scale),
/// so it never limits the face's scale ([S-12814]). [S-16604] Upright, its first section stands on its top and the
/// sections run down, each filling from its top.
/// </summary>
internal sealed partial class PomodoroBar : Panel
{
    private readonly List<(Border Track, Border Fill)> _parts = [];
    private PomodoroSection[] _sections = [];
    private bool _upright;

    public PomodoroBar()
    {
        IsHitTestVisible = false;
        // Laid at its drawn box by its block, across or upright: it stretches to it, whatever it asked.
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>[S-6330] The sections as they are now; the same again draws nothing.</summary>
    public void Show(IReadOnlyList<PomodoroSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.SequenceEqual(_sections))
        {
            return;
        }

        _sections = [.. sections];
        while (_parts.Count < _sections.Length)
        {
            var round = Family.Round(FamilyShape.Track, FamilyShape.Track);
            var part = (new Border { Background = Family.Brush(ColourRole.Track), CornerRadius = round }, new Border { CornerRadius = round });
            _parts.Add(part);
            Children.Add(part.Item1);
            Children.Add(part.Item2);
        }

        while (_parts.Count > _sections.Length)
        {
            var (track, fill) = _parts[^1];
            _parts.RemoveAt(_parts.Count - 1);
            Children.Remove(track);
            Children.Remove(fill);
        }

        for (var i = 0; i < _sections.Length; i++)
        {
            var brush = Family.Brush(_sections[i].Role);
            if (!ReferenceEquals(_parts[i].Fill.Background, brush))
            {
                _parts[i].Fill.Background = brush;
            }
        }

        InvalidateArrange();
    }

    /// <summary>[S-16604] Whether the bar stands upright, its sections from its top down.</summary>
    public bool Upright
    {
        get => _upright;
        set
        {
            if (value != _upright)
            {
                _upright = value;
                InvalidateArrange();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(0, FamilyShape.Track));
        }

        return new Size(0, FamilyShape.Track);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Its length along its axis, its thickness the box's other side.
        var (length, thick) = _upright ? (finalSize.Height, finalSize.Width) : (finalSize.Width, finalSize.Height);
        var rects = PomodoroSections.Bar(length, _sections);
        for (var i = 0; i < _parts.Count; i++)
        {
            var (track, fill) = _parts[i];
            if (i >= rects.Count || !(thick > 0))
            {
                track.Arrange(default);
                fill.Arrange(default);
                continue;
            }

            var r = rects[i];
            track.Arrange(Along(r.Left, r.Width, thick));
            track.CornerRadius = Family.Round(Math.Min(r.Width, thick), thick);
            fill.Arrange(Along(r.Left, r.Filled, thick));
            fill.CornerRadius = Family.Round(Math.Min(r.Filled, thick), thick);
        }

        return finalSize;
    }

    /// <summary>A stretch of the bar from <paramref name="start"/>, <paramref name="length"/> long, across its axis.</summary>
    private Rect Along(double start, double length, double thick)
        => _upright ? new Rect(0, start, thick, Math.Max(0, length)) : new Rect(start, 0, Math.Max(0, length), thick);
}

/// <summary>
/// [S-8115] The cycle as a sectioned ring round the time: the Chart's ring at its stroke cut into an arc per phase
/// (<see cref="PomodoroSections.Ring"/>), from twelve, clockwise, round caps; each arc's fill in its kind's role from
/// its start. Square, the size of its box's short side.
/// </summary>
internal sealed partial class PomodoroRing : Grid
{
    private readonly List<(Path Track, Path Fill)> _parts = [];
    private PomodoroSection[] _sections = [];

    public PomodoroRing()
    {
        IsHitTestVisible = false;
        SizeChanged += (_, _) => Draw();
    }

    /// <summary>[S-6330] The sections as they are now; the same again draws nothing.</summary>
    public void Show(IReadOnlyList<PomodoroSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.SequenceEqual(_sections))
        {
            return;
        }

        _sections = [.. sections];
        while (_parts.Count < _sections.Length)
        {
            var part = (Arc(ColourRole.Track), Arc(ColourRole.Accent));
            _parts.Add(part);
            Children.Add(part.Item1);
            Children.Add(part.Item2);
        }

        while (_parts.Count > _sections.Length)
        {
            var (track, fill) = _parts[^1];
            _parts.RemoveAt(_parts.Count - 1);
            Children.Remove(track);
            Children.Remove(fill);
        }

        for (var i = 0; i < _sections.Length; i++)
        {
            var brush = Family.Brush(_sections[i].Role);
            if (!ReferenceEquals(_parts[i].Fill.Stroke, brush))
            {
                _parts[i].Fill.Stroke = brush;
            }
        }

        Draw();
    }

    private static Path Arc(ColourRole role) => new()
    {
        Stroke = Family.Brush(role),
        StrokeThickness = FamilyShape.Stroke,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private void Draw()
    {
        var (w, h) = (ActualWidth, ActualHeight);
        var side = Math.Min(w, h);
        var arcs = PomodoroSections.Ring(side, _sections);
        var r = (side - FamilyShape.Stroke) / 2;
        for (var i = 0; i < _parts.Count; i++)
        {
            var (track, fill) = _parts[i];
            var arc = i < arcs.Count ? arcs[i] : default;
            track.Data = Geometry(w / 2, h / 2, r, arc.Start, arc.Sweep);
            fill.Data = Geometry(w / 2, h / 2, r, arc.Start, arc.Filled);
        }
    }

    /// <summary>An arc of radius <paramref name="r"/> from <paramref name="start"/> degrees past twelve, clockwise, <paramref name="sweep"/> long; a whole turn as two halves.</summary>
    private static PathGeometry? Geometry(double cx, double cy, double r, double start, double sweep)
    {
        if (!(r > 0) || !(sweep > 0))
        {
            return null;
        }

        static Point At(double cx, double cy, double r, double degrees)
        {
            var a = degrees * Math.PI / 180;
            return new Point(cx + (r * Math.Sin(a)), cy - (r * Math.Cos(a)));
        }

        var figure = new PathFigure { StartPoint = At(cx, cy, r, start), IsClosed = false, IsFilled = false };
        var size = new Size(r, r);
        if (sweep >= 359.99)
        {
            figure.Segments.Add(new ArcSegment { Point = At(cx, cy, r, start + 180), Size = size, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new ArcSegment { Point = At(cx, cy, r, start + 359.99), Size = size, SweepDirection = SweepDirection.Clockwise, IsLargeArc = false });
        }
        else
        {
            figure.Segments.Add(new ArcSegment { Point = At(cx, cy, r, start + sweep), Size = size, SweepDirection = SweepDirection.Clockwise, IsLargeArc = sweep > 180 });
        }

        return new PathGeometry { Figures = { figure } };
    }
}
