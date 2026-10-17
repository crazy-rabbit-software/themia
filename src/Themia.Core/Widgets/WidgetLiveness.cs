using Themia.Core.Accounts;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// One widget slot of a surface after a layout pass: the widget, and the card already showing it
/// (null when one has to be built).
/// </summary>
public readonly record struct SurfaceSlot<T>(WidgetInstance Model, T? Existing)
    where T : class;

/// <summary>What a layout pass does to a surface's cards: release some, then keep or build one per widget shown.</summary>
public sealed record SurfaceChange<T>(IReadOnlyList<T> Release, IReadOnlyList<SurfaceSlot<T>> Slots)
    where T : class;

/// <summary>
/// [S-1880]..[S-1882] Which widgets are live: attached, ticking, polling and holding resources.
/// <para>
/// Only a widget in a view some display shows, and only once it is set up. A view no display shows
/// costs its JSON and nothing else: no card, no ticker subscription, no service subscriber, no cache
/// warmed for it. Shared services start and stop with their subscribers, so a service whose widgets
/// are all in hidden views does no work at all.
/// </para>
/// </summary>
public static class WidgetLiveness
{
    /// <summary>
    /// [S-1880] The views on screen, one per display in <paramref name="displayIds"/> (each display's
    /// own assignment, or the active view: <see cref="AppState.ViewForMonitor"/>), each view once even
    /// when two displays show it.
    /// </summary>
    public static IReadOnlyList<ViewState> ShownViews(AppState state, IEnumerable<string> displayIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(displayIds);

        var shown = new List<ViewState>();
        foreach (var id in displayIds)
        {
            var view = state.ViewForMonitor(id);
            if (!shown.Contains(view))
            {
                shown.Add(view);
            }
        }

        return shown;
    }

    /// <summary>
    /// [S-1880] Every widget that runs: those of the shown views that <see cref="Runs"/>. Everything
    /// else in the state is data only.
    /// </summary>
    public static IReadOnlyList<WidgetInstance> Live(AppState state, IEnumerable<string> displayIds, AccountFeatures signedIn)
        => [.. ShownViews(state, displayIds).SelectMany(v => v.Widgets).Where(w => Runs(w.Config, signedIn))];

    /// <summary>
    /// [S-1881] Whether a widget on screen runs its view. One that needs setting up
    /// (<see cref="WidgetSetup.Needed"/>) shows only the Set up button and does not: it has nothing
    /// to poll, nothing to tick and nothing to draw until the user configures it. The one exception
    /// is a widget the user sets up by using it: an empty Shortcuts widget takes a dropped file
    /// under its button ([S-1862]), so it keeps running (it polls nothing).
    /// </summary>
    public static bool Runs(WidgetConfig config, AccountFeatures signedIn)
    {
        ArgumentNullException.ThrowIfNull(config);
        return !WidgetSetup.Needed(config, signedIn) || SetUpInPlace(config);
    }

    /// <summary>[S-1881] A widget whose setup is done on the widget itself, under its Set up button.</summary>
    public static bool SetUpInPlace(WidgetConfig config) => config is ISetupRule { SetsUpInPlace: true };

    /// <summary>
    /// [S-1882] Match the cards a surface has to the widgets it must show. A card is kept only for the
    /// <i>same</i> widget object (an undo that brings a widget back builds a new card); every other
    /// card is released. The release comes first so the old view's cards are gone before the new
    /// view's are built, and the two never sit in memory together.
    /// </summary>
    public static SurfaceChange<T> Reconcile<T>(IReadOnlyList<T> current, Func<T, WidgetInstance> modelOf, IReadOnlyList<WidgetInstance> wanted)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(modelOf);
        ArgumentNullException.ThrowIfNull(wanted);

        var byId = new Dictionary<string, T>(StringComparer.Ordinal);
        var release = new List<T>();
        foreach (var card in current)
        {
            // A second card for one id cannot be kept: only one can stand for the widget.
            if (!byId.TryAdd(modelOf(card).Id, card))
            {
                release.Add(card);
            }
        }

        var slots = new List<SurfaceSlot<T>>(wanted.Count);
        foreach (var model in wanted)
        {
            if (byId.Remove(model.Id, out var existing) && ReferenceEquals(modelOf(existing), model))
            {
                slots.Add(new SurfaceSlot<T>(model, existing));
                continue;
            }

            if (existing is not null)
            {
                release.Add(existing);
            }

            slots.Add(new SurfaceSlot<T>(model, null));
        }

        release.AddRange(byId.Values);
        return new SurfaceChange<T>(release, slots);
    }
}
