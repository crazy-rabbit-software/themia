using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.WeatherNow;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11100] The weather's reading where the user says they are, from open-meteo, on the family's
/// blocks: the temperature as the Readout on 28, the condition the line under it, the sky's glyph
/// leading it (Standard) or beside it at 48 (Large icon), or the temperature alone (Minimal); the
/// sizes are <see cref="WeatherNowStyles"/>'. The readings are the weather facts card's, the days the
/// forecast's: never drawn here, at any size.
/// <para>
/// [S-7042] A reading kept through a failed refresh stays on screen and the header's problem glyph says
/// so; with no reading at all the Status block's one row at the top says why ([S-6734]).
/// </para>
/// </summary>
internal sealed partial class WeatherNowView : Grid, IWidgetView, IWidgetOpens, IWidgetHeaderContent,
    IWidgetContextActions, IWidgetAligned
{
    /// <inheritdoc />
    public CardOpen? Opens => WeatherNowModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>[S-7064] The header's problem glyph ([S-7042]) and the place as its label ([S-2260]).</summary>
    private readonly KitHeader _header;

    private readonly FontIcon _bigGlyph = Family.Glyph(Glyphs.Weather, WeatherNowStyles.LargeGlyph, ColourRole.TextSecondary);
    private readonly ReadoutBlock _reading = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _face;

    /// <summary>[S-6734] Without a reading: why, in one row at the top.</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };

    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private WeatherNowConfig _config = new();

    /// <summary>[S-6583] Where the reading sits: the widget's alignment.</summary>
    private ContentAlign _align = ContentAlign.Left;

    public WeatherNowView(IWidgetHost host)
    {
        _header = new KitHeader(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Reading, host.Weather.Show),
            () => WeatherInterests.For(_config),
            Render);

        _bigGlyph.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _bigGlyph.Width = WeatherNowStyles.LargeGlyph;
        _bigGlyph.Height = WeatherNowStyles.LargeGlyph;
        _reading.Use(FamilyType.Big28);

        // A condition longer than the card ends in its ellipsis: the reading's column takes what the glyph leaves.
        _reading.UnderText.TextTrimming = TextTrimming.CharacterEllipsis;
        _face = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Family.SideInset,
            Visibility = Visibility.Collapsed,
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
            Children = { _bigGlyph, _reading },
        };
        SetColumn(_reading, 1);
        Children.Add(_face);
        Children.Add(_empty);
    }

    /// <summary>The header's problem glyph. Built in the constructor because the card reads this once, when it builds that header.</summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>[S-730] "Refresh": the network now, past both the back-off and the cache.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as WeatherNowConfig ?? new WeatherNowConfig();
        _feed.Attach(ticker, _config.TickInterval);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as WeatherNowConfig ?? new WeatherNowConfig();
        PublishPlace();
        _feed.Cadence(_config.TickInterval);
        _feed.Reload();
    }

    /// <summary>[S-6583] The reading at the start, the middle or the end of the card.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _align = align;
        _face.HorizontalAlignment = Aligned.Horizontal(_align);
    }

    public void Detach() => _feed.Detach();

    /// <summary>Draw the reading in hand in the style's look; with none, say which state it is ([S-6734]).</summary>
    private void Render()
    {
        if (_feed.Held is not { Value: { } reading } held)
        {
            OnlineParts.ShowState(_empty, _feed.Status, Glyphs.Weather, _feed.Refresh);
            _face.Visibility = Visibility.Collapsed;
            _header.Clear();
            return;
        }

        _empty.Hide();
        _header.Show(OnlineParts.Problems(held), busy: false);
        var unit = _feed.Want?.Unit ?? RegionFormat.Current.Temperature;
        var (number, degrees) = WeatherReport.TemperatureParts(reading.Temperature, unit);
        var condition = WeatherReport.Describe(reading.Code);
        var sky = WeatherGlyph.Of(WeatherReport.Look(reading.Code));

        _face.Visibility = Visibility.Visible;
        _face.HorizontalAlignment = Aligned.Horizontal(_align);
        _bigGlyph.Visibility = _config.Style == WeatherNowStyle.LargeIcon ? Visibility.Visible : Visibility.Collapsed;
        _bigGlyph.Glyph = sky;
        switch (_config.Style)
        {
            case WeatherNowStyle.Minimal:
                _reading.Show(number, degrees);
                break;
            case WeatherNowStyle.LargeIcon:
                _reading.Show(number, degrees, condition);
                break;
            default:
                _reading.Show(number, degrees, condition, lead: sky);
                break;
        }
    }

    /// <summary>[S-2260] The place is the header's label beside the widget's name; no place, no label ([S-2848]).</summary>
    private void PublishPlace() => _header.SetAbout(_config.PlaceLabel);
}
