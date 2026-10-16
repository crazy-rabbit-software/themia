using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Run = Microsoft.UI.Xaml.Documents.Run;
using Typography = Microsoft.UI.Xaml.Documents.Typography;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6161] [S-6162] The large-icon and minimal styles: the same reading, drawn by
/// <see cref="WeatherNowStyles"/>' plans in one panel of their own beside the standard group, which
/// is collapsed while either is shown. Built once; a style change only re-lays them.
/// </summary>
internal sealed partial class WeatherNowView
{
    private readonly Grid _styled = new() { Visibility = Visibility.Collapsed };
    private readonly FontIcon _bigIcon = Fluent.Icon(Glyphs.Weather, "TextFillColorSecondaryBrush", WeatherNowStyles.IconMin);
    private readonly TextBlock _bigTemperature = new() { TextWrapping = TextWrapping.NoWrap, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
    private readonly Run _bigValue = new();
    private readonly Run _bigUnit = new();
    private readonly TextBlock _bigCondition = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _bigRange = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _bigText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _bigStack = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private bool _styledBuilt;

    private void BuildStyled()
    {
        if (_styledBuilt)
        {
            return;
        }

        _styledBuilt = true;
        _bigTemperature.Foreground = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
        _bigUnit.Foreground = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
        _bigTemperature.Inlines.Add(_bigValue);
        _bigTemperature.Inlines.Add(_bigUnit);
        Typography.SetNumeralAlignment(_bigTemperature, FontNumeralAlignment.Tabular);
        _bigCondition.TextTrimming = TextTrimming.CharacterEllipsis;
        _bigRange.TextTrimming = TextTrimming.CharacterEllipsis;

        _bigText.Children.Add(_bigTemperature);
        _bigText.Children.Add(_bigCondition);
        _bigText.Children.Add(_bigRange);
        _bigStack.Children.Add(_bigIcon);
        _bigStack.Children.Add(_bigText);

        _styled.Margin = new Thickness(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset, WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        _styled.Children.Add(_bigStack);
        Children.Add(_styled);
    }

    /// <summary>Show the standard group or the styled panel, as the style says.</summary>
    private void ShowStyle(bool standard)
    {
        _group.Visibility = standard ? Visibility.Visible : Visibility.Collapsed;
        _styled.Visibility = standard ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>[S-6161] [S-6162] Lay the reading in hand for the large-icon or the minimal style.</summary>
    private void LayStyled(CurrentWeather reading)
    {
        BuildStyled();
        ShowStyle(standard: false);

        var (number, unit) = WeatherReport.TemperatureParts(reading.Temperature, Unit);
        _bigValue.Text = number;
        _bigUnit.Text = unit;
        _bigCondition.Text = _condition.Text;
        _bigRange.Text = _highLow.Text;
        _bigIcon.Glyph = _hero.Icon.Glyph;

        TypeStep step;
        if (_config.Style == WeatherNowStyle.Minimal)
        {
            // [S-6162] The temperature alone, light: no glyph, no condition, no range, no readings.
            step = WeatherNowStyles.Minimal(_area);
            _bigIcon.Visibility = _bigCondition.Visibility = _bigRange.Visibility = Visibility.Collapsed;
            _bigStack.Orientation = Orientation.Vertical;
            _bigValue.FontWeight = FontWeights.Light;
            _bigUnit.FontWeight = FontWeights.Light;
            _bigText.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            var plan = WeatherNowStyles.LargeIcon(_area, _highLow.Text.Length > 0, _bigCondition.Text.Length);
            step = plan.Temperature;
            _bigIcon.Visibility = Visibility.Visible;
            _bigIcon.FontSize = plan.IconSize;
            _bigIcon.Margin = plan.Stacked
                ? new Thickness(0, 0, 0, WeatherNowLayout.Gap)
                : new Thickness(0, 0, WeatherNowStyles.IconGap, 0);
            _bigStack.Orientation = plan.Stacked ? Orientation.Vertical : Orientation.Horizontal;
            _bigText.HorizontalAlignment = plan.Stacked ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            _bigCondition.Visibility = plan.Condition && _bigCondition.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            _bigRange.Visibility = plan.HighLow ? Visibility.Visible : Visibility.Collapsed;
            _bigCondition.TextAlignment = _bigRange.TextAlignment = plan.Stacked ? TextAlignment.Center : TextAlignment.Left;
            _bigValue.FontWeight = step.SemiBold ? FontWeights.SemiBold : FontWeights.Normal;
            _bigUnit.FontWeight = FontWeights.Normal;
        }

        _bigTemperature.LineHeight = step.LineHeight;
        _bigValue.FontSize = step.FontSize;
        _bigUnit.FontSize = HeroLayout.UnitStep(step).FontSize;
        _bigTemperature.TextAlignment = _bigText.HorizontalAlignment == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Left;
    }
}
