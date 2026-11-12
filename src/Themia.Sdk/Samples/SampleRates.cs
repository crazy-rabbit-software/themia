#if DEBUG
using Themia.App.Services.Web;
using Themia.Core.Widgets;

namespace Themia.Sdk.Samples;

/// <summary>
/// Debug renders only ([S-2056]): the sample day's ECB table, ninety days (63 working days) of the euro
/// as seeded walks of small daily moves per currency, built through the SDK's parser. The latest day is
/// the last working day before the sample day, as the ECB's 16:00 reference rates stand at 10:42. The
/// rate and its history atoms both read it, so a recipe's two draw one pair to the digit ([S-11171]).
/// </summary>
public static class SampleRates
{
    public static Fetched<RateTable> Table()
    {
        const int Days = 63;
        var latest = SampleDay.Today.AddDays(-1);
        while (latest.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            latest = latest.AddDays(-1);
        }

        (string Currency, double Last, double Move)[] currencies =
        [
            ("USD", 1.1584, 0.0035), ("CHF", 0.9312, 0.0018), ("GBP", 0.8697, 0.0025), ("JPY", 176.42, 0.0045), ("SEK", 10.962, 0.003),
        ];
        var walks = currencies.Select(c => SampleSeries.Walk("ecb-" + c.Currency, Days, c.Last, c.Move)).ToArray();
        var xml = new System.Text.StringBuilder("<Envelope><Cube>");
        for (int i = 0, written = 0; written < Days; i++)
        {
            var date = latest.AddDays(-i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var at = Days - 1 - written++;
            xml.Append("<Cube time=\"").Append(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append("\">");
            for (var c = 0; c < currencies.Length; c++)
            {
                var rate = walks[c][at].ToString(currencies[c].Last > 100 ? "0.00" : "0.0000", System.Globalization.CultureInfo.InvariantCulture);
                xml.Append("<Cube currency=\"").Append(currencies[c].Currency).Append("\" rate=\"").Append(rate).Append("\"/>");
            }

            xml.Append("</Cube>");
        }

        xml.Append("</Cube></Envelope>");
        return new Fetched<RateTable>(FetchStatus.Ok, EcbWire.Parse(xml.ToString()).Value, null, SampleDay.HoursAgo(2));
    }
}
#endif
