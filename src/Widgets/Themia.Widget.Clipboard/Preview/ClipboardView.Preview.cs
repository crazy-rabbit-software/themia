#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> draws a fixed history (never the user's clipboard,
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
            var now = DateTimeOffset.Now;
            ClipboardEntry At(string text, double minutesAgo) => new(text, now.AddMinutes(-minutesAgo));
            _sample =
            [
                At("https://example.com/reports/q3", 2),
                At("Meet at the north entrance at 10:30", 9),
                At("anna.berger@example.org", 25),
                At("rgb(96, 205, 255)", 70),
                At("48213.75", 190),
                At("Street 1\n8000 Zurich\nSwitzerland", 300),
                At("The quarterly numbers are in the shared folder.", 60 * 26),
                At("git commit -m \"Fix the header\"", 60 * 50),
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
