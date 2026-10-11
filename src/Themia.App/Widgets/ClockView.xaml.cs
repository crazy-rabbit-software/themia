using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// The clock. Subscribes to the shared <see cref="Ticker"/> at the cadence its config actually
/// needs, and unsubscribes when it leaves the tree — a widget that is not on screen must not keep
/// the process waking up.
/// <para>
/// [S-665]/[S-1487] Its disclosure ladder, smallest tile first: the time (or the dial); the label of
/// a world clock; the date (short when narrow, with the offset appended on a wide strip); the
/// offset line. No week, day of the year or time zone line (concise widgets). A tall column stacks the hours over the minutes, which is the only
/// way its width lets the digits grow. Whatever the rungs leave, the time (or the dial) fills. An
/// analog dial on a wide strip puts its text beside the dial instead of under it.
/// </para>
/// </summary>
public sealed partial class ClockView : UserControl, IWidgetView, IWidgetSizeAware
{
    /// <summary>The longest long date: "Wednesday, 30 September".</summary>
    private const int LongDateCharacters = 23;

    /// <summary>The short date: "Wed 30 Sep".</summary>
    private const int ShortDateCharacters = 10;

    /// <summary>The detail line, the longest offset: "+8 h zur Ortszeit".</summary>
    private const int DetailCharacters = 18;

    /// <summary>The longest offset line, "−12:45h local", joined to the date with " · ".</summary>
    private const int OffsetCharacters = 16;

    /// <summary>Line height of stacked digits, as a multiple of their size.</summary>
    private const double StackedLeading = 1.05;

    /// <summary>[S-1488] Under this an analog dial is a smear of hairlines; it never draws smaller.</summary>
    private const double DialFloor = 40;

    /// <summary>
    /// One device-independent pixel. The rim and the ticks are drawn at this thickness at the
    /// tile's real scale, which is the whole reason the analog face is no longer inside a Viewbox:
    /// a hairline that has been scaled up is not a hairline.
    /// </summary>
    private const double Hairline = 1;

    private Ticker? _ticker;
    private ClockConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;

    // The analog face is rebuilt when its diameter changes, so the hands' transforms and the second
    // hand are held here rather than in the XAML.
    private RotateTransform _hourRotation = new();
    private RotateTransform _minuteRotation = new();
    private RotateTransform _secondRotation = new();
    private Rectangle? _secondHand;
    private double _analogDiameter;

    // The last area the card gave the view, and the rungs it settled on there. Kept so a config
    // change or a tick can lay out and render against them without waiting for a resize.
    private WidgetArea _area;
    private bool _stacked;
    private bool _shortDate;
    private bool _wideDate;
    private bool _showOffset;
    private bool _offsetOnDate;
    private bool _side;
    private string? _label;

    public ClockView()
    {
        InitializeComponent();

        // The card no longer pads its content; the clock carries its own inset. [S-645] The full
        // inset either side, but only half of it above and below: the readout is centred and sized
        // to its line height, which already carries its own leading, and the card's header leaves
        // a gap under itself. A full 16px top and bottom cost a wide strip a third of the height
        // the digits could have had.
        Root.Padding = new Thickness(WidgetLayout.ContentInset, Fluent.Space2, WidgetLayout.ContentInset, Fluent.Space2);
        TimeRow.Spacing = Fluent.Space1;
        LabelText.Margin = new Thickness(0, 0, 0, Fluent.Space1);
        DateText.Margin = new Thickness(0, Fluent.Space1, 0, 0);
        OffsetText.Margin = new Thickness(0, Fluent.Space1, 0, 0);

        // Block line height, so a stacked readout's lines sit exactly one ramp line apart rather
        // than at the font's own (looser) spacing.
        TimeText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

        Unloaded += (_, _) => Detach();
    }

    /// <summary>Wire the widget to its data. Called once when the card is built.</summary>
    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1109] Names and the meridiem follow the language at once, not at the next tick.</summary>
    private void OnLanguage(Locale locale) => ApplyConfig(_config);

    /// <summary>Re-read the config after the user changed it, resubscribing if the cadence changed.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ClockConfig ?? new ClockConfig();
        _label = ClockFormatter.Label(_config);
        LabelText.Text = _label ?? string.Empty;

        var analog = _config.Face == ClockFace.Analog;
        TimeRow.Visibility = analog ? Visibility.Collapsed : Visibility.Visible;
        AnalogFace.Visibility = analog ? Visibility.Visible : Visibility.Collapsed;
        MeridiemText.Visibility = _config.Use24Hour ? Visibility.Collapsed : Visibility.Visible;

        if (_secondHand is not null)
        {
            _secondHand.Visibility = _config.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
        }

        Lay();
        Resubscribe();
    }

    /// <summary>[S-663] The card's word on how much room the clock has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// Climb the ladder for the current area, then give the face everything the rungs left.
    /// <para>
    /// The size classes decide which rungs are considered — a date on a 70-pixel dial would be
    /// taking room from the one thing it is for — and <see cref="Disclosure"/> decides which of those
    /// actually fit, so a rung is never drawn clipped. Every size is a step of the Windows 11 ramp, or
    /// for a tile larger than the ramp, a multiple of it that keeps Fluent's line-height ratio.
    /// </para>
    /// </summary>
    private void Lay()
    {
        var box = _area.Deflate(Root.Padding.Left, Root.Padding.Top);
        var analog = _config.Face == ClockFace.Analog;

        // [S-1488] A dial on a strip: text beside it, not under it, or the dial is a postage stamp
        // in the middle of a band of nothing.
        _side = analog && _area.Shape == AreaShape.Wide && _area.WidthClass >= SizeClass.Regular;
        Arrange(_side);
        ApplyAlignment();

        if (_side)
        {
            LaySide(box);
        }
        else
        {
            LayColumn(box, analog);
        }

        Render();
    }

    /// <summary>The ordinary column: label, face, then the rungs under it.</summary>
    private void LayColumn(WidgetArea box, bool analog)
    {
        var width = box.Width;
        var height = box.Height;
        var room = new Disclosure(box, Fluent.Space1);

        // The face at its smallest is always there; the rungs share what it leaves.
        var floor = analog ? DialFloor
            : _config.Face == ClockFace.Stacked ? 2 * FluentType.Subtitle.LineHeight
            : FluentType.Subtitle.LineHeight;
        room.Reserve(floor);

        // [S-1488] A dial on the smallest tile is the whole tile: no words under a 70px clock face.
        var words = !analog || _area.WidthClass >= SizeClass.Regular || _area.HeightClass >= SizeClass.Regular;

        // [S-1482] The label first: on a world clock "Tokyo" says more than the date does.
        var labelStep = FluentType.Fit(width, height * 0.16, Math.Max(4, _label?.Length ?? 0), FluentType.Caption, FluentType.BodyLarge);
        var showLabel = words && _label is not null && room.Take(labelStep.LineHeight);

        // The date: the long one when the line can hold it, the short one when it cannot, and on a
        // wide strip — which has width it cannot otherwise use — the offset after it.
        _wideDate = !analog && _area.Shape == AreaShape.Wide && _area.WidthClass >= SizeClass.Expanded;
        _shortDate = !_wideDate && width < FluentType.EstimateWidth(FluentType.Caption, LongDateCharacters);
        _offsetOnDate = _wideDate && _config.ShowDate && _config.OffsetMode != ClockOffsetMode.None;
        var dateCharacters = (_shortDate ? ShortDateCharacters : LongDateCharacters)
            + (_offsetOnDate ? OffsetCharacters : 0);

        // Support, never a second headline: capped at BodyLarge however large the tile grows (the
        // steps above it are semibold and would read as a second title).
        var dateStep = FluentType.Fit(width, height * 0.22, dateCharacters, FluentType.Caption, FluentType.BodyLarge);
        var showDate = words && _config.ShowDate && room.Take(dateStep.LineHeight);
        _offsetOnDate &= showDate;

        // The details are a step under the date, so the eye reads time, date, then the rest.
        var detailStep = FluentType.Fit(width, height * 0.12, DetailCharacters, FluentType.Caption, FluentType.Body);
        _showOffset = words && !_offsetOnDate && _config.OffsetMode != ClockOffsetMode.None && room.Take(detailStep.LineHeight);

        Show(showLabel, showDate);
        LabelText.Apply(FluentType.Strong(labelStep));
        DateText.Apply(dateStep);
        OffsetText.Apply(detailStep);

        // The face has what the rungs left, plus the minimum it reserved for itself.
        var faceHeight = room.Remaining + floor;

        if (analog)
        {
            SizeDial(Math.Min(width, faceHeight));
            return;
        }

        SizeDigits(width, faceHeight);
    }

    /// <summary>[S-1488] A dial on a strip: the dial takes the height, the text column the rest.</summary>
    private void LaySide(WidgetArea box)
    {
        var diameter = Math.Floor(Math.Max(DialFloor, Math.Min(box.Height, box.Width * 0.45)));
        var textWidth = Math.Max(0, box.Width - diameter - Fluent.Space4);
        var room = new Disclosure(textWidth, box.Height, Fluent.Space1);

        var labelStep = FluentType.Fit(textWidth, box.Height * 0.3, Math.Max(4, _label?.Length ?? 0), FluentType.Caption, FluentType.Subtitle);
        var showLabel = _label is not null && room.Take(labelStep.LineHeight);

        _wideDate = false;
        _offsetOnDate = false;
        _shortDate = textWidth < FluentType.EstimateWidth(FluentType.Caption, LongDateCharacters);
        var dateStep = FluentType.Fit(textWidth, box.Height * 0.25, _shortDate ? ShortDateCharacters : LongDateCharacters, FluentType.Caption, FluentType.BodyLarge);
        var showDate = _config.ShowDate && room.Take(dateStep.LineHeight);

        var detailStep = FluentType.Fit(textWidth, box.Height * 0.2, DetailCharacters, FluentType.Caption, FluentType.Body);
        _showOffset = _config.OffsetMode != ClockOffsetMode.None && room.Take(detailStep.LineHeight);

        Show(showLabel, showDate);
        LabelText.Apply(FluentType.Strong(labelStep));
        DateText.Apply(dateStep);
        OffsetText.Apply(detailStep);
        Info.Width = textWidth;

        SizeDial(diameter);
    }

    private void Show(bool label, bool date)
    {
        LabelText.Visibility = Visible(label);
        DateText.Visibility = Visible(date);
        OffsetText.Visibility = Visible(_showOffset);
    }

    private static Visibility Visible(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The digital and stacked readouts: the largest ramp step that fits what the rungs left.</summary>
    private void SizeDigits(double width, double timeHeight)
    {
        // "12:34:56" plus " PM" — the meridiem sits on the same line, so it counts towards the width.
        var meridiem = _config.Use24Hour ? 0 : 3;
        var characters = (_config.ShowSeconds ? 8 : 5) + meridiem;
        var timeStep = FluentType.Hero(width, timeHeight, characters);

        // A tall column is too narrow for "15:40" to be large on one line; the hours over the minutes
        // use its height instead — only when that actually draws them larger. [S-1483] The Stacked
        // style always stacks, with the seconds and the meridiem a step down beside the minutes, as
        // the original set them. Stacked digits are set solid (StackedLeading) rather than at the
        // ramp's 1.3 leading: two lines of figures with a ramp line's air between them read as two
        // separate numbers.
        var tail = _config.ShowSeconds || !_config.Use24Hour ? 1.2 : 0;
        var perLine = timeHeight / 2;
        var byWidth = FluentType.Hero(width, perLine * 2, 2 + tail);
        var stackedSize = Math.Min(byWidth.FontSize, perLine / StackedLeading);
        var stackedStep = new TypeStep(stackedSize, stackedSize * StackedLeading, true);
        _stacked = _config.Face == ClockFace.Stacked
            || (_area.Shape == AreaShape.Tall && stackedStep.FontSize > timeStep.FontSize * 1.2);

        if (_stacked)
        {
            timeStep = stackedStep;
        }

        TimeText.Apply(timeStep);

        // The meridiem is the largest ramp step that stays well under the readout: a clock whose
        // "PM" is as tall as its digits reads as two numbers. Stacked, the seconds share its step.
        var tailStep = _stacked
            ? new TypeStep(Math.Max(FluentType.Caption.FontSize, stackedSize * 0.4), Math.Max(FluentType.Caption.FontSize, stackedSize * 0.4) * 1.15, false)
            : FluentType.Fit(width, timeStep.FontSize * 0.45, 2, FluentType.Caption, FluentType.Title);
        MeridiemText.Apply(tailStep);
        SecondsText.Apply(tailStep);
        SecondsText.Visibility = _stacked && _config.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;

        // Both blocks are bottom-aligned, so lifting the tail by the difference in half-leading
        // sits its glyphs on the readout's baseline instead of under it.
        var lift = HalfLeading(timeStep) - HalfLeading(tailStep);
        Tail.Margin = new Thickness(0, 0, 0, Math.Max(0, lift));
    }

    private static double HalfLeading(TypeStep step) => (step.LineHeight - step.FontSize) / 2;

    /// <summary>
    /// [S-1484] Left, centre or right, for the whole column and every line in it. Beside a dial the
    /// text reads from the dial outwards, so it stays left-aligned to it; the alignment places the pair.
    /// </summary>
    private void ApplyAlignment()
    {
        var (horizontal, text) = _config.Align switch
        {
            ClockAlign.Left => (HorizontalAlignment.Left, TextAlignment.Left),
            ClockAlign.Right => (HorizontalAlignment.Right, TextAlignment.Right),
            _ => (HorizontalAlignment.Center, TextAlignment.Center),
        };

        Stack.HorizontalAlignment = horizontal;
        TimeRow.HorizontalAlignment = horizontal;
        AnalogFace.HorizontalAlignment = horizontal;
        TimeText.TextAlignment = text;

        var lines = _side ? HorizontalAlignment.Left : horizontal;
        var lineText = _side ? TextAlignment.Left : text;

        foreach (var line in new[] { LabelText, DateText, OffsetText })
        {
            line.HorizontalAlignment = lines;
            line.TextAlignment = lineText;
        }
    }

    /// <summary>
    /// Column or side by side. The label is the one part that changes parent: over the face in a
    /// column, at the top of the text beside a dial. Only on a change, never per pass.
    /// </summary>
    private void Arrange(bool side)
    {
        Stack.Orientation = side ? Orientation.Horizontal : Orientation.Vertical;
        Stack.Spacing = side ? Fluent.Space4 : 0;

        var inInfo = Info.Children.Contains(LabelText);

        if (side && !inInfo)
        {
            Stack.Children.Remove(LabelText);
            Info.Children.Insert(0, LabelText);
        }
        else if (!side && inInfo)
        {
            Info.Children.Remove(LabelText);
            Stack.Children.Insert(0, LabelText);
        }

        if (!side)
        {
            Info.Width = double.NaN;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Analog face
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1488] Size the dial to what the ladder left it. Rebuilt only when the diameter actually
    /// changed — the once-a-second path only moves three transforms.
    /// </summary>
    private void SizeDial(double available)
    {
        var diameter = Math.Floor(available);

        // An unmeasured area gets no face rather than a dial drawn at a guess.
        if (!_area.IsMeasured || !double.IsFinite(diameter) || diameter < 1)
        {
            AnalogFace.Children.Clear();
            _secondHand = null;
            _analogDiameter = 0;
            return;
        }

        diameter = Math.Max(DialFloor, diameter);

        if (Math.Abs(diameter - _analogDiameter) < 1)
        {
            return;
        }

        _analogDiameter = diameter;
        BuildAnalogFace(diameter);
    }

    private void BuildAnalogFace(double diameter)
    {
        AnalogFace.Children.Clear();
        AnalogFace.Width = diameter;
        AnalogFace.Height = diameter;

        var radius = diameter / 2;

        var rim = FluentLayout.Themed("ControlStrokeColorDefaultBrush", Colors.Gray);
        var hourTick = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
        var minuteTick = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);
        var hand = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
        var accent = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);

        var dial = new Ellipse
        {
            Width = diameter - Hairline,
            Height = diameter - Hairline,
            Stroke = rim,
            StrokeThickness = Hairline,
        };

        Canvas.SetLeft(dial, Hairline / 2);
        Canvas.SetTop(dial, Hairline / 2);
        AnalogFace.Children.Add(dial);

        // Sixty marks is the real Windows dial, but under about 160 DIPs they are closer together
        // than they are long, so a small face keeps the twelve that actually tell the time.
        var minutes = diameter >= 160;
        var outer = radius - (Hairline * 2);

        for (var mark = 0; mark < 60; mark++)
        {
            var onHour = mark % 5 == 0;

            if (!onHour && !minutes)
            {
                continue;
            }

            var angle = mark * 6 * Math.PI / 180;
            var length = onHour ? Math.Max(3, radius * 0.10) : Math.Max(1.5, radius * 0.04);
            var inner = outer - length;

            AnalogFace.Children.Add(new Line
            {
                X1 = radius + (Math.Sin(angle) * inner),
                Y1 = radius - (Math.Cos(angle) * inner),
                X2 = radius + (Math.Sin(angle) * outer),
                Y2 = radius - (Math.Cos(angle) * outer),
                Stroke = onHour ? hourTick : minuteTick,
                StrokeThickness = Hairline,
            });
        }

        // Fresh transforms each rebuild: the previous hands are unparented but still hold a
        // reference to the old ones, and a transform with two owners is a rotation applied twice.
        _hourRotation = new RotateTransform();
        _minuteRotation = new RotateTransform();
        _secondRotation = new RotateTransform();

        AddHand(hand, radius * 0.50, Math.Max(2, radius * 0.042), 0, _hourRotation, radius);
        AddHand(hand, radius * 0.74, Math.Max(1.5, radius * 0.030), 0, _minuteRotation, radius);

        _secondHand = AddHand(accent, radius * 0.80, Math.Max(1, radius * 0.014), radius * 0.16, _secondRotation, radius);
        _secondHand.Visibility = _config.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;

        var hubSize = Math.Max(4, radius * 0.10);

        var hub = new Ellipse
        {
            Width = hubSize,
            Height = hubSize,
            Fill = accent,
        };

        Canvas.SetLeft(hub, radius - (hubSize / 2));
        Canvas.SetTop(hub, radius - (hubSize / 2));
        AnalogFace.Children.Add(hub);
    }

    /// <summary>
    /// One hand: a rounded bar standing straight up from the centre, rotated about the point where
    /// it meets the hub. <paramref name="tail"/> is the stub that carries on past the centre, which
    /// only the second hand has.
    /// </summary>
    private Rectangle AddHand(
        Brush brush,
        double length,
        double thickness,
        double tail,
        RotateTransform rotation,
        double centre)
    {
        var bar = new Rectangle
        {
            Width = thickness,
            Height = length + tail,
            RadiusX = thickness / 2,
            RadiusY = thickness / 2,
            Fill = brush,
            RenderTransformOrigin = new Point(0.5, length / (length + tail)),
            RenderTransform = rotation,
        };

        Canvas.SetLeft(bar, centre - (thickness / 2));
        Canvas.SetTop(bar, centre - length);
        AnalogFace.Children.Add(bar);

        return bar;
    }

    // -----------------------------------------------------------------------------------------
    // Ticking
    // -----------------------------------------------------------------------------------------

    private void Resubscribe()
    {
        // Only when the cadence actually changed. Dropping and remaking the subscription on every
        // config touch is what made dragging a settings slider stutter: each tick tore down the
        // timer subscription for every widget on the desktop and built it again.
        var wanted = _config.TickInterval;
        if (_subscription is not null && wanted == _subscribedInterval)
        {
            Render();
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = wanted;
        _subscription = _ticker?.Subscribe(wanted, Render);
    }

    private void Render()
    {
        var now = DateTimeOffset.UtcNow;
        var locale = Strings.Current;
        var reading = ClockFormatter.Read(now, _config, locale);
        var offset = _showOffset || _offsetOnDate ? ClockFormatter.OffsetLine(now, _config, locale) : null;
        OffsetText.Text = offset ?? string.Empty;

        var date = _shortDate ? ClockFormatter.ShortDate(now, _config, locale) : reading.Date ?? string.Empty;
        DateText.Text = _offsetOnDate && offset is not null ? $"{date} · {offset}" : date;

        if (_config.Face != ClockFace.Analog)
        {
            if (_stacked)
            {
                // Hours over minutes; the seconds, if any, ride in the tail beside the minutes.
                var parts = reading.Time.Split(':');
                TimeText.Text = $"{parts[0]}\n{parts[1]}";
                SecondsText.Text = parts.Length > 2 ? parts[2] : string.Empty;
            }
            else
            {
                TimeText.Text = reading.Time;
            }

            MeridiemText.Text = reading.Meridiem ?? string.Empty;
            return;
        }

        _hourRotation.Angle = reading.HourAngle;
        _minuteRotation.Angle = reading.MinuteAngle;
        _secondRotation.Angle = reading.SecondAngle;
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }
}
