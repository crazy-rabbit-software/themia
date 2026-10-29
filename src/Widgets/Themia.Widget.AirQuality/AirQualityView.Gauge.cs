using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6170] The gauge style: the index on the Chart's half ring (<see cref="ChartRing"/>, the meters'
/// gauge, [S-6302]), its arc to the index's place on the six equal bands
/// (<see cref="AirQualityRules.ScalePosition"/>) in the accent, or Caution and Critical for the worse
/// bands; the index in the bowl on its foot, the band's words centred under the ring (<see cref="AirLayout.Gauge"/>).
/// Built on first use; the number style's Readout is collapsed while it is shown, and the other way round.
/// </summary>
internal sealed partial class AirQualityView
{
    private ChartRing? _dial;
    private Grid? _gaugeBox;
    private ReadoutBlock? _gaugeReading;
    private TextBlock? _gaugeBand;
    private StackPanel? _gauge;

    private StackPanel Gauge()
    {
        if (_gauge is not null)
        {
            return _gauge;
        }

        _dial = new ChartRing(half: true);
        _gaugeReading = new ReadoutBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
        _gaugeBox = new Grid { Children = { _dial, _gaugeReading } };
        _gaugeBand = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
        _gaugeBand.HorizontalAlignment = HorizontalAlignment.Center;
        _gaugeBand.TextAlignment = TextAlignment.Center;
        _gauge = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { _gaugeBox, _gaugeBand } };
        Children.Add(_gauge);
        return _gauge;
    }

    /// <summary>[S-6170] Lay the reading in hand on the gauge, and the readings the plan has room for.</summary>
    private void LayGauge(AirShown reading, List<(string Glyph, string Label, string Value)> facts)
    {
        var gauge = Gauge();
        var plan = AirLayout.Gauge(_area, facts.Count);
        Columns(plan.Side, plan.Column);
        gauge.Visibility = Visibility.Visible;
        SetRowSpan(gauge, plan.Side ? 3 : 1);
        gauge.Margin = plan.Side ? new Thickness(FamilySpace.Inset, 0, 0, 0) : Family.SideInset;
        gauge.HorizontalAlignment = Aligned.Horizontal(_align);

        var dial = _dial!;
        var box = _gaugeBox!;
        var index = _gaugeReading!;
        var band = _gaugeBand!;
        Family.Step(band, FamilyType.Under(plan.Reading));
        band.Text = reading.Band;

        if (plan.Diameter > 0)
        {
            dial.Visibility = Visibility.Visible;
            dial.Show(reading.Position);
            dial.Role(reading.Arc);
            box.Width = plan.Diameter;
            box.Height = ChartRules.GaugeHeight(plan.Diameter);
            band.MaxWidth = plan.Diameter;
            band.Visibility = Visibility.Visible;
            index.HorizontalAlignment = HorizontalAlignment.Center;
            index.VerticalAlignment = VerticalAlignment.Bottom;
            index.Use(plan.Reading);
            index.Show(reading.Value, reading.Unit, valueRole: reading.Role);
        }
        else
        {
            // Too small a box for a ring: the index alone, its band as the line under it.
            dial.Visibility = Visibility.Collapsed;
            box.Width = double.NaN;
            box.Height = double.NaN;
            band.Visibility = Visibility.Collapsed;
            index.HorizontalAlignment = HorizontalAlignment.Left;
            index.VerticalAlignment = VerticalAlignment.Top;
            index.Use(plan.Reading);
            index.Show(reading.Value, reading.Unit, plan.Part > plan.Reading.LineHeight ? reading.Band : null, valueRole: reading.Role);
        }

        LayFacts(facts, plan.Facts, plan.Side);
    }

    /// <summary>Back to the number style: the gauge, if it was ever built, out of the way.</summary>
    private void HideGauge()
    {
        if (_gauge is not null)
        {
            _gauge.Visibility = Visibility.Collapsed;
        }
    }
}
