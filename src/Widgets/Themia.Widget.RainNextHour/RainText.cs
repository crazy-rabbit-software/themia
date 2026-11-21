using Themia.Core.Localization;
using Themia.Widget.RainNextHour;

namespace Themia.Core.Widgets;

/// <summary>[S-5124] [S-14943] The words the rain family prints.</summary>
public static class RainText
{
    /// <summary>[S-5124] The rain headline in words: the sentence of <c>rainOutlook</c>.</summary>
    public static string Headline(RainOutlook outlook) => Headline(Strings.Current, outlook);

    /// <inheritdoc cref="Headline(RainOutlook)"/>
    public static string Headline(Locale locale, RainOutlook outlook)
    {
        ArgumentNullException.ThrowIfNull(outlook);
        var minutes = ("minutes", LocaleFormat.Integer(locale, outlook.Minutes));
        return outlook.Headline switch
        {
            RainHeadline.StartsIn => RainNextHourStr.Online_Rain_StartsIn.Format(locale, minutes),
            RainHeadline.StopsIn => RainNextHourStr.Online_Rain_StopsIn.Format(locale, minutes),
            RainHeadline.Continues => RainNextHourStr.Online_Rain_Continues.Text(locale),
            RainHeadline.Dry => RainNextHourStr.Online_Rain_Dry.Text(locale),
            _ => RainNextHourStr.Online_Rain_Outdated.Text(locale),
        };
    }

    /// <summary>
    /// [S-14943] The rain the window holds as a single metric's value: its millimetres, a tenth under 10 and whole above
    /// (as the forecast writes an amount, <see cref="WeatherReport.PrecipitationAmount(Locale, double?)"/>), "0 mm" dry; a
    /// window too short to speak for the hour has no value ("--", why its tooltip, [S-14602]).
    /// </summary>
    public static MetricValue Total(Locale locale, RainOutlook outlook)
    {
        ArgumentNullException.ThrowIfNull(outlook);
        if (outlook.Headline == RainHeadline.Outdated)
        {
            return MetricValue.Unread;
        }

        var amount = WeatherReport.PrecipitationAmount(locale, outlook.Total);
        var text = RainNextHourStr.Rain_Total_Value.Format(locale, ("amount", amount.Length > 0 ? amount : LocaleFormat.Integer(locale, 0)));
        return new MetricValue(text, outlook.Total);
    }
}
