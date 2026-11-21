#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-15101]): a family's own states (its sample, none, ...), and <c>long</c>: a text
/// longer than any card holds, so a render shows the line cut with its ellipsis at the room's end.
/// </summary>
public abstract partial class AtomTextView : IWidgetPreviewState
{
    /// <summary>The family's own render states, in its order.</summary>
    protected virtual IReadOnlyList<string> OwnPreviewStates => [];

    /// <summary>Show one of <see cref="OwnPreviewStates"/>.</summary>
    protected virtual Task ShowOwnPreviewStateAsync(string state) => Task.CompletedTask;

    public IReadOnlyList<string> PreviewStates => [.. OwnPreviewStates, "long"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (state == "long")
        {
            if (OwnPreviewStates.Contains("sample"))
            {
                await ShowOwnPreviewStateAsync("sample");
            }

            var text = string.Join(' ', Enumerable.Repeat(_shown.Text.Length > 0 ? _shown.Text : "Northern Lights", 6));
            Present(_shown with { Text = text, Available = true });
            return;
        }

        await ShowOwnPreviewStateAsync(state);
    }
}
#endif
