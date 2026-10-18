namespace Themia.Core.Model;

/// <summary>
/// [S-1411] A widget config that holds app layouts (the Shortcuts widget's layout items): how the
/// host's one layout editor finds the item it edits across every view without naming the widget.
/// </summary>
public interface IHoldsAppLayouts
{
    /// <summary>The layout item with this id, or null when this config holds none by that id (or it is no longer a layout).</summary>
    IAppLayoutItem? FindLayout(string itemId);
}

/// <summary>[S-1411] One app layout as the editor edits it: its name and its pane tree.</summary>
public interface IAppLayoutItem
{
    /// <summary>The name shown for it (empty: the kind's label stands in).</summary>
    string Label { get; set; }

    /// <summary>Its pane tree; null when the item is not a layout.</summary>
    LayoutNode? Layout { get; set; }
}
