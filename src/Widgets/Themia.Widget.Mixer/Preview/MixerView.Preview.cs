#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is a fixed set of apps (five rows, one of them two sessions
/// grouped, one silent, one muted; no icon paths, so every lead is the glyph), <c>unavailable</c> a PC with no
/// output device ([S-1940]); never this PC's.
/// </summary>
internal sealed partial class MixerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "unavailable"];

    private static readonly AudioSessionInfo[] Sample =
    [
        new("s1", "spotify.exe", "Spotify", null, 0.8, false, true, false),
        new("s2", "msedge.exe", "Microsoft Edge", null, 0.45, false, true, false),
        new("s3", "msedge.exe", "Microsoft Edge", null, 0.3, false, true, false),
        new("s4", "discord.exe", "Discord", null, 1.0, false, false, false),
        new("s5", "steam.exe", "Steam", null, 0.6, true, true, false),
        new("s6", "teams.exe", "Microsoft Teams", null, 0.7, false, true, false),
    ];

    public Task ShowPreviewStateAsync(string state)
    {
        AudioSnapshot? snapshot = state switch
        {
            "sample" => new AudioSnapshot(AudioStatus.Ready, null, Sample, [], null, AudioTarget.Default),
            "unavailable" => new AudioSnapshot(AudioStatus.NoDevice, null, [], [], null, AudioTarget.Default),
            _ => null,
        };

        if (snapshot is not null)
        {
            // The live feed would put this machine's sessions back.
            _frozen = true;
            _feed.Dispose();
            _snapshot = snapshot;
            Lay();
        }

        return Task.CompletedTask;
    }
}
#endif
