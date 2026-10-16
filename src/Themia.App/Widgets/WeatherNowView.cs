using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The weather where the user says they are, from open-meteo.
/// <para>
/// A readout that grows with its tile ([S-723]): the temperature alone at the smallest size, then a
/// "Feels" token, then a flat row of icon + label + value cells (the original's details, S-07),
/// today's high and low, and at the largest size a second row of readings (the next hours and
/// days are the forecast widget's, never drawn here). A strip puts the readings
/// beside the number, a tall column stacks them as rows under it. The ladder is <see cref="WeatherNowLayout"/>; this view only builds it.
/// </para>
/// <para>
/// The header carries how old the reading is, always, in every state. That line is the reason this
/// widget can be trusted: every other thing on it looks exactly as authoritative four days stale as
/// four seconds fresh, and a weather tile silently showing Tuesday's conditions on Friday is the
/// specific way this kind of widget misleads somebody.
/// </para>
/// </summary>
internal sealed partial class WeatherNowView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetAligned
{
    private const string Scope = "weather";

    /// <summary>The widest number this widget prints, in characters — "-18". Stable, so the step is.</summary>
    private const double ReadingCharacters = 3;

    /// <summary>The unit beside it, "°F".</summary>
    private const double UnitCharacters = 2;

    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");

    // [S-667] The shared hero readout: the number, its unit smaller beside it, the sky's glyph, and
    // the condition, today's range and the "Feels" token as its caption lines.
    private readonly HeroReadout _hero = new();
    private readonly TextBlock _condition;
    private readonly TextBlock _highLow;
    private readonly TextBlock _feels;
    private readonly Grid _top = new();

    private readonly Grid _cells = new() { ColumnSpacing = WeatherNowLayout.CellGap, RowSpacing = WeatherNowLayout.Gap };
    private readonly Grid _side = new() { ColumnSpacing = Fluent.Space4, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _stack = new();

    /// <summary>The reading and its rungs as one group, centred in the tile: never a number floating over a band.</summary>
    private readonly StackPanel _group = new() { VerticalAlignment = VerticalAlignment.Center };

    private readonly StackPanel _emptyState;
    private readonly TextBlock _emptyMessage = new();
    private readonly FontIcon _emptyIcon;

    // Looked up once. Swapping a brush per tick is a dictionary hit and a possible log line for a
    // colour that changes twice a day at most.
    private readonly Brush _freshBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _staleBrush = FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Goldenrod);


    private Ticker? _ticker;
    private WeatherNowConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the settings page edits the live config object in place, so by
    /// the time a change arrives the "previous" config already holds the new values.
    /// </summary>
    private WeatherNowConfig _applied = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    private IWidgetSurface? _surface;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private WebSync? _web;

    /// <summary>What the widget asks for now, or null without a place ([S-2663]).</summary>
    private WeatherInterest? _want;

    /// <summary>The state to say when there is no reading: not set up, or how the last sync went.</summary>
    private FetchStatus _status = FetchStatus.Loading;

    private Fetched<CurrentWeather>? _reading;
    private IReadOnlyList<WeatherFact> _facts = [];
    private WidgetArea _area;

    /// <summary>[S-6583] Where the reading sits: the widget's alignment, from the start by default in the standard style.</summary>
    private ContentAlign _align = ContentAlign.Left;
    private WeatherNowPlan? _built;
    private bool _detached;

    public WeatherNowView()
    {
        _condition = _hero.AddLine(FluentType.Body);
        _highLow = _hero.AddLine(FluentType.Caption);
        _feels = _hero.AddLine(FluentType.Caption);
        _hero.VerticalAlignment = VerticalAlignment.Center;

        // The top band: the reading, and beside it (a strip, a broad tile) the readings column.
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_side, 1);
        _top.Children.Add(_hero);
        _top.Children.Add(_side);

        _cells.Margin = new Thickness(0, WeatherNowLayout.Gap, 0, 0);
        _stack.Margin = new Thickness(0, WeatherNowLayout.Gap, 0, 0);

        _group.Margin = new Thickness(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset, WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        _group.Children.Add(_top);
        _group.Children.Add(_cells);
        _group.Children.Add(_stack);

        _emptyState = Fluent.EmptyState(Glyphs.Weather, _emptyMessage, out _emptyIcon);
        _emptyState.Visibility = Visibility.Collapsed;

        Children.Add(_group);
        Children.Add(_emptyState);

        Collapse(_feels, _cells, _side, _stack);
    }

    /// <summary>
    /// The age of the reading, in the card's header. Built in the constructor because the card
    /// reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _freshness;

    /// <summary>[S-730] "Refresh": the network now, past both the back-off and the cache.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true))] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-2663] The reading comes from the background sync; the widget only hears it.
        _web = AppServices.WebSync;
        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web.Updated += OnWebUpdated;
        }

        ApplyConfig(config);
    }

    /// <summary>The language changed: every text on the tile is rebuilt from the reading in hand.</summary>
    /// <para>
    /// [S-3770] [S-3780] It is also how a change to the shared temperature scale or cadence arrives:
    /// the reading is asked for again under the new scale (the sync holds both, keyed apart).
    /// </para>
    private void OnLanguage(Locale locale)
    {
        Resubscribe();
        Reload(force: false);
    }

    /// <summary>[S-3770] The scale of the reading in hand: the one it was asked in.</summary>
    private TemperatureUnit Unit => _want?.Unit ?? RegionFormat.Current.Temperature;

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as WeatherNowConfig ?? new WeatherNowConfig();
        var previous = _applied;
        _config = next;
        _applied = (WeatherNowConfig)next.Clone();
        PublishTitle();

        var placeChanged = previous.Latitude != next.Latitude
            || previous.Longitude != next.Longitude;

        if (placeChanged)
        {
            // A different place, or a different scale, is a different reading. The one in hand is
            // about somewhere else and must not linger on screen under the new heading.
            _reading = null;
        }

        // [S-2663] Always through the sync: it asks nothing for a reading already held and fresh,
        // so a toggle that only changes the drawing costs no request.
        Resubscribe();
        Reload(force: false);
    }

    /// <summary>[S-663] The card's word on how much room the widget has.</summary>
    /// <summary>[S-6583] The reading at the start, the middle or the end; the readings under it keep their grid.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        _hero.Align(align);
        Lay();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>Rebuild the ticker subscription only when the cadence actually changed.</summary>
    private void Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
    }

    /// <summary>
    /// A tick only redraws: the freshness line keeps counting up. [S-2663] The reading is kept fresh
    /// by the sync service, never fetched from here.
    /// </summary>
    private void OnTick()
    {
        if (!_detached)
        {
            Render();
        }
    }

    /// <summary>
    /// [S-2663] Tell the sync what is on screen and draw what it holds. Nothing is fetched for a
    /// reading already held and fresh; <paramref name="force"/> (the user's Refresh) asks now.
    /// </summary>
    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        _want = SyncInterests.For(_config);
        if (_want is null)
        {
            _status = FetchStatus.NotConfigured;
            _reading = null;
            Render();
            return;
        }

        _web?.Show(_want, force);
        Take();
    }

    /// <summary>[S-2663] A reading's answer or state changed in the sync service.</summary>
    private void OnWebUpdated(string key)
    {
        if (!_detached && _want is { } want && WebSync.Key(want) == key)
        {
            Take();
        }
    }

    /// <summary>
    /// What the sync holds for the place: the newest reading (kept through a failure, annotated with
    /// it), or the state to say when there is none.
    /// </summary>
    private void Take()
    {
        if (_want is not { } want)
        {
            return;
        }

        var answer = _web is { } web ? web.Reading(want) : WeatherService.PeekCurrent(want.Latitude, want.Longitude, want.Unit);
        _reading = answer?.Value is not null ? answer : null;
        _status = answer?.Status ?? FetchStatus.Loading;
        Render();
    }

    /// <summary>
    /// Draw what is in hand. Every state with no reading says which state it is: a blank weather
    /// tile is indistinguishable from a broken one.
    /// </summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var held = _reading;

        if (held?.Value is not { } reading)
        {
            var status = _status;

            // No place yet: the card's Set up button covers this ([S-1862]).
            _emptyMessage.Text = status == FetchStatus.NotConfigured
                ? string.Empty
                : FetchState.Message(status);

            _emptyIcon.Glyph = Glyphs.Weather;
            _emptyState.Visibility = Visibility.Visible;
            Collapse(_group, _styled);
            _freshness.Text = string.Empty;
            _built = null;
            return;
        }

        _emptyState.Visibility = Visibility.Collapsed;

        var (number, unit) = WeatherReport.TemperatureParts(reading.Temperature, Unit);
        _hero.SetValue(number, unit, spaced: false);
        _condition.Text = WeatherReport.Describe(reading.Code);
        _hero.Icon.Glyph = LookGlyph(WeatherReport.Look(reading.Code));
        _highLow.Text = reading.Today is { } today ? WeatherReport.HighLow(today.High, today.Low) : string.Empty;
        _feels.Text = Str.WidgetsC_Weather_FeelsShort.Format(("temperature", WeatherReport.Temperature(reading.FeelsLike, Unit)));

        _facts = WeatherFacts.For(reading, Unit);

        // The age of the reading, and — when the last attempt to refresh it failed, or it was read
        // back from disk at launch and not yet replaced — in the caution colour. Said in the
        // tooltip as well, because a state carried only by a colour is one some readers never get.
        var failed = FetchState.IsFailure(held.Status);
        var stale = failed || held.Restored || FetchState.IsStale(held.At, now, _config.TickInterval);
        var age = FetchState.Freshness(held.At, now);

        _freshness.Text = age;
        _freshness.Foreground = stale ? _staleBrush : _freshBrush;

        var inline = InlineAge(age);
        ToolTipService.SetToolTip(
            _freshness,
            failed ? Str.WidgetsC_Weather_Tooltip_ReadingFailed.Format(("problem", FetchState.Message(held.Status)), ("age", inline))
            : held.Restored ? Str.WidgetsC_Weather_Tooltip_ReadingSaved.Format(("age", inline))
            : Str.WidgetsC_Weather_Tooltip_Updated.Format(("age", inline)));

        _built = null;
        Lay();
    }

    /// <summary>
    /// Climb the ladder for the current area: ask <see cref="WeatherNowLayout"/> which rungs are up,
    /// build them, then size the reading from what they leave.
    /// </summary>
    private void Lay()
    {
        if (_detached || _reading?.Value is not { } reading || !_area.IsMeasured)
        {
            return;
        }

        // [S-6160] The other styles draw the same reading in a panel of their own.
        if (_config.Style != WeatherNowStyle.Standard)
        {
            _built = null;
            LayStyled(reading);
            return;
        }

        ShowStyle(standard: true);
        var width = Math.Max(0, _area.Width - (2 * WeatherNowLayout.Inset));
        var hasToday = _highLow.Text.Length > 0;

        var plan = WeatherNowLayout.Plan(_area, HeroFloor(width), _facts.Count, _config.ShowDetails, hasToday);

        // A strip's readings column narrows the reading; measure the floor again for what is left.
        if (plan.Detail == WeatherDetailMode.Side)
        {
            plan = WeatherNowLayout.Plan(_area, HeroFloor(width - plan.SideWidth - Fluent.Space4), _facts.Count, _config.ShowDetails, hasToday);
        }

        if (plan != _built)
        {
            Build(plan);
            _built = plan;
        }

        var heroWidth = plan.Detail == WeatherDetailMode.Side ? width - plan.SideWidth - Fluent.Space4 : width;
        FitHero(heroWidth, plan.HeroRoom);
    }

    /// <summary>The reading at its smallest step, with the condition under it if it cannot sit beside.</summary>
    private double HeroFloor(double width)
    {
        var step = FluentType.Subtitle;
        var room = ShowsIcon(width, step) ? width - HeroLayout.IconSize(step) - HeroLayout.IconGap : width;
        var beside = ConditionFitsBeside(room, step, FluentType.Body.LineHeight);
        return step.LineHeight + (beside ? 0 : FluentType.Body.LineHeight);
    }

    /// <summary>The glyph is the first thing to go: "19°C" matters, the cloud beside it does not.</summary>
    private static bool ShowsIcon(double width, TypeStep step)
        => width >= HeroLayout.IconSize(step) + HeroLayout.IconGap + HeroLayout.ValueWidth(step, ReadingCharacters, UnitCharacters);

    /// <summary>
    /// [S-656] Whether the caption lines can stand beside the number: across, the number, the gap
    /// and the longest line fit; down, the lines are no taller than the number's own line.
    /// </summary>
    private bool ConditionFitsBeside(double room, TypeStep step, double linesHeight)
    {
        if (_condition.Text.Length == 0)
        {
            return true;
        }

        var longest = FluentType.EstimateWidth(FluentType.Body, _condition.Text.Length);
        var needed = HeroLayout.ValueWidth(step, ReadingCharacters, UnitCharacters) + HeroLayout.BesideGap + longest;
        return needed <= room && linesHeight <= step.LineHeight + Disclosure.Tolerance;
    }

    /// <summary>Build the rungs the plan has up, and hide the rest.</summary>
    private void Build(WeatherNowPlan plan)
    {
        Collapse(_feels, _cells, _side, _stack);
        _cells.Children.Clear();
        _cells.ColumnDefinitions.Clear();
        _cells.RowDefinitions.Clear();
        _side.Children.Clear();
        _side.ColumnDefinitions.Clear();
        _side.RowDefinitions.Clear();
        _stack.Children.Clear();

        _highLow.Visibility = plan.HighLow ? Visibility.Visible : Visibility.Collapsed;
        var facts = _facts.Take(plan.FactCount).ToList();

        switch (plan.Detail)
        {
            case WeatherDetailMode.Feels:
                // A caption line of the reading, under today's range: not a token stranded at the
                // foot of the tile, far from the number it qualifies.
                _feels.Visibility = Visibility.Visible;
                break;

            case WeatherDetailMode.Cells:
                BuildCells(facts, Math.Max(1, plan.FactColumns));
                _cells.Visibility = Visibility.Visible;
                break;

            case WeatherDetailMode.Side:
                BuildSide(facts, Math.Max(1, plan.FactColumns), plan.SideWidth);
                _side.Visibility = Visibility.Visible;
                break;

            case WeatherDetailMode.Stack:
                foreach (var fact in facts)
                {
                    _stack.Children.Add(WidgetParts.StatLine(FactGlyph(fact.Kind), fact.Label, fact.Value));
                }

                _stack.Visibility = Visibility.Visible;
                break;
        }
    }

    /// <summary>[S-668] The flat cells: an icon and caption label over a strong value, no boxes (S-07).</summary>
    private void BuildCells(List<WeatherFact> facts, int columns)
    {
        for (var c = 0; c < columns; c++)
        {
            _cells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rows = (facts.Count + columns - 1) / columns;

        for (var r = 0; r < rows; r++)
        {
            _cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < facts.Count; i++)
        {
            var fact = facts[i];
            var cell = WidgetParts.StatCell(FactGlyph(fact.Kind), fact.Label, fact.Value);
            SetColumn(cell, i % columns);
            SetRow(cell, i / columns);
            _cells.Children.Add(cell);
        }
    }

    /// <summary>[S-668] The readings beside the reading: stat lines in one or two columns.</summary>
    private void BuildSide(List<WeatherFact> facts, int columns, double width)
    {
        var columnWidth = (width - ((columns - 1) * Fluent.Space4)) / columns;
        var perColumn = (facts.Count + columns - 1) / columns;

        for (var c = 0; c < columns; c++)
        {
            _side.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWidth) });
        }

        for (var r = 0; r < perColumn; r++)
        {
            _side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < facts.Count; i++)
        {
            var line = WidgetParts.StatLine(FactGlyph(facts[i].Kind), facts[i].Label, facts[i].Value);
            SetColumn(line, i / perColumn);
            SetRow(line, i % perColumn);
            _side.Children.Add(line);
        }

        _side.Margin = new Thickness(Fluent.Space4, 0, 0, 0);
    }

    /// <summary>
    /// Step the temperature to the room the ladder left it: Subtitle up to Display, fitted against
    /// the widest number this widget prints rather than the one on screen, so "8°C" and "-18°C" do
    /// not pick different steps and make the number jump between two readings.
    /// </summary>
    /// <param name="width">The reading's width.</param>
    /// <param name="height">The height the plan left the reading (and its condition).</param>
    private void FitHero(double width, double height)
    {
        if (!(width > 0) || !(height > 0))
        {
            return;
        }

        var showIcon = ShowsIcon(width, FluentType.Subtitle);

        TypeStep Pick(double h)
        {
            var first = HeroLayout.Fit(width, h, ReadingCharacters, UnitCharacters, FluentType.Subtitle, FluentType.Display);
            var room = showIcon ? width - HeroLayout.IconSize(first) - HeroLayout.IconGap : width;
            var fitted = HeroLayout.Fit(room, h, ReadingCharacters, UnitCharacters, FluentType.Subtitle, FluentType.Display);

            // Past Display on a large tile; the condition's width is held back for it to sit beside.
            var aside = FluentType.EstimateWidth(FluentType.Body, _condition.Text.Length) + HeroLayout.BesideGap;
            return HeroLayout.Grow(fitted, room - aside, h, ReadingCharacters, UnitCharacters);
        }

        // The caption lines that stand with the reading: the sky, then today's range and the
        // "Feels" token when the ladder granted them their lines.
        var lines = (_condition.Text.Length > 0 ? FluentType.Body.LineHeight : 0)
            + (_highLow.Visibility == Visibility.Visible ? FluentType.Caption.LineHeight : 0)
            + (_feels.Visibility == Visibility.Visible ? FluentType.Caption.LineHeight : 0);

        var step = Pick(height);
        var readingRoom = showIcon ? width - HeroLayout.IconSize(step) - HeroLayout.IconGap : width;
        var beside = ConditionFitsBeside(readingRoom, step, lines);

        if (!beside)
        {
            step = Pick(height - FluentType.Body.LineHeight);
        }

        _hero.Lay(step, showIcon, trailingBeside: true, linesBeside: beside);
    }

    /// <summary>
    /// The age for use inside a sentence: "Just now" starts the header line but reads "just now"
    /// mid-sentence. Only the first letter changes, so a German noun later in the phrase keeps its
    /// capital; a language without case is untouched.
    /// </summary>
    internal static string InlineAge(string age)
        => age.Length == 0 ? age : char.ToLowerInvariant(age[0]) + age[1..];

    private static void Collapse(params UIElement[] parts)
    {
        foreach (var part in parts)
        {
            part.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>The glyph for a reading, checked against the font ([S-733]).</summary>
    private static string FactGlyph(WeatherFactKind kind) => kind switch
    {
        WeatherFactKind.FeelsLike => Glyphs.Thermometer,
        WeatherFactKind.Humidity or WeatherFactKind.Precipitation => Glyphs.Droplet,
        WeatherFactKind.Wind or WeatherFactKind.Gusts => Glyphs.Wind,
        WeatherFactKind.Pressure => Glyphs.Gauge,
        WeatherFactKind.Uv or WeatherFactKind.Sunrise => Glyphs.Sun,
        WeatherFactKind.Sunset => Glyphs.Moon,
        WeatherFactKind.CloudCover => Glyphs.Cloudy,
        _ => Glyphs.Info,
    };

    /// <summary>
    /// The Segoe glyph for a condition. Shared with the forecast, so a day reads the same in both.
    /// [S-652] One glyph per kind of weather.
    /// </summary>
    internal static string LookGlyph(WeatherLook look) => look switch
    {
        WeatherLook.Clear => Glyphs.Sun,
        WeatherLook.PartlyCloudy or WeatherLook.Overcast => Glyphs.Cloudy,
        WeatherLook.Fog => Glyphs.Fog,
        WeatherLook.Drizzle or WeatherLook.Rain => Glyphs.Rain,
        WeatherLook.Snow => Glyphs.Snow,
        WeatherLook.Thunderstorm => Glyphs.Thunderstorm,
        _ => Glyphs.Weather,
    };

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        if (_web is not null)
        {
            _web.Updated -= OnWebUpdated;
            _web = null;
        }

        _ticker = null;
    }

    /// <summary>[S-728] The city as the header title, or the widget's label when the user hid it.</summary>
    private void PublishTitle() => _surface?.SetHeaderTitle(_config.Title);
}
