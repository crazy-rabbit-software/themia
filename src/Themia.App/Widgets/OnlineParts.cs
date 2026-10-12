using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5160] What the air, rain and exchange-rate widgets share: the header's age line (every state,
/// caution colour when the last refresh failed or the answer was restored, the reason in the
/// tooltip, as weather does it, [S-2612]) and the Fluent status brushes their tones map onto.
/// </summary>
internal static class OnlineParts
{
    public static Brush FreshBrush() => FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);

    public static Brush StaleBrush() => FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Goldenrod);

    /// <summary>[S-5104] Good, caution and critical, from the Fluent status fills.</summary>
    public static Brush Tone(AirTone tone) => tone switch
    {
        AirTone.Good => FluentLayout.Themed("SystemFillColorSuccessBrush", Colors.SeaGreen),
        AirTone.Caution => FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Goldenrod),
        _ => FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red),
    };

    /// <summary>
    /// The age line: its text (<paramref name="text"/>, or the answer's age), its colour, and a
    /// tooltip that says why it is in the caution colour.
    /// </summary>
    public static void Stamp<T>(TextBlock label, Fetched<T> held, TimeSpan interval, string? text = null, string? note = null)
        where T : class
    {
        var now = DateTimeOffset.Now;
        var failed = FetchState.IsFailure(held.Status);
        var stale = failed || held.Restored || FetchState.IsStale(held.At, now, interval);
        var age = FetchState.Freshness(held.At, now);
        var inline = WeatherNowView.InlineAge(age);

        label.Text = text ?? age;
        label.Foreground = stale ? StaleBrush() : FreshBrush();

        var tip = failed ? Str.WidgetsC_Weather_Tooltip_ReadingFailed.Format(("problem", FetchState.Message(held.Status)), ("age", inline))
            : held.Restored ? Str.WidgetsC_Weather_Tooltip_ReadingSaved.Format(("age", inline))
            : Str.WidgetsC_Weather_Tooltip_Updated.Format(("age", inline));

        ToolTipService.SetToolTip(label, note is null ? tip : $"{note}\n{tip}");
    }
}
