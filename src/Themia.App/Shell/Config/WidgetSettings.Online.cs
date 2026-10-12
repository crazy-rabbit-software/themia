using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-5108] [S-5127] [S-5148] The air quality, rain and exchange-rate pages: only what changes what
/// the widget shows. The cadence is every widget's (Settings › Widgets › Updates, [S-3780]).
/// </summary>
internal static partial class WidgetSettings
{
    /// <summary>[S-5108] The place (the weather widgets' picker), the location name, the index.</summary>
    public static void AirQuality(List<FrameworkElement> rows, AirQualityConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(Str.Online_Air_Label.Text()));
        AddWeatherDisplay(rows, config, save);

        (AirQualityScale, string)[] scales =
        [
            (AirQualityScale.Auto, Str.Online_Air_Settings_Index_Auto.Text()),
            (AirQualityScale.European, Str.Online_Air_Settings_Index_European.Text()),
            (AirQualityScale.Us, Str.Online_Air_Settings_Index_Us.Text()),
        ];

        rows.Add(FluentLayout.Row(Glyphs.AirQuality, Str.Online_Air_Settings_Index_Label.Text(), Str.Online_Air_Settings_Index_Desc.Text(),
            FluentLayout.Choice<AirQualityScale>(scales, config.Scale, value => { config.Scale = value; save(); }),
            "aqi", "eaqi", "european", "us"));

        // CAMS and Open-Meteo ask to be credited.
        rows.Add(FluentLayout.Note(Str.Online_Source.Format(("name", "Copernicus Atmosphere Monitoring Service (CAMS) · Open-Meteo"))));
    }

    /// <summary>[S-5127] The place and the location name: rain has nothing else to choose.</summary>
    public static void RainNextHour(List<FrameworkElement> rows, RainNextHourConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Weather_Settings_Place.Text()));
        AddPlacePicker(rows, config, save);

        rows.Add(FluentLayout.Header(Str.Online_Rain_Label.Text()));
        AddWeatherDisplay(rows, config, save);
        rows.Add(FluentLayout.Note(Str.Online_Source.Format(("name", "Open-Meteo"))));
    }

    /// <summary>
    /// [S-5148] The base (the Windows region's currency first, naming it), the currencies as one
    /// several-of-many row (the last one cannot be unticked), and the chart switch.
    /// </summary>
    public static void ExchangeRates(List<FrameworkElement> rows, ExchangeRatesConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Online_Rates_Label.Text()));

        // By position (the choice control takes a value type): 0 is the region, then the published codes.
        var bases = new List<(int, string)> { (0, Str.Online_Rates_Settings_Base_Region.Format(("code", RegionCurrency.Current))) };
        bases.AddRange(ExchangeRules.Published.Select((code, i) => (i + 1, code)));
        var chosen = config.BaseCurrency.Length == 0 ? 0 : ExchangeRules.Published.ToList().IndexOf(config.BaseCurrency) + 1;

        rows.Add(FluentLayout.Row(Glyphs.ExchangeRates, Str.Online_Rates_Settings_Base_Label.Text(), Str.Online_Rates_Settings_Base_Desc.Text(),
            FluentLayout.Choice<int>(bases, chosen, value =>
            {
                config.BaseCurrency = value <= 0 || value > ExchangeRules.Published.Count ? string.Empty : ExchangeRules.Published[value - 1];
                save();
            }),
            "base", "home currency"));

        var currencies = FluentLayout.MultiChoice(
            Str.Online_Rates_Settings_Quotes_All.Text(),
            () => [.. ExchangeRules.Published.Select(code => new MultiChoiceItem(code, code))],
            () =>
            {
                var picked = config.Currencies.ToHashSet(StringComparer.Ordinal);
                var all = ExchangeRules.Published.All(picked.Contains);
                return new MultiChoiceState(all, picked, all ? Str.Online_Rates_Settings_Quotes_All.Text() : string.Join(", ", config.Currencies));
            },
            on =>
            {
                // Unticking "all" keeps the first currency: the list is never empty ([S-5144]).
                config.Currencies = on ? [.. config.Currencies, .. ExchangeRules.Published.Where(c => !config.Currencies.Contains(c))] : [config.Currencies[0]];
                save();
            },
            (code, on) =>
            {
                if (on && !config.Currencies.Contains(code))
                {
                    config.Currencies.Add(code);
                    save();
                }
                else if (!on && config.Currencies.Count > 1 && config.Currencies.Remove(code))
                {
                    save();
                }
            });

        rows.Add(FluentLayout.Row(Glyphs.ExchangeRates, Str.Online_Rates_Settings_Quotes_Label.Text(), Str.Online_Rates_Settings_Quotes_Desc.Text(),
            currencies, [.. ExchangeRules.Published]));

        rows.Add(FluentLayout.Row(Glyphs.Stocks, Str.Online_Rates_Settings_Chart_Label.Format(("days", ExchangeRules.HistoryDays)), Str.Online_Rates_Settings_Chart_Desc.Text(),
            FluentLayout.Toggle(config.ShowChart, value => { config.ShowChart = value; save(); })));

        // The ECB's terms: free reuse, with the source named.
        rows.Add(FluentLayout.Note(Str.Online_Source.Format(("name", "European Central Bank"))));
    }
}
