using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-13060] A part of a widget that tells the card what a press on it is (<see cref="PressPart"/>) where its type does not
/// say it: a seek bar's track and its thumb. The card reads the part met first on the way up from the element pressed
/// (<c>WidgetPress.Part</c>); WinUI's own controls (a button, a slider and its thumb, a text box) are known by their type.
/// </summary>
public static class PressParts
{
    private static readonly ConditionalWeakTable<UIElement, object> Parts = [];

    /// <summary>Mark <paramref name="element"/> as <paramref name="part"/>.</summary>
    public static void Mark(UIElement element, PressPart part)
    {
        ArgumentNullException.ThrowIfNull(element);
        Parts.AddOrUpdate(element, part);
    }

    /// <summary>What <paramref name="element"/> was marked as, if it was.</summary>
    public static bool Marked(UIElement element, out PressPart part)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Parts.TryGetValue(element, out var marked) && marked is PressPart found)
        {
            part = found;
            return true;
        }

        part = PressPart.Ground;
        return false;
    }
}
