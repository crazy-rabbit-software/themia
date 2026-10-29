using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6161] [S-6162] The large-icon and minimal styles: the same reading, drawn by
/// <see cref="WeatherNowStyles"/>' plans in one row of their own at the view's top, which stands where the
/// widget's alignment puts it (centred unless chosen). Built on first use; a style change only re-lays it.
/// </summary>
internal sealed partial class WeatherNowView
{
    private FontIcon? _bigGlyph;
    private ReadoutBlock? _bigReading;
    private StackPanel? _styled;

    private StackPanel Styled()
    {
        if (_styled is not null)
        {
            return _styled;
        }

        _bigGlyph = Family.Glyph(Glyphs.Weather, FamilyShape.Icon, ColourRole.TextSecondary);
        _bigGlyph.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _bigReading = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Center };
        _styled = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Family.SideInset,
            Children = { _bigGlyph, _bigReading },
        };
        SetColumnSpan(_styled, 2);
        SetRowSpan(_styled, 3);
        Children.Add(_styled);
        return _styled;
    }

    private void HideStyled()
    {
        if (_styled is not null)
        {
            _styled.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>[S-6161] [S-6162] Lay the reading in hand for the large-icon or the minimal style.</summary>
    private void LayStyled()
    {
        var styled = Styled();
        styled.Visibility = Visibility.Visible;
        styled.HorizontalAlignment = Aligned.Horizontal(_align);
        ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = new GridLength(0);

        var glyph = _bigGlyph!;
        var reading = _bigReading!;
        if (_config.Style == WeatherNowStyle.Minimal)
        {
            // [S-6162] The temperature alone: no glyph, no condition, no readings.
            glyph.Visibility = Visibility.Collapsed;
            reading.Width = double.NaN;
            reading.Use(WeatherNowStyles.Minimal(_area));
            reading.Show(_number, _unit);
            return;
        }

        var plan = WeatherNowStyles.LargeIcon(_area);
        glyph.Visibility = plan.Glyph > 0 ? Visibility.Visible : Visibility.Collapsed;
        glyph.Glyph = _sky;
        if (plan.Glyph > 0)
        {
            glyph.FontSize = plan.Glyph;
            glyph.Width = plan.Glyph;
            glyph.Height = plan.Glyph;
        }

        // The temperature fits beside the glyph by the plan; a condition longer than the room left ends in an
        // ellipsis there, and the reading then takes that whole room, so the pair stays centred on it.
        var room = Math.Max(0, MeterPlan.Content(_area).Width - (plan.Glyph > 0 ? plan.Glyph + FamilySpace.GlyphGap : 0));
        reading.Use(plan.Temperature);
        reading.Show(_number, _unit, plan.Condition ? _condition : null);
        reading.Width = double.NaN;
        reading.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        reading.Width = reading.DesiredSize.Width > room ? room : double.NaN;
    }
}
