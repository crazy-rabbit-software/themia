using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.Widget.NextMeeting;

/// <summary>
/// [S-4826] Next meeting's options, all on its properties menu ([S-3719]): Appearance ([S-3723]),
/// then the accounts and calendars every calendar widget chooses (the SDK's
/// <see cref="OfficeMenuLines"/>), without their colours, which it never draws. No birthdays line:
/// they are all-day, never a meeting. It has no settings page.
/// </summary>
internal static class NextMeetingMenu
{
    /// <param name="office">The host's office source (<see cref="SettingsHost.Office"/>), null before it has started.</param>
    public static void Lines(WidgetMenuLines menu, NextMeetingConfig config, IOfficeSource? office)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // [S-11407] Which of the meeting's lines show under its title and time.
        menu.Lines.Add(menu.Toggle(Glyphs.Place, NextMeetingStr.NextMeeting_ShowPlace.Text(), config.ShowPlace, on => config.ShowPlace = on));
        menu.Lines.Add(menu.Toggle(Glyphs.Contact, NextMeetingStr.NextMeeting_ShowOrganizer.Text(), config.ShowOrganizer, on => config.ShowOrganizer = on));
        menu.Lines.Add(ItemMenuEntry.Separator);

        // [S-4826] It draws no calendar colours, so the calendars are switches only.
        menu.Lines.AddRange(OfficeMenuLines.CalendarAccounts(menu, config, office, colours: false));
    }
}
