using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3525] General, on the menu's first level: start with Windows, what the desktop shows and game
/// mode. Start with Windows and game mode show something Windows owns or decides, so the host reads
/// Windows for them rather than trusting what Themia last wrote; the desktop is Themia's own choice
/// ([S-5500]).
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-3525] The General lines, with under Start with Windows what holds against it.</summary>
    public static IReadOnlyList<SettingsLine> General(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var state = host.State;
        var status = host.Startup;

        // [S-935] An unread or unreadable state keeps the stored value rather than claiming "off".
        var startsWithWindows = status is { State: not AutostartState.Unknown } read ? read.WillStart : state.Design.RunAtStartup;

        var lines = new List<SettingsLine>
        {
            new SettingsToggle(Keys.Startup, Str.Shell_Settings_General_Startup.Text(), Glyphs.StartWithWindows, startsWithWindows, host.SetStartup),
        };

        var (advice, detail) = StartupAdvice(status);
        if (advice is not null)
        {
            lines.Add(new SettingsNote(Keys.Startup + "/advice", advice));
        }

        if (detail is not null)
        {
            lines.Add(new SettingsNote(Keys.Startup + "/detail", detail));
        }

        // [S-5500] What the desktop shows: Themia's own choice, so it shows what was chosen.
        lines.Add(Desktop(host));

        // [S-5575] Under it, why it does not show while another program holds the desktop.
        if (Themia.Core.Desktop.DesktopCoexistence.Note(state.Preferences.Desktop, host.DesktopHolder) is { } held)
        {
            lines.Add(new SettingsNote(Keys.Desktop + "/held", held));
        }

        // [S-920] Game mode, "Paused now" at its end while it is holding the widgets.
        lines.Add(new SettingsToggle(Keys.GameMode, Str.SettingsMenu_GameMode.Text(), Glyphs.Game, state.Preferences.GameModeSuspend, host.SetGameModeSuspend)
        {
            Value = host.Suspended ? Str.Shell_Settings_General_PausedNow.Text() : null,
        });

        return lines;
    }

    /// <summary>
    /// [S-5500] On the desktop: Themia's items, Windows' icons or nothing, the one line that sets it
    /// ([S-3592]). A pick goes to the host, which stores it and hides or shows Explorer's icons.
    /// </summary>
    private static SettingsChoice Desktop(ISettingsMenuHost host)
    {
        var shows = host.State.Preferences.Desktop;
        SettingsRadio Option(DesktopShows option, string key, Str text)
            => new(Keys.Desktop + "/" + key, text.Text(), shows == option, () => host.SetDesktop(option));

        return new SettingsChoice(Keys.Desktop, Str.Desktop_Choice_Label.Text(), Glyphs.Apps,
        [
            Option(DesktopShows.ThemiaItems, "themia", Str.Desktop_Choice_Themia),
            Option(DesktopShows.WindowsIcons, "windows", Str.Desktop_Choice_Windows),
            Option(DesktopShows.Nothing, "nothing", Str.Desktop_Choice_Nothing),
        ]);
    }

    /// <summary>
    /// [S-935] What to say under Start with Windows: why a change did not take, else that Windows
    /// has Themia turned off, else that a policy decides; and the raw diagnostic under that advice
    /// only, never on its own. Nothing while it is plainly on or off, or not read yet.
    /// </summary>
    public static (string? Advice, string? Detail) StartupAdvice(AutostartStatus? status)
    {
        if (status is null)
        {
            return (null, null);
        }

        var advice = AutostartRules.Advice(status.Problem)
            ?? (status.State == AutostartState.TurnedOffInWindows ? AutostartRules.Advice(AutostartProblem.TurnedOffInWindows) : null)
            ?? (status.IsLocked ? AutostartRules.Advice(AutostartProblem.Policy) : null);

        var detail = advice is not null && status.Detail is { Length: > 0 } raw ? raw : null;
        return (advice, detail);
    }
}
