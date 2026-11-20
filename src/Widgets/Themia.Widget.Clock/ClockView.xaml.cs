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
using Themia.Widget.Clock;

namespace Themia.App.Widgets;

/// <summary>
/// The clock, a one-readout widget on the family's steps ([S-6737]). Subscribes to the shared
/// <see cref="Ticker"/> at the cadence it actually draws, and unsubscribes when it leaves the tree.
/// <para>
/// [S-6011] Six styles, each one block in the card's inset of 12, placed by the widget's alignment ([S-6581]):
/// the Readout block for the digital, stacked and minimal faces ([S-6741]); the sentence on the Body to 68 steps
/// for the words ([S-6024]); split-flap cards for flip (<see cref="FlipCard"/>, [S-6742]); a dial from the
/// family's strokes and roles for analog ([S-6743]). Under the face (beside a dial on a strip) one line: the city
/// of a world clock and the offset as they fit (<see cref="ClockFace.Under"/>), on the step's line under. The date
/// is the Date widget's ([S-12805]).
/// </para>
/// <para>
/// [S-12804] The face fills its box: it is planned at its Size's step in the natural box (<see cref="ClockFace"/>)
/// and scaled as one to the card (<see cref="InkFit"/>, uniformly; down to its figures at the legibility floor, its meridiem
/// and line under giving way first, [S-12833]) until its ink, not its line's
/// leading, fills the card's height or its width, as a display clock is, so a larger card is a larger clock and never
/// a band round a small one (owner, 2026-11-19: "the clock widget has too big of a space at the top and bottom"). The dial is planned in the whole box.
/// </para>
/// </summary>
public sealed partial class ClockView : UserControl, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetAligned
{
    /// <inheritdoc />
    public CardOpen? Opens => ClockModule.Opens;

    /// <summary>The face's one block, at its planned step; the fit scales it to the box.</summary>
    private readonly StackPanel _stack = new();

    /// <summary>[S-12804] The face scaled up as one until its ink fills the content box (<see cref="InkFit"/>); the alignment places it.</summary>
    private readonly InkFit _fit = new();

    /// <summary>[S-6741] The digital, stacked and minimal readout.</summary>
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

    private Ticker? _ticker;
    private ClockConfig _config = new();
    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();

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
    private bool _secondsDrawn;
    private TypeStep _underStep = FamilyType.Caption;
    private double _underWidth;
    private bool _underShown;
    private string? _label;
    private ContentAlign _align = ContentAlign.Center;

    /// <summary>[S-11850] The sample's fixed instant (<see cref="ShowSampleAsync"/>), so every style shows the same time.</summary>
    private DateTimeOffset? _frozen;

    private DateTimeOffset Now => _frozen ?? DateTimeOffset.UtcNow;

    public ClockView()
    {
        InitializeComponent();

        // [S-6703] The card's inset on the sides and the foot; the top is the card's ([S-11600]: the inset
        // when no header stands above, the header's gap under one), so a header added moves the face down
        // by its own cost and leaves it its size.
        Root.Padding = Family.Inset with { Top = 0 };
        _fit.Child = _stack;
        Root.Children.Add(_fit);
        _under.TextTrimming = TextTrimming.CharacterEllipsis;
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

    /// <summary>[S-1484] [S-6583] The widget's alignment, from the selection's page.</summary>
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

    /// <summary>
    /// The box the face is planned in: the natural box ([S-12804]: the fit scales the face from there to the card, so its
    /// step is its Size's and a larger card is a larger face, never a band; [S-12833] a smaller one a smaller face). The dial is planned in the whole box: drawn at its
    /// size, its hairlines stay hairlines.
    /// </summary>
    private (double Width, double Height) ContentBox()
    {
        var width = _area.Width - (2 * FamilySpace.Inset);
        var height = _area.Height - FamilySpace.Inset;
        if (_config.Style != ClockStyle.Analog)
        {
            var meridiem = RegionFormat.Current.Use24Hour ? string.Empty : ClockFormatter.WidestMeridiem(Strings.Current);
            var natural = ClockFace.Sizing(_config, meridiem).Natural;
            // [S-12833] Planned in the natural box at every size: a smaller card scales the same face down (to its figures
            // at the floor), never a folded one (seconds kept, never stacked); only the meridiem and the line under give way (Fit).
            width = natural.Width - (2 * FamilySpace.Inset);
            height = natural.Height - FamilySpace.Inset - FamilySpace.Inset;
        }

        return (Math.Max(0, width), Math.Max(0, height));
    }

    /// <summary>
    /// [S-12833] Which parts the face draws in the card it was given and the most it is drawn at: the meridiem and the line
    /// under give way where the card would draw them under the legibility floor (<see cref="ClockFace.Fit"/>).
    /// </summary>
    private FaceFit Fit(string meridiem)
        => _area.IsMeasured
            ? ClockFace.Fit(_area.Width - (2 * FamilySpace.Inset), _area.Height - FamilySpace.Inset, _config, meridiem)
            : FaceFit.All;

    /// <summary>Plan the face for the area and the style, then draw the instant into it.</summary>
    private void Lay()
    {
        var (width, height) = ContentBox();
        var style = _config.Style;
        var meridiem = RegionFormat.Current.Use24Hour ? string.Empty : ClockFormatter.WidestMeridiem(Strings.Current);
        var wantsLine = style != ClockStyle.Minimal && (_label is not null || _config.OffsetMode != ClockOffsetMode.None);
        var fit = Fit(meridiem);
        _secondsDrawn = _config.DrawsSeconds;
        _underShown = false;
        _underWidth = width;
        // Every part leaves its last parent first: a part has one parent, and the styles share some.
        _stack.Children.Clear();
        _dialLine.Children.Clear();

        switch (style)
        {
            case ClockStyle.Word:
                var step = WordClock.Fit(width, height, WordClock.Longest(Strings.Current), wantsLine);
                Family.Step(_words, step);
                _words.MaxWidth = Math.Max(1, width);
                _underStep = FamilyType.Under(step);
                _underShown = wantsLine && WordClock.Holds(width, height, WordClock.Longest(Strings.Current), step, under: true) && fit.Draws(_underStep.FontSize);
                _stack.Children.Add(_words);
                AddUnder();
                break;

            case ClockStyle.Flip:
                LayFlip(width, height, meridiem, wantsLine, fit);
                break;

            case ClockStyle.Analog:
                LayDial(width, height, wantsLine);
                break;

            default:
                _digits = ClockFace.Readout(width, height, style, _config.DrawsSeconds, meridiem, wantsLine);
                _digits = _digits with
                {
                    Meridiem = _digits.Meridiem && fit.Draws(FamilyType.Unit(_digits.Step).FontSize),
                    Under = _digits.Under && fit.Draws(FamilyType.Under(_digits.Step).FontSize),
                };
                _secondsDrawn = _config.DrawsSeconds && _digits.Seconds;

                // [S-6014] The minimal clock: the same readout at the regular weight, alone.
                _readout.Use(style == ClockStyle.Minimal ? FamilyType.Regular(_digits.Step) : _digits.Step, Aligned.Horizontal(_align));

                // [S-14319] Held to its widest time and meridiem: the face never moves or grows as the time runs.
                var template = ClockFormatter.Template(_config, Strings.Current).Split(':');
                var held = _digits.Seconds ? template : template[..Math.Min(2, template.Length)];
                _readout.Hold(string.Join(_digits.Stacked ? "\n" : ":", held), _digits.Meridiem && meridiem.Length > 0 ? " " + meridiem : string.Empty);
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

    /// <summary>[S-6742] The cards on their step, the meridiem after the last, the line under them ([S-12833]: as the card keeps them).</summary>
    private void LayFlip(double width, double height, string meridiem, bool wantsLine, FaceFit fit)
    {
        var count = _config.DrawsSeconds ? 3 : 2;
        var (plan, vertical, withMeridiem) = ClockFace.Flip(width, height, count, meridiem, wantsLine);
        withMeridiem = withMeridiem && fit.Draws(FamilyType.Unit(plan.Step).FontSize);
        plan = plan with { Under = plan.Under && fit.Draws(FamilyType.Under(plan.Step).FontSize) };

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

    /// <summary>[S-1484] Left, centre or right ([S-6583]) for the face and every line of it.</summary>
    private void ApplyAlignment()
    {
        var horizontal = Aligned.Horizontal(_align);
        var text = Aligned.Text(_align);
        _stack.HorizontalAlignment = horizontal;
        _fit.Align = horizontal;
        _words.TextAlignment = text;
        _words.HorizontalAlignment = horizontal;
        _flipLine.HorizontalAlignment = horizontal;
        _dial.HorizontalAlignment = horizontal;

        _under.HorizontalAlignment = horizontal;
        _under.TextAlignment = text;
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
        if (!_tick.Set(_ticker, _secondsDrawn ? TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(1), Render))
        {
            Render();
        }
    }

    private void Render()
    {
        var now = Now;
        var locale = Strings.Current;
        var reading = ClockFormatter.Read(now, _config, locale, RegionFormat.Current.Use24Hour);

        // [S-14201] New words or figures may ink higher or lower in the same box (a descender in the line under): the fit
        // places the face on its ink again, which XAML does not do while its size stays.
        _fit.InvalidateArrange();

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
        return ClockFace.Under(_underWidth, _underStep, _label, offset);
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _tick.Dispose();
    }
}
