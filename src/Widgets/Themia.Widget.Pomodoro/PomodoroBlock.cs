using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-8117] The pomodoro's one block (the part its <see cref="ReadingFit"/> scales as one, [S-12808]): the face (the
/// time, or the ring with the time in it), the caption, the sectioned bar and the Controls row, stacked or side by side
/// (<see cref="PomodoroArrangement"/>, chosen by its view from the card's aspect). Stacked: the face, the caption under
/// it, the bar 8 under that, the controls on the foot, all centred, the bar the block's width. Side by side: the face on
/// the left, centred on the height; 24 right of it a column as tall as the block, the caption on its top, the bar 8
/// under it as wide as the column, the controls on its foot. What the block is given past its parts goes between them,
/// never over the first ink or under the last (WR-1, WR-4): it says where its ink stands (<see cref="IInked"/>).
/// </summary>
internal sealed partial class PomodoroBlock : Panel, IInked
{
    private readonly FrameworkElement _face;
    private readonly FrameworkElement _caption;
    private readonly FrameworkElement _bar;
    private readonly FrameworkElement _controls;
    private PomodoroArrangement _arrangement = PomodoroArrangement.Wide;
    private LineInk _faceInk;
    private bool _ringFace;
    private LineInk _ink;

    public PomodoroBlock(FrameworkElement face, FrameworkElement caption, FrameworkElement bar, FrameworkElement controls)
    {
        (_face, _caption, _bar, _controls) = (face, caption, bar, controls);
        Children.Add(face);
        Children.Add(caption);
        Children.Add(bar);
        Children.Add(controls);
    }

    /// <summary>[S-8117] Stacked or side by side.</summary>
    public PomodoroArrangement Arrangement
    {
        get => _arrangement;
        set
        {
            if (value != _arrangement)
            {
                _arrangement = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The room in the face's box over its first ink and under its last: the time's line's, or none for a ring.</summary>
    public LineInk FaceInk
    {
        get => _faceInk;
        set
        {
            if (value != _faceInk)
            {
                _faceInk = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>Whether the face is a ring: its caption stands 8 under it (a time's stands right under, as a readout's line under).</summary>
    public bool RingFace
    {
        get => _ringFace;
        set
        {
            if (value != _ringFace)
            {
                _ringFace = value;
                InvalidateMeasure();
            }
        }
    }

    public LineInk Ink => _ink;

    public bool Grows => false;

    private double BarPart => _bar.Visibility == Visibility.Visible ? FamilySpace.S2 + _bar.DesiredSize.Height : 0;

    private double CaptionGap => _ringFace ? FamilySpace.S2 : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        // Every part at its own size: the block's size is its parts', never the room's (ReadingFit scales it).
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(free);
        }

        var (face, caption, controls) = (_face.DesiredSize, _caption.DesiredSize, _controls.DesiredSize);
        var foot = FamilySpace.S2 + controls.Height;
        if (_arrangement == PomodoroArrangement.Tall)
        {
            _ink = new LineInk(_faceInk.Over, 0);
            return new Size(
                Math.Max(face.Width, Math.Max(caption.Width, controls.Width)),
                face.Height + CaptionGap + caption.Height + BarPart + foot);
        }

        // Side by side the face's ink and the column's (the caption's capitals to the controls' foot) are one band, as
        // tall as the taller; the block's box starts at the caption's line, so its first ink is the caption's capitals.
        var over = PartInk.Of(_caption).Over;
        var band = Math.Max(FaceSpan(face.Height), caption.Height + BarPart + foot - over);
        _ink = new LineInk(over, 0);
        return new Size(face.Width + PomodoroFace.ColumnGap + Math.Max(caption.Width, controls.Width), over + band);
    }

    /// <summary>The face's ink from its first to its last: a ring's whole box, a time's capitals to its baseline.</summary>
    private double FaceSpan(double height) => Math.Max(0, height - _faceInk.Over - _faceInk.Under);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (face, caption, controls) = (_face.DesiredSize, _caption.DesiredSize, _controls.DesiredSize);
        var (w, h) = (finalSize.Width, finalSize.Height);
        var bar = _bar.DesiredSize.Height;
        if (_arrangement == PomodoroArrangement.Tall)
        {
            _face.Arrange(new Rect((w - face.Width) / 2, 0, face.Width, face.Height));
            var y = face.Height + CaptionGap;
            _caption.Arrange(new Rect(0, y, w, caption.Height));
            _bar.Arrange(new Rect(0, y + caption.Height + FamilySpace.S2, w, bar));
            _controls.Arrange(new Rect((w - controls.Width) / 2, h - controls.Height, controls.Width, controls.Height));
            return finalSize;
        }

        // The face's ink centred on the band under the caption's line (a time's leading reaches past the box, empty).
        var top = PartInk.Of(_caption).Over;
        _face.Arrange(new Rect(0, top + ((h - top - FaceSpan(face.Height)) / 2) - _faceInk.Over, face.Width, face.Height));
        var left = face.Width + PomodoroFace.ColumnGap;
        var width = Math.Max(0, w - left);
        _caption.Arrange(new Rect(left, 0, width, caption.Height));
        _bar.Arrange(new Rect(left, caption.Height + FamilySpace.S2, width, bar));
        _controls.Arrange(new Rect(left, h - controls.Height, controls.Width, controls.Height));
        return finalSize;
    }
}
