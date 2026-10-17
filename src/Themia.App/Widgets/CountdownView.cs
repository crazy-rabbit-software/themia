using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// Time left until one date the user cares about ([S-6036]): one widget, one target. [C-01]
/// <para>
/// [S-1447] The readout is the whole tile: the name and the duration always, then the target's date
/// and, on a large tile, a progress bar from when the target was set to the target ([S-1448]), as
/// far as they fit; what they leave goes to the duration. A small tile centres it; a large one reads
/// from the left like the clock's text.
/// </para>
/// <para>
/// [S-6030] The style is how the duration draws: the text, a ring of the time left with the duration
/// inside it ([S-6031], <see cref="RingPart"/>), or split-flap cards ([S-6032], the clock's
/// <see cref="FlipCard"/>). The ladder is the same for all three; a tile too small for its ring or its
/// cards draws the text.
/// </para>
/// </summary>
internal sealed partial class CountdownView : Grid, IWidgetView, IWidgetSizeAware, IWidgetAligned
{
    private const double ProgressHeight = 4;

    /// <summary>[S-6031] Under this a ring with the duration inside it is a donut: the text instead.</summary>
    private const double RingFloor = 72;

    /// <summary>[S-6032] Under this card height the figures are smaller than the text style's: the text instead.</summary>
    private const double FlipFloor = 36;

    private readonly StackPanel _hero = new() { Spacing = 0 };
    private readonly TextBlock _heroLabel = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _heroValue = Fluent.Text(FluentType.Title);
    private readonly TextBlock _heroDate = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly ProgressBar _heroProgress = new() { Minimum = 0, Maximum = 1, Height = ProgressHeight, MinHeight = ProgressHeight };

    /// <summary>[S-6031] The Ring style: the time left as a ring, the duration inside it.</summary>
    private readonly RingPart _ring = new();
    private readonly TextBlock _ringValue = Fluent.Text(FluentType.BodyStrong);

    /// <summary>[S-6032] The Flip style: one card per unit, its unit under it.</summary>
    private readonly StackPanel _flipRow = new() { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight };
    private readonly List<(FlipCard Card, TextBlock Unit)> _flips = [];

    /// <summary>The look the last <see cref="Lay"/> settled on: the style, or the text where its parts did not fit.</summary>
    private CountdownStyle _look;

    private readonly Brush _primary = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _critical = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);

    private Ticker? _ticker;
    private CountdownConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private WidgetArea _area;
    private bool _heroDateShown;
    private bool _heroProgressShown;
    private bool _detached;

    public CountdownView()
    {
        // [C-04] Tabular figures, so a ticking duration does not shuffle sideways.
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_heroValue, FontNumeralAlignment.Tabular);
        _heroValue.MaxLines = 1;
        _heroValue.TextTrimming = TextTrimming.Clip;
        _heroLabel.MaxLines = 1;
        _heroLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        _heroDate.MaxLines = 1;
        _heroProgress.Margin = new Thickness(0, Fluent.Space1, 0, 0);

        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_ringValue, FontNumeralAlignment.Tabular);
        _ringValue.MaxLines = 1;
        _ringValue.TextTrimming = TextTrimming.Clip;
        _ringValue.TextAlignment = TextAlignment.Center;
        _ring.Add(_ringValue);
        _ring.Root.Margin = new Thickness(0, Fluent.Space1, 0, Fluent.Space1);
        _flipRow.Spacing = Fluent.Space2;
        _flipRow.Margin = new Thickness(0, Fluent.Space1, 0, 0);

        _hero.Children.Add(_heroLabel);
        _hero.Children.Add(_heroValue);
        _hero.Children.Add(_ring.Root);
        _hero.Children.Add(_flipRow);
        _hero.Children.Add(_heroDate);
        _hero.Children.Add(_heroProgress);
        _hero.VerticalAlignment = VerticalAlignment.Center;

        Children.Add(_hero);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1109] The reading and the date are worded again in the new language.</summary>
    private void OnLanguage(Locale locale) => Rebuild();

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as CountdownConfig ?? new CountdownConfig();
        Rebuild();
    }

    /// <summary>[S-6583] Where the name, the time left, the date and the bar sit: the widget's alignment, from the start by default ([S-6582]).</summary>
    private ContentAlign _align = ContentAlign.Left;

    /// <summary>[S-6583] The widget's alignment, from the properties panel.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        Rebuild();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Rebuild();
    }

    /// <summary>The target, its format or the room changed: cadence, layout and letters.</summary>
    private void Rebuild()
    {
        if (_detached)
        {
            return;
        }

        Resubscribe();
        Lay();
        Refresh();
    }

    /// <summary>
    /// Only when the cadence actually changed. Dropping and remaking the subscription on every
    /// config touch makes dragging a settings slider stutter. [C-20]
    /// </summary>
    private void Resubscribe()
    {
        // [S-142] Nothing to re-letter means nothing to wake for.
        if (_config.Target is null)
        {
            _subscription?.Dispose();
            _subscription = null;
            _subscribedInterval = TimeSpan.Zero;
            return;
        }

        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
    }

    /// <summary>[S-1447] Give the readout the tile: the name and the duration, then the date and the bar as they fit.</summary>
    private void Lay()
    {
        // [S-1862] No target: the card shows its Set up button over this, so draw nothing.
        if (_config.Target is not { } target)
        {
            _hero.Visibility = Visibility.Collapsed;
            return;
        }

        _hero.Visibility = Visibility.Visible;

        // [S-6583] The readout where the widget's alignment puts it, at every size: the column
        // stretches (so the bar keeps the width), its lines, the ring and the cards follow.
        var place = Aligned.Horizontal(_align);
        _hero.HorizontalAlignment = HorizontalAlignment.Stretch;
        _ring.Root.HorizontalAlignment = place;
        _flipRow.HorizontalAlignment = place;
        _hero.Margin = new Thickness(WidgetLayout.ContentInset, 0, WidgetLayout.ContentInset, 0);
        foreach (var text in (TextBlock[])[_heroLabel, _heroValue, _heroDate])
        {
            text.TextAlignment = Aligned.Text(_align);
        }

        if (!_area.IsMeasured)
        {
            return;
        }

        // The ladder: the name and the readout at its smallest always; then the date and the progress
        // bar as they fit; whatever is left goes to the readout.
        var boxWidth = _area.Width - (2 * WidgetLayout.ContentInset);
        var room = new Disclosure(boxWidth, _area.Height - Fluent.Space2, gap: 0);

        var labelStep = _area.HeightClass == SizeClass.Compact ? FluentType.Caption : FluentType.Body;
        _heroLabel.Apply(labelStep);
        room.Reserve(labelStep.LineHeight);
        room.Reserve(FluentType.Subtitle.LineHeight);

        _heroDateShown = _area.HeightClass >= SizeClass.Regular && room.Take(FluentType.Caption.LineHeight);

        // [S-6031] The ring is the progress: no bar under it.
        _heroProgressShown = CountdownLayout.ShowsProgress(_area) && _config.Style != CountdownStyle.Ring
            && room.Take(ProgressHeight + Fluent.Space1);

        _heroDate.Visibility = _heroDateShown ? Visibility.Visible : Visibility.Collapsed;
        _heroProgress.Visibility = _heroProgressShown ? Visibility.Visible : Visibility.Collapsed;

        var elapsed = target <= DateTimeOffset.Now;
        var characters = CountdownLayout.HeroCharacters(_config.Format, elapsed);
        var valueHeight = room.Remaining + FluentType.Subtitle.LineHeight;
        var step = FluentType.Hero(boxWidth, valueHeight, characters);

        _heroValue.Apply(step);
        _look = _config.Style switch
        {
            CountdownStyle.Ring when LayRing(boxWidth, valueHeight, characters) => CountdownStyle.Ring,
            CountdownStyle.Flip when LayFlip(target, boxWidth, valueHeight) => CountdownStyle.Flip,
            _ => CountdownStyle.Text,
        };

        _heroValue.Visibility = _look == CountdownStyle.Text ? Visibility.Visible : Visibility.Collapsed;
        _ring.Root.Visibility = _look == CountdownStyle.Ring ? Visibility.Visible : Visibility.Collapsed;
        _flipRow.Visibility = _look == CountdownStyle.Flip ? Visibility.Visible : Visibility.Collapsed;

        // [S-6005] What one style built is let go when another is chosen.
        if (_look != CountdownStyle.Flip && _flips.Count > 0)
        {
            _flips.Clear();
            _flipRow.Children.Clear();
        }
    }

    /// <summary>[S-6031] The ring as large as the room, the duration inside it; false when that is under its floor.</summary>
    private bool LayRing(double width, double height, int characters)
    {
        var diameter = Math.Floor(Math.Min(width, height - (2 * Fluent.Space1)));
        if (diameter < RingFloor)
        {
            return false;
        }

        _ring.Resize(diameter);
        var inner = diameter - (2 * RingArc.Stroke(diameter)) - (2 * Fluent.Space2);
        _ringValue.Apply(FluentType.Strong(FluentType.Hero(inner, inner * 0.4, characters)));
        _ringValue.MaxWidth = Math.Max(1, inner);
        return true;
    }

    /// <summary>[S-6032] One card per unit, side by side across the tile, its unit a caption under it; false when the cards would be too small.</summary>
    private bool LayFlip(DateTimeOffset target, double width, double height)
    {
        var cards = CountdownLayout.Cards(target, DateTimeOffset.Now, _config.Format == CountdownFormat.DaysHoursMinutesSeconds);
        var gap = Fluent.Space2;
        var cardWidth = (width - (gap * (cards.Count - 1))) / cards.Count;
        var cardHeight = Math.Floor(Math.Min(height - FluentType.Caption.LineHeight - (2 * Fluent.Space1), cardWidth * FlipCard.Aspect));
        cardWidth = Math.Floor(Math.Min(cardWidth, cardHeight / FlipCard.Aspect * 1.2));

        if (cardHeight < FlipFloor)
        {
            return false;
        }

        EnsureFlips(cards.Count);
        var widest = cards.Max(c => c.Digits.Length);
        var digits = FluentType.Hero(cardWidth * 0.86, cardHeight * FlipCard.DigitShare, Math.Max(2, widest));

        for (var i = 0; i < _flips.Count; i++)
        {
            _flips[i].Card.Lay(cardWidth, cardHeight, digits);
            _flips[i].Card.Redraw();
            _flips[i].Unit.Text = UnitText(cards[i].Unit);
            _flips[i].Unit.Width = cardWidth;
        }

        return true;
    }

    private void EnsureFlips(int count)
    {
        if (_flips.Count == count)
        {
            return;
        }

        _flips.Clear();
        _flipRow.Children.Clear();

        for (var i = 0; i < count; i++)
        {
            var card = new FlipCard();
            var unit = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            unit.TextAlignment = TextAlignment.Center;
            unit.TextTrimming = TextTrimming.CharacterEllipsis;
            unit.Margin = new Thickness(0, Fluent.Space1, 0, 0);
            _flips.Add((card, unit));
            _flipRow.Children.Add(new StackPanel { Children = { card.Root, unit } });
        }
    }

    private static string UnitText(CountdownUnit unit) => (unit switch
    {
        CountdownUnit.Days => Str.Styles_Countdown_UnitDays,
        CountdownUnit.Hours => Str.Styles_Countdown_UnitHours,
        CountdownUnit.Minutes => Str.Styles_Countdown_UnitMinutes,
        _ => Str.Styles_Countdown_UnitSeconds,
    }).Text();

    private void OnTick()
    {
        if (!_detached)
        {
            Refresh();
        }
    }

    /// <summary>Re-letter the readout. Called on every tick, so it allocates nothing but strings.</summary>
    private void Refresh()
    {
        if (_config.Target is not { } target)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var zone = TimeZoneInfo.Local;
        var reading = CountdownFormatter.Read(target, now, _config.Format, Strings.Current);
        _heroLabel.Text = string.IsNullOrWhiteSpace(_config.Label) ? CountdownFormatter.Untitled(Strings.Current) : _config.Label;
        _heroValue.Text = reading.Text;

        // [C-40] Gone by: the readout carries it in the theme's critical colour.
        _heroValue.Foreground = reading.Elapsed ? _critical : _primary;
        _ringValue.Text = reading.Text;
        _ringValue.Foreground = _heroValue.Foreground;

        if (_look == CountdownStyle.Ring)
        {
            _ring.SetFraction(CountdownLayout.Left(_config, now));
        }

        if (_look == CountdownStyle.Flip)
        {
            var cards = CountdownLayout.Cards(target, now, _config.Format == CountdownFormat.DaysHoursMinutesSeconds);

            // The days card comes and goes at the last day: a new row of cards, not new figures.
            if (cards.Count != _flips.Count)
            {
                Lay();
            }

            for (var i = 0; i < _flips.Count && i < cards.Count; i++)
            {
                _flips[i].Card.Foreground = _heroValue.Foreground;
                _flips[i].Card.Show(cards[i].Digits, IsLoaded);
            }
        }

        _heroDate.Text = CountdownFormatter.TargetLabel(target, now, zone, Strings.Current);

        // [C-05] The whole instant, for a date line that is necessarily abbreviated (or not shown).
        ToolTipService.SetToolTip(_hero, CountdownFormatter.FullTargetLabel(target, zone, Strings.Current));

        if (_heroProgressShown && CountdownLayout.Progress(_config, now) is { } progress)
        {
            _heroProgress.Value = progress;
            _heroProgress.Visibility = Visibility.Visible;
        }
        else
        {
            _heroProgress.Visibility = Visibility.Collapsed;
        }
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }
}
