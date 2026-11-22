using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.App.Shell;

/// <summary>
/// [S-15502] [S-15525] [S-15526] The repeaters on this surface: each copied row's widgets drawn as cards of their own
/// (a card a copy, over a model that is not stored, its config the template's), every card's config as its row shows it,
/// and a press on a copy (or, on the locked desktop, on any row) standing for the repeater.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-15502] What this view's repeaters draw, read once per layout.</summary>
    private RepeaterPlan _repeaterPlan = RepeaterPlan.Empty;

    /// <summary>[S-15502] The plan's copies by key: what a copy's card resolves its config by, even while it is being built.</summary>
    private Dictionary<string, RepeaterCopy> _planCopies = new(StringComparer.Ordinal);

    /// <summary>[S-15502] The cards drawing copies, by key; kept from one layout to the next so a copy's view is not rebuilt.</summary>
    private readonly Dictionary<string, WidgetCard> _copies = new(StringComparer.Ordinal);

    /// <summary>
    /// [S-15533] While a move is drawn as if let go: the view it would leave (the moving widgets where they land, joined or
    /// left), whose repeaters the copies show; null otherwise (the stored view).
    /// </summary>
    private ViewState? _planView;

    /// <summary>[S-15502] The cards drawing copies now.</summary>
    internal IEnumerable<WidgetCard> CopyCards => _copies.Values;

    /// <summary>[S-15502] Read what the repeaters of <paramref name="view"/> draw, before any card is built or given its config.</summary>
    private void PlanRepeaters(ViewState view)
    {
        _planView = null;
        _repeaterPlan = AppHost.RepeaterPlanOf(view, Metrics);
        _planCopies = _repeaterPlan.Copies.ToDictionary(c => c.Key, StringComparer.Ordinal);
    }

    /// <summary>
    /// [S-15529] [S-15660] The parts that show something else after <paramref name="change"/> though their own config stands:
    /// every part of a container whose config changed (its place, its first day, its layout), and every part whose row is
    /// another than in <paramref name="before"/> (it joined, it left, the days scrolled).
    /// </summary>
    private HashSet<string> RowsChanged(ViewState view, StateChange change, IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> before)
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        if (before.Count == 0 && _repeaterPlan.Templates.Count == 0)
        {
            return changed;
        }

        foreach (var on in Repeaters.On(view).Where(on => change.Reapplies(on.Widget.Id)))
        {
            changed.UnionWith(Repeaters.Descendants(view, on.Widget.Id));
        }

        var after = _repeaterPlan.Templates;
        foreach (var id in before.Keys.Union(after.Keys, StringComparer.Ordinal))
        {
            var was = before.TryGetValue(id, out var b) ? b : null;
            var now = after.TryGetValue(id, out var a) ? a : null;
            if (was is null || now is null || was.Count != now.Count || !was.All(p => now.TryGetValue(p.Key, out var v) && v == p.Value))
            {
                changed.Add(id);
            }
        }

        return changed;
    }

    /// <summary>
    /// [S-15533] A step of <paramref name="move"/> landed on another cell: the repeaters drawn as if it were let go there (a
    /// member moved on its template, a widget joining or leaving, a repeater moved with its rows), the copies laid again at
    /// once. The copies the move carries itself (a pressed copy) follow the pointer. Nothing is stored until the drop.
    /// </summary>
    private void PreviewRepeaters(MoveSession move)
    {
        var view = ViewForThisMonitor(_host.State);
        if (!view.Widgets.Exists(w => w.Config is IRepeater) || !Metrics.IsUsable)
        {
            return;
        }

        var landed = new Dictionary<string, FracRect>(StringComparer.Ordinal);
        foreach (var (moving, start) in move.Cards)
        {
            if (!IsCopy(moving))
            {
                landed[moving.Model.Id] = move.Block.At(start, move.Block.Landing);
            }
        }

        var before = _repeaterPlan.Templates;
        var (scratch, plan) = Repeaters.Preview(view, landed, Metrics, _repeaterPlan.Values);
        _planView = scratch;
        _repeaterPlan = plan;
        _planCopies = plan.Copies.ToDictionary(c => c.Key, StringComparer.Ordinal);
        LayCopies(live: [.. move.Cards.Select(c => c.Card)]);
        Reshown(before);

        // [S-13917] While a layout is being edited, a part dragged out of its day is muted: let go there, it is removed.
        if (_composing is { } id && view.Find(id)?.Config is IRepeater held && scratch.Find(id)?.Config is IRepeater lands)
        {
            foreach (var (moving, _) in move.Cards.Where(c => !IsCopy(c.Card) && held.Members.Contains(c.Card.Model.Id)))
            {
                moving.SetMuted(!lands.Members.Contains(moving.Model.Id));
            }
        }
    }

    /// <summary>
    /// [S-15533] The move is over (landed, handed off or undone): the copies are the stored view's again (a drop's change lays
    /// them once more).
    /// </summary>
    private void EndRepeaterPreview()
    {
        if (_planView is null || _closed)
        {
            return;
        }

        var before = _repeaterPlan.Templates;
        PlanRepeaters(ViewForThisMonitor(_host.State));
        LayCopies(live: []);
        Reshown(before);
    }

    /// <summary>
    /// [S-15533] What the plan changed on real cards: each repeater's own view (its slots' outlines, its status) and each template
    /// member whose row changed (one joining shows its first row's value, one leaving its own).
    /// </summary>
    private void Reshown(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> before)
    {
        foreach (var card in _cards.Where(c => c.Model.Config is IRepeater))
        {
            card.ApplyConfig();
        }

        var after = _repeaterPlan.Templates;
        foreach (var id in before.Keys.Union(after.Keys, StringComparer.Ordinal))
        {
            var was = before.TryGetValue(id, out var b) ? b : null;
            var now = after.TryGetValue(id, out var a) ? a : null;
            if (was is null || now is null || !SamePath(was, now))
            {
                CardFor(id)?.ApplyConfig();
            }
        }

        static bool SamePath(IReadOnlyDictionary<string, int> x, IReadOnlyDictionary<string, int> y)
            => x.Count == y.Count && x.All(p => y.TryGetValue(p.Key, out var v) && v == p.Value);
    }

    /// <summary>
    /// [S-15502] The copies' cards made to match the plan: a copy no longer drawn is released, a new one built, each one given
    /// its template's arrangement and config, its row's place, its template's paint order and the edit mode's look ([S-15534]:
    /// muted in edit mode). [S-15533] <paramref name="live"/>, during a move: a copy already drawn only takes its new place (its
    /// key is its row, so its value is the same), and the copies among <paramref name="live"/> (the move carries them) are left
    /// to the move; null outside a move.
    /// </summary>
    private void LayCopies(IReadOnlyCollection<WidgetCard>? live = null)
    {
        foreach (var key in _copies.Keys.Where(k => !_planCopies.ContainsKey(k)).ToList())
        {
            var gone = _copies[key];
            _copies.Remove(key);
            _host.KeyboardRemoved(gone);
            gone.Detach();
            _widgetLayer.Children.Remove(gone);
        }

        foreach (var copy in _repeaterPlan.Copies)
        {
            if (!_copies.TryGetValue(copy.Key, out var card) || !ReferenceEquals(card.Model.Config, copy.Template.Config))
            {
                if (card is not null)
                {
                    card.Detach();
                    _widgetLayer.Children.Remove(card);
                }

                var model = new WidgetInstance { Id = copy.Key, Config = copy.Template.Config };
                model.CopyArrangementFrom(copy.Template);
                model.Rect = copy.Rect;
                card = new WidgetCard(this, model);
                _widgetLayer.Children.Add(card);
                _copies[copy.Key] = card;
            }
            else if (live is not null)
            {
                if (live.Contains(card))
                {
                    continue;
                }

                Canvas.SetZIndex(card, CardFor(copy.Template.Id) is { } lifted ? Canvas.GetZIndex(lifted) : 0);
                if (card.Model.Rect != copy.Rect)
                {
                    card.Model.Rect = copy.Rect;
                    PositionCard(card, card.Model.Rect);
                }

                continue;
            }
            else
            {
                card.Model.CopyArrangementFrom(copy.Template);
                card.Model.Rect = copy.Rect;
                card.ApplyConfig();
            }

            Canvas.SetZIndex(card, CardFor(copy.Template.Id) is { } template ? Canvas.GetZIndex(template) : 0);
            card.Visibility = Visibility.Visible;
            card.ApplyEditMode(EditMode);
            card.SetMuted(EditMode);
            card.ApplyContentScale(ContentScale(card.Model));
            PositionCard(card, card.Model.Rect);
        }

        SyncDayBox();
    }

    /// <summary>[S-15501] Edit mode came or went: the repeaters' own views draw their outlines or not, the copies take its look.</summary>
    private void RefreshRepeaters()
    {
        // [S-13917] Leaving edit mode ends a layout's editing.
        if (!EditMode && _composing is not null)
        {
            EndCompose();
            return;
        }

        foreach (var card in _cards.Where(c => c.Model.Config is IRepeater))
        {
            card.ApplyConfig();
            card.ApplySelection();
        }

        foreach (var card in _copies.Values)
        {
            card.ApplyEditMode(EditMode);
            card.SetMuted(EditMode);
        }

        SyncDayBox();
    }

    /// <summary>Every copy's card released (the surface closes or suspends).</summary>
    private void ReleaseCopies()
    {
        foreach (var card in _copies.Values)
        {
            card.Detach();
            _widgetLayer.Children.Remove(card);
        }

        _copies.Clear();
        _planCopies.Clear();
        _repeaterPlan = RepeaterPlan.Empty;
    }

    /// <summary>
    /// [S-15511] [S-15501] <paramref name="card"/>'s config as its row shows it: a copy's, the template's with the row's values in
    /// its bound slots; a real member's, its first row's; a repeater's, with what its own view draws. Anything else as it is.
    /// </summary>
    public WidgetConfig SeriesConfig(WidgetCard card, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_closed || (_repeaterPlan.Values.Count == 0 && config is not IRepeater))
        {
            return config;
        }

        var view = _planView ?? ViewForThisMonitor(_host.State);
        if (_planCopies.TryGetValue(card.Model.Id, out var copy))
        {
            return Repeaters.Resolve(view, copy.Template, config, copy.Path, _repeaterPlan.Values);
        }

        if (config is IRepeater && view.Find(card.Model.Id) is { Config: IRepeater repeater } widget)
        {
            var shown = config.Clone();
            ((IRepeater)shown).Shown = Repeaters.ShownOf(view, new RepeaterOn(widget, repeater), Metrics, _repeaterPlan.Values, EditMode);
            return shown;
        }

        return _repeaterPlan.Templates.TryGetValue(card.Model.Id, out var path)
            ? Repeaters.Resolve(view, card.Model, config, path, _repeaterPlan.Values)
            : config;
    }

    /// <summary>
    /// [S-15525] [S-15526] The card a press on <paramref name="card"/> stands for: a copy's, the repeater whose copied row it is in
    /// (edit mode) or the outermost repeater (the locked desktop); on the locked desktop a real member's, its outermost
    /// repeater too: a drag on any row moves the whole repeater. Any other card stands for itself.
    /// </summary>
    private WidgetCard Stand(WidgetCard card)
    {
        if (_closed)
        {
            return card;
        }

        // [S-15526] [S-13917] The rules are Core's (LayoutEditing.Stands): a part stands for itself only while its container's
        // layout is edited; a copied day always for its container.
        var view = ViewForThisMonitor(_host.State);
        var copied = _planCopies.TryGetValue(card.Model.Id, out var copy);
        var id = LayoutEditing.Stands(view, copied ? copy!.Template.Id : card.Model.Id, copied ? copy!.Owner : null, EditMode, _composing);
        return id is not null && CardFor(id) is { } stands ? stands : card;
    }

    /// <inheritdoc />
    public bool HeldByContainer(WidgetCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return _planCopies.ContainsKey(card.Model.Id) || _repeaterPlan.Templates.ContainsKey(card.Model.Id);
    }

    /// <summary>Whether <paramref name="card"/> draws a copy.</summary>
    private bool IsCopy(WidgetCard card) => _planCopies.ContainsKey(card.Model.Id);
}
