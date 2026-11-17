using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.AirQuality;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11130] The air quality index at a place, on the family's blocks: the index as the Readout on 28
/// with its unit, its band written out under it, in Caution or Critical for the worse bands ([S-5104]:
/// never green, the colour never the only carrier); or ([S-6170]) the index on the Chart's half ring,
/// its band under it. The pollutants and pollen are the air facts' atom. The answer comes through the
/// SDK's <see cref="SkyFeed{TWant, T}"/>, the same the air facts read.
/// </summary>
internal sealed partial class AirQualityView : Grid, IWidgetView, IWidgetContextActions, IWidgetAligned, IWidgetJoins
{
    /// <summary>[S-12481] The problem glyph ([S-7042]) over the content's top end.</summary>
    private readonly KitStatus _status;
    private readonly ReadoutBlock _hero = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-6170] The gauge: the half ring, the index in its bowl, the band under it.</summary>
    private readonly ChartRing _dial = new(half: true);
    private readonly ReadoutBlock _gaugeReading = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _gaugeBand = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly StackPanel _gauge;

    /// <summary>[S-6734] Without a reading: why, in one row at the top.</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };
    private readonly SkyFeed<AirInterest, AirReading> _feed;
    private AirQualityConfig _config = new();

    /// <summary>[S-6583] Where the index sits: from the start in the number style, centred on the gauge by default.</summary>
    private ContentAlign _align = ContentAlign.Left;

    public AirQualityView(IWidgetHost host)
    {
        _status = new KitStatus(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Air, host.Weather.Show),
            () => _config.Interest(),
            Render);

        // [S-12410] The readout (or the gauge) is one block, centred in the room with the foot's inset
        // counted with it ([S-12483]: against a side a group mate joins, the foot's inset gone there): at its natural box nothing moves, a larger box spaces it, never huddled.
        _hero.Margin = Family.SideInset with { Bottom = FamilySpace.Inset };
        _hero.Use(FamilyType.Big28);
        _hero.UnderText.TextTrimming = TextTrimming.CharacterEllipsis;
        _gaugeReading.Use(FamilyType.Big28);
        _gaugeBand.HorizontalAlignment = HorizontalAlignment.Center;
        _gaugeBand.TextAlignment = TextAlignment.Center;
        _gaugeBand.TextTrimming = TextTrimming.CharacterEllipsis;
        _gaugeBand.MaxWidth = AirLayout.Diameter;
        var box = new Grid { Width = AirLayout.Diameter, Height = ChartRules.GaugeHeight(AirLayout.Diameter), Children = { _dial, _gaugeReading } };
        _gauge = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = Family.SideInset with { Bottom = FamilySpace.Inset }, Children = { box, _gaugeBand } };

        _hero.Visibility = Visibility.Collapsed;
        _gauge.Visibility = Visibility.Collapsed;
        Children.Add(_hero);
        Children.Add(_gauge);
        Children.Add(_empty);
        _status.Place(this);
    }

    /// <summary>[S-12483] The block stands against a side a group mate joins, with no foot inset where one joins it.</summary>
    public void ApplyJoins(JoinedSides sides)
    {
        var edge = sides.Block switch
        {
            BlockEdge.Top => VerticalAlignment.Top,
            BlockEdge.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };
        _hero.VerticalAlignment = edge;
        _gauge.VerticalAlignment = edge;
        _hero.Margin = Family.SideInset with { Bottom = sides.Foot };
        _gauge.Margin = Family.SideInset with { Bottom = sides.Foot };
    }

    /// <summary>[S-730] Refresh: the network now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as AirQualityConfig ?? new AirQualityConfig();
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as AirQualityConfig ?? new AirQualityConfig();
        _feed.Reload();
    }

    /// <summary>[S-6583] The index (or the gauge) at the start, the middle or the end of the card.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _align = align;
        Render();
    }

    public void Detach() => _feed.Detach();

    private AirQualityScale InForce => AirQualityRules.Resolve(_config.Scale, _config.Latitude, _config.Longitude);

    private void Render()
    {
        var scale = InForce;
        if (_feed.Held is not { Value: { } air } held || AirQualityRules.Index(air, scale) is not { } index)
        {
            // [S-6734] Not set up is the card's Set up row; an answer without this index is one with nothing in it.
            OnlineParts.ShowState(_empty, _feed.Held is not null ? FetchStatus.Empty : _feed.Status, AirQualityModule.Glyph, _feed.Refresh);
            _hero.Visibility = _gauge.Visibility = Visibility.Collapsed;
            _status.Clear();
            return;
        }

        _empty.Hide();
        _status.Show(OnlineParts.Problems(held), busy: false);
        var band = AirQualityRules.Band(scale, index);
        var tone = AirQualityRules.Tone(band);
        var value = AirText.Index(index);
        var unit = " " + AirText.ScaleUnit(scale);
        // [S-12801] The band alone: the place is the group's Header widget's to say, once.
        var words = AirText.Band(scale, band);
        var role = AirQualityRules.ValueRole(tone);

        var gauge = _config.Style == AirQualityStyle.Gauge;
        _hero.Visibility = gauge ? Visibility.Collapsed : Visibility.Visible;
        _gauge.Visibility = gauge ? Visibility.Visible : Visibility.Collapsed;
        if (gauge)
        {
            _gauge.HorizontalAlignment = Aligned.Horizontal(_align);
            _dial.Show(AirQualityRules.ScalePosition(scale, index));
            _dial.Role(AirQualityRules.ArcRole(tone));
            _gaugeReading.Show(value, unit, valueRole: role);
            _gaugeBand.Text = words;
            return;
        }

        _hero.HorizontalAlignment = Aligned.Horizontal(_align);
        _hero.Show(value, unit, words, valueRole: role);
    }
}
