using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13901] One weather reading at the card's place as its one fact row (<see cref="FactList"/>, the row every facts
/// widget draws, grown to the card, [S-12807]). The reading comes from the background sync through
/// <see cref="SkyFeed{TWant, T}"/>, the same answer the reading card (<c>weatherNow</c>) draws. A reading kept
/// through a failed refresh stays and the status's problem glyph says so ([S-7042]); with none, the row stays with
/// "--" and its tooltip says why ([S-13902]).
/// </summary>
internal sealed partial class WeatherFactView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions
{
    private readonly WeatherFactKind _fact;
    private readonly KitStatus _status;
    private readonly FactList _row = new();
    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private WeatherFactConfig _config;

    public WeatherFactView(IWidgetHost host, WeatherFactKind fact)
    {
        _fact = fact;
        _config = WeatherFactKinds.Of(fact).Create();
        _status = new KitStatus(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Reading, host.Weather.Show),
            () => WeatherInterests.For(_config),
            Render);
        _row.Visibility = Visibility.Collapsed;
        Children.Add(_row);
        _status.Place(this);
    }

    /// <inheritdoc />
    public CardOpen? Opens => WeatherFactModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>[S-730] "Refresh": the network now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = Own(config);
        _feed.Attach(ticker, _config.TickInterval);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = Own(config);
        _feed.Cadence(_config.TickInterval);
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    /// <summary>The card's config, or its kind's default for one that is not (never another fact's).</summary>
    private WeatherFactConfig Own(WidgetConfig config)
        => config is WeatherFactConfig own && own.Fact == _fact ? own : WeatherFactKinds.Of(_fact).Create();

    /// <summary>The reading, or the row's default without one.</summary>
    private void Render()
    {
        _row.Visibility = Visibility.Visible;
        if (_feed.Held is not { Value: { } reading } held)
        {
            // [S-13902] The row stays, "--", the reason its tooltip (a status glyph would stand over the "--").
            var unread = WeatherFactRules.Unread(Strings.Current, _fact);
            _row.Show([new FactLine(FactGlyph(_fact), unread.Label, unread.Value, Available: false)]);
            ToolTipService.SetToolTip(_row, OnlineParts.Caption(_feed.Status) is { Length: > 0 } why ? why : null);
            _status.Clear();
            return;
        }

        _status.Show(OnlineParts.Problems(held), busy: false);
        ToolTipService.SetToolTip(_row, null);
        var fact = WeatherFactRules.For(Strings.Current, _fact, reading, _feed.Want?.Unit ?? RegionFormat.Current.Temperature);
        _row.Show([new FactLine(FactGlyph(_fact), fact.Label, fact.Value)]);
    }

    /// <summary>The glyph for a reading, checked against the font ([S-733]): its row's lead and its kind's.</summary>
    internal static string FactGlyph(WeatherFactKind kind) => kind switch
    {
        WeatherFactKind.Today => Glyphs.Calendar,
        WeatherFactKind.FeelsLike => Glyphs.Thermometer,
        WeatherFactKind.Humidity or WeatherFactKind.Precipitation => Glyphs.Droplet,
        WeatherFactKind.Wind or WeatherFactKind.Gusts => Glyphs.Wind,
        WeatherFactKind.Pressure => Glyphs.Gauge,
        WeatherFactKind.Uv or WeatherFactKind.Sunrise => Glyphs.Sun,
        WeatherFactKind.Sunset => Glyphs.Moon,
        WeatherFactKind.CloudCover => Glyphs.Cloudy,
        _ => Glyphs.Info,
    };
}
