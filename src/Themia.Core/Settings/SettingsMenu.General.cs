using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3525] General, the General page's first section: start with Windows and game mode (snap to grid is
/// the edit bar's, [S-2330]); [S-2315] the desktop icons are a section of their own (<see cref="Desktop"/>). Start with Windows and game mode show something Windows owns or decides, so the host reads
/// Windows for them rather than trusting what Themia last wrote.
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

        // [S-920] Game mode, "Paused now" at its end while it is holding the widgets.
        lines.Add(new SettingsToggle(Keys.GameMode, Str.SettingsMenu_GameMode.Text(), Glyphs.Game, state.Preferences.GameModeSuspend, host.SetGameModeSuspend)
        {
            Value = host.Suspended ? Str.Shell_Settings_General_PausedNow.Text() : null,
        });

        return lines;
    }

    /// <summary>
    /// [S-2315] Desktop icons, the General page's section of its own: the desktop's icon size ([S-5593]), its icon
    /// layout ([S-13970]) and Lock widgets ([S-12950]).
    /// </summary>
    public static IReadOnlyList<SettingsLine> Desktop(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var state = host.State;
        var lines = new List<SettingsLine>();

        // [S-5593] The desktop's one view setting, the one its background menu's View › sets too, for every
        // display. The icons always show and always stand on the icon grid; Auto arrange icons is the
        // menu's one-shot command, no setting ([S-5594], [S-5595]).
        var size = state.Preferences.DesktopIconSize;
        SettingsRadio Size(DesktopIconSize value, Str text) => new(
            Keys.DesktopIconSize + "/" + value.ToString().ToLowerInvariant(), text.Text(), size == value,
            () => host.Mutate(s => s.Preferences.DesktopIconSize = value, relayout: true));
        lines.Add(new SettingsChoice(Keys.DesktopIconSize, Str.Desktop_Icons_Size.Text(), Glyphs.View,
        [
            Size(DesktopIconSize.Large, Str.Desktop_Icons_Large),
            Size(DesktopIconSize.Medium, Str.Desktop_Icons_Medium),
            Size(DesktopIconSize.Small, Str.Desktop_Icons_Small),
        ]));

        // [S-13970] The desktop's icon layout, beside the size and as it is: one choice for the whole desktop (every view,
        // every display, the fences), the one View › sets too.
        var layout = state.Preferences.DesktopIconLayout;
        SettingsRadio Layout(DesktopIconLayout value, Str text) => new(
            Keys.DesktopIconLayout + "/" + value.ToString().ToLowerInvariant(), text.Text(), layout == value,
            () => host.Mutate(s => s.Preferences.DesktopIconLayout = value, relayout: true));
        lines.Add(new SettingsChoice(Keys.DesktopIconLayout, Str.Desktop_Icons_Layout.Text(), Glyphs.BulletList,
        [
            Layout(DesktopIconLayout.Below, Str.Desktop_Icons_TextBelow),
            Layout(DesktopIconLayout.Beside, Str.Desktop_Icons_TextBeside),
            Layout(DesktopIconLayout.IconOnly, Str.Desktop_Icons_IconOnly),
        ]));

        // [S-12950] [S-12951] Lock widgets: this is its one place ([S-3590]); the desktop's menus' Lock widgets line
        // is a command that flips it, as View › flips the icon size above.
        lines.Add(new SettingsToggle(Keys.LockWidgets, Str.Shell_Menu_LockWidgets.Text(), Glyphs.Lock, state.Preferences.LockWidgets,
            on => host.Mutate(s => s.Preferences.LockWidgets = on, relayout: false)));
        return lines;
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
