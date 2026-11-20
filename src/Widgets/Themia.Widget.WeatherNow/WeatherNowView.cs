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
/// [S-7042] A reading kept through a failed refresh stays on screen and the status's problem glyph says
/// so ([S-12481]); with no reading at all the Status block's one row at the top says why ([S-6734]). The
/// place stands on the condition's line ("Overcast · Zürich", [S-12481]); [S-12483] the same joined or alone.
/// </para>
/// </summary>
internal sealed partial class WeatherNowView : Grid, IWidgetView, IWidgetOpens,
    IWidgetContextActions, IWidgetAligned
{
    /// <inheritdoc />
    public CardOpen? Opens => WeatherNowModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>[S-12481] The problem glyph ([S-7042]) over the content's top end.</summary>
    private readonly KitStatus _status;

    private readonly FontIcon _bigGlyph = Family.Glyph(Glyphs.Weather, WeatherNowStyles.LargeGlyph, ColourRole.TextSecondary);
    private readonly ReadoutBlock _reading = new();
    private readonly Grid _face;

    /// <summary>[S-12808] The block, planned at the natural box and scaled up as one.</summary>
    private readonly ReadingFit _fit = new();

    /// <summary>[S-6734] Without a reading: why, in one row at the top.</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };

    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private WeatherNowConfig _config = new();

    /// <summary>[S-6583] Where the reading sits: the widget's alignment.</summary>
    private ContentAlign _align = ContentAlign.Left;

    public WeatherNowView(IWidgetHost host)
    {
        _status = new KitStatus(host);
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
        // [S-12401] [S-12808] The reading is the content's one block, its value and condition together, scaled up
        // as one in a larger box (the sides and the foot keep the inset, the fit's margin), joined or alone ([S-12483]).
        _face = new Grid
        {
            Visibility = Visibility.Collapsed,
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
            Children = { _bigGlyph, _reading },
        };
        SetColumn(_reading, 1);
        _fit.Parts.Add(_face);
        Children.Add(_fit);
        Children.Add(_empty);
        _status.Place(this);
    }

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

        // [S-12833] Its parts: in a smaller card the condition, the unit and the glyph give way, the temperature stays.
        _fit.Face = WeatherNowStyles.Face(_config);
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
            // [S-12813] The reading stays: "--", the reason its caption.
            _empty.Hide();
            _bigGlyph.Visibility = Visibility.Collapsed;
            OnlineParts.ShowDefault(_reading, _feed.Status, "°");
            _face.Visibility = Visibility.Visible;
            _status.Clear();
            return;
        }

        _empty.Hide();
        _status.Show(OnlineParts.Problems(held), busy: false);
        var unit = _feed.Want?.Unit ?? RegionFormat.Current.Temperature;
        var (number, degrees) = WeatherReport.TemperatureParts(reading.Temperature, unit);
        // [S-12801] The condition alone: the place is the group's Header widget's to say, once.
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
}
