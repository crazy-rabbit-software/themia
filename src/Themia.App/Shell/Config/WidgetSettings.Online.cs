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
    /// [S-5148] The pair: the base (the Windows region's currency first, naming it) and the currency
    /// priced in it (the default first, naming it), then the source line.
    /// </summary>
    public static void ExchangeRates(List<FrameworkElement> rows, ExchangeRatesConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Online_Rates_Label.Text()));

        // By position (the choice control takes a value type): 0 is the default, then the published codes.
        static int Position(string code) => code.Length == 0 ? 0 : ExchangeRules.Published.ToList().IndexOf(code) + 1;
        static string Code(int position) => position <= 0 || position > ExchangeRules.Published.Count ? string.Empty : ExchangeRules.Published[position - 1];

        var bases = new List<(int, string)> { (0, Str.Online_Rates_Settings_Base_Region.Format(("code", RegionCurrency.Current))) };
        bases.AddRange(ExchangeRules.Published.Select((code, i) => (i + 1, code)));

        rows.Add(FluentLayout.Row(Glyphs.ExchangeRates, Str.Online_Rates_Settings_Base_Label.Text(), Str.Online_Rates_Settings_Base_Desc.Text(),
            FluentLayout.Choice<int>(bases, Position(config.BaseCurrency), value =>
            {
                config.BaseCurrency = Code(value);
                save();
            }),
            "base", "home currency"));

        var quotes = new List<(int, string)> { (0, Str.Online_Rates_Settings_Quote_Default.Format(("code", ExchangeRules.DefaultQuote(config.EffectiveBase)))) };
        quotes.AddRange(ExchangeRules.Published.Select((code, i) => (i + 1, code)));

        rows.Add(FluentLayout.Row(Glyphs.ExchangeRates, Str.Online_Rates_Settings_Quote_Label.Text(), Str.Online_Rates_Settings_Quote_Desc.Text(),
            FluentLayout.Choice<int>(quotes, Position(config.QuoteCurrency), value =>
            {
                config.QuoteCurrency = Code(value);
                save();
            }),
            "currency", "quote", "pair"));

        // The ECB's terms: free reuse, with the source named.
        rows.Add(FluentLayout.Note(Str.Online_Source.Format(("name", "European Central Bank"))));
    }
}
