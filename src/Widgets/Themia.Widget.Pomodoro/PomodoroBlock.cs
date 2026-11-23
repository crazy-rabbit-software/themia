using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-8117] [S-16603] The pomodoro's face in its room: the ring, the time, the caption, the sectioned bar and the Controls
/// row, each measured at its own size and laid where <see cref="PomodoroFace.Lay"/> puts it for the room (the arrangement the
/// card's shape picks, the parts scaled as one, the ring as large as the room's smaller side with everything inside it, the
/// bar the room's whole length). A part is drawn at its natural size under one scale transform; the bar is laid at its drawn
/// size. The scale follows the room and the parts' held sizes alone (the time held to its widest reading, the caption to
/// its longest, Start as wide as Pause), so neither a tick, a flip nor a press moves it (WR-12, WR-15).
/// </summary>
internal sealed partial class PomodoroBlock : Panel
{
    private readonly PomodoroRing _ring;
    private readonly ReadoutBlock _time;
    private readonly TextBlock _caption;
    private readonly PomodoroBar _bar;
    private readonly FrameworkElement _main;
    private readonly FrameworkElement _tools;
    private PomodoroLook _look = PomodoroLook.Ring;
    private PomodoroLayout _layout;

    /// <summary>
    /// The face's parts: the Controls as two blocks, the main action (<paramref name="main"/>) and the icon buttons
    /// (<paramref name="tools"/>), 8 apart in a row, or the icons under the main action upright beside the bar ([S-16608]).
    /// </summary>
    public PomodoroBlock(PomodoroRing ring, ReadoutBlock time, TextBlock caption, PomodoroBar bar, FrameworkElement main, FrameworkElement tools)
    {
        (_ring, _time, _caption, _bar, _main, _tools) = (ring, time, caption, bar, main, tools);
        foreach (var part in new FrameworkElement[] { ring, time, caption, bar, main, tools })
        {
            // Drawn scaled: laid out unrounded, as a ReadingFit's parts are ([S-14301]).
            part.UseLayoutRounding = false;
            part.RenderTransformOrigin = default;
            Children.Add(part);
        }
    }

    /// <summary>[S-6035] The look the parts are laid for.</summary>
    public PomodoroLook Look
    {
        get => _look;
        set
        {
            if (value != _look)
            {
                _look = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>[S-16603] The arrangement the last room took.</summary>
    public PomodoroArrangement Arrangement => _layout.Arrangement;

    /// <summary>Raised in the measure when the room picks another arrangement: its view aligns the caption for it.</summary>
    public event Action<PomodoroArrangement>? Arranged;

    /// <summary>[S-16600] The parts as measured, at their own size: the time and the caption with the room over and under their ink.</summary>
    private PomodoroParts Parts()
    {
        var (time, caption, main, tools) = (_time.DesiredSize, _caption.DesiredSize, _main.DesiredSize, _tools.DesiredSize);
        return new PomodoroParts(
            new SizeDip(time.Width, time.Height),
            _time.Ink,
            new SizeDip(caption.Width, caption.Height),
            Ink.Of(_caption).Ink,
            new SizeDip(main.Width, main.Height),
            new SizeDip(tools.Width, tools.Height));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Every part at its own size: the room decides the scale, never the parts' room.
        var free = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(free);
        }

        var parts = Parts();
        var before = _layout.Arrangement;
        _layout = PomodoroFace.Lay(_look, parts, availableSize.Width, availableSize.Height);
        if (_layout.Arrangement != before)
        {
            Arranged?.Invoke(_layout.Arrangement);
        }

        if (double.IsFinite(availableSize.Width) && double.IsFinite(availableSize.Height))
        {
            return availableSize;
        }

        var block = PomodoroFace.BlockOf(_layout.Arrangement, _look, parts);
        return new Size(block.Width, block.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _layout = PomodoroFace.Lay(_look, Parts(), finalSize.Width, finalSize.Height);
        var scale = _layout.Scale;
        // The ring's natural diameter is the one its box was planned on: the box at the scale.
        Place(_ring, _layout.Ring, scale, new Size(_layout.Ring.Width / scale, _layout.Ring.Height / scale));
        Place(_time, _layout.Time, scale);
        Place(_caption, _layout.Caption, scale);
        Place(_main, _layout.Main, scale);
        Place(_tools, _layout.Tools, scale);

        // The bar at its drawn size: its length the room's, its thickness the track's at the scale.
        _bar.Upright = _layout.BarUpright;
        _bar.Arrange(_layout.Bar.IsEmpty ? default : new Rect(_layout.Bar.X, _layout.Bar.Y, _layout.Bar.Width, _layout.Bar.Height));
        return finalSize;
    }

    /// <summary>A part at its natural size where its box stands, drawn at <paramref name="scale"/>; none: laid empty.</summary>
    private static void Place(FrameworkElement part, PomodoroRect box, double scale, Size? natural = null)
    {
        if (box.IsEmpty || part.Visibility != Visibility.Visible)
        {
            part.Arrange(default);
            return;
        }

        if (part.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform();
            part.RenderTransform = transform;
        }

        transform.ScaleX = scale;
        transform.ScaleY = scale;
        var size = natural ?? part.DesiredSize;
        part.Arrange(new Rect(box.X, box.Y, size.Width, size.Height));
    }
}
