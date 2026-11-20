using System.Runtime.CompilerServices;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-2447] The sizing each widget had when it was last noted, by instance: what a config change is
/// compared with. A widget whose sizing changed since (its config changed: a style, its rows) has
/// its stored rect fitted into the new one on the display that shows its view (and, [S-14503], put at its new natural
/// where only that moved); a widget first seen (loaded, brought back by an undo) is noted as it is: being shown never
/// rewrites a rect ([S-2446]). [S-854] While the surface showing its view has not settled, the refit waits (the old
/// sizing stays noted) and runs at the first refit on settled metrics. [S-14104] Every refit moves the cards joined to
/// the edge it moved with it (<see cref="RefitJoins"/>), so joins hold.
/// </summary>
public sealed class SizingNotes
{
    private const string Scope = "sizing";

    private readonly ConditionalWeakTable<WidgetInstance, WidgetSizing> _noted = [];

    /// <summary>Note every widget's sizing as it is, refitting nothing.</summary>
    public void NoteAll(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var widget in state.Views.SelectMany(v => v.Widgets))
        {
            _noted.AddOrUpdate(widget, SizeFit.Of(widget));
        }
    }

    /// <summary>
    /// [S-2447] [S-854] Refit every widget whose sizing changed since it was noted, on the first of
    /// <paramref name="surfaces"/> that shows its view, and note every sizing. One whose view no surface
    /// shows is noted as it is; one whose surface has not settled is left noted as it was, so it refits
    /// once that surface has. Returns how many rects changed.
    /// </summary>
    public int Refit(AppState state, IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(surfaces);
        var changed = 0;
        foreach (var view in state.Views)
        {
            var at = IndexOf(surfaces, view.Id);
            foreach (var widget in view.Widgets)
            {
                var sizing = SizeFit.Of(widget);

                // [S-11887] A desktop icon's box is the desktop grid's (DesktopGrid: the icon size's two-line
                // cell, laid with the others), never refitted one by one at its own corner: that let icons of
                // a column grow into each other.
                if (_noted.TryGetValue(widget, out var was) && was != sizing && at >= 0 && widget.Config is not IDesktopItemConfig)
                {
                    var surface = surfaces[at];
                    if (!surface.Writable)
                    {
                        continue;
                    }

                    var stored = widget.Rect;
                    if (SizeFit.Refit(widget, surface.Screen, WidgetScale.For(widget, surface.DisplayScale), was))
                    {
                        changed++;

                        // [S-14104] The cards it touched on the edge that moved move with it: what was joined stays joined.
                        changed += Follow(view.Widgets, widget, stored, surface.Screen);
                    }
                }

                _noted.AddOrUpdate(widget, sizing);
            }
        }

        return changed;
    }

    /// <summary>[S-14104] Move the cards joined to <paramref name="refit"/>'s moved edge with it; one log line a group. Returns how many moved.</summary>
    private static int Follow(IReadOnlyList<WidgetInstance> widgets, WidgetInstance refit, FracRect before, ScreenMetrics screen)
    {
        var moved = 0;
        foreach (var shift in RefitJoins.Follow(widgets, refit, before, screen.Allowed))
        {
            var dip = shift.Delta * (shift.Vertical ? screen.Height : screen.Width);
            var edge = shift.Vertical ? "bottom" : "right";
            if (shift.Kept is { } why)
            {
                Log.Info(Scope, $"the cards joined to {refit.Config.Kind} {refit.Id}'s {edge} edge stay: moving them {dip:0.#} DIP would take them {why}");
                continue;
            }

            moved += shift.Moved.Count;
            Log.Info(Scope, $"{shift.Moved.Count} card(s) joined to {refit.Config.Kind} {refit.Id}'s {edge} edge moved {dip:0.#} DIP with it: {string.Join(", ", shift.Moved.Select(w => w.Id))}");
        }

        return moved;
    }

    /// <summary>[S-854] Whether a widget's refit is waiting: its sizing is not the one noted.</summary>
    public bool Waits(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Views.SelectMany(v => v.Widgets).Any(w => w.Config is not IDesktopItemConfig
            && _noted.TryGetValue(w, out var was) && was != SizeFit.Of(w));
    }

    private static int IndexOf(IReadOnlyList<SurfaceMetrics> surfaces, string viewId)
    {
        for (var i = 0; i < surfaces.Count; i++)
        {
            if (string.Equals(surfaces[i].ViewId, viewId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
