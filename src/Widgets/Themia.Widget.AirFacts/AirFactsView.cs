using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.AirFacts;

/// <summary>
/// [S-11135] The air's readings at the card's place as fact rows: exactly the picked rows, in their order,
/// from the content's top (<see cref="SkyFactList"/>), from the same air answer the index draws
/// (<see cref="SkyFeed{TWant, T}"/>). A reading kept through a failed refresh stays and the header's
/// problem glyph says so ([S-7042]); with none, the Status row says why ([S-6734]).
/// </summary>
internal sealed partial class AirFactsView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetContextActions
{
    private readonly KitHeader _header;
    private readonly SkyFactList _rows = new() { Visibility = Visibility.Collapsed };
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };
    private readonly SkyFeed<AirInterest, AirReading> _feed;
    private AirFactsConfig _config = new();

    public AirFactsView(IWidgetHost host)
    {
        _header = new KitHeader(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Air, host.Weather.Show),
            () => AirInterests.For(_config),
            Render);
        Children.Add(_rows);
        Children.Add(_empty);
    }

    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>[S-730] Refresh: the network now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as AirFactsConfig ?? new AirFactsConfig();
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as AirFactsConfig ?? new AirFactsConfig();
        _header.SetAbout(_config.PlaceLabel);
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    private void Render()
    {
        if (_feed.Held is not { Value: { } air } held)
        {
            OnlineParts.ShowState(_empty, _feed.Status, AirFactsModule.Glyph, _feed.Refresh);
            _rows.Visibility = Visibility.Collapsed;
            _header.Clear();
            return;
        }

        _empty.Hide();
        _header.Show(OnlineParts.Problems(held), busy: false);
        _rows.Visibility = Visibility.Visible;
        _rows.Show([.. AirFactRules.For(_config.Facts, air).Select(f => new SkyFact(FactGlyph(f.Kind), f.Label, f.Value))]);
    }

    /// <summary>A row's 16 lead, checked against the font ([S-733]).</summary>
    internal static string FactGlyph(AirFactKind kind) => kind switch
    {
        AirFactKind.Uv => Glyphs.Sun,
        AirFactKind.Pm25 or AirFactKind.Pm10 => Glyphs.Fog,
        AirFactKind.Ozone => Glyphs.Globe,
        _ => AirFactsModule.Glyph,
    };
}
