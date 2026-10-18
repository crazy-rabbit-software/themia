using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-1411] The app-layout editor: one window for the whole app, opened on demand and retargeted
/// when it is asked for again (original S-58: a singleton labelled <c>layout</c>).
/// </summary>
internal sealed partial class AppHost
{
    private LayoutEditorWindow? _layoutEditor;

    /// <summary>Open the editor on the layout item <paramref name="itemId"/>, or bring it forward on that item.</summary>
    public void ShowLayoutEditor(string itemId)
    {
        if (FindShortcut(itemId) is null)
        {
            Log.Warn(Scope, "asked to edit an app layout that no longer exists");
            return;
        }

        if (_layoutEditor is null)
        {
            _layoutEditor = new LayoutEditorWindow(this);
            _layoutEditor.Closed += () => _layoutEditor = null;
        }

        _layoutEditor.Show(itemId);
    }

    private void CloseLayoutEditor() => _layoutEditor?.Close();

    /// <summary>
    /// The layout item with this id and the widget that holds it (<see cref="IHoldsAppLayouts"/>), across
    /// every view; null when it is gone (removed, undone, imported over) or no longer a layout.
    /// </summary>
    public (WidgetInstance Widget, IAppLayoutItem Item)? FindShortcut(string itemId)
    {
        foreach (var view in State.Views)
        {
            foreach (var widget in view.Widgets)
            {
                if (widget.Config is IHoldsAppLayouts holder && holder.FindLayout(itemId) is { } item)
                {
                    return (widget, item);
                }
            }
        }

        return null;
    }
}
