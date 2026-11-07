using System.Globalization;
using System.Text;
using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-2006] The forecast page a click on a weather widget opens: MSN Weather for its own place.</summary>
public static class WeatherPages
{
    /// <summary>
    /// [S-2006] MSN Weather's forecast for the widget's coordinates, in the UI language's market and
    /// the unit shown. The place travels as MSN's own <c>loc</c> parameter (base64 JSON of the name and
    /// the coordinates), because a name alone lands on whichever town MSN guesses. Null with no place.
    /// </summary>
    public static CardOpen? Forecast(WeatherConfig weather, TemperatureUnit unit, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(weather);

        if (!weather.HasLocation)
        {
            return null;
        }

        var name = weather.LocationName.Split(',', 2)[0].Trim();

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("l", name);
            json.WriteString("x", weather.Longitude!.Value.ToString("0.####", CultureInfo.InvariantCulture));
            json.WriteString("y", weather.Latitude!.Value.ToString("0.####", CultureInfo.InvariantCulture));
            json.WriteEndObject();
        }

        var loc = Convert.ToBase64String(buffer.ToArray());
        var degrees = unit == TemperatureUnit.Fahrenheit ? "F" : "C";

        return CardOpen.Web($"https://www.msn.com/{Market(locale)}/weather/forecast/in-{Slug(name)}?loc={Uri.EscapeDataString(loc)}&weadegreetype={degrees}");
    }

    /// <summary>MSN's market for a UI language; English and the rest take <c>en-us</c>.</summary>
    public static string Market(Locale locale) => locale switch
    {
        Locale.De => "de-de",
        Locale.Es => "es-es",
        Locale.Fr => "fr-fr",
        Locale.It => "it-it",
        Locale.Ja => "ja-jp",
        Locale.Pl => "pl-pl",
        Locale.Pt => "pt-br",
        Locale.Zh => "zh-cn",
        _ => "en-us",
    };

    /// <summary>A place name as one URL path segment: letters and digits kept, every run of anything else one hyphen.</summary>
    internal static string Slug(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return Uri.EscapeDataString(slug.Length == 0 ? "here" : slug);
    }
}
