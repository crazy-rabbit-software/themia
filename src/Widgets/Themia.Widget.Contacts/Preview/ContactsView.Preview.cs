#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2053]): <c>sample</c> is the contacts' sample (<see cref="ShowSampleAsync"/>), so a render
/// can be matched with its mockup. [S-13950] The search box over the sample: <c>search</c> with the keys in it, <c>searching</c>
/// with a name typed; <c>nobox</c> the sample with the box off, <c>opened</c> the same after the card menu's Search (the line in
/// the list's first line), <c>openedTyping</c> with a name typed into it, <c>openedClosed</c> after it was emptied and closed.
/// </summary>
internal sealed partial class ContactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "search", "searching", "nobox", "opened", "openedTyping", "openedClosed"];

    /// <summary>[S-11603] [S-14500] A state without the box is sized without it: the rows' card, no band over them.</summary>
    public Themia.Core.Model.WidgetConfig? DrawnConfig { get; private set; }

    public async Task ShowPreviewStateAsync(string state)
    {
        _config.SearchBox = state is not ("nobox" or "opened" or "openedTyping" or "openedClosed");
        DrawnConfig = _config.SearchBox ? null : _config;
        await ShowSampleAsync().ConfigureAwait(true);
        if (state is "search" or "searching")
        {
            OpenSearch();
            if (state == "searching")
            {
                _search.Text = "an";
            }
        }
        else if (state is "opened" or "openedTyping" or "openedClosed")
        {
            // The card menu's Search; then words typed; then Escape twice (empties it, folds it away).
            OpenSearch();
            if (state != "opened")
            {
                _lineSearch.Text = "an";
            }

            if (state == "openedClosed")
            {
                _lineSearch.Text = string.Empty;
                CloseLineSearch();
            }
        }
    }
}
#endif
