using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2053]): <c>sample</c> is the contacts' sample (<see cref="ShowSampleAsync"/>), so a render
/// can be matched with its mockup. [S-13950] The search box over the sample: <c>search</c> with the keys in it, <c>searching</c>
/// with a name typed; <c>nobox</c> the sample with the box off, <c>opened</c> the same after the card menu's Search (the line in
/// the list's first line), <c>openedTyping</c> with a name typed into it, <c>openedClosed</c> after it was emptied and closed.
/// [S-14814] <c>compact</c>, <c>details</c> and <c>grid</c>: the sample in each other layout, the box off, so the checks judge
/// every look of the people (the layout is a look the catalog lists as no style). [S-15776] WR-10's states, the face kept:
/// <c>none</c> (an account with no people), <c>noAccount</c> (no account offers contacts: the card's Set up line over the
/// empty rows) and <c>offline</c> (the one account could not be reached: its sentence in the first row's place).
/// </summary>
internal sealed partial class ContactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "search", "searching", "nobox", "opened", "openedTyping", "openedClosed", "compact", "details", "grid", "none", "noAccount", "offline"];

    /// <summary>[S-11603] [S-14500] A state without the box is sized without it: the rows' card, no band over them.</summary>
    public Themia.Core.Model.WidgetConfig? DrawnConfig { get; private set; }

    public async Task ShowPreviewStateAsync(string state)
    {
        _config.SearchBox = state is not ("nobox" or "opened" or "openedTyping" or "openedClosed" or "compact" or "details" or "grid");
        _config.Layout = state switch
        {
            "compact" => ContactsLayout.Compact,
            "details" => ContactsLayout.Details,
            "grid" => ContactsLayout.Grid,
            _ => _config.Layout,
        };
        DrawnConfig = _config.SearchBox ? null : _config;
        if (state is "none" or "noAccount" or "offline")
        {
            // A load held as if it had come back; the feed stopped so no real load replaces it.
            _feed.Stop();
            _held = null;
            _feed.Hold(state switch
            {
                "none" => new OfficeLoad<IReadOnlyList<Contact>>(OfficeLoadState.Asked, [], true, [], new Dictionary<string, string>()),
                "noAccount" => OfficeLoad<IReadOnlyList<Contact>>.Of(OfficeLoadState.NoAccounts),
                _ => new OfficeLoad<IReadOnlyList<Contact>>(OfficeLoadState.Asked, null, false, [new OfficeProblem("work", FetchStatus.Offline, null)], new Dictionary<string, string> { ["work"] = "Work" }),
            });
            Render();
            return;
        }

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
