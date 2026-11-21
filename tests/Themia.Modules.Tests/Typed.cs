using Themia.App.Widgets;
using Themia.Core.Settings;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-2332] A line's box typed into as the page types into it: what is typed trimmed, kept where the line's refusal
/// takes it (an empty box only where it may be empty). Read as the question a line used to ask on a page of its own,
/// so a module's test reads its box's hint, what it holds, its refusal and what typing does the one way.
/// [S-2351] A line that asks in place (Rename…, Add…, a path typed) is read as the line of text it carries.
/// [S-2370] A number's box as the page's number box takes it: what is typed read in the box's unit ([S-2309]), held to
/// the range and the step; only words that read as no number are refused (the box puts back what it said).
/// </summary>
internal static class Typed
{
    public static WidgetTextAsk Ask(SettingsLine line)
    {
        if (line is SettingsPrompt prompt)
        {
            return prompt.Ask;
        }

        if (line is SettingsSlider slider)
        {
            var box = new MenuSlider(slider.Value, slider.Min, slider.Max, slider.Step, slider.Format, slider.Set);
            string? Refuse(string typed) => NumberEntry.Value(typed, box.Max != 0 ? box.Max : box.Min, box.Format) is null ? slider.Text : null;
            return new WidgetTextAsk(
                slider.Text,
                slider.Text,
                string.Empty,
                Refuse,
                typed =>
                {
                    if (NumberEntry.Value(typed, box.Max != 0 ? box.Max : box.Min, box.Format) is { } value)
                    {
                        box.Commit(value);
                    }
                },
                Initial: box.Value is { } held ? box.Format(held) : null);
        }

        var field = line as SettingsAsk ?? throw new InvalidOperationException($"'{line.Text}' has no box typed in place");
        return new WidgetTextAsk(
            field.Text,
            field.Placeholder,
            string.Empty,
            field.Refuse,
            typed =>
            {
                var text = (typed ?? string.Empty).Trim();
                if ((text.Length > 0 || field.AllowEmpty) && field.Refuse(text) is null)
                {
                    field.Done(text);
                }
            },
            Initial: field.Initial.Length > 0 ? field.Initial : null,
            AllowEmpty: field.AllowEmpty);
    }
}
