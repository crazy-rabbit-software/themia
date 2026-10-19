#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): <c>unavailable</c> is a PC with no output device ([S-1940]), never this PC's.</summary>
internal sealed partial class MixerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "unavailable")
        {
            // The live feed would put this machine's sessions back.
            _frozen = true;
            _feed.Dispose();
            _snapshot = new AudioSnapshot(AudioStatus.NoDevice, null, [], [], null, AudioTarget.Default);
            Lay();
        }

        return Task.CompletedTask;
    }
}
#endif
