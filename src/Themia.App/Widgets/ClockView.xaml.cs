using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;
using DayText = Themia.Core.Widgets.DateText;

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
/// <para>
/// [S-6011] Seven styles over the same ladder: the digital, stacked and minimal readouts share the
/// digits; the analog dial is drawn in code; the word style is a wrapped sentence sized by the
/// language's longest ([S-6013]); the flip style is a split-flap card per part of the time that
/// turns over when it changes (<see cref="FlipCard"/>, [S-6012]). The minimal clock has no rungs
/// at all ([S-6014]), and the date style has its own ladder (<see cref="DateLayout"/>, [S-5301]):
/// the weekday, the day of the month large and the month, in the clock's zone, and no time.
/// </para>
/// </summary>
public sealed partial class ClockView : UserControl, IWidgetView, IWidgetSizeAware, IWidgetAligned
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

    /// <summary>The AM/PM marker's size against the digits: beside a digital readout, and beside stacked minutes.</summary>
    private const double MeridiemScale = 0.45;
    private const double StackedTailScale = 0.4;

    /// <summary>[S-1488] Under this an analog dial is a smear of hairlines; it never draws smaller.</summary>
    private const double DialFloor = 40;

    /// <summary>
    /// One device-independent pixel. The rim and the ticks are drawn at this thickness at the
    /// tile's real scale, which is the whole reason the analog face is no longer inside a Viewbox:
    /// a hairline that has been scaled up is not a hairline.
    /// </summary>
    private const double Hairline = 1;

    /// <summary>[S-6012] Under this card height there is no corner left for AM/PM beside the digits.</summary>
    private const double FlipMeridiemFloor = 96;

    /// <summary>[S-6012] The flip style's cards: hours, minutes, and seconds when drawn.</summary>
    private readonly List<FlipCard> _flipCards = [];

    /// <summary>[S-6012] The AM/PM marker, in the corner of the hours card.</summary>
    private readonly TextBlock _flipMeridiem = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };

    /// <summary>[S-5300] The date style's face: the weekday, the day of the month and the month.</summary>
    private readonly StackPanel _dayFace = new() { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly StackPanel _dayWords = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _dayWeekday = Fluent.Text(FluentType.BodyStrong, "AccentTextFillColorPrimaryBrush");
    private readonly TextBlock _dayNumber = Fluent.Text(FluentType.Title);
    private readonly TextBlock _dayMonth = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private DatePlan _dayPlan = DateLayout.Plan(default);
    private DateFace? _dayShape;

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

        _dayWeekday.TextTrimming = TextTrimming.CharacterEllipsis;
        _dayMonth.TextTrimming = TextTrimming.CharacterEllipsis;
        _dayNumber.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_dayNumber, FontNumeralAlignment.Tabular);
        FaceHost.Children.Add(_dayFace);

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

        // [S-6005] A style is a config value: show its face, and nothing of the others.
        var style = _config.Style;
        TimeRow.Visibility = Visible(style is ClockStyle.Digital or ClockStyle.Stacked or ClockStyle.Minimal);
        AnalogFace.Visibility = Visible(style == ClockStyle.Analog);
        WordsText.Visibility = Visible(style == ClockStyle.Word);
        FlipRow.Visibility = Visible(style == ClockStyle.Flip);
        _dayFace.Visibility = Visible(style == ClockStyle.Date);
        ToolTipService.SetToolTip(this, null);
        MeridiemText.Visibility = RegionFormat.Current.Use24Hour ? Visibility.Collapsed : Visibility.Visible;
        _flipMeridiem.Visibility = MeridiemText.Visibility;

        if (style != ClockStyle.Analog)
        {
            // A dial built for another style is let go, not kept hidden.
            AnalogFace.Children.Clear();
            _secondHand = null;
            _analogDiameter = 0;
        }

        if (style != ClockStyle.Flip)
        {
            FlipRow.Children.Clear();
            _flipCards.Clear();
        }

        // [S-6012] A changed config or language redraws the cards at once: only a tick flips them.
        foreach (var card in _flipCards)
        {
            card.Redraw();
        }

        if (_secondHand is not null)
        {
            _secondHand.Visibility = _config.DrawsSeconds ? Visibility.Visible : Visibility.Collapsed;
        }

        Lay();
        Resubscribe();
    }

    /// <summary>[S-6583] Where the column sits: the widget's own alignment, centred by default ([S-6582]).</summary>
    private ContentAlign _align = ContentAlign.Center;

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
        var analog = _config.Style == ClockStyle.Analog;

        // [S-1488] A dial on a strip: text beside it, not under it, or the dial is a postage stamp
        // in the middle of a band of nothing.
        _side = analog && _area.Shape == AreaShape.Wide && _area.WidthClass >= SizeClass.Regular;
        Arrange(_side);

        if (_config.Style == ClockStyle.Date)
        {
            LayDate(box);
        }
        else if (_side)
        {
            LaySide(box);
        }
        else
        {
            LayColumn(box, _config.Style);
        }

        ApplyAlignment();
        Render();
    }

    /// <summary>
    /// [S-5301] The date style: no label, date line or offset (the face is the date), the parts
    /// placed for <see cref="DateLayout"/>'s plan: a column, or on a strip the day beside the words.
    /// </summary>
    private void LayDate(WidgetArea box)
    {
        _showOffset = false;
        Show(label: false, date: false);
        _dayPlan = DateLayout.Plan(box);

        if (_dayShape != _dayPlan.Face)
        {
            // Rebuilt only when the face changes, never per pass.
            _dayShape = _dayPlan.Face;
            _dayFace.Children.Clear();
            _dayWords.Children.Clear();

            if (_dayPlan.Face == DateFace.Row)
            {
                _dayFace.Orientation = Orientation.Horizontal;
                _dayFace.Spacing = DateLayout.Gap;
                _dayWords.Children.Add(_dayWeekday);
                _dayWords.Children.Add(_dayMonth);
                _dayFace.Children.Add(_dayNumber);
                _dayFace.Children.Add(_dayWords);
            }
            else
            {
                _dayFace.Orientation = Orientation.Vertical;
                _dayFace.Spacing = 0;
                _dayFace.Children.Add(_dayWeekday);
                _dayFace.Children.Add(_dayNumber);
                _dayFace.Children.Add(_dayMonth);
            }
        }

        _dayNumber.Apply(_dayPlan.Number);
        _dayWeekday.Apply(_dayPlan.WeekdayStep);
        _dayMonth.Apply(_dayPlan.MonthStep);
        _dayWeekday.Visibility = Visible(_dayPlan.Weekday);
        _dayMonth.Visibility = Visible(_dayPlan.Month);

        // A bound, not a width: the pair is aligned as a whole, the words trimmed at the bound.
        _dayWords.MaxWidth = _dayPlan.Face == DateFace.Row ? _dayPlan.TextWidth : double.PositiveInfinity;
    }

    /// <summary>The ordinary column: label, face, then the rungs under it.</summary>
    private void LayColumn(WidgetArea box, ClockStyle style)
    {
        var analog = style == ClockStyle.Analog;
        var width = box.Width;
        var height = box.Height;
        var room = new Disclosure(box, Fluent.Space1);

        // The face at its smallest is always there; the rungs share what it leaves.
        var floor = style switch
        {
            ClockStyle.Analog => DialFloor,
            ClockStyle.Stacked => 2 * FluentType.Subtitle.LineHeight,
            ClockStyle.Word => 2 * FluentType.Body.LineHeight,
            ClockStyle.Flip => 1.5 * FluentType.Subtitle.LineHeight,
            _ => FluentType.Subtitle.LineHeight,
        };
        room.Reserve(floor);

        // [S-1488] A dial on the smallest tile is the whole tile: no words under a 70px clock face.
        // [S-6014] The minimal clock is the time alone at every size: no label, date or offset.
        var words = style != ClockStyle.Minimal
            && (!analog || _area.WidthClass >= SizeClass.Regular || _area.HeightClass >= SizeClass.Regular);

        // [S-1482] The label first: on a world clock "Tokyo" says more than the date does.
        // [S-1476] The label, the date and the offset at the widget's own text size.
        var size = _config.TextSize;
        var labelStep = ClockText.Fit(size, width, height, 0.16, Math.Max(4, _label?.Length ?? 0), FluentType.Caption, FluentType.BodyLarge);
        var showLabel = words && _label is not null && room.Take(labelStep.LineHeight);

        // The date: the long one when the line can hold it, the short one when it cannot, and on a
        // wide strip — which has width it cannot otherwise use — the offset after it.
        _wideDate = !analog && _area.Shape == AreaShape.Wide && _area.WidthClass >= SizeClass.Expanded;
        _shortDate = !_wideDate && width < FluentType.EstimateWidth(ClockText.Floor(size, FluentType.Caption), LongDateCharacters);
        _offsetOnDate = _wideDate && _config.ShowDate && _config.OffsetMode != ClockOffsetMode.None;
        var dateCharacters = (_shortDate ? ShortDateCharacters : LongDateCharacters)
            + (_offsetOnDate ? OffsetCharacters : 0);

        // Support, never a second headline: capped at BodyLarge however large the tile grows (the
        // steps above it are semibold and would read as a second title).
        var dateStep = ClockText.Fit(size, width, height, 0.22, dateCharacters, FluentType.Caption, FluentType.BodyLarge);
        var showDate = words && _config.ShowDate && room.Take(dateStep.LineHeight);
        _offsetOnDate &= showDate;

        // The details are a step under the date, so the eye reads time, date, then the rest.
        var detailStep = ClockText.Fit(size, width, height, 0.12, DetailCharacters, FluentType.Caption, FluentType.Body);
        _showOffset = words && !_offsetOnDate && _config.OffsetMode != ClockOffsetMode.None && room.Take(detailStep.LineHeight);

        Show(showLabel, showDate);
        LabelText.Apply(FluentType.Strong(labelStep));
        DateText.Apply(dateStep);
        OffsetText.Apply(detailStep);

        // The face has what the rungs left, plus the minimum it reserved for itself.
        var faceHeight = room.Remaining + floor;

        switch (style)
        {
            case ClockStyle.Analog:
                SizeDial(Math.Min(width, faceHeight));
                break;

            case ClockStyle.Word:
                SizeWords(width, faceHeight);
                break;

            case ClockStyle.Flip:
                SizeFlip(width, faceHeight);
                break;

            default:
                SizeDigits(width, faceHeight);
                break;
        }
    }

    /// <summary>
    /// [S-6013]/[S-6024] The sentence at the largest step the language's longest sentence fits at,
    /// so the type keeps its size through the day; the room it leaves is the column's.
    /// </summary>
    private void SizeWords(double width, double height)
    {
        var step = WordClock.Fit(width, height, WordClock.Longest(Strings.Current), FluentType.Caption);
        WordsText.Apply(step);
        WordsText.MaxWidth = Math.Max(1, width);
    }

    /// <summary>
    /// [S-6012] Split-flap cards, side by side (one above the other in a tall column), as large as
    /// the face's room lets them be, the digits the largest step that fits a card.
    /// </summary>
    private void SizeFlip(double width, double height)
    {
        var count = _config.DrawsSeconds ? 3 : 2;
        var vertical = _area.Shape == AreaShape.Tall;
        var gap = Fluent.Space2;

        double cardWidth;
        double cardHeight;
        if (vertical)
        {
            cardHeight = (height - (gap * (count - 1))) / count;
            cardWidth = Math.Min(width, cardHeight * FlipCard.Aspect * 1.4);
        }
        else
        {
            cardWidth = (width - (gap * (count - 1))) / count;
            cardHeight = Math.Min(height, cardWidth * FlipCard.Aspect);
            cardWidth = Math.Min(cardWidth, cardHeight / FlipCard.Aspect * 1.2);
        }

        cardWidth = Math.Floor(Math.Max(1, cardWidth));
        cardHeight = Math.Floor(Math.Max(1, cardHeight));

        FlipRow.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        FlipRow.Spacing = gap;
        EnsureFlipCards(count);

        // The meridiem in the hours card's corner, a caption on a flip clock, never a third card;
        // on a card too small to hold it beside the digits it is left out (the digits are the point).
        var meridiem = !RegionFormat.Current.Use24Hour && cardHeight >= FlipMeridiemFloor;
        _flipMeridiem.Visibility = Visible(meridiem);
        var digits = FluentType.Hero(cardWidth * 0.86, cardHeight * (meridiem ? FlipCard.DigitShare - 0.12 : FlipCard.DigitShare), 2);
        foreach (var card in _flipCards)
        {
            card.Lay(cardWidth, cardHeight, digits);
        }

        var tag = FluentType.Fit(cardWidth * 0.4, cardHeight * 0.18, 2, FluentType.Caption, FluentType.BodyLarge);
        _flipMeridiem.Apply(tag);
        _flipMeridiem.Margin = new Thickness(Fluent.Space2, 0, 0, Fluent.Space1);
    }

    private void EnsureFlipCards(int count)
    {
        if (_flipCards.Count == count)
        {
            return;
        }

        FlipRow.Children.Clear();
        _flipCards.Clear();
        (_flipMeridiem.Parent as Panel)?.Children.Remove(_flipMeridiem);

        for (var i = 0; i < count; i++)
        {
            var card = new FlipCard();
            _flipCards.Add(card);
            FlipRow.Children.Add(card.Root);
        }

        _flipMeridiem.Foreground = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
        _flipCards[0].Root.Children.Add(_flipMeridiem);
    }

    /// <summary>[S-1488] A dial on a strip: the dial takes the height, the text column the rest.</summary>
    private void LaySide(WidgetArea box)
    {
        var diameter = Math.Floor(Math.Max(DialFloor, Math.Min(box.Height, box.Width * 0.45)));
        var textWidth = Math.Max(0, box.Width - diameter - Fluent.Space4);
        var room = new Disclosure(textWidth, box.Height, Fluent.Space1);

        var size = _config.TextSize;
        var labelStep = ClockText.Fit(size, textWidth, box.Height, 0.3, Math.Max(4, _label?.Length ?? 0), FluentType.Caption, FluentType.Subtitle);
        var showLabel = _label is not null && room.Take(labelStep.LineHeight);

        _wideDate = false;
        _offsetOnDate = false;
        _shortDate = textWidth < FluentType.EstimateWidth(ClockText.Floor(size, FluentType.Caption), LongDateCharacters);
        var dateStep = ClockText.Fit(size, textWidth, box.Height, 0.25, _shortDate ? ShortDateCharacters : LongDateCharacters, FluentType.Caption, FluentType.BodyLarge);
        var showDate = _config.ShowDate && room.Take(dateStep.LineHeight);

        var detailStep = ClockText.Fit(size, textWidth, box.Height, 0.2, DetailCharacters, FluentType.Caption, FluentType.Body);
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
        // "12:34:56" plus " PM" — the meridiem sits on the same line, so it counts towards the width, by its own length ("பிற்பகல்").
        var meridiem = RegionFormat.Current.Use24Hour ? 0 : ClockFormatter.MeridiemCharacters(Strings.Current, MeridiemScale, 3);
        var characters = (_config.DrawsSeconds ? 8 : 5) + meridiem;
        var timeStep = FluentType.Hero(width, timeHeight, characters);

        // A tall column is too narrow for "15:40" to be large on one line; the hours over the minutes
        // use its height instead — only when that actually draws them larger. [S-1483] The Stacked
        // style always stacks, with the seconds and the meridiem a step down beside the minutes, as
        // the original set them. Stacked digits are set solid (StackedLeading) rather than at the
        // ramp's 1.3 leading: two lines of figures with a ramp line's air between them read as two
        // separate numbers.
        var tail = !RegionFormat.Current.Use24Hour ? ClockFormatter.MeridiemCharacters(Strings.Current, StackedTailScale, 1.2)
            : _config.DrawsSeconds ? 1.2 : 0;
        var perLine = timeHeight / 2;
        var byWidth = FluentType.Hero(width, perLine * 2, 2 + tail);
        var stackedSize = Math.Min(byWidth.FontSize, perLine / StackedLeading);
        var stackedStep = new TypeStep(stackedSize, stackedSize * StackedLeading, true);
        _stacked = _config.Style == ClockStyle.Stacked
            || (_area.Shape == AreaShape.Tall && stackedStep.FontSize > timeStep.FontSize * 1.2);

        if (_stacked)
        {
            timeStep = stackedStep;
        }

        TimeText.Apply(timeStep);


        // The meridiem is the largest ramp step that stays well under the readout: a clock whose
        // "PM" is as tall as its digits reads as two numbers. Stacked, the seconds share its step.
        var tailStep = _stacked
            ? new TypeStep(Math.Max(FluentType.Caption.FontSize, stackedSize * StackedTailScale), Math.Max(FluentType.Caption.FontSize, stackedSize * StackedTailScale) * 1.15, false)
            : FluentType.Fit(width, timeStep.FontSize * MeridiemScale, 2, FluentType.Caption, FluentType.Title);
        MeridiemText.Apply(tailStep);
        SecondsText.Apply(tailStep);

        // [S-6014] The minimal clock: the same digits, and what rides beside them, in Segoe's light weight.
        if (_config.Style == ClockStyle.Minimal)
        {
            TimeText.FontWeight = FontWeights.Light;
            MeridiemText.FontWeight = FontWeights.Light;
            SecondsText.FontWeight = FontWeights.Light;
        }
        SecondsText.Visibility = _stacked && _config.DrawsSeconds ? Visibility.Visible : Visibility.Collapsed;

        // Both blocks are bottom-aligned, so lifting the tail by the difference in half-leading
        // sits its glyphs on the readout's baseline instead of under it.
        var lift = HalfLeading(timeStep) - HalfLeading(tailStep);
        Tail.Margin = new Thickness(0, 0, 0, Math.Max(0, lift));
    }

    private static double HalfLeading(TypeStep step) => (step.LineHeight - step.FontSize) / 2;

    /// <summary>
    /// [S-1484] Left, centre or right ([S-6583]), for the whole column and every line in it. Beside a
    /// dial the text reads from the dial outwards, so it stays left-aligned to it; the alignment places the pair.
    /// </summary>
    private void ApplyAlignment()
    {
        var horizontal = Aligned.Horizontal(_align);
        var text = Aligned.Text(_align);

        Stack.HorizontalAlignment = horizontal;
        TimeRow.HorizontalAlignment = horizontal;
        AnalogFace.HorizontalAlignment = horizontal;
        TimeText.TextAlignment = text;
        WordsText.TextAlignment = text;
        WordsText.HorizontalAlignment = horizontal;
        FlipRow.HorizontalAlignment = horizontal;

        // [S-5301] The date style's lines read from the alignment in a column; beside the day on a
        // strip they read from it outwards.
        _dayFace.HorizontalAlignment = horizontal;
        var row = _dayPlan.Face == DateFace.Row;
        foreach (var line in new[] { _dayWeekday, _dayNumber, _dayMonth })
        {
            line.HorizontalAlignment = row ? HorizontalAlignment.Left : horizontal;
            line.TextAlignment = row ? TextAlignment.Left : text;
        }

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
        _secondHand.Visibility = _config.DrawsSeconds ? Visibility.Visible : Visibility.Collapsed;

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
        var now = Now;
        var locale = Strings.Current;
        var reading = ClockFormatter.Read(now, _config, locale, RegionFormat.Current.Use24Hour);
        var offset = _showOffset || _offsetOnDate ? ClockFormatter.OffsetLine(now, _config, locale) : null;
        OffsetText.Text = offset ?? string.Empty;

        var date = _shortDate ? ClockFormatter.ShortDate(now, _config, locale) : reading.Date ?? string.Empty;
        DateText.Text = _offsetOnDate && offset is not null ? $"{date} · {offset}" : date;

        if (_config.Style == ClockStyle.Date)
        {
            // [S-5300] The day in the clock's own zone: a world clock's date is that place's.
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _config.ResolveTimeZone()).DateTime);
            _dayNumber.Text = DayText.Number(locale, day);
            _dayWeekday.Text = DayText.Weekday(locale, day, _dayPlan.ShortWeekday);
            _dayMonth.Text = DayText.Month(locale, day, _dayPlan.ShortMonth);
            ToolTipService.SetToolTip(this, DayText.Full(locale, day));
            return;
        }

        if (_config.Style == ClockStyle.Word)
        {
            WordsText.Text = ClockFormatter.Words(now, _config, locale);
            return;
        }

        if (_config.Style == ClockStyle.Flip)
        {
            var parts = reading.Time.Split(':');
            for (var i = 0; i < _flipCards.Count && i < parts.Length; i++)
            {
                _flipCards[i].Show(parts[i], IsLoaded);
            }

            _flipMeridiem.Text = reading.Meridiem ?? string.Empty;
            return;
        }

        if (_config.Style != ClockStyle.Analog)
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
