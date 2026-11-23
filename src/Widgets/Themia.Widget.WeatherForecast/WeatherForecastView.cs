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
using Themia.Widget.WeatherForecast;

namespace Themia.App.Widgets;

/// <summary>
/// The next few days, from open-meteo's daily block, in one of four styles ([S-6163]), drawn from the
/// family's blocks: the days as rows (the Row block with its range bar), side by side as columns, the
/// highs and lows as a temperature curve (the Chart block), or as cells in a grid shaped to the tile
/// ([S-6171]).
/// <para>
/// [S-11145] The days are the config's in every style and at every size; each style's plan
/// (<see cref="WeatherForecastLayout"/>, <see cref="ForecastCurve"/>, <see cref="ForecastGrid"/>) only
/// shares the box among them: a bar or a chart longer, a column wider.
/// </para>
/// <para>
/// The bar on each day is scaled against the whole forecast rather than that day's own range: a
/// week that runs 12° to 14° every day should draw seven short marks in the same place.
/// </para>
/// <para>
/// [S-16440] With no forecast the style's face stays (the days from today, "--", the unknown sky, every range bar its
/// track, the curve a flat line) and why is the Status block's one compact row on the card's top edge, the face right
/// under it. [S-16441] A forecast kept through a failed refresh keeps its rows alike: the problem glyph stands in a room
/// every row leaves at its end, centred on the first row.
/// </para>
/// </summary>
internal sealed partial class WeatherForecastView : Grid, IWidgetView, IWidgetOpens,
    IWidgetSizeAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => WeatherForecastModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    private const string Scope = "forecast";

    /// <summary>[S-7064] The status's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitStatus _mark;
    private readonly Grid _body = new();

    /// <summary>[S-16440] Without a forecast: why, the Status block's one row on the card's top edge, the face under it.</summary>
    private readonly StatusBlock _state = StatusBlock.Line();

    // [S-16440] Whether there is a state to say (the row shows where the card holds it over a day).
    private bool _stateWanted;


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

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _state.Visibility = Visibility.Collapsed;
        Children.Add(_state);
        SetRow(_body, 1);
        Children.Add(_body);
        _mark.Place(this);
    }

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
            _mark.Clear();
            OnlineParts.ShowState(_state, _feed.Status, Glyphs.Weather, _feed.Refresh);
            _stateWanted = _state.Visibility == Visibility.Visible;
            ToolTipService.SetToolTip(_body, OnlineParts.Caption(_feed.Status) is { Length: > 0 } why ? why : null);
            _built = null;
            Lay();
            return;
        }

        _unread = null;
        _state.Hide();
        _stateWanted = false;
        ToolTipService.SetToolTip(_body, null);
        _body.Visibility = Visibility.Visible;
        _mark.Show(OnlineParts.Problems(held), busy: false);

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
        object plan = style switch
        {
            ForecastStyle.Columns => WeatherForecastLayout.Columns(area, _config),
            ForecastStyle.Curve => ForecastCurve.Plan(area, _config),
            ForecastStyle.Grid => ForecastGrid.Plan(area, _config),
            _ => WeatherForecastLayout.Rows(area, _config),
        };

        // [S-16441] The problem glyph centred on the first row (in the room every row leaves at its end); elsewhere where
        // the kit puts it.
        _mark.StandAt(plan is ForecastRowsPlan rowsPlan && !stated
            ? (ItemSize.Optimal(FamilyShape.RowHeight(rowsPlan.Lines), _area.Grid()) - KitStatus.Side) / 2 - FamilySpace.Inset
            : null);

        // One clock reading for the whole build, so a forecast drawn across midnight cannot label
        // two different days "Today".
        var today = DateOnly.FromDateTime(DateTime.Now);
        var key = (style, plan, today, _config.ShowPrecipitation);

        if (_built is { } built && built.Style == key.style && Equals(built.Plan, key.plan)
            && built.Today == key.today && built.Precipitation == key.ShowPrecipitation)
        {
            return;
        }

        _laid = area;

        ClearBody();
        _built = key;

        // The bars scale against the days shown, not the ones left out for room.
        var shown = plan switch
        {
            ForecastRowsPlan r => r.Days,
            ForecastColumnsPlan c => c.Days,
            _ => days.Count,
        };
        var (coldest, warmest) = Extremes([.. days.Take(shown)]);
        var range = new Range(coldest, warmest);

        _body.Children.Add(plan switch
        {
            ForecastColumnsPlan columns => BuildColumns(days, today, range, columns),
            ForecastCurvePlan curve => BuildCurve(days, today, curve),
            ForecastGridPlan cells => BuildGrid(days, today, cells),
            ForecastRowsPlan rows => BuildRows(days, today, range, rows),
            _ => new Grid(),
        });
    }

    /// <summary>The coldest low and warmest high across the forecast, which every bar scales against.</summary>
    private readonly record struct Range(double Coldest, double Warmest);

    private static (double Coldest, double Warmest) Extremes(IReadOnlyList<ForecastDay> days)
    {
        var coldest = double.MaxValue;
        var warmest = double.MinValue;

        foreach (var day in days)
        {
            foreach (var value in new[] { day.Low, day.High })
            {
                if (value is { } v && double.IsFinite(v))
                {
                    coldest = Math.Min(coldest, v);
                    warmest = Math.Max(warmest, v);
                }
            }
        }

        // Every day missing both numbers: the bar spans the row, which RangeBar does for no span.
        return coldest <= warmest ? (coldest, warmest) : (0, 1);
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
