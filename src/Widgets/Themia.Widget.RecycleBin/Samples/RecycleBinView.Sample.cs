using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] [S-1917] The bin's sample: a fixed bin (37 items, 1.2 GB), never this PC's.</summary>
internal sealed partial class RecycleBinView : IWidgetSample
{
    public Task ShowSampleAsync() => ShowBin(new RecycleBinState(37, 1_288_490_189));

    /// <summary>Show <paramref name="bin"/> in place of the shell's.</summary>
    internal Task ShowBin(RecycleBinState bin)
    {
        RecycleBinState? fixture = bin;
        _state = () => fixture;
        Render();
        return Task.CompletedTask;
    }
}
