using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Quote;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1502] The quote widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the list it turns over (each quote's text and author, its place, Remove, Add), the
/// author switch, the shuffle and how often it turns. It has no settings page.
/// <para>
/// There is nothing here about a source, a feed or an account, because there is no network in this
/// widget at all: the list is the user's own, seeded with a bundled set they can edit or throw away.
/// </para>
/// </summary>
internal static class QuoteMenu
{
    private static (QuoteCadence, string)[] Cadences() =>
    [
        (QuoteCadence.Manual, QuoteStr.WidgetsC_Quote_Settings_Cadence_Manual.Text()),
        (QuoteCadence.Hourly, QuoteStr.WidgetsC_Quote_Settings_Cadence_Hourly.Text()),
        (QuoteCadence.Daily, QuoteStr.WidgetsC_Quote_Settings_Cadence_Daily.Text()),
    ];

    public static void Lines(WidgetMenuLines menu, QuoteConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(Quotes(menu, config));
        menu.Lines.Add(menu.Toggle(Glyphs.Contact, QuoteStr.WidgetsC_Quote_Settings_ShowAuthor_Label.Text(), config.ShowAuthor, on => config.ShowAuthor = on));

        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Shuffle, QuoteStr.Quote_Shuffle.Text(), config.Shuffle, on => config.Shuffle = on));

        // The shuffle is a pure function of a seed, so "reshuffle" is a new seed and nothing else.
        // That is what makes the same quote come back on the same day on every machine. [S-6074]
        // Without the shuffle the seed is never read, so the line is offered with it only.
        if (config.Offers(QuoteSetting.Reshuffle))
        {
            menu.Lines.Add(menu.Command(Glyphs.Reset, QuoteStr.WidgetsC_Quote_Settings_Reshuffle_Label.Text(), () => config.Seed = unchecked(config.Seed + 1)));
        }

        menu.Lines.Add(menu.Choice(Glyphs.Timer, Str.Common_Refresh.Text(), Cadences(), config.Cadence, value => config.Cadence = value));
    }

    /// <summary>
    /// [S-1502] The quotes in their order (the unshuffled rotation's, [S-1495]), as the menu's editable
    /// list ([S-3729]): each a submenu of Rename… (its text), Author…, Move up, Move down and Remove;
    /// Add… asks for a new one's text, up to <see cref="QuoteRotation.MaxQuotes"/>.
    /// </summary>
    private static ItemMenuEntry Quotes(WidgetMenuLines menu, QuoteConfig config)
        => menu.List(Glyphs.Quote, QuoteStr.WidgetsC_Quote_Settings_Quotes.Text(), config.Quotes,
            new MenuList<QuoteItem>(Name, QuoteStr.WidgetsC_Quote_Settings_EmptyList.Text())
            {
                Rename = new MenuListRename<QuoteItem>((item, typed) => SetText(item, typed))
                {
                    Current = item => item.Text,
                    Placeholder = _ => QuoteStr.WidgetsC_Quote_Settings_TextPlaceholder.Text(),
                },
                Extra = item =>
                [
                    new(Glyphs.Contact, QuoteStr.WidgetsC_Quote_Menu_Author.Text(), () => menu.Ask(new WidgetTextAsk(
                        QuoteStr.WidgetsC_Quote_Menu_Author.Text().TrimEnd('…'),
                        QuoteStr.WidgetsC_Quote_Settings_AuthorPlaceholder.Text(),
                        Str.Common_Save.Text(),
                        _ => null,
                        typed =>
                        {
                            if (SetAuthor(item, typed))
                            {
                                menu.Save();
                            }
                        },
                        Initial: item.Author,
                        AllowEmpty: true)), Value: item.Author.Length > 0 ? item.Author : null),
                ],
                Add = new MenuListAdd<QuoteItem>(
                    QuoteStr.WidgetsC_Quote_Settings_Add_Title.Text(),
                    QuoteStr.WidgetsC_Quote_Settings_TextPlaceholder.Text(),
                    _ => null,
                    typed => new QuoteItem { Text = typed.Trim() }),
                Max = QuoteRotation.MaxQuotes,
            });

    /// <summary>A quote's line: its text, else (a hand-edited one with none) its author.</summary>
    internal static string Name(QuoteItem item) => item.Text.Length > 0 ? item.Text : item.Author;

    /// <summary>A typed text, trimmed; true when it changed. An empty one is refused by the box.</summary>
    internal static bool SetText(QuoteItem item, string typed)
    {
        var text = typed.Trim();
        if (text.Length == 0 || text == item.Text)
        {
            return false;
        }

        item.Text = text;
        return true;
    }

    /// <summary>A typed author, trimmed (empty takes it off); true when it changed.</summary>
    internal static bool SetAuthor(QuoteItem item, string typed)
    {
        var author = typed.Trim();
        if (author == item.Author)
        {
            return false;
        }

        item.Author = author;
        return true;
    }
}
