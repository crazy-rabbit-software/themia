#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): made-up snapshots, never this PC's. <c>level</c> is a device at 60 %;
/// <c>unavailable</c> a PC with no output device ([S-1940]).
/// </summary>
internal sealed partial class VolumeView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["level", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        AudioSnapshot? snapshot = state switch
        {
            "level" => new AudioSnapshot(AudioStatus.Ready, new AudioLevel("sample", "Speakers", 0.6, false), [], [], "sample", AudioTarget.Default),
            "unavailable" => new AudioSnapshot(AudioStatus.NoDevice, null, [], [], null, AudioTarget.Default),
            _ => null,
        };

        if (snapshot is not null)
        {
            // The live feed would put this machine's device back.
            _frozen = true;
            _feed.Dispose();
            _snapshot = snapshot;
            Lay();
        }

        return Task.CompletedTask;
    }
}
#endif
