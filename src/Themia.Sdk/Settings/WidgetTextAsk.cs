namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3721] [S-2351] One line of text asked for on the selection's page, in place in its line's card: what it asks for
/// (its field's name for a screen reader), the field's placeholder, the words of the accept that gives it, the check run
/// as it is typed (the reason it cannot be used, or null) and what is done with it. Enter or the accept gives it;
/// Escape or a press elsewhere closes it and gives nothing.
/// <paramref name="Initial"/> is what the box starts with (a name being changed);
/// <paramref name="AllowEmpty"/> lets an empty box be given (a name cleared).
/// </summary>
public sealed record WidgetTextAsk(string Title, string Placeholder, string Accept, Func<string, string?> Refuse, Action<string> Done, string? Initial = null, bool AllowEmpty = false);
