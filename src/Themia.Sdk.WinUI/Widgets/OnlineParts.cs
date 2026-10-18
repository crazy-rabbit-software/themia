using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5160] What the online widgets share: the header's age line (every state, caution colour when
/// the last refresh failed or the answer was restored, the reason in the tooltip, as weather does
/// it, [S-2612]) and the age as it reads inside that tooltip.
/// </summary>
public static class OnlineParts
{
    public static Brush FreshBrush() => ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);

    public static Brush StaleBrush() => ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Goldenrod);

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
        var inline = InlineAge(age);

        label.Text = text ?? age;
        label.Foreground = stale ? StaleBrush() : FreshBrush();

        var tip = failed ? Str.WidgetsC_Weather_Tooltip_ReadingFailed.Format(("problem", FetchState.Message(held.Status)), ("age", inline))
            : held.Restored ? Str.WidgetsC_Weather_Tooltip_ReadingSaved.Format(("age", inline))
            : Str.WidgetsC_Weather_Tooltip_Updated.Format(("age", inline));

        ToolTipService.SetToolTip(label, note is null ? tip : $"{note}\n{tip}");
    }

    /// <summary>
    /// The age for use inside a sentence: "Just now" starts the header line but reads "just now"
    /// mid-sentence. Only the first letter changes, so a German noun later in the phrase keeps its
    /// capital; a language without case is untouched.
    /// </summary>
    public static string InlineAge(string age)
        => age.Length == 0 ? age : char.ToLowerInvariant(age[0]) + age[1..];
}
