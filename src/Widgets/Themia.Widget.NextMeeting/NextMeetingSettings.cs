using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.Widget.NextMeeting;

/// <summary>
/// [S-4826] Next meeting's page: it chooses its accounts and calendars like every calendar widget
/// (the SDK's shared rows), and has no display rows: birthdays are all-day, never a meeting.
/// </summary>
internal static class NextMeetingSettings
{
    public static void Rows(WidgetSettingsRows rows, NextMeetingConfig config) => OfficeSettingsRows.CalendarAccounts(rows, config);
}
