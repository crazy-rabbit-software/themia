using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-5124] The words the rain widget prints.</summary>
public static class RainText
{
    /// <summary>[S-5124] The rain headline in words.</summary>
    public static string Headline(RainOutlook outlook)
    {
        ArgumentNullException.ThrowIfNull(outlook);
        var minutes = ("minutes", LocaleFormat.Integer(outlook.Minutes));
        return outlook.Headline switch
        {
            RainHeadline.StartsIn => Str.Online_Rain_StartsIn.Format(minutes),
            RainHeadline.StopsIn => Str.Online_Rain_StopsIn.Format(minutes),
            RainHeadline.Continues => Str.Online_Rain_Continues.Text(),
            RainHeadline.Dry => Str.Online_Rain_Dry.Text(),
            _ => Str.Online_Rain_Outdated.Text(),
        };
    }

    public static string Intensity(RainIntensity intensity) => (intensity switch
    {
        RainIntensity.Heavy => Str.Online_Rain_Intensity_Heavy,
        RainIntensity.Moderate => Str.Online_Rain_Intensity_Moderate,
        _ => Str.Online_Rain_Intensity_Light,
    }).Text();

    /// <summary>
    /// [S-5124] The caption under the headline: the heaviest rain in the window and the total, or the
    /// hour's chance when it stays dry; parts the window has nothing for are left out.
    /// </summary>
    public static string Caption(RainOutlook outlook)
    {
        ArgumentNullException.ThrowIfNull(outlook);

        var parts = new List<string>(3);
        var heaviest = outlook.Steps.Count == 0 ? RainIntensity.None : outlook.Steps.Max(s => RainRules.Intensity(s.Millimetres));

        if (outlook.Headline != RainHeadline.Outdated && heaviest > RainIntensity.None)
        {
            parts.Add(Intensity(heaviest));
            parts.Add(Str.Online_Rain_Total.Format(("amount", LocaleFormat.Number(outlook.Total, 1))));
        }

        if (outlook.Chance is { } chance && (heaviest > RainIntensity.None || chance > 0))
        {
            parts.Add(Str.Online_Rain_Chance.Format(("percent", LocaleFormat.Percent(Strings.Current, chance))));
        }

        return string.Join(" · ", parts);
    }
}
