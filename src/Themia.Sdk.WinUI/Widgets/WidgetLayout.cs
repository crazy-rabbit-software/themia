using Microsoft.UI.Xaml;

namespace Themia.App.Widgets;

/// <summary>
/// The inset between a widget's content and the edge of its card, and who is responsible for it.
/// <para>
/// The card does <b>not</b> apply it. It used to, which meant every list row was inset from the
/// card edge and its hover highlight stopped short of it — a row that is the full width of the
/// widget reads as a row, and one floating inside a margin reads as a box inside a box. So the
/// card's content area goes edge to edge and each view applies the inset to the parts that need
/// it: a header, a readout, a footer. A list row applies it as its own <i>padding</i>, so the text
/// still lines up exactly where it did while the row itself — and the highlight it draws when the
/// pointer is over it — reaches both edges.
/// </para>
/// </summary>
internal static class WidgetLayout
{
    /// <summary>The standard content inset: Fluent's 16px, not the 12 the web app used.</summary>
    public const double ContentInset = Fluent.Space4;

    /// <summary>The inset on all four sides, for a view whose content is not a list.</summary>
    public static Thickness Inset { get; } = new(ContentInset);

    /// <summary>The inset either side only, for a header or footer sitting above or below a list.</summary>
    public static Thickness SideInset { get; } = new(ContentInset, 0, ContentInset, 0);

    /// <summary>A header's inset: both sides and the top, with the gap below it left to the header.</summary>
    public static Thickness HeaderInset { get; } = new(ContentInset, ContentInset, ContentInset, 0);

    /// <summary>A footer's inset: both sides and the bottom.</summary>
    public static Thickness FooterInset { get; } = new(ContentInset, 0, ContentInset, ContentInset);

    /// <summary>
    /// A list's own inset: none either side, so rows reach both edges, and a little top and bottom
    /// so the first and last row are not flush against the header and the card.
    /// </summary>
    public static Thickness ListInset { get; } = new(0, 2, 0, ContentInset - 4);
}
