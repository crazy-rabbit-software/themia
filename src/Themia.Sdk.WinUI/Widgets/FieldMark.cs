using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14303] A widget's free field: a box that is the card's content and holds what its person puts in it where they put
/// it (the note's editor, its text from the top as written; the scratchpad's pad, its strokes where drawn). The layout dump
/// (<c>--widget-png … --layout-json</c>'s <c>fields</c>) and the gap check judge the field's box against the card's edges,
/// never the bands round the text or the strokes in it, as they judge a list's item boxes rather than its short titles.
/// </summary>
public static class FieldMark
{
    private static readonly ConditionalWeakTable<FrameworkElement, object> Fields = [];
    private static readonly object Marker = new();

    /// <summary>Mark <paramref name="element"/> as its widget's free field.</summary>
    public static void Mark(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        Fields.AddOrUpdate(element, Marker);
    }

    /// <summary>Whether <paramref name="node"/> is a free field.</summary>
    public static bool IsField(DependencyObject? node) => node is FrameworkElement element && Fields.TryGetValue(element, out _);
}
