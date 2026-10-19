namespace Themia.Sdk;

/// <summary>
/// [S-2849] What a widget's properties menu lines ([S-3719]) may need to know of the card they are
/// built for: whether its header shows and whether the user typed a title of its own. A line that only
/// changes the header's title (the weather's Location name) is offered only where that title is read.
/// </summary>
/// <param name="HeaderShows">The card draws a header (top or bottom): the widget's title is read there.</param>
/// <param name="Titled">The user typed a title, which wins over any title the widget gives.</param>
public readonly record struct MenuCard(bool HeaderShows, bool Titled)
{
    /// <summary>A card with its header showing and no title of its own: what a menu built without a card (a test) assumes.</summary>
    public static MenuCard Plain => new(HeaderShows: true, Titled: false);

    /// <summary>[S-2849] Whether a title the widget gives its header is read: the header shows and no typed title wins over it.</summary>
    public bool ReadsWidgetTitle => HeaderShows && !Titled;
}
