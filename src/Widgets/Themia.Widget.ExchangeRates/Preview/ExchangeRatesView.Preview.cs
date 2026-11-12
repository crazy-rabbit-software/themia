#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2056]): a fixture table (<c>sample</c>), ninety days of the ECB
/// document as seeded random walks, so the ladder can be looked at without the network or the sync.
/// Built through the SDK's parser. Nothing is fetched or saved. Its latest day is the working day
/// before the sample day ([S-2050]), so the header's date agrees with the calendar beside it.
/// </summary>
internal sealed partial class ExchangeRatesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "stale"))
        {
            return Task.CompletedTask;
        }

        // Ninety days (63 working days) of the euro, as the ECB document would carry them: a seeded
        // walk of small daily moves per currency ([S-2056]). The latest day is the last working day
        // before the sample day, as the ECB's 16:00 reference rates stand at 10:42.
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

        _config = new ExchangeRatesConfig { BaseCurrency = "CHF", QuoteCurrency = "EUR", Style = _config.Style };
        _table = new Fetched<RateTable>(FetchStatus.Ok, EcbWire.Parse(xml.ToString()).Value, null, SampleDay.HoursAgo(2));
        if (state == "stale")
        {
            // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
            _table = _table.Stale(FetchStatus.Offline, null);
        }
        _web = null;
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
