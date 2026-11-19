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
/// order, from the view's top (<see cref="FactList"/>). The reading comes from the background sync
/// through <see cref="SkyFeed{TWant, T}"/>, the same answer the reading card (<c>weatherNow</c>) draws.
/// A reading kept through a failed refresh stays and the status's problem glyph says so ([S-7042]);
/// with none, the rows stay with "--" and the status glyph's tooltip says why ([S-12813]).
/// </summary>
internal sealed partial class WeatherFactsView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions
{
    private readonly KitStatus _status;
    private readonly FactList _rows = new();
    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private WeatherFactsConfig _config = new();

    public WeatherFactsView(IWidgetHost host)
    {
        _status = new KitStatus(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Reading, host.Weather.Show),
            () => WeatherInterests.For(_config),
            Render);
        _rows.Visibility = Visibility.Collapsed;
        Children.Add(_rows);
        _status.Place(this);
    }

    /// <inheritdoc />
    public CardOpen? Opens => WeatherFactsModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

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
        _feed.Cadence(_config.TickInterval);
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    /// <summary>The picked readings, or the state to say without a reading.</summary>
    private void Render()
    {
        if (_feed.Held is not { Value: { } reading } held)
        {
            // [S-12813] The rows stay, "--", the reason their tooltip (a status glyph would stand over the first "--").
            _rows.Visibility = Visibility.Visible;
            _rows.Show([.. WeatherFactRules.Unread(Strings.Current, _config.Facts).Select(f => new FactLine(FactGlyph(f.Kind), f.Label, f.Value, Available: false))]);
            ToolTipService.SetToolTip(_rows, OnlineParts.Caption(_feed.Status) is { Length: > 0 } why ? why : null);
            _status.Clear();
            return;
        }

        _status.Show(OnlineParts.Problems(held), busy: false);
        ToolTipService.SetToolTip(_rows, null);
        var unit = _feed.Want?.Unit ?? RegionFormat.Current.Temperature;
        _rows.Visibility = Visibility.Visible;
        _rows.Show([.. WeatherFactRules.For(_config.Facts, reading, unit).Select(f => new FactLine(FactGlyph(f.Kind), f.Label, f.Value))]);
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
