using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] The clipboard history's sample: a fixed history of the sample day, held by this view
/// alone (never the user's clipboard, [S-1443]); nothing is copied or cleared.
/// </summary>
internal sealed partial class ClipboardView : IWidgetSample
{
    public Task ShowSampleAsync()
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
            At("rgb(58, 123, 213)", 95),
            At("Uferweg 12\n8008 Zürich\nSwitzerland", 190),
            At("Please find the signed contract attached.", 300),
            At("onboarding-flow-v3.fig", 60 * 20),
            At("git commit -m \"Fix the header\"", 60 * 26),
        ];
        Render();
        return Task.CompletedTask;
    }
}
