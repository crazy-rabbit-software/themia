using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using DayText = Themia.Core.Widgets.DateText;
using Themia.Widget.Clock;

namespace Themia.App.Widgets;

/// <summary>
/// The clock, a one-readout widget on the family's steps ([S-6737]). Subscribes to the shared
/// <see cref="Ticker"/> at the cadence it actually draws, and unsubscribes when it leaves the tree.
/// <para>
/// [S-6011] Seven styles, each one block in the card's inset of 12, centred top to bottom and placed by the
/// widget's alignment ([S-6581]), on the largest fixed step that fits, never filling the box
/// (<see cref="ClockFace"/>): the Readout block for the digital, stacked and minimal faces ([S-6741]) and the
/// date style's day ([S-6744]); the sentence on the Body to 68 steps for the words ([S-6024]); split-flap cards
/// for flip (<see cref="FlipCard"/>, [S-6742]); a dial from the family's strokes and roles for analog ([S-6743]).
/// Under the face (beside a dial on a strip) one line: the city of a world clock, the date and the offset as
/// they fit (<see cref="ClockFace.Under"/>), on the step's line under.
/// </para>
/// </summary>
public sealed partial class ClockView : UserControl, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetAligned
{
    /// <inheritdoc />
    public CardOpen? Opens => ClockModule.Opens;

    /// <summary>The face's one block, centred top to bottom in the inset; the alignment places it across.</summary>
    private readonly StackPanel _stack = new() { VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-6741] The digital, stacked and minimal readout, and the date style's day ([S-6744]).</summary>
    private readonly ReadoutBlock _readout = new();

    /// <summary>[S-6013] The words.</summary>
    private readonly TextBlock _words = new() { TextWrapping = TextWrapping.Wrap, Foreground = Family.Brush(ColourRole.TextPrimary) };

    /// <summary>The one line under the words, the cards or the dial.</summary>
    private readonly TextBlock _under = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);

    /// <summary>[S-6742] The cards, and the meridiem after the last one.</summary>
    private readonly StackPanel _flipLine = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _flipRow = new() { FlowDirection = FlowDirection.LeftToRight, Spacing = FlipRules.Gap };
    private readonly TextBlock _flipMeridiem = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly List<FlipCard> _flipCards = [];

    /// <summary>[S-6743] The dial and its line, under it or beside it.</summary>
    private readonly StackPanel _dialLine = new();
    private readonly Canvas _dial = new() { FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-6744] The date style's words: the weekday over the day, the month under it (beside it on a strip).</summary>
    private readonly TextBlock _weekday = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly TextBlock _month = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);

    private Ticker? _ticker;
    private ClockConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;

    // The analog face is rebuilt when its diameter changes, so the hands' transforms and the second
    // hand are held here.
    private RotateTransform _hourRotation = new();
    private RotateTransform _minuteRotation = new();
    private RotateTransform _secondRotation = new();
    private Rectangle? _secondHand;
    private double _dialDiameter;

    // The last area the card gave the view, and what was planned for it.
    private WidgetArea _area;
    private ClockReadoutPlan _digits;
    private ClockDatePlan _date;
    private bool _secondsDrawn;
    private TypeStep _underStep = FamilyType.Caption;
    private double _underWidth;
    private bool _underShown;
    private string? _label;
    private ContentAlign _align = ContentAlign.Center;

#if DEBUG
    /// <summary>A render's fixed instant (the <c>sample</c> preview state), so every style shows the same time.</summary>
    private DateTimeOffset? _frozen;

    private DateTimeOffset Now => _frozen ?? DateTimeOffset.UtcNow;
#else
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;
#endif

    public ClockView()
    {
        InitializeComponent();

        // [S-6703] The card's inset on the sides and the foot; the top is the card's ([S-11600]: the inset
        // when no header stands above, the header's gap under one), so a header added moves the face down
        // by its own cost and leaves it its size.
        Root.Padding = Family.Inset with { Top = 0 };
        Root.Children.Add(_stack);
        _under.TextTrimming = TextTrimming.CharacterEllipsis;
        _weekday.TextTrimming = TextTrimming.CharacterEllipsis;
        _month.TextTrimming = TextTrimming.CharacterEllipsis;
        _flipMeridiem.VerticalAlignment = VerticalAlignment.Bottom;
        _flipMeridiem.Margin = new Thickness(FamilySpace.S2, 0, 0, 0);
        _flipLine.Children.Add(_flipRow);
        _flipLine.Children.Add(_flipMeridiem);
        _words.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;

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
        ToolTipService.SetToolTip(this, null);

        // [S-6016] A style lets go of what the last one built before the next is laid.
        if (_config.Style != ClockStyle.Analog)
        {
            _dial.Children.Clear();
            _secondHand = null;
            _dialDiameter = 0;
        }

        if (_config.Style != ClockStyle.Flip)
        {
            _flipRow.Children.Clear();
            _flipCards.Clear();
        }

        // [S-6012] A changed config or language redraws the cards at once: only a tick flips them.
        foreach (var card in _flipCards)
        {
            card.Redraw();
        }

        Lay();
        Resubscribe();
    }

    /// <summary>[S-1484] [S-6583] The widget's alignment, from the properties panel.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        Lay();
    }

    /// <summary>[S-663] The card's word on how much room the clock has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>The content box: the view less its inset of 12 on the sides and the foot (the card's is over it).</summary>
    private (double Width, double Height) ContentBox()
        => (Math.Max(0, _area.Width - (2 * FamilySpace.Inset)), Math.Max(0, _area.Height - FamilySpace.Inset));

    /// <summary>Plan the face for the area and the style, then draw the instant into it.</summary>
    private void Lay()
    {
        var (width, height) = ContentBox();
        var style = _config.Style;
        var meridiem = RegionFormat.Current.Use24Hour ? string.Empty : ClockFormatter.WidestMeridiem(Strings.Current);
        var wantsLine = style != ClockStyle.Minimal && (_label is not null || _config.ShowDate || _config.OffsetMode != ClockOffsetMode.None);
        _secondsDrawn = _config.DrawsSeconds;
        _underShown = false;
        _underWidth = width;
        // Every part leaves its last parent first: a part has one parent, and the styles share some.
        _stack.Children.Clear();
        _dialLine.Children.Clear();

        switch (style)
        {
            case ClockStyle.Date:
                LayDate(width, height);
                break;

            case ClockStyle.Word:
                var step = WordClock.Fit(width, height, WordClock.Longest(Strings.Current), wantsLine);
                Family.Step(_words, step);
                _words.MaxWidth = Math.Max(1, width);
                _underStep = FamilyType.Under(step);
                _underShown = wantsLine && WordClock.Holds(width, height, WordClock.Longest(Strings.Current), step, under: true);
                _stack.Children.Add(_words);
                AddUnder();
                break;

            case ClockStyle.Flip:
                LayFlip(width, height, meridiem, wantsLine);
                break;

            case ClockStyle.Analog:
                LayDial(width, height, wantsLine);
                break;

            default:
                _digits = ClockFace.Readout(width, height, style, _config.DrawsSeconds, meridiem, wantsLine);
                _secondsDrawn = _config.DrawsSeconds && _digits.Seconds;

                // [S-6014] The minimal clock: the same readout at the regular weight, alone.
                _readout.Use(style == ClockStyle.Minimal ? FamilyType.Regular(_digits.Step) : _digits.Step, Aligned.Horizontal(_align));
                _underStep = FamilyType.Under(_digits.Step);
                _underShown = _digits.Under;
                _stack.Children.Add(_readout);
                break;
        }

        ApplyAlignment();
        Render();
    }

    private void AddUnder()
    {
        Family.Step(_under, _underStep);
        if (_underShown)
        {
            _stack.Children.Add(_under);
        }
    }

    /// <summary>[S-6742] The cards on their step, the meridiem after the last, the line under them.</summary>
    private void LayFlip(double width, double height, string meridiem, bool wantsLine)
    {
        var count = _config.DrawsSeconds ? 3 : 2;
        var (plan, vertical, withMeridiem) = ClockFace.Flip(width, height, count, meridiem, wantsLine);

        if (_flipCards.Count != count)
        {
            _flipRow.Children.Clear();
            _flipCards.Clear();
            for (var i = 0; i < count; i++)
            {
                var card = new FlipCard();
                _flipCards.Add(card);
                _flipRow.Children.Add(card.Root);
            }
        }

        foreach (var card in _flipCards)
        {
            card.Lay(plan);
        }

        _flipRow.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        Family.Step(_flipMeridiem, FamilyType.Unit(plan.Step));
        _flipMeridiem.Visibility = withMeridiem ? Visibility.Visible : Visibility.Collapsed;
        _underStep = FamilyType.Under(plan.Step);
        _underShown = plan.Under;
        _stack.Children.Add(_flipLine);
        AddUnder();
    }

    /// <summary>[S-6743] The dial, its line under it.</summary>
    private void LayDial(double width, double height, bool wantsLine)
    {
        var plan = ClockFace.Dial(width, height, wantsLine);
        SizeDial(plan.Dial);
        _underStep = FamilyType.Body;
        _underShown = plan.Line;
        Family.Step(_under, _underStep);

        _dialLine.Spacing = ClockFace.DialGap;
        _dialLine.Children.Add(_dial);
        if (plan.Line)
        {
            _under.VerticalAlignment = VerticalAlignment.Center;
            _under.MaxWidth = double.PositiveInfinity;
            _dialLine.Children.Add(_under);
        }

        _stack.Children.Add(_dialLine);
    }

    /// <summary>[S-6744] The day a Readout, the weekday over it and the month under it.</summary>
    private void LayDate(double width, double height)
    {
        _date = ClockFace.Date(width, height);
        _secondsDrawn = false;
        _readout.Use(_date.Step, Aligned.Horizontal(_align));
        var line = FamilyType.Under(_date.Step);
        Family.Step(_weekday, line);
        Family.Step(_month, line);
        _weekday.Visibility = _date.Weekday ? Visibility.Visible : Visibility.Collapsed;
        _month.Visibility = _date.Month ? Visibility.Visible : Visibility.Collapsed;

        _stack.Children.Add(_weekday);
        _stack.Children.Add(_readout);
        _stack.Children.Add(_month);
    }

    /// <summary>[S-1484] Left, centre or right ([S-6583]) for the face and every line of it.</summary>
    private void ApplyAlignment()
    {
        var horizontal = Aligned.Horizontal(_align);
        var text = Aligned.Text(_align);
        _stack.HorizontalAlignment = horizontal;
        _words.TextAlignment = text;
        _words.HorizontalAlignment = horizontal;
        _flipLine.HorizontalAlignment = horizontal;
        _dial.HorizontalAlignment = horizontal;

        foreach (var line in new[] { _under, _weekday, _month })
        {
            line.HorizontalAlignment = horizontal;
            line.TextAlignment = text;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Analog face
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-6743] Size the dial; rebuilt only when the diameter changed (a tick moves three transforms).</summary>
    private void SizeDial(double diameter)
    {
        if (!_area.IsMeasured || !double.IsFinite(diameter) || diameter < 1)
        {
            _dial.Children.Clear();
            _secondHand = null;
            _dialDiameter = 0;
            return;
        }

        if (Math.Abs(diameter - _dialDiameter) < 1)
        {
            if (_secondHand is not null)
            {
                _secondHand.Visibility = _config.DrawsSeconds ? Visibility.Visible : Visibility.Collapsed;
            }

            return;
        }

        _dialDiameter = diameter;
        BuildDial(diameter);
    }

    /// <summary>
    /// [S-6743] The dial from the family's strokes and roles: the rim a hairline in the control stroke, twelve
    /// hour marks (stroke 2, secondary) and from 160 the minute marks (hairline, tertiary), the hour hand a
    /// track (4) and the minute hand a stroke (2) in the primary role, round, the second hand a stroke in the
    /// accent (the dial's one moving meter), the hub an 8 dot in the primary role.
    /// </summary>
    private void BuildDial(double diameter)
    {
        _dial.Children.Clear();
        _dial.Width = diameter;
        _dial.Height = diameter;
        var radius = diameter / 2;
        var hand = Family.Brush(ColourRole.TextPrimary);

        var rim = new Ellipse
        {
            Width = diameter - FamilyShape.Hairline,
            Height = diameter - FamilyShape.Hairline,
            Stroke = Family.Brush(ColourRole.ControlStroke),
            StrokeThickness = FamilyShape.Hairline,
        };

        Canvas.SetLeft(rim, FamilyShape.Hairline / 2);
        Canvas.SetTop(rim, FamilyShape.Hairline / 2);
        _dial.Children.Add(rim);

        // Sixty marks is the real Windows dial, but under 160 they are closer together than they are long.
        var minutes = diameter >= 160;
        var outer = radius - (FamilyShape.Hairline * 3);
        var hourMark = Family.Brush(ColourRole.TextSecondary);
        var minuteMark = Family.Brush(ColourRole.TextTertiary);

        for (var mark = 0; mark < 60; mark++)
        {
            var onHour = mark % 5 == 0;
            if (!onHour && !minutes)
            {
                continue;
            }

            var angle = mark * 6 * Math.PI / 180;
            var length = onHour ? Math.Max(FamilySpace.S1, radius * 0.10) : Math.Max(FamilyShape.Stroke, radius * 0.04);
            var inner = outer - length;

            _dial.Children.Add(new Line
            {
                X1 = radius + (Math.Sin(angle) * inner),
                Y1 = radius - (Math.Cos(angle) * inner),
                X2 = radius + (Math.Sin(angle) * outer),
                Y2 = radius - (Math.Cos(angle) * outer),
                Stroke = onHour ? hourMark : minuteMark,
                StrokeThickness = onHour ? FamilyShape.Stroke : FamilyShape.Hairline,
            });
        }

        // Fresh transforms each rebuild: a transform with two owners is a rotation applied twice.
        _hourRotation = new RotateTransform();
        _minuteRotation = new RotateTransform();
        _secondRotation = new RotateTransform();

        AddHand(hand, radius * 0.50, FamilyShape.Track, 0, _hourRotation, radius);
        AddHand(hand, radius * 0.74, FamilyShape.Stroke, 0, _minuteRotation, radius);
        _secondHand = AddHand(Family.Brush(ColourRole.Accent), radius * 0.80, FamilyShape.Stroke, radius * 0.16, _secondRotation, radius);
        _secondHand.Visibility = _config.DrawsSeconds ? Visibility.Visible : Visibility.Collapsed;

        var hub = new Ellipse { Width = FamilySpace.S2, Height = FamilySpace.S2, Fill = hand };
        Canvas.SetLeft(hub, radius - (FamilySpace.S2 / 2));
        Canvas.SetTop(hub, radius - (FamilySpace.S2 / 2));
        _dial.Children.Add(hub);
    }

    /// <summary>One hand: a round bar standing up from the centre, turned about where it meets the hub; <paramref name="tail"/> carries on past it.</summary>
    private Rectangle AddHand(Brush brush, double length, double thickness, double tail, RotateTransform rotation, double centre)
    {
        var bar = new Rectangle
        {
            Width = thickness,
            Height = length + tail,
            RadiusX = FamilyShape.Round(thickness, length + tail),
            RadiusY = FamilyShape.Round(thickness, length + tail),
            Fill = brush,
            RenderTransformOrigin = new Point(0.5, length / (length + tail)),
            RenderTransform = rotation,
        };

        Canvas.SetLeft(bar, centre - (thickness / 2));
        Canvas.SetTop(bar, centre - length);
        _dial.Children.Add(bar);
        return bar;
    }

    // -----------------------------------------------------------------------------------------
    // Ticking
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-52] Every second only while seconds are drawn (a readout that folded them away at a small size does not wake for them).</summary>
    private void Resubscribe()
    {
        var wanted = _secondsDrawn ? TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(1);
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
        var now = Now;
        var locale = Strings.Current;
        var reading = ClockFormatter.Read(now, _config, locale, RegionFormat.Current.Use24Hour);

        if (_config.Style == ClockStyle.Date)
        {
            // [S-5300] The day in the clock's own zone: a world clock's date is that place's.
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _config.ResolveTimeZone()).DateTime);
            _readout.Show(DayText.Number(locale, day));
            _weekday.Text = DayText.Weekday(locale, day, _date.ShortWeekday);
            _month.Text = DayText.Month(locale, day, _date.ShortMonth);
            ToolTipService.SetToolTip(this, DayText.Full(locale, day));
            return;
        }

        var under = _underShown ? Line(now, reading, locale) : null;
        if (_under.Text != (under ?? string.Empty))
        {
            _under.Text = under ?? string.Empty;
        }

        switch (_config.Style)
        {
            case ClockStyle.Word:
                _words.Text = ClockFormatter.Words(now, _config, locale);
                return;

            case ClockStyle.Flip:
                var cards = reading.Time.Split(':');
                for (var i = 0; i < _flipCards.Count && i < cards.Length; i++)
                {
                    _flipCards[i].Show(cards[i], IsLoaded);
                }

                _flipMeridiem.Text = reading.Meridiem ?? string.Empty;
                return;

            case ClockStyle.Analog:
                _hourRotation.Angle = reading.HourAngle;
                _minuteRotation.Angle = reading.MinuteAngle;
                _secondRotation.Angle = reading.SecondAngle;
                return;
        }

        // [S-6741] The digits: one line, or the hours over the minutes (over the seconds); a part the plan folded away is not drawn.
        var parts = reading.Time.Split(':');
        var shown = _digits.Seconds ? parts : parts[..Math.Min(2, parts.Length)];
        var value = string.Join(_digits.Stacked ? "\n" : ":", shown);
        var unit = _digits.Meridiem && reading.Meridiem is not null ? " " + reading.Meridiem : string.Empty;
        _readout.Show(value, unit, _digits.Under ? Line(now, reading, locale) : null);
    }

    /// <summary>[S-6741] The one line under the face, as its room holds it.</summary>
    private string? Line(DateTimeOffset now, ClockReading reading, Locale locale)
    {
        var offset = _config.OffsetMode != ClockOffsetMode.None ? ClockFormatter.OffsetLine(now, _config, locale) : null;
        var shortDate = _config.ShowDate ? ClockFormatter.ShortDate(now, _config, locale) : null;
        return ClockFace.Under(_underWidth, _underStep, _label, reading.Date, shortDate, offset);
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }
}
