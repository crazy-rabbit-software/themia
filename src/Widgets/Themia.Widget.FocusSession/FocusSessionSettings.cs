using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.FocusSession;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-4428] [S-4446] The focus session's and the brightness slider's rows. The notification list has
/// none: its page is hidden ([S-1750]). Each writes straight to the live config and saves.
/// </summary>
internal static class FocusSessionSettings
{
    public static void Rows(List<FrameworkElement> rows, FocusSessionConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(FocusSessionStr.Panels_Focus_Label.Text()));

        // [S-4421] Five-minute steps, as Windows' Clock; seeded from the clamped length.
        rows.Add(FluentLayout.Row(FocusSessionModule.Glyph, FocusSessionStr.Panels_Focus_LengthLabel.Text(), FocusSessionStr.Panels_Focus_LengthDesc.Text(),
            FluentLayout.Slider(FocusSessionRules.ClampMinutes(config.Minutes), FocusSessionRules.MinMinutes, FocusSessionRules.MaxMinutes, FocusSessionRules.StepMinutes,
                Str.WidgetsC_Pomodoro_MinutesUnit.Text(),
                value => { config.Minutes = FocusSessionRules.ClampMinutes((int)Math.Round(value)); save(); }),
            "minutes", "duration", "timer"));

        // [S-4423] Windows' Focus turns on do not disturb; so does this unless it is turned off.
        rows.Add(FluentLayout.Row(Glyphs.QuietHours, FocusSessionStr.Panels_Focus_QuietLabel.Text(), FocusSessionStr.Panels_Focus_QuietDesc.Text(),
            FluentLayout.Toggle(config.DoNotDisturb, value => { config.DoNotDisturb = value; save(); }),
            "dnd", "notifications", "silence"));
    }
}
