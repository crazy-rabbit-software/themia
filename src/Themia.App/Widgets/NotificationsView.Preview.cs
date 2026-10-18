#if DEBUG
using Themia.Sdk.WinUI;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): made-up notifications, never the person's.</summary>
internal sealed partial class NotificationsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["list", "many", "open", "empty", "denied", "unspecified", "unsupported"];

    public Task ShowPreviewStateAsync(string state)
    {
        _open.Clear();
        _preview = state switch
        {
            "list" => new NotificationSnapshot(NotificationAccess.Allowed, Sample(5)),
            "many" or "open" => new NotificationSnapshot(NotificationAccess.Allowed, Sample(9)),
            "empty" => new NotificationSnapshot(NotificationAccess.Allowed, []),
            "denied" => new NotificationSnapshot(NotificationAccess.Denied, []),
            "unspecified" => new NotificationSnapshot(NotificationAccess.Unspecified, []),
            "unsupported" => new NotificationSnapshot(NotificationAccess.Unsupported, []),
            _ => _preview,
        };

        if (state == "open")
        {
            _open.Add("Contoso.Chat_8wekyb3d8bbwe!App");
        }

        Render();
        return Task.CompletedTask;
    }

    /// <summary>A chat app with a run of messages, mail, and Windows' own: the three kinds of sender.</summary>
    private static NotificationItem[] Sample(int count)
    {
        var now = DateTimeOffset.Now;
        NotificationItem[] all =
        [
            new(9, "Contoso.Chat_8wekyb3d8bbwe!App", "Contoso Chat", "Anna Berger", "Running ten minutes late, start without me.", now.AddMinutes(-2)),
            new(8, "Microsoft.OutlookForWindows_8wekyb3d8bbwe!App", "Outlook", "Quarterly review moved", "The review is now on Thursday at 14:00 in room 3.", now.AddMinutes(-9)),
            new(7, "Contoso.Chat_8wekyb3d8bbwe!App", "Contoso Chat", "Team Rocket", "Ben: the build is green again", now.AddMinutes(-15)),
            new(6, "Windows.SystemToast.SecurityAndMaintenance", "", "Virus & threat protection", "No threats found in the last scan.", now.AddMinutes(-40)),
            new(5, "Contoso.Chat_8wekyb3d8bbwe!App", "Contoso Chat", "Mia Keller", "Can you send me the slides?\nThe ones from Monday.", now.AddHours(-1)),
            new(4, "Contoso.Chat_8wekyb3d8bbwe!App", "Contoso Chat", "Lukas", "👍", now.AddHours(-2)),
            new(3, "Contoso.Chat_8wekyb3d8bbwe!App", "Contoso Chat", "Anna Berger", "Lunch at 12?", now.AddHours(-3)),
            new(2, "Microsoft.OutlookForWindows_8wekyb3d8bbwe!App", "Outlook", "Invoice 2026-114", "Payment received, thank you.", now.AddHours(-5)),
            new(1, "Windows.SystemToast.WindowsUpdate", "", "Restart required", "Updates are ready to install.", now.AddDays(-1)),
        ];

        return [.. all.Take(count)];
    }
}
#endif
