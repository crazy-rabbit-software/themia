#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): what a render machine cannot show by itself. <c>sample</c> draws the
/// part's own fixture sheet where it has one (<see cref="Details.DetailPart.SampleSheet"/>: a laptop's battery on a desktop);
/// <c>unavailable</c> the sheet of a thing not on this machine where the kind can be absent ([S-1942]: a
/// battery on a laptop); <c>focus</c> puts the keyboard ring on a list's second row.
/// </summary>
public sealed partial class DetailView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "unavailable", "focus"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample" when _part.SampleSheet() is { } sheet:
                // Stop the real reads, which on a desktop would put the empty state back.
                _part.Detach();
                Present(sheet);
                break;

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
