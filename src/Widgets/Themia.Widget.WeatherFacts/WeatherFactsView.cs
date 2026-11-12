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
/// [S-11105] The weather's readings at the card's place as fact rows: exactly the picked facts, in their
/// order, from the view's top (<see cref="SkyFactList"/>). The reading comes from the background sync
/// through <see cref="SkyFeed{TWant, T}"/>, the same answer the reading card (<c>weatherNow</c>) draws.
/// A reading kept through a failed refresh stays and the header's problem glyph says so ([S-7042]);
/// with none, the Status row at the top says why ([S-6734]).
/// </summary>
internal sealed partial class WeatherFactsView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent, IWidgetContextActions
{
    private readonly KitHeader _header;
    private readonly SkyFactList _rows = new();
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };
    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private WeatherFactsConfig _config = new();

    public WeatherFactsView(IWidgetHost host)
    {
        _header = new KitHeader(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Reading, host.Weather.Show),
            () => WeatherInterests.For(_config),
            Render);
        _rows.Visibility = Visibility.Collapsed;
        Children.Add(_rows);
        Children.Add(_empty);
    }

    /// <inheritdoc />
    public CardOpen? Opens => WeatherFactsModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>The header's problem glyph ([S-7042]) and the place as its label ([S-2260]).</summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>[S-730] "Refresh": the network now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as WeatherFactsConfig ?? new WeatherFactsConfig();
        _feed.Attach(ticker, _config.TickInterval);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as WeatherFactsConfig ?? new WeatherFactsConfig();
        _header.SetAbout(_config.PlaceLabel);
        _feed.Cadence(_config.TickInterval);
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    /// <summary>The picked readings, or the state to say without a reading.</summary>
    private void Render()
    {
        if (_feed.Held is not { Value: { } reading } held)
        {
            OnlineParts.ShowState(_empty, _feed.Status, Glyphs.Weather, _feed.Refresh);
            _rows.Visibility = Visibility.Collapsed;
            _header.Clear();
            return;
        }

        _empty.Hide();
        _header.Show(OnlineParts.Problems(held), busy: false);
        var unit = _feed.Want?.Unit ?? RegionFormat.Current.Temperature;
        _rows.Visibility = Visibility.Visible;
        _rows.Show([.. WeatherFactRules.For(_config.Facts, reading, unit).Select(f => new SkyFact(FactGlyph(f.Kind), f.Label, f.Value))]);
    }

    /// <summary>The glyph for a reading, checked against the font ([S-733]).</summary>
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
