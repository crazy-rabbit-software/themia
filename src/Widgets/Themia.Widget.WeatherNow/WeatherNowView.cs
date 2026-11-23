using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
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
/// so ([S-12481]), the block leaving it room at its end where it would stand under it, the glyph giving way to the face's
/// tooltip where the card is too small for both ([S-16433]). With no reading the face stays, its sky unknown and its
/// reading "--", why its line under, the one block scaled as the sample's is ([S-16447]). [S-12483] The same joined or
/// alone. The card is one item, lit under the pointer while a click opens MSN Weather ([S-16434]).
/// </para>
/// </summary>
internal sealed partial class WeatherNowView : Grid, IWidgetView, IWidgetOpens,
    IWidgetContextActions, IWidgetAligned, IWidgetSizeAware, IWidgetSurfaceAware
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

    /// <summary>[S-16434] The card lit under the pointer while a click opens something (WR-13).</summary>
    private readonly OpensLight _opensLight;
    private IWidgetSurface? _surface;
    private WidgetArea _area;

    // [S-16447] Why there is no reading (null: a reading, or nothing to say: not set up is the card's Set up row).
    private string? _whyText;

    // [S-16433] The problem lines of a reading kept through a failed refresh (none: fresh, or no reading).
    private IReadOnlyList<string> _problems = [];

    private readonly SkyFeed<WeatherInterest, CurrentWeather> _feed;
    private WeatherNowConfig _config = new();

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

        _status.Place(this);
        _opensLight = new OpensLight(this, () => _surface?.EditMode != true && Opens is not null);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    /// <summary>[S-16433] The card's box: the status mark's room in it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>[S-730] "Refresh": the network now, past both the back-off and the cache.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as WeatherNowConfig ?? new WeatherNowConfig();
        _feed.Attach(ticker, _config.TickInterval);
        _opensLight.Attach();
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
    public void ApplyAlign(ContentAlign align) => _fit.Align = align;

    public void Detach()
    {
        _opensLight.Detach();
        _feed.Detach();
    }

    /// <summary>Draw the reading in hand in the style's look; with none, say which state it is ([S-6734]).</summary>
    private void Render()
    {
        if (_feed.Held is not { Value: { } reading } held)
        {
            // [S-12813] [S-16447] The face stays (WR-10) and fills its card as the sample's does: the sky unknown in the
            // style's place, "--" held to the widest reading's box so it stands as large as a reading would, why its line
            // under, the reading and its line one block scaled as one (WR-7: "Keep them together").
            var why = OnlineParts.Caption(_feed.Status);
            _whyText = why.Length > 0 ? why : null;
            _problems = [];
            var face = WeatherNowStyles.Unread(_config.Style);
            _face.Visibility = Visibility.Visible;
            _bigGlyph.Visibility = face.BigGlyph ? Visibility.Visible : Visibility.Collapsed;
            _bigGlyph.Glyph = Glyphs.Weather;
            _bigGlyph.Foreground = Family.Brush(ColourRole.TextDisabled);
            _reading.Hold(face.Held.Value, face.Held.Unit);
            _reading.Show("--", "°", _whyText, lead: face.Lead ? Glyphs.Weather : null, valueRole: ColourRole.TextDisabled);
            Lay();
            return;
        }

        _whyText = null;
        _problems = OnlineParts.Problems(held);
        _reading.Hold(string.Empty);
        _bigGlyph.Foreground = Family.Brush(ColourRole.TextSecondary);
        var unit = _feed.Want?.Unit ?? RegionFormat.Current.Temperature;
        var (number, degrees) = WeatherReport.TemperatureParts(reading.Temperature, unit);
        // [S-12801] The condition alone: the place is the group's Header widget's to say, once.
        var condition = WeatherReport.Describe(reading.Code);
        var sky = WeatherGlyph.Of(WeatherReport.Look(reading.Code));

        _face.Visibility = Visibility.Visible;
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

        Lay();
    }

    /// <summary>
    /// [S-16433] Lay the face in the card: the status mark over the top end corner, the block leaving it room at its end where
    /// it would stand under it, or giving way to the face's tooltip where the card is too small for both. [S-16447] Why there
    /// is no reading is the face's tooltip too (its line under gives way in the smallest cards).
    /// </summary>
    private void Lay()
    {
        var width = _area.IsMeasured ? _area.Width - (2 * FamilySpace.Inset) : double.NaN;
        var share = _area.IsMeasured ? _area.Height - FamilySpace.Inset : double.NaN;

        // The status shows the problems first: its room is its width as shown.
        _status.Show(_problems, busy: false);
        var place = MarkPlace.Clear;
        if (_problems.Count > 0 && _area.IsMeasured)
        {
            _face.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var block = new SizeDip(_face.DesiredSize.Width, _face.DesiredSize.Height);
            place = WeatherNowStyles.Mark(width, share, block, WeatherNowStyles.Face(_config).LeastBox(), _status.Room, FamilySpace.S1 + KitStatus.Side);
        }

        if (place == MarkPlace.Yields)
        {
            _status.Clear();
        }

        var tip = _whyText ?? (place == MarkPlace.Yields ? string.Join("\n", _problems) : null);
        ToolTipService.SetToolTip(this, tip);
        _fit.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset + (place == MarkPlace.Beside ? _status.Room : 0), FamilySpace.Inset);
    }
}
