#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): what a render cannot show by itself. <c>unavailable</c> draws the
/// sheet of a thing not on this machine where the kind can be absent ([S-1942]: a battery on a
/// laptop); <c>focus</c> puts the keyboard ring on a list's second row. Fixed figures (a laptop's
/// battery on a desktop) are the sample machine's (<c>--machine sample</c>, [S-1931]).
/// </summary>
public sealed partial class DetailView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["unavailable", "focus"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "unavailable" when _part.AbsentSheet() is { } absent:
                _part.Detach();
                Present(absent);
                break;

            case "focus":
                HandleKey(WidgetKey.Down);
                HandleKey(WidgetKey.Down);
                break;

            default:
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
