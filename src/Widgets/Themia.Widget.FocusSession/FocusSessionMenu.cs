using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.FocusSession;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-4428] The focus session's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the length and do not disturb. It has no settings page.
/// </summary>
internal static class FocusSessionMenu
{
    public static void Lines(WidgetMenuLines menu, FocusSessionConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);


        // [S-4421] Five-minute steps, as Windows' Clock; more steps than dots, so it is typed.
        menu.Lines.Add(menu.Number(FocusSessionModule.Glyph, FocusSessionStr.Panels_Focus_LengthLabel.Text(),
            FocusSessionRules.ClampMinutes(config.Minutes), FocusSessionRules.MinMinutes, FocusSessionRules.MaxMinutes, FocusSessionRules.StepMinutes,
            Minutes, value => config.Minutes = FocusSessionRules.ClampMinutes(value)));

        // [S-4423] Windows' Focus turns on do not disturb; so does this unless it is turned off.
        menu.Lines.Add(menu.Toggle(Glyphs.QuietHours, FocusSessionStr.Panels_Focus_QuietLabel.Text(), config.DoNotDisturb, on => config.DoNotDisturb = on));
    }

    /// <summary>"30 min".</summary>
    internal static string Minutes(int minutes) => $"{minutes}{Str.WidgetsC_Pomodoro_MinutesUnit.Text()}";
}
