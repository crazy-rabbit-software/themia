using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-1580] [S-1593] [S-1973] The selection's gestures in edit mode: a press, a click, Escape and Tab, by the
/// selection's rules (<see cref="WidgetSelection"/>). There are no groups: every widget is selected on its own,
/// and several together with Ctrl or Shift or the lasso.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>
    /// [S-1593] A press on a widget's card: the widget alone, or a selection that holds it kept so the drag carries
    /// all of it; a press on a resize handle is about that widget alone. Returns whether the selection changed; a
    /// press that kept it lets the release (<see cref="ClickSelect"/>) decide.
    /// </summary>
    public bool PressSelect(string widgetId, bool onHandle)
    {
        if (Find(widgetId) is not var (view, _))
        {
            return false;
        }

        var changed = onHandle ? Selection.Set(view.Id, widgetId) : Selection.Press(view, widgetId);
        ChangeSelection(changed);
        return changed;
    }

    /// <summary>[S-1593] Released without moving after a press that kept the selection: narrow to the widget.</summary>
    public void ClickSelect(string widgetId)
    {
        if (Find(widgetId) is var (view, _))
        {
            ChangeSelection(Selection.Click(view, widgetId));
        }
    }

    /// <summary>[S-1583] Escape: nothing selected. False when nothing was selected.</summary>
    public bool StepOutOfSelection()
    {
        if (Selection.IsEmpty)
        {
            return false;
        }

        ChangeSelection(Selection.Clear());
        return true;
    }

    /// <summary>[S-1973] Tab on a view: the next widget in reading order. The primary widget after, or null.</summary>
    public string? CycleSelection(ViewState view, bool backwards, IReadOnlySet<string>? hidden)
    {
        ArgumentNullException.ThrowIfNull(view);
        ChangeSelection(Selection.Cycle(view, backwards, hidden));
        return Selection.Primary;
    }
}
