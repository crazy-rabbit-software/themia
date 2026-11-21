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
/// it, the bar 8 under that, the controls 8 under the last, all centred, the bar the block's width. Side by side: the
/// face on the left; 24 right of it a column of the caption, the bar 8 under it as wide as the column and the controls 8
/// under that, the face's ink and the column's centred on one band. [S-14281] Its parts always touch, where they stand
/// is <see cref="PomodoroFace.Stand"/> (WR-7): the block is its parts' size and its fit scales it, never a room spread
/// between them; it says where its ink stands (<see cref="IInked"/>).
/// </summary>
internal sealed partial class PomodoroBlock : Panel, IInked
{
    private readonly FrameworkElement _face;
    private readonly FrameworkElement _caption;
    private readonly FrameworkElement _bar;
    private readonly FrameworkElement _controls;
    private PomodoroArrangement _arrangement = PomodoroArrangement.Wide;
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

    /// <summary>
    /// [S-14200] The room in the face's box over its first ink and under its last: the time's as it is drawn (its held
    /// template's figures, measured), or none for a ring (its box is its ink).
    /// </summary>
    private LineInk FaceInk => _ringFace ? LineInk.None : Themia.App.Widgets.Ink.Of(_face).Ink;

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

    public bool IsRow => false;

    private double CaptionGap => _ringFace ? FamilySpace.S2 : 0;

    /// <summary>[S-14281] The parts as they stand in the block (<see cref="PomodoroFace.Stand"/>), from their measured sizes.</summary>
    private PomodoroStand Stand()
        => PomodoroFace.Stand(
            _arrangement,
            _face.DesiredSize.Height,
            FaceInk,
            _caption.DesiredSize.Height,
            Themia.App.Widgets.Ink.Of(_caption).Ink.Over,
            _bar.Visibility == Visibility.Visible ? _bar.DesiredSize.Height : 0,
            _controls.DesiredSize.Height,
            CaptionGap);

    protected override Size MeasureOverride(Size availableSize)
    {
        // Every part at its own size: the block's size is its parts', never the room's (ReadingFit scales it).
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(free);
        }

        var (face, caption, controls) = (_face.DesiredSize, _caption.DesiredSize, _controls.DesiredSize);
        var stand = Stand();
        if (_arrangement == PomodoroArrangement.Tall)
        {
            _ink = new LineInk(FaceInk.Over, 0);
            return new Size(Math.Max(face.Width, Math.Max(caption.Width, controls.Width)), stand.Height);
        }

        // Side by side the block's box starts at the caption's line, so its first ink is the caption's capitals (or the
        // face's, level with them).
        _ink = new LineInk(Themia.App.Widgets.Ink.Of(_caption).Ink.Over, 0);
        return new Size(face.Width + PomodoroFace.ColumnGap + Math.Max(caption.Width, controls.Width), stand.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (face, caption, controls) = (_face.DesiredSize, _caption.DesiredSize, _controls.DesiredSize);
        var w = finalSize.Width;
        var bar = _bar.DesiredSize.Height;
        var at = Stand();
        if (_arrangement == PomodoroArrangement.Tall)
        {
            _face.Arrange(new Rect((w - face.Width) / 2, at.Face, face.Width, face.Height));
            _caption.Arrange(new Rect(0, at.Caption, w, caption.Height));
            _bar.Arrange(new Rect(0, at.Bar, w, bar));
            _controls.Arrange(new Rect((w - controls.Width) / 2, at.Controls, controls.Width, controls.Height));
            return finalSize;
        }

        _face.Arrange(new Rect(0, at.Face, face.Width, face.Height));
        var left = face.Width + PomodoroFace.ColumnGap;
        var width = Math.Max(0, w - left);
        _caption.Arrange(new Rect(left, at.Caption, width, caption.Height));
        _bar.Arrange(new Rect(left, at.Bar, width, bar));
        _controls.Arrange(new Rect(left, at.Controls, controls.Width, controls.Height));
        return finalSize;
    }
}
