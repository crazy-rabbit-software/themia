using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-6261]..[S-6266] A meter style that draws its reading itself, in place of the reading over its
/// history: the network's two numbers, the battery's shape, a ring, the ping's signal bars. The
/// shared <see cref="MeterView"/> builds one when the look is chosen and drops it when another is
/// ([S-6005]); it hands it every reading and the room. The series and the hub are untouched.
/// </summary>
internal abstract partial class MeterStyleFace : Grid
{
    /// <summary>The air under the face, above the card's bottom edge (as the other looks' parts).</summary>
    protected const double Bottom = Fluent.Space3;

    protected static readonly Brush PrimaryBrush = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    protected static readonly Brush DisabledBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);
    protected static readonly Brush OutlineBrush = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);

    protected MeterStyleFace()
    {
        IsHitTestVisible = false;
        Margin = new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, Bottom);
    }

    /// <summary>The face a look draws, or null for a look that is not a face.</summary>
    public static MeterStyleFace? For(MeterLook look) => look switch
    {
        MeterLook.Numbers => new NumbersFace(),
        MeterLook.Battery => new BatteryFace(),
        MeterLook.Ring => new RingMeterFace(),
        MeterLook.Signal => new SignalFace(),
        _ => null,
    };

    /// <summary>Whether a reading has been drawn yet: a face built after the last sample shows that one at once.</summary>
    public bool HasReading { get; private set; }

    /// <summary>One reading reached the screen, with the pressure colour the history would have had.</summary>
    public void Present(MeterReading reading, Brush pressure)
    {
        HasReading = true;
        Draw(reading, pressure);
    }

    /// <summary>Draw one reading.</summary>
    protected abstract void Draw(MeterReading reading, Brush pressure);

    /// <summary>Lay the face out for the card's area.</summary>
    public abstract void Lay(WidgetArea area);

    /// <summary>The card's area inside the face's margin.</summary>
    protected static WidgetArea Box(WidgetArea area)
        => new(Math.Max(0, area.Width - (2 * HeroLayout.SideInset)), Math.Max(0, area.Height - HeroLayout.TopInset - Bottom));

    /// <summary>A figure on a readout: split as every meter's reading is ([S-3860]), dimmed when unavailable ([S-82]).</summary>
    protected static void Show(HeroReadout readout, string text, bool available)
    {
        var split = HeroLayout.Split(text);
        readout.SetValue(split.Value, split.Unit, split.Spaced);
        readout.ValueLine.Foreground = available ? PrimaryBrush : DisabledBrush;
    }

    protected static HeroReadout Readout()
    {
        var readout = new HeroReadout { VerticalAlignment = VerticalAlignment.Center };
        readout.SetValue("--", string.Empty, false);
        return readout;
    }
}

/// <summary>
/// [S-6264] [S-6270] The network's Numbers: download and upload, each behind its arrow, both at the
/// largest step the tile allows, stacked or side by side (<see cref="MeterNumbers"/>).
/// </summary>
internal sealed partial class NumbersFace : MeterStyleFace
{
    private static readonly HeroBound Bound = HeroBound.Of(Formatting.Rate(1023 * 1024));

    private readonly HeroReadout _down = Readout();
    private readonly HeroReadout _up = Readout();
    private TypeStep? _step;
    private bool? _side;

    public NumbersFace()
    {
        _down.Icon.Glyph = Glyphs.Download;
        _up.Icon.Glyph = Glyphs.Upload;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Children.Add(_down);
        Children.Add(_up);
    }

    protected override void Draw(MeterReading reading, Brush pressure)
    {
        Show(_down, reading.Primary, reading.Available);
        Show(_up, reading.Available && reading.Second is { } up ? Formatting.Rate(up) : "--", reading.Available);
        _down.Icon.Foreground = pressure;
        _up.Icon.Foreground = pressure;
    }

    public override void Lay(WidgetArea area)
    {
        var plan = MeterNumbers.Plan(Box(area), Bound, icon: true, Fluent.Space3);

        if (plan.Side != _side)
        {
            _side = plan.Side;
            RowDefinitions[1].Height = plan.Side ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            ColumnDefinitions[1].Width = plan.Side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            SetRow(_up, plan.Side ? 0 : 1);
            SetColumn(_up, plan.Side ? 1 : 0);
            SetColumnSpan(_down, plan.Side ? 1 : 2);
            SetColumnSpan(_up, plan.Side ? 1 : 2);

            // Stacked, the two lines meet in the middle; side by side, each is centred in its half.
            _down.VerticalAlignment = plan.Side ? VerticalAlignment.Center : VerticalAlignment.Bottom;
            _up.VerticalAlignment = plan.Side ? VerticalAlignment.Center : VerticalAlignment.Top;
            _down.Margin = plan.Side ? new Thickness(0) : new Thickness(0, 0, 0, Fluent.Space3 / 2);
            _up.Margin = plan.Side ? new Thickness(Fluent.Space3, 0, 0, 0) : new Thickness(0, Fluent.Space3 / 2, 0, 0);
        }

        if (plan.Step != _step)
        {
            _step = plan.Step;
            _down.Lay(plan.Step, icon: true, trailingBeside: true, linesBeside: false);
            _up.Lay(plan.Step, icon: true, trailingBeside: true, linesBeside: false);
        }
    }
}

/// <summary>
/// [S-6261] A face that is a picture and the figure (<see cref="PictureFace"/>): the picture as large
/// as the room the figure leaves, the figure under it, or beside it on a wide strip.
/// </summary>
internal abstract partial class PictureFaceBase : MeterStyleFace
{
    private readonly HeroReadout _figure = Readout();
    private readonly Canvas _picture = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _stack = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private TypeStep? _step;

    protected PictureFaceBase()
    {
        _stack.Children.Add(_picture);
        _stack.Children.Add(_figure);
        Children.Add(_stack);
    }

    /// <summary>The picture's width per height.</summary>
    protected abstract double Aspect { get; }

    /// <summary>The widest figure ("100%", "999 ms").</summary>
    protected abstract HeroBound Figure { get; }

    protected Canvas Picture => _picture;

    /// <summary>Draw the picture into <see cref="Picture"/> at its size.</summary>
    protected abstract void DrawPicture(double width, double height);

    protected void ShowFigure(string text, bool available) => Show(_figure, text, available);

    public override void Lay(WidgetArea area)
    {
        var box = Box(area);
        var plan = PictureFace.Plan(box, Aspect, Figure, area.Shape == AreaShape.Wide, HeroLayout.TierStep(area));
        var beside = plan.Arrangement == PictureArrangement.Beside;

        _stack.Orientation = beside ? Orientation.Horizontal : Orientation.Vertical;
        _stack.Spacing = PictureFace.Gap;
        _picture.Visibility = plan.Arrangement == PictureArrangement.FigureOnly ? Visibility.Collapsed : Visibility.Visible;
        _picture.Width = plan.PictureWidth;
        _picture.Height = plan.PictureHeight;
        _figure.HorizontalAlignment = beside ? HorizontalAlignment.Left : HorizontalAlignment.Center;

        if (plan.Figure != _step)
        {
            _step = plan.Figure;
            _figure.Lay(plan.Figure, icon: false, trailingBeside: true, linesBeside: false);
        }

        if (plan.Arrangement != PictureArrangement.FigureOnly)
        {
            DrawPicture(plan.PictureWidth, plan.PictureHeight);
        }
    }
}

/// <summary>
/// [S-6262] [S-6272] The battery style: a battery drawn as Windows draws its glyph, filled from the
/// left to the charge in the charge's colour, the bolt over it while charging, the figure with it.
/// </summary>
internal sealed partial class BatteryFace : PictureFaceBase
{
    private readonly Rectangle _body = new() { Stroke = OutlineBrush };
    private readonly Rectangle _cap = new() { Fill = OutlineBrush };
    private readonly Rectangle _fill = new();
    private readonly FontIcon _bolt = new() { Glyph = Glyphs.ChargingBolt, Foreground = PrimaryBrush, Visibility = Visibility.Collapsed };
    private BatteryShapePlan _plan;
    private double _fraction;

    public BatteryFace()
    {
        Picture.Children.Add(_body);
        Picture.Children.Add(_cap);
        Picture.Children.Add(_fill);
        Picture.Children.Add(_bolt);
    }

    protected override double Aspect => BatteryShape.Aspect;

    protected override HeroBound Figure => HeroBound.Of(Formatting.Percent(1));

    protected override void Draw(MeterReading reading, Brush pressure)
    {
        ShowFigure(reading.Primary, reading.Available);
        _fill.Fill = pressure;
        _fraction = reading.Available ? reading.Fraction ?? 0 : 0;
        _bolt.Visibility = reading.Available && reading.Charging ? Visibility.Visible : Visibility.Collapsed;
        Fill();
    }

    protected override void DrawPicture(double width, double height)
    {
        _plan = BatteryShape.Fit(width, height);
        Place(_body, _plan.Body);
        _body.StrokeThickness = _plan.Stroke;
        _body.RadiusX = _plan.Corner;
        _body.RadiusY = _plan.Corner;
        Place(_cap, _plan.Cap);
        _cap.RadiusX = Math.Min(_plan.Cap.Width, _plan.Corner) / 2;
        _cap.RadiusY = _cap.RadiusX;

        _bolt.FontSize = Math.Max(8, _plan.Bolt);
        _bolt.Width = _plan.Inner.Width;
        _bolt.Height = _plan.Inner.Height;
        Canvas.SetLeft(_bolt, _plan.Inner.X);
        Canvas.SetTop(_bolt, _plan.Inner.Y);
        Fill();
    }

    private void Fill()
    {
        var width = BatteryShape.FillWidth(_plan, _fraction);
        var corner = Math.Max(0, _plan.Corner - _plan.Stroke - 1);
        _fill.RadiusX = corner;
        _fill.RadiusY = corner;
        Place(_fill, _plan.Inner with { Width = width });
    }

    private static void Place(FrameworkElement shape, Core.Geometry.PixelRect rect)
    {
        shape.Width = Math.Max(0, rect.Width);
        shape.Height = Math.Max(0, rect.Height);
        Canvas.SetLeft(shape, rect.X);
        Canvas.SetTop(shape, rect.Y);
    }
}

/// <summary>
/// [S-6263] [S-6273] The ping's signal bars: four bars rising left to right, as many lit as the
/// latency earns (<see cref="PingSignal.Level"/>) in the status colour, the rest the track; the
/// latency with them.
/// </summary>
internal sealed partial class SignalFace : PictureFaceBase
{
    private readonly Rectangle[] _bars = new Rectangle[PingSignal.BarCount];
    private int _level;
    private Brush? _lit;

    public SignalFace()
    {
        // [S-1113] Signal strength rises left to right in every language, as Windows draws it.
        Picture.FlowDirection = FlowDirection.LeftToRight;

        for (var i = 0; i < _bars.Length; i++)
        {
            _bars[i] = new Rectangle();
            ItemFill.Paint(_bars[i], ItemRole.Track);
            Picture.Children.Add(_bars[i]);
        }
    }

    protected override double Aspect => PingSignal.Aspect;

    protected override HeroBound Figure => HeroBound.Of(Formatting.Latency(999));

    protected override void Draw(MeterReading reading, Brush pressure)
    {
        ShowFigure(reading.Primary, reading.Available);
        _level = PingSignal.Level(reading.Available ? reading.Value : null);
        _lit = pressure;
        Paint();
    }

    protected override void DrawPicture(double width, double height)
    {
        var bars = PingSignal.Bars(width, height);

        for (var i = 0; i < _bars.Length && i < bars.Count; i++)
        {
            var bar = _bars[i];
            bar.Width = bars[i].Width;
            bar.Height = bars[i].Height;
            bar.RadiusX = Math.Min(Fluent.ControlRadius, bars[i].Width / 4);
            bar.RadiusY = bar.RadiusX;
            Canvas.SetLeft(bar, bars[i].X);
            Canvas.SetTop(bar, bars[i].Y);
        }

        Paint();
    }

    private void Paint()
    {
        for (var i = 0; i < _bars.Length; i++)
        {
            if (i >= _level || _lit is null)
            {
                ItemFill.Paint(_bars[i], ItemRole.Track);
            }
            else if (!ReferenceEquals(_bars[i].Fill, _lit))
            {
                _bars[i].Fill = _lit;
            }
        }
    }
}

/// <summary>
/// [S-6266] [S-6272] A ring style: a ring of the reading's share as large as the tile allows, the
/// figure in its hole at the largest step the hole takes (<see cref="Themia.Core.Widgets.RingFace"/>).
/// </summary>
internal sealed partial class RingMeterFace : MeterStyleFace
{
    private readonly RingPart _ring = new();
    private readonly HeroReadout _figure = Readout();
    private TypeStep? _step;

    public RingMeterFace()
    {
        _ring.Root.HorizontalAlignment = HorizontalAlignment.Center;
        _ring.Root.VerticalAlignment = VerticalAlignment.Center;
        _ring.Add(_figure);
        Children.Add(_ring.Root);
    }

    protected override void Draw(MeterReading reading, Brush pressure)
    {
        Show(_figure, reading.Primary, reading.Available);
        _ring.SetFraction(reading.Available ? reading.Fraction : null);
        _ring.SetBrush(pressure);
    }

    public override void Lay(WidgetArea area)
    {
        var plan = Themia.Core.Widgets.RingFace.Plan(Box(area), wide: false, [], 0);
        var bound = HeroBound.Of(Formatting.Percent(1));
        _ring.Resize(plan.Diameter);

        var step = Themia.Core.Widgets.RingFace.Figure(plan.Diameter, bound);
        if (step != _step)
        {
            _step = step;
            _figure.Lay(step, icon: false, trailingBeside: true, linesBeside: false);
        }
    }
}
