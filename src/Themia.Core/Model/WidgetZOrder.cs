namespace Themia.Core.Model;

/// <summary>
/// [S-874] Which widget paints over which, and the two commands that change it.
/// <para>
/// The order is the view's widget list: later paints over earlier. It is persisted because the list
/// is, so "bring to front" survives a restart without a second index that could disagree with it.
/// </para>
/// </summary>
public static class WidgetZOrder
{
    /// <summary>Whether <see cref="BringToFront"/> would change anything.</summary>
    public static bool CanBringToFront(this ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Find(id) is { } widget && !ReferenceEquals(view.Widgets[^1], widget);
    }

    /// <summary>Whether <see cref="SendToBack"/> would change anything.</summary>
    public static bool CanSendToBack(this ViewState view, string id)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Find(id) is { } widget && !ReferenceEquals(view.Widgets[0], widget);
    }

    /// <summary>Paint the widget over every other widget. False when nothing changed.</summary>
    public static bool BringToFront(this ViewState view, string id)
    {
        if (!view.CanBringToFront(id))
        {
            return false;
        }

        var widget = view.Find(id)!;
        view.Widgets.Remove(widget);
        view.Widgets.Add(widget);
        return true;
    }

    /// <summary>Paint the widget under every other widget. False when nothing changed.</summary>
    public static bool SendToBack(this ViewState view, string id)
    {
        if (!view.CanSendToBack(id))
        {
            return false;
        }

        var widget = view.Find(id)!;
        view.Widgets.Remove(widget);
        view.Widgets.Insert(0, widget);
        return true;
    }
}
