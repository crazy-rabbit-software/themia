using System.Runtime.CompilerServices;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-2447] The sizing each widget had when it was last noted, by instance: what a config change is
/// compared with. A widget whose sizing changed since (its config changed: a style, a count, a size) has
/// its stored rect fitted into the new one on the display that shows its view; a widget first seen
/// (loaded, brought back by an undo) is noted as it is: being shown never rewrites a rect ([S-2446]).
/// [S-854] While the surface showing its view has not settled, the refit waits (the old sizing stays
/// noted) and runs at the first refit on settled metrics. [S-14103] A widget loaded with a stepped axis
/// (<see cref="NoteAll"/> with <c>fit</c>) has its stored rect fitted to its nearest step once, at that first
/// refit, logged: a card stored before its steps were whole cells keeps no leftover cell in its stored rect. [S-14104]
/// Every refit moves the cards joined to the edge it moved with it (<see cref="RefitJoins"/>), so joins hold.
/// </summary>
public sealed class SizingNotes
{
    private const string Scope = "sizing";

    private readonly ConditionalWeakTable<WidgetInstance, WidgetSizing> _noted = [];

    /// <summary>[S-14103] The loaded widgets whose stored rect is fitted to their steps at the first refit on settled metrics.</summary>
    private readonly ConditionalWeakTable<WidgetInstance, object> _unfitted = [];

    private static readonly object Unfitted = new();

    /// <summary>
    /// Note every widget's sizing as it is, refitting nothing. [S-14103] <paramref name="fit"/> (the state as loaded):
    /// a widget with a stepped axis is fitted to its nearest step at the first refit on settled metrics.
    /// </summary>
    public void NoteAll(AppState state, bool fit = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var widget in state.Views.SelectMany(v => v.Widgets))
        {
            var sizing = SizeFit.Of(widget);
            _noted.AddOrUpdate(widget, sizing);
            if (fit && Stepped(widget, sizing))
            {
                _unfitted.AddOrUpdate(widget, Unfitted);
            }
        }
    }

    /// <summary>[S-14103] A widget whose sizing steps an axis (a list's rows, a month's weeks): what a load fits to its steps.</summary>
    private static bool Stepped(WidgetInstance widget, WidgetSizing sizing)
        => widget.Config is not IDesktopItemConfig && (sizing.HeightSteps is not null || sizing.WidthSteps is not null);

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
                var loaded = _unfitted.TryGetValue(widget, out _);

                // [S-11887] A desktop icon's box is the desktop grid's (DesktopGrid: the icon size's two-line
                // cell, laid with the others), never refitted one by one at its own corner: that let icons of
                // a column grow into each other.
                if (((_noted.TryGetValue(widget, out var was) && was != sizing) || loaded) && at >= 0 && widget.Config is not IDesktopItemConfig)
                {
                    var surface = surfaces[at];
                    if (!surface.Writable)
                    {
                        continue;
                    }

                    var stored = widget.Rect;
                    if (SizeFit.Refit(widget, surface.Screen, WidgetScale.For(widget, surface.DisplayScale)))
                    {
                        changed++;

                        // [S-14103] A loaded card fitted to its steps says so: what it stored and what it is now.
                        if (loaded)
                        {
                            Log.Info(Scope, $"{widget.Config.Kind} {widget.Id} fitted to its steps: {stored.W:0.####}x{stored.H:0.####} -> {widget.Rect.W:0.####}x{widget.Rect.H:0.####}");
                        }

                        // [S-14104] The cards it touched on the edge that moved move with it: what was joined stays joined.
                        changed += Follow(view.Widgets, widget, stored, surface.Screen);
                    }
                }

                _unfitted.Remove(widget);
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
            && ((_noted.TryGetValue(w, out var was) && was != SizeFit.Of(w)) || _unfitted.TryGetValue(w, out _)));
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
