using Themia.Core.Model;

namespace Themia.Core.Shell;

/// <summary>
/// [S-3516] Snap, the edit bar's one setting of its own: grid snap while widgets are moved and
/// resized. The bar's Snap button sets it, and nothing else does ([S-3590]: not ⚙ › Appearance, not
/// the surface menu). One writer, so the ownership guard sees it among the menus' ([S-3592]).
/// </summary>
public static class EditBarSnap
{
    public static void Set(AppState state, bool on)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Design.GridSnap = on;
    }
}
