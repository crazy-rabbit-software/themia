using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Clipboard;

/// <summary>
/// [S-1441] [S-11405] The Clipboard widget on the kit ([S-7074]): rows of the rich look, an entry's preview the name on
/// one line (BodyStrong for what is on the clipboard now), how much it is ("32 characters") on the second where the
/// config gives two, its marker as the lead ([S-1442]) and when it was copied as the value, compact ("4m"). A single
/// click copies an entry back ([S-7075]): the worst a mis-click can do. Nothing is ever opened.
/// </summary>
public static class ClipboardSpec
{
    /// <summary>The Delete action's id: the view takes the entry off the shared history.</summary>
    public const string RemoveId = "remove";

    /// <summary>[S-1442] The lead's key of the entry on the clipboard now.</summary>
    public const string Current = "current";

    /// <summary>[S-1442] The lead's key of a colour entry: this, then its colour as #RRGGBB.</summary>
    public const string ColourLead = "colour:";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        static () => string.Empty,
        static () => ClipboardStr.WidgetsA_Clipboard_Empty.Text(),
        KitTime.Compact)
    {
        ClickRuns = true,
    };

    /// <summary>
    /// [S-1441] [S-1442] An entry as a kit item: its text's fingerprint is its id (the history holds each text once, and a
    /// text may be far longer than an id), its preview the title, how much it is the subtitle and the tooltip, the first
    /// one emphasised, its marker's key the lead.
    /// </summary>
    public static KitItem Item(ClipboardEntry entry, bool current, Locale locale)
    {
        var describe = ClipboardFormatting.Describe(entry.Text, locale);
        return new KitItem(
            Id(entry.Text),
            ClipboardFormatting.Preview(entry.Text, ClipboardLayout.PreviewLength),
            Subtitle: describe,
            Lead: new KitLead(KitLeadKind.Icon, LeadKey(entry.Text, current)),
            When: KitWhen.Instant(entry.CapturedAt),
            Emphasis: current,
            Tooltip: describe);
    }

    /// <summary>[S-1441] An entry's id: its text's fingerprint (<see cref="KitItems.Fingerprint"/>), never the text itself.</summary>
    public static string Id(string text) => KitItems.Fingerprint(text);

    /// <summary>[S-1442] What leads an entry: the tick for what is on the clipboard now, a colour's own swatch, else its kind.</summary>
    public static string LeadKey(string text, bool current)
    {
        if (current)
        {
            return Current;
        }

        return ClipboardLayout.TryColour(text, out var colour)
            ? $"{ColourLead}#{colour.R:X2}{colour.G:X2}{colour.B:X2}"
            : ClipboardLayout.Classify(text).ToString();
    }

    /// <summary>[S-1444] An entry's menu: Copy <paramref name="text"/> (the click's, Enter's and Ctrl+C's), then Remove (Delete).</summary>
    public static IReadOnlyList<KitAction> Actions(string? text)
    {
        if (text is null)
        {
            return [];
        }

        return
        [
            new KitAction(KitActionKind.CopyText, Str.Common_Copy.Text(), Text: text),
            new KitAction(KitActionKind.Delete, ClipboardStr.WidgetsA_Clipboard_Remove.Text(), Id: RemoveId),
        ];
    }
}
