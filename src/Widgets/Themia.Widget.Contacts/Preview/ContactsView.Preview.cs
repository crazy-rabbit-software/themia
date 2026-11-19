#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2053]): <c>sample</c> is the contacts' sample (<see cref="ShowSampleAsync"/>), so a render
/// can be matched with its mockup. [S-13950] The search box over the sample: <c>search</c> with the keys in it, <c>searching</c>
/// with a name typed; <c>nobox</c> the sample with the box off, <c>opened</c> the same after the card menu's Search.
/// </summary>
internal sealed partial class ContactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "search", "searching", "nobox", "opened"];

    public async Task ShowPreviewStateAsync(string state)
    {
        _config.SearchBox = state is not ("nobox" or "opened");
        await ShowSampleAsync().ConfigureAwait(true);
        if (state is "search" or "searching" or "opened")
        {
            OpenSearch();
            if (state == "searching")
            {
                _search.Text = "an";
            }
        }
    }
}
#endif
