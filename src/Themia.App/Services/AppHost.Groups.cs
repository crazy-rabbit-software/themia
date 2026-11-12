using Themia.Core.Model;
using Themia.Core.Shell;

namespace Themia.App.Services;

/// <summary>
/// [S-2451]..[S-2456] Groups in edit mode: a press, a click, Ctrl+click, Escape and Tab by the group
/// rules (<see cref="WidgetSelection"/>), and Group, Ungroup and Remove from group, each one undo step.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>
    /// [S-2451] A press on a widget's card selects by the group rules. Returns whether the selection
    /// changed; a press that kept it lets the release (<see cref="ClickSelect"/>) decide.
    /// </summary>
    public bool PressSelect(string widgetId, bool onHandle)
    {
        if (Find(widgetId) is not var (view, _))
        {
            return false;
        }

        var changed = onHandle ? Selection.PressHandle(view, widgetId) : Selection.Press(view, widgetId);
        ChangeSelection(changed);
        return changed;
    }

    /// <summary>[S-2451] Released without moving: enter the group, or narrow to the widget's unit.</summary>
    public void ClickSelect(string widgetId)
    {
        if (Find(widgetId) is var (view, _))
        {
            ChangeSelection(Selection.Click(view, widgetId));
        }
    }

    /// <summary>[S-2451] Escape: out of an entered group to the group, else nothing selected. False when nothing was selected.</summary>
    public bool StepOutOfSelection()
    {
        if (Selection.IsEmpty)
        {
            return false;
        }

        ChangeSelection(Selection.StepOut(SelectionView));
        return true;
    }

    /// <summary>[S-2451] Tab on a view: the next unit, or the next member inside an entered group. The primary widget after, or null.</summary>
    public string? CycleSelection(ViewState view, bool backwards, IReadOnlySet<string>? hidden)
    {
        ArgumentNullException.ThrowIfNull(view);
        ChangeSelection(Selection.Cycle(view, backwards, hidden));
        return Selection.Primary;
    }

    /// <summary>[S-2454] What the card menu offers about groups for the selection now.</summary>
    public (bool Group, bool Ungroup, bool Leave) GroupCommands()
    {
        if (SelectionView is not { } view)
        {
            return default;
        }

        var entered = Selection.Entered is not null;
        return (
            !entered && WidgetGroups.CanGroup(view, Selection.Ids),
            !entered && WidgetGroups.CanUngroup(view, Selection.Ids),
            entered);
    }

    /// <summary>[S-2454] Ctrl+G: one group of the selection; the new group is selected whole.</summary>
    public void GroupSelected()
    {
        if (SelectionView is not { } view || Selection.Entered is not null || !WidgetGroups.CanGroup(view, Selection.Ids))
        {
            return;
        }

        var ids = Selection.Ids.ToList();
        WidgetGroup? made = null;
        Mutate(_ => made = WidgetGroups.Group(view, ids), relayout: true, undo: UndoAction.Group);
        if (made is not null)
        {
            SelectMany(view.Id, WidgetGroups.Members(view, made.Id).Select(w => w.Id));
        }
    }

    /// <summary>[S-2454] Ctrl+Shift+G: every group the selection touches dissolves; the widgets stay selected.</summary>
    public void UngroupSelected()
    {
        if (SelectionView is not { } view)
        {
            return;
        }

        var ids = Selection.Entered is { } entered ? WidgetGroups.Members(view, entered).Select(w => w.Id).ToList() : [.. Selection.Ids];
        if (!WidgetGroups.CanUngroup(view, ids))
        {
            return;
        }

        Mutate(_ => WidgetGroups.Ungroup(view, ids), undo: UndoAction.Ungroup);
        SelectMany(view.Id, ids);
    }

    /// <summary>[S-2454] "Remove from group": the entered member leaves its group and is selected alone.</summary>
    public void LeaveGroupSelected()
    {
        if (SelectionView is not { } view || Selection.Entered is null || Selection.Primary is not { } id)
        {
            return;
        }

        Mutate(_ => WidgetGroups.RemoveFromGroup(view, id), undo: UndoAction.Ungroup);
        SelectMany(view.Id, [id]);
    }

    /// <summary>[S-2453] Whether a card draws its own edit markers: a lone widget, or the member of the entered group that is selected.</summary>
    public bool ShowsOwnMarkers(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return widget.Group is null
            || (string.Equals(Selection.Entered, widget.Group, StringComparison.Ordinal) && Selection.Contains(widget.Id));
    }

    /// <summary>[S-2454] The card menu's facts about groups, folded into the menu's.</summary>
    public CardMenuFacts WithGroups(CardMenuFacts facts)
    {
        var (group, ungroup, leave) = GroupCommands();
        return facts with { CanGroup = group, CanUngroup = ungroup, CanLeaveGroup = leave };
    }
}
