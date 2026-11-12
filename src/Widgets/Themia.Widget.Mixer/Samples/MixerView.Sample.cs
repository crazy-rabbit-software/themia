using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] The mixer's sample: a fixed set of apps (five rows, one of them two sessions grouped,
/// one silent, one muted; no icon paths, so every lead is the glyph); never this PC's.
/// </summary>
internal sealed partial class MixerView : IWidgetSample
{
    private static readonly AudioSessionInfo[] Sample =
    [
        new("s1", "spotify.exe", "Spotify", null, 0.8, false, true, false),
        new("s2", "msedge.exe", "Microsoft Edge", null, 0.45, false, true, false),
        new("s3", "msedge.exe", "Microsoft Edge", null, 0.3, false, true, false),
        new("s4", "discord.exe", "Discord", null, 1.0, false, false, false),
        new("s5", "steam.exe", "Steam", null, 0.6, true, true, false),
        new("s6", "teams.exe", "Microsoft Teams", null, 0.7, false, true, false),
    ];

    public Task ShowSampleAsync()
    {
        Hold(new AudioSnapshot(AudioStatus.Ready, null, Sample, [], null, AudioTarget.Default));
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="snapshot"/> and hold it: the live feed would put this machine's sessions back.</summary>
    internal void Hold(AudioSnapshot snapshot)
    {
        _frozen = true;
        _feed.Dispose();
        _snapshot = snapshot;
        Lay();
    }
}
