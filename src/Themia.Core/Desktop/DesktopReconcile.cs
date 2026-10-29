using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5520] [S-5522] The desktop layer's bookkeeping across views, as pure rules: what the state
/// remembers of the desktop's items and how it is kept whole.
/// </summary>
public static partial class DesktopReconcile
{
    /// <summary>
    /// [S-5522] Keep the known set a list of distinct (ignoring case), non-empty keys no longer than
    /// <see cref="DesktopItemKeys.MaxLength"/> and holding no control character (<see cref="DesktopItemKeys.IsValid"/>);
    /// a hand-edited null is an empty set. Each repair is said.
    /// </summary>
    public static IReadOnlyList<string> RepairKnown(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var repairs = new List<string>();
        if (state.Desktop is null)
        {
            state.Desktop = new DesktopState();
            repairs.Add("the desktop's state was null; it knows no item");
            return repairs;
        }

        if (state.Desktop.Known is null)
        {
            state.Desktop.Known = [];
            repairs.Add("the desktop's known items were null; it knows none");
            return repairs;
        }

        var seen = new HashSet<string>(DesktopItemKeys.Comparer);
        var kept = new List<string>(state.Desktop.Known.Count);
        var dropped = 0;
        foreach (var key in state.Desktop.Known)
        {
            if (!DesktopItemKeys.IsValid(key) || !seen.Add(key))
            {
                dropped++;
                continue;
            }

            kept.Add(key);
        }

        if (dropped > 0)
        {
            state.Desktop.Known = kept;
            repairs.Add($"{dropped} known desktop item(s) were empty, too long or listed twice; dropped");
        }

        return repairs;
    }
}
