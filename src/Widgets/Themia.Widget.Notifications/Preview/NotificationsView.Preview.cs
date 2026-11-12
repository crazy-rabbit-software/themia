#if DEBUG
using Themia.Sdk.WinUI;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2056]): the sample day's notifications, never the person's. A render
/// holds no lease, so no sender's icon is read and every row shows the fallback bell ([S-2271]).
/// </summary>
internal sealed partial class NotificationsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["list", "many", "open", "empty", "denied", "unspecified", "unsupported"];

    public Task ShowPreviewStateAsync(string state)
    {
        _open.Clear();
        _clock = static () => SampleDay.Now;
        _preview = state switch
        {
            "list" => new NotificationSnapshot(NotificationAccess.Allowed, Sample(10)),
            "many" or "open" => new NotificationSnapshot(NotificationAccess.Allowed, Sample(12)),
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

    /// <summary>
    /// [S-2056] The sample day's last few hours: a chat app with a run of messages from the cast, its
    /// mail, and Windows' own (the three kinds of sender), ages from the sample moment.
    /// </summary>
    private static NotificationItem[] Sample(int count)
    {
        var now = SampleDay.Now;
        const string chat = "Contoso.Chat_8wekyb3d8bbwe!App";
        const string outlook = "Microsoft.OutlookForWindows_8wekyb3d8bbwe!App";
        NotificationItem[] all =
        [
            new(12, chat, "Contoso Chat", SampleCast.Ana.Name, "I'll share the onboarding flow first, see you at 11.", now.AddMinutes(-3)),
            new(11, outlook, "Outlook", SampleCast.Dana.Name, "Quarterly numbers", now.AddMinutes(-21)),
            new(10, chat, "Contoso Chat", "Onboarding team", $"{SampleCast.Alex.First}: the new sketches are in the shared folder", now.AddMinutes(-30)),
            new(9, outlook, "Outlook", SampleCast.Lena.Name, "Lunch today?", now.AddMinutes(-38)),
            new(8, "Windows.SystemToast.SecurityAndMaintenance", "", "Virus & threat protection", "No threats found in the last scan.", now.AddMinutes(-52)),
            new(7, chat, "Contoso Chat", SampleCast.Sam.Name, "Added two points to our 1:1 notes.", now.AddMinutes(-58)),
            new(6, chat, "Contoso Chat", SampleCast.Priya.Name, "Store listing is done, screenshots next.", now.AddMinutes(-75)),
            new(5, outlook, "Outlook", SampleCast.Bea.Name, "Budget check-in at 4", now.AddMinutes(-140)),
            new(4, chat, "Contoso Chat", SampleCast.Dana.Name, "The build is green again.", now.AddHours(-3)),
            new(3, outlook, "Outlook", "Travel desk", "Your tickets to Lisbon", now.AddHours(-6)),
            new(2, chat, "Contoso Chat", SampleCast.Mia.Name, "Wednesday works for me!", now.AddHours(-14)),
            new(1, "Windows.SystemToast.WindowsUpdate", "", "Restart required", "Updates are ready to install.", now.AddHours(-16)),
        ];

        return [.. all.Take(count)];
    }
}
#endif
