using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6170] The gauge style: the index on the meters' half-circle gauge (<see cref="GaugeDial"/>,
/// [S-6302]), filled to its place on the six equal bands (<see cref="AirQualityRules.ScalePosition"/>)
/// in the band's status colour, the index in the bowl and the band's words under it. Built on first
/// use; the number style's hero is collapsed while it is shown, and the other way round.
/// </summary>
internal sealed partial class AirQualityView
{
    private GaugeDial? _dial;
    private readonly Grid _gaugeBox = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel _gauge = new() { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly TextBlock _gaugeIndex = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _gaugeBand = Fluent.Text(FluentType.Body);

    private GaugeDial Dial()
    {
        if (_dial is not null)
        {
            return _dial;
        }

        _dial = new GaugeDial();
        _gaugeIndex.Foreground = ThemeBrush.Of("TextFillColorPrimaryBrush", Microsoft.UI.Colors.White);
        _gaugeBand.HorizontalAlignment = HorizontalAlignment.Center;
        _gaugeBand.TextAlignment = TextAlignment.Center;
        _gaugeBand.TextTrimming = TextTrimming.CharacterEllipsis;
        _gaugeBox.Children.Add(_dial);
        _gaugeBox.Children.Add(_gaugeIndex);
        _gauge.Children.Add(_gaugeBox);
        _gauge.Children.Add(_gaugeBand);
        _gauge.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.VerticalInset, OnlineLayouts.Inset, OnlineLayouts.VerticalInset);
        Children.Add(_gauge);
        return _dial;
    }

    /// <summary>[S-6170] Lay the reading in hand on the gauge, and the readings the plan has room for.</summary>
    private void LayGauge(List<(string Glyph, string Label, string Value)> facts, AirQualityScale scale, double index, Brush tone)
    {
        var dial = Dial();
        var plan = OnlineLayouts.AirGauge(_area, facts.Count);
        var geometry = GaugeLayout.Fit(plan.GaugeWidth, plan.GaugeHeight);

        _hero.Visibility = Visibility.Collapsed;
        _strip.Visibility = Visibility.Collapsed;
        _gauge.Visibility = Visibility.Visible;

        _gaugeIndex.Text = OnlineText.Index(index);
        _gaugeBand.Text = _band.Text;
        _gaugeBand.Foreground = tone;
        _gaugeBand.Visibility = plan.Band ? Visibility.Visible : Visibility.Collapsed;
        _gaugeBand.Margin = new Thickness(0, OnlineLayouts.Gap, 0, 0);

        // [S-6583] The gauge and its band where the widget's alignment puts them.
        _gaugeBox.HorizontalAlignment = Aligned.Horizontal(_align);
        _gaugeBand.HorizontalAlignment = Aligned.Horizontal(_align);
        _gaugeBand.TextAlignment = Aligned.Text(_align);

        if (geometry is { } g)
        {
            dial.Visibility = Visibility.Visible;
            dial.Lay(g);
            dial.SetFraction(AirQualityRules.ScalePosition(scale, index));
            dial.SetBrush(tone);
            _gaugeBox.Width = g.Width;
            _gaugeBox.Height = g.Height;

            // The index stands in the bowl, its line on the gauge's bottom edge.
            var step = GaugeLayout.Reading(g, new HeroBound(ValueCharacters, 0), FluentType.Body, FluentType.Display);
            _gaugeIndex.Apply(step);
        }
        else
        {
            // Too small a box for an arc: the index alone, as large as the box holds it.
            dial.Visibility = Visibility.Collapsed;
            _gaugeBox.Width = double.NaN;
            _gaugeBox.Height = double.NaN;
            _gaugeIndex.Apply(HeroLayout.Fit(plan.GaugeWidth, plan.GaugeHeight, ValueCharacters, 0, FluentType.Subtitle, FluentType.Display));
        }

        _facts.Children.Clear();
        _facts.ColumnDefinitions.Clear();
        _facts.RowDefinitions.Clear();
        _side.Children.Clear();
        _side.ColumnDefinitions.Clear();
        _side.RowDefinitions.Clear();
        _facts.Visibility = _side.Visibility = Visibility.Collapsed;
        _facts.RowSpacing = 0;

        var shown = facts.Take(plan.Count).ToList();
        switch (plan.Facts)
        {
            case AirFacts.Lines:
                Place(_facts, shown, 1, cells: false, columnWidth: null);
                _facts.Visibility = Visibility.Visible;
                break;

            case AirFacts.Side:
                Place(_side, shown, plan.Columns, cells: false, columnWidth: (plan.SideWidth - ((plan.Columns - 1) * Fluent.Space4)) / plan.Columns);
                _side.Visibility = Visibility.Visible;
                break;
        }

        var under = plan.Facts == AirFacts.Lines;
        _below.Visibility = under ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_gauge, 0);
        SetRowSpan(_gauge, under ? 1 : 2);
        _gauge.Margin = new Thickness(OnlineLayouts.Inset, OnlineLayouts.VerticalInset, plan.Facts == AirFacts.Side ? OnlineLayouts.SideGap : OnlineLayouts.Inset, under ? 0 : OnlineLayouts.VerticalInset);
        RowDefinitions[0].Height = under ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        RowDefinitions[1].Height = under ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
    }

    /// <summary>Back to the number style: the gauge, if it was ever built, out of the way.</summary>
    private void HideGauge() => _gauge.Visibility = Visibility.Collapsed;
}
