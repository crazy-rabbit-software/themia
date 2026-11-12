using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] [S-1917] The volume's sample: a made-up device at 60 %, never this PC's.</summary>
internal sealed partial class VolumeView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSnapshot(new AudioSnapshot(AudioStatus.Ready, new AudioLevel("sample", "Speakers", 0.6, false), [], [], "sample", AudioTarget.Default));
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="snapshot"/> and let go of the live feed, which would put this machine's device back.</summary>
    private void HoldSnapshot(AudioSnapshot snapshot)
    {
        _frozen = true;
        _feed.Dispose();
        _snapshot = snapshot;
        Lay();
    }
}
