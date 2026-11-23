using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Sdk.Items;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.WeatherForecast;

namespace Themia.App.Widgets;

/// <summary>
/// The next few days, from open-meteo's daily block ([S-6163], [S-16974]): on the item base (<see cref="ItemList"/>) as a table
/// (what Rows was: the sky, the day, the range bar, "low high", aligned on every row), a row list (what Columns was), a grid, a
/// list or a column list, every fetched day laid at its one size and scrolled to by whole days ([S-16591], [S-16955]); or as
/// the temperature curve, the forecast's own chart (the Chart block).
/// <para>
/// [S-11145] The days are the card's in every style and at every size; the base and the curve's plan only share the box
/// among them: a bar or a chart longer.
/// </para>
/// <para>
/// The bar on each day is scaled against the whole forecast rather than that day's own range: a
/// week that runs 12° to 14° every day should draw seven short marks in the same place.
/// </para>
/// <para>
/// [S-16440] With no forecast the style's face stays (the days from today, "--", the unknown sky, every range bar its
/// track, the curve a flat line) and why is the Status block's one compact row on the card's top edge, the face right
/// under it; in a card one row tall the problem glyph in the first day's lead says it. [S-16441] A forecast kept through a
/// failed refresh keeps its rows the same as a fresh one's: in the rows style the problem glyph stands in the first day's
/// lead in place of its sky (no row gives up room for it); in the other styles it is the kit's status over the top end.
/// </para>
/// </summary>
internal sealed partial class WeatherForecastView : Grid, IWidgetView, IWidgetOpens,
    IWidgetSizeAware, IWidgetContextActions, IWidgetKeys, IWidgetGrounds
{
    /// <inheritdoc />
    public CardOpen? Opens => WeatherForecastModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    private const string Scope = "forecast";

    /// <summary>[S-7064] The status's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitStatus _mark;
    private readonly Grid _body = new();

    /// <summary>[S-16974] The days on the item base, in every style but the curve.</summary>
    private readonly ItemList _list;

    /// <summary>[S-16440] Without a forecast: why, the Status block's one row on the card's top edge, the face under it.</summary>
    private readonly StatusBlock _state = StatusBlock.Line();

    // [S-16440] Whether there is a state to say (the row shows where the card holds it over a day).
    private bool _stateWanted;

    // [S-16441] The problem lines a stale forecast's glyph says, or a failure's where no state's row shows (none: fresh).
    private IReadOnlyList<string> _problems = [];
    private IReadOnlyList<string> _failure = [];

    // [S-16441] Whether the face was last built under a state's row.
    private bool _laidStated;


    private WeatherForecastConfig _config = new();

    /// <summary>
    /// [S-11171] [S-2663] The forecast the background sync keeps, followed as every sky atom follows its answer: what it
    /// asks for (null without a place), the newest forecast (kept through a failure) or the state to say.
    /// </summary>
    private readonly SkyFeed<WeatherInterest, Forecast> _feed;

    /// <summary>[S-12813] Without a forecast: its days with nothing read, drawn in the style ("--").</summary>
    private IReadOnlyList<ForecastDay>? _unread;
    private WidgetArea _area;

    /// <summary>[S-16440] The box the face is laid in: the view's, less a state's row over it.</summary>
    private WidgetArea _laid;

    /// <summary>[S-16441] The problem lines the face says now (the rows' first lead, or the kit's status).</summary>
    private IReadOnlyList<string> _shownProblems = [];

    /// <summary>What is on screen: the style, its plan and the day the labels were computed for.</summary>
    private (ForecastStyle Style, object Plan, DateOnly Today, bool Precipitation)? _built;
    private bool _detached;

    public WeatherForecastView(IWidgetHost host)
    {
        _mark = new KitStatus(host);

        // [S-2663] The forecast comes with the reading: its answer is heard under the reading's key too.
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Forecast, host.Weather.Show)
            {
                Hears = (want, key) => key == host.Weather.Key(want) || key == host.Weather.Key(want with { ForecastDays = null }),
            },
            () => WeatherInterests.For(_config),
            Render);
        _days = new LinkLights<DateLink>(_links, LinkTopics.HoveredDate, LinkTopics.SelectedDate);
        _list = new ItemList(host)
        {
            Cells = ForecastItems.Cells,
            PartTitle = part => ForecastItems.PartName(part, Strings.Current),
            Decorate = (item, view) =>
            {
                if (DateOnly.TryParse(item.Id, System.Globalization.CultureInfo.InvariantCulture, out var day))
                {
                    LinkDay(view, day);
                }
            },
            Redrawing = () => _days.Clear(),
        };
        _list.GroundsChanged += () => GroundsChanged?.Invoke();

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _state.Visibility = Visibility.Collapsed;
        Children.Add(_state);
        SetRow(_body, 1);
        Children.Add(_body);
        _mark.Place(this);
    }

    /// <inheritdoc />
    public event Action? GroundsChanged;

    /// <summary>[S-16640] With the gap on, every day's box (under the state's row where it shows).</summary>
    public IReadOnlyList<Themia.Core.Geometry.DipRect>? Grounds
        => _body.Children.Contains(_list) && _list.ViewGrounds is { } grounds
            ? [.. grounds.Select(r => r with { Y = r.Y + _body.Margin.Top + (_state.Visibility == Visibility.Visible ? _state.ActualHeight : 0) })]
            : null;

    /// <summary>[S-16591] A click takes the keyboard where there are days to walk.</summary>
    public bool WantsKeys => _body.Children.Contains(_list) && _list.WantsKeys;

    /// <summary>[S-16591] [S-16955] The item base's keys over the days.</summary>
    public bool HandleKey(WidgetKey key) => _body.Children.Contains(_list) && _list.HandleKey(key);

    /// <summary>
    /// [S-730] Refresh. The style is chosen on the selection's page ([S-6007]).
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;

        // [S-1110] [S-2663] The forecast from the background sync and the texts in the app's language: the feed hears both.
        _feed.Attach(ticker, _config.TickInterval);
        _links.Join(_surface?.Links);

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as WeatherForecastConfig ?? new WeatherForecastConfig();
        _config = next;

        // [S-2663] Always through the sync: it asks nothing for a forecast already held and fresh. Another place is
        // another want, so the forecast in hand goes with it; the precipitation column or the style redraws what is held.
        _feed.Cadence(_config.TickInterval);
        _feed.Reload();
    }

    /// <summary>[S-663] The card's word on how much room the widget has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var held = _feed.Held;
        var days = held?.Value?.Days;

        if (days is null || days.Count == 0)
        {
            // [S-12813] [S-16440] The days stay in the style, "--" and the unknown sky (WR-10); why is the Status block's
            // compact row over them, and their tooltip (not set up is the card's Set up row, [S-1862]: no row).
            _unread = ForecastDefaults.Unread(DateOnly.FromDateTime(DateTime.Now), WeatherForecastConfig.MostDays);
            _body.Visibility = Visibility.Visible;
            _problems = [];
            OnlineParts.ShowState(_state, _feed.Status, Glyphs.Weather, _feed.Refresh);
            _stateWanted = _state.Visibility == Visibility.Visible;
            _failure = _stateWanted && OnlineParts.Caption(_feed.Status) is { Length: > 0 } said ? [said] : [];
            ToolTipService.SetToolTip(_body, OnlineParts.Caption(_feed.Status) is { Length: > 0 } why ? why : null);
            _built = null;
            Lay();
            return;
        }

        _unread = null;
        _state.Hide();
        _stateWanted = false;
        _failure = [];
        ToolTipService.SetToolTip(_body, null);
        _body.Visibility = Visibility.Visible;
        _problems = OnlineParts.Problems(held);

        // New data always rebuilds; a tick with the same data rebuilds only when the day changed.
        _built = null;
        Lay();
    }

    /// <summary>Pick the style's plan for the current area, and rebuild only if it changed.</summary>
    private void Lay()
    {
        if (_detached || (_feed.Held?.Value?.Days is { Count: > 0 } held ? held : _unread) is not { Count: > 0 } days || !_area.IsMeasured)
        {
            return;
        }

        // [S-16440] A state's row on the card's top edge: the face stands right under it, in the box it leaves.
        // Only where the card holds it over a day's row: a card one row tall keeps its face, why its tooltip (WR-10: never
        // the status line alone).
        var edge = LineRules.EdgeRow(_area.Grid());
        var stated = _stateWanted && _area.Height + FamilySpace.Inset >= edge + ItemSize.Optimal(FamilyShape.RowHeight(1), _area.Grid());
        _state.Visibility = stated ? Visibility.Visible : Visibility.Collapsed;
        var over = stated ? edge - FamilySpace.Inset : 0;
        var area = _area with { Height = Math.Max(1, _area.Height - over) };
        _body.Margin = new Thickness(0, stated ? FamilySpace.Inset : 0, 0, 0);

        var style = _config.Style;

        // [S-16441] What the problem glyph says: a stale forecast's lines, or a failure the state's row has no room for. In the
        // table and the lists it is the first day's lead; elsewhere the kit's status over the top end.
        var problems = ForecastDefaults.Problems(_problems, _failure, stated);
        _shownProblems = problems;
        var inLead = style is ForecastStyle.Table or ForecastStyle.List or ForecastStyle.ColumnList;
        _mark.Show(inLead ? [] : problems, busy: false);

        // One clock reading for the whole build, so a forecast drawn across midnight cannot label two different days "Today".
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (ForecastItems.Arrangement(style) is { } arrangement)
        {
            ShowItems(days, today, arrangement, area, inLead ? problems : []);
            return;
        }

        object plan = ForecastCurve.Plan(area, _config);

        var key = (style, plan, today, _config.ShowPrecipitation);

        // [S-16439] The same plan in another width still draws anew: the curve's plot runs to the card's right edge.
        if (_built is { } built && built.Style == key.style && Equals(built.Plan, key.plan)
            && built.Today == key.today && built.Precipitation == key.ShowPrecipitation && _laid.Width.Equals(area.Width) && _laidStated == stated)
        {
            return;
        }

        _laid = area;
        _laidStated = stated;

        ClearBody();
        _built = key;

        _body.Children.Add(BuildCurve(days, today, (ForecastCurvePlan)plan));
    }

    /// <summary>
    /// [S-16974] Every fetched day on the item base in <paramref name="arrangement"/>, its bars on the scale of every day laid
    /// (the ones scrolled to too, so a scroll never redraws them, [S-16591]); the problem glyph in the first day's lead where
    /// <paramref name="problems"/> say one ([S-16441]).
    /// </summary>
    private void ShowItems(IReadOnlyList<ForecastDay> days, DateOnly today, ItemStyle arrangement, WidgetArea area, IReadOnlyList<string> problems)
    {
        if (!_body.Children.Contains(_list))
        {
            ClearBody();
            _body.Children.Add(_list);
        }

        _laid = area;
        _laidStated = _state.Visibility == Visibility.Visible;
        _built = null;
        var style = _config.Style;
        var (coldest, warmest) = ForecastItems.Extremes(days);
        var locale = Strings.Current;
        var problem = problems.Count > 0 ? string.Join("\n", problems) : null;
        var items = days.Select((day, i) => ForecastItems.Item(day, style, today, coldest, warmest, _config.ShowPrecipitation, locale, i == 0 ? problem : null)).ToList();
        _list.Arrangement = arrangement;
        _list.Gap = _config.Gap;
        _list.Titles = _config.ColumnTitles;
        _list.Size(area);
        var hidden = ForecastItems.Hidden(style, _config.HiddenParts);
        _list.Show(items, new ItemDraw(hidden, ForecastItems.Shape(style, _config.HiddenParts, _config.ShowPrecipitation)), ItemOrder.Shown(ForecastItems.Parts, _config));
    }

    /// <summary>The same glyph the current-conditions widget draws for a condition. [S-652]</summary>
    private static string LookGlyph(WeatherLook look) => WeatherGlyph.Of(look);

    /// <summary>Drop the layout: the next one is built from the plan.</summary>
    private void ClearBody()
    {
        _body.Children.Clear();
        _days.Clear();
        _built = null;
    }

    public void Detach()
    {
        _detached = true;
        _links.Leave();

        ClearBody();
        _feed.Detach();
    }

}
