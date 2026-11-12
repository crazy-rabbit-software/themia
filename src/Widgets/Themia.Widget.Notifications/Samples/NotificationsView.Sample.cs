using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] [S-2056] The notifications' sample: the sample day's notifications, never the
/// person's. A preview holds no lease, so no sender's icon is read and every row shows the fallback bell
/// ([S-2271]).
/// </summary>
internal sealed partial class NotificationsView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        ShowSnapshot(new NotificationSnapshot(NotificationAccess.Allowed, Sample(10)), open: null);
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="snapshot"/> (null keeps what is shown) at the sample moment, the app <paramref name="open"/> opened.</summary>
    internal void ShowSnapshot(NotificationSnapshot? snapshot, string? open)
    {
        _open.Clear();
        _clock = static () => SampleDay.Now;
        _preview = snapshot ?? _preview;

        if (open is not null)
        {
            _open.Add(open);
        }

        Render();
    }

    /// <summary>
    /// [S-2056] The sample day's last few hours: a chat app with a run of messages from the cast, its
    /// mail, and Windows' own (the three kinds of sender), ages from the sample moment.
    /// </summary>
    internal static NotificationItem[] Sample(int count)
    {
        var now = SampleDay.Now;
        const string chat = SampleChat;
        // A classic app id that names its executable, so the sender links to the same app elsewhere ([S-11930]).
        const string outlook = "Microsoft.Office.OUTLOOK.EXE.15";
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

    /// <summary>The sample's chat app.</summary>
    internal const string SampleChat = "Contoso.Chat_8wekyb3d8bbwe!App";
}
