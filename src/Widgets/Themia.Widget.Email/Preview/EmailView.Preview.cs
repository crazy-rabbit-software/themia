#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-2056]): <c>--widget-png Email --state messages</c> draws the
/// sample (<see cref="ShowSampleAsync"/>). <c>accounts</c> reads two accounts, so the compose button asks
/// which; <c>empty</c> is an empty folder, which still offers compose. <c>focused</c> is the messages
/// with the second row under the keyboard, so its quick actions show ([S-1284]). <c>stale</c> is the
/// messages with the last refresh of the account failed (no network), the list held from 20 minutes
/// ago; <c>offline</c> the same failure with nothing held.
/// </summary>
internal sealed partial class EmailView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["messages", "accounts", "empty", "focused", "stale", "offline"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (state == "messages")
        {
            return ShowSampleAsync();
        }

        HoldSample(
            twoAccounts: state == "accounts",
            empty: state is "empty" or "offline",
            failed: state is "stale" or "offline");

        if (state == "focused")
        {
            _binding.List.Focus(1);
        }

        return Task.CompletedTask;
    }
}
#endif
