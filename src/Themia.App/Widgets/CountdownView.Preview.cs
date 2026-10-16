#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is three timers (a launch in three days, the one
/// featured, a holiday twelve days off, a deadline gone by), in the widget's own style,
/// so the three styles ([S-6030]) are drawn over the same list. <c>confirm</c> is the sample with
/// the remove question open for the first timer ([S-1500]).
/// </summary>
internal sealed partial class CountdownView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "confirm"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "confirm"))
        {
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.Now;
        _config = new CountdownConfig
        {
            Style = _config.Style,
            Timers =
            {
                CountdownTimer.Create("Holiday", now.AddDays(12).AddHours(3).AddMinutes(7), now.AddDays(-6)),
                CountdownTimer.Create("Launch", now.AddDays(3).AddHours(5).AddMinutes(20), now.AddDays(-2)),
                CountdownTimer.Create("Deadline", now.AddDays(-2), now.AddDays(-9)),
            },
        };

        Rebuild();

        if (state == "confirm")
        {
            // Never answered: the render closes with the question open.
            _ = RemoveAsync(_config.Timers[0].Id);

            // The flyout opens with an animation; the render draws it once it has settled.
            return Task.Delay(900);
        }

        return Task.CompletedTask;
    }
}
#endif
