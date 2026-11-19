using Themia.App.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-2332] A line's box typed into as the page types into it: what is typed trimmed, kept where the line's refusal
/// takes it (an empty box only where it may be empty). Read as the question a line used to ask on a page of its own,
/// so a module's test reads its box's hint, what it holds, its refusal and what typing does the one way.
/// </summary>
internal static class Typed
{
    public static WidgetTextAsk Ask(ItemMenuEntry line)
    {
        var field = line.Field ?? throw new InvalidOperationException($"'{line.Text}' has no box typed in place");
        return new WidgetTextAsk(
            line.Text,
            field.Placeholder,
            string.Empty,
            field.Refuse,
            typed =>
            {
                var text = (typed ?? string.Empty).Trim();
                if ((text.Length > 0 || field.AllowEmpty) && field.Refuse(text) is null)
                {
                    field.Keep(text);
                }
            },
            Initial: field.Current.Length > 0 ? field.Current : null,
            AllowEmpty: field.AllowEmpty);
    }
}
