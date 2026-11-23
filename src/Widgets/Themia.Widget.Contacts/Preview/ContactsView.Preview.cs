using Themia.App.Shell.Gallery;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2053]): <c>sample</c> is the contacts' sample (<see cref="ShowSampleAsync"/>); <c>search</c>
/// with the keys in the box, <c>searching</c> with a name typed; <c>nobox</c> the sample with the box off, <c>opened</c> the same
/// after the card menu's Search; [S-15776] WR-10's states, the face kept: <c>none</c> (an account with no people),
/// <c>noAccount</c> (no account offers contacts) and <c>offline</c> (the one account could not be reached). Each arrangement is
/// the style's (<c>--style</c>).
/// </summary>
internal sealed partial class ContactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "search", "searching", "nobox", "opened", "none", "noAccount", "offline"];

    /// <summary>[S-11603] [S-14500] A state without the box is sized without it.</summary>
    public Themia.Core.Model.WidgetConfig? DrawnConfig { get; private set; }

    public async Task ShowPreviewStateAsync(string state)
    {
        _config.SearchBox = state is not ("nobox" or "opened");
        DrawnConfig = _config.SearchBox ? null : _config;
        if (state is "none" or "noAccount" or "offline")
        {
            // A load held as if it had come back; the feed stopped so no real load replaces it.
            _feed.Stop();
            _feed.Held = null;
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

/// <summary>Debug renders only: <c>sample</c> (the first of the cast), <c>nobody</c> (none picked yet).</summary>
internal sealed partial class ContactView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "nobody"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        _feed.Held = ContactsView.SamplePeople();
        _sample = false;
        Render();
        return Task.CompletedTask;
    }
}
