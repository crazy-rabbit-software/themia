#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2056]): <c>sample</c> draws a fixed history of the sample day (never the user's clipboard,
/// [S-1443]); <c>confirm</c> opens the clear question ([S-1500]) over the empty history a render has;
/// nothing is copied or cleared.
/// </summary>
internal sealed partial class ClipboardView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["sample", "confirm"];

    Task IWidgetPreviewState.ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var now = SampleDay.Now;
            _clock = () => now;
            ClipboardEntry At(string text, double minutesAgo) => new(text, now.AddMinutes(-minutesAgo));
            _sample =
            [
                At("Room 4.12", 2),
                At("https://lumen.example/onboarding/flow-v3", 6),
                At(SampleCast.Bea.Email, 25),
                At("Thursday's review moves to 14:00 in the board room.", 48),
                At("CHF 48,213.75", 70),
                At("#3A7BD5", 95),
                At("Uferweg 12\n8008 Zürich\nSwitzerland", 190),
                At("Please find the signed contract attached.", 300),
                At("onboarding-flow-v3.fig", 60 * 20),
                At("git commit -m \"Fix the header\"", 60 * 26),
            ];
            Render();
            return Task.CompletedTask;
        }

        if (state != "confirm")
        {
            return Task.CompletedTask;
        }

        // Never answered: the render closes with the question open.
        _ = AskToClearAsync();

        // The flyout opens with an animation; the render draws it once it has settled.
        return Task.Delay(900);
    }
}
#endif
