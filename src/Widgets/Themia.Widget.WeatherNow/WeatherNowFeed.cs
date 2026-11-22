using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherNow;

/// <summary>
/// [S-15020] The weather summary family's one connection to its place's reading for an icon or text view: the sync's answer
/// through the SDK's <see cref="SkyFeed{TWant, T}"/>, at the weather's cadence ([S-2663]), the same answer the weather facts
/// draw.
/// </summary>
internal sealed class WeatherNowFeed
{
    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private WeatherNowPartConfig _config = new WeatherSkyConfig();

    public WeatherNowFeed(IWidgetHost host, Action changed)
    {
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Reading, host.Weather.Show),
            () => WeatherInterests.For(_config),
            changed);
    }

    /// <summary>The reading held, if any.</summary>
    public CurrentWeather? Reading => _feed.Held?.Value;

    /// <summary>Why there is no reading, in words ("Set a place", "Offline").</summary>
    public string Why => OnlineParts.Caption(_feed.Status);

    /// <summary>
    /// [S-7042] The status glyph's problems: the reading kept through a failed refresh; [S-15711] with no reading, the failure
    /// that left it so (never a place not set).
    /// </summary>
    public IReadOnlyList<string> Problems => _feed.Held is { Value: not null } held ? OnlineParts.Problems(held) : OnlineParts.Failure(_feed.Status);

    /// <summary>[S-730] Refresh, on the card menu while it has a place.</summary>
    public IReadOnlyList<WidgetAction> Actions
        => _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as WeatherNowPartConfig ?? new WeatherSkyConfig();
        _feed.Attach(ticker, _config.TickInterval);
        _feed.Reload();
    }

    public void Apply(WidgetConfig config)
    {
        _config = config as WeatherNowPartConfig ?? new WeatherSkyConfig();
        _feed.Cadence(_config.TickInterval);
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    /// <summary>[S-11850] Hold <paramref name="answer"/> at <paramref name="config"/>'s place (the sample place).</summary>
    public void Preview(WeatherNowPartConfig config, Fetched<CurrentWeather>? answer, FetchStatus status)
    {
        _config = config;
        _feed.Preview(answer, status);
    }
}
