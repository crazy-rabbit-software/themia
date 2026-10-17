using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.Quote;

namespace Themia.App.Shell.Config;

/// <summary>
/// The quote widget's settings: the list it turns over, and how often it turns.
/// <para>
/// There is nothing here about a source, a feed or an account, because there is no network in this
/// widget at all: the list is the user's own, seeded with a bundled set they can edit or throw away.
/// </para>
/// </summary>
internal static class QuoteSettings
{
    public static void Rows(List<FrameworkElement> rows, QuoteConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(QuoteStr.WidgetsC_Quote_Settings_Quotes.Text()));

        FluentLayout.ListEditor(rows, new ListEditorOptions<QuoteItem>
        {
            Items = config.Quotes,
            Save = save,
            EmptyText = QuoteStr.WidgetsC_Quote_Settings_EmptyList.Text(),
            AddTitle = QuoteStr.WidgetsC_Quote_Settings_Add_Title.Text(),
            AddDescription = QuoteStr.WidgetsC_Quote_Settings_Add_Desc.Text(),
            AddAction = Str.Common_Add.Text(),
            RemoveTooltip = QuoteStr.WidgetsC_Quote_Settings_Remove.Text(),
            Capacity = QuoteRotation.MaxQuotes,
            Create = () => new QuoteItem(),
            Body = item => QuoteBody(item, save),
        });

        rows.Add(FluentLayout.Row(Glyphs.Contact, QuoteStr.WidgetsC_Quote_Settings_ShowAuthor_Label.Text(), QuoteStr.WidgetsC_Quote_Settings_ShowAuthor_Desc.Text(),
            FluentLayout.Toggle(config.ShowAuthor, value => { config.ShowAuthor = value; save(); })));

        rows.Add(FluentLayout.Header(QuoteStr.WidgetsC_Quote_Settings_Behaviour.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Shuffle, QuoteStr.Quote_Shuffle.Text(),
            null,
            FluentLayout.Toggle(config.Shuffle, value => { config.Shuffle = value; save(); })));

        // The shuffle is a pure function of a seed, so "reshuffle" is a new seed and nothing else.
        // That is what makes the same quote come back on the same day on every machine. [S-6074]
        // Without the shuffle the seed is never read, so the button is offered with it only.
        if (config.Offers(QuoteSetting.Reshuffle))
        {
            var reshuffle = new Button { Content = QuoteStr.WidgetsC_Quote_Settings_Reshuffle_Label.Text() };
            reshuffle.Click += (_, _) =>
            {
                config.Seed = unchecked(config.Seed + 1);
                save();
            };

            rows.Add(FluentLayout.Row(Glyphs.Reset, QuoteStr.WidgetsC_Quote_Settings_Reshuffle_Label.Text(), QuoteStr.WidgetsC_Quote_Settings_Reshuffle_Desc.Text(), reshuffle));
        }

        rows.Add(FluentLayout.Header(QuoteStr.WidgetsC_Quote_Settings_Updating.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Timer, Str.Common_Refresh.Text(),
            QuoteStr.WidgetsC_Quote_Settings_Refresh_Desc.Text(),
            FluentLayout.Choice(
                [
                    (QuoteCadence.Manual, QuoteStr.WidgetsC_Quote_Settings_Cadence_Manual.Text()),
                    (QuoteCadence.Hourly, QuoteStr.WidgetsC_Quote_Settings_Cadence_Hourly.Text()),
                    (QuoteCadence.Daily, QuoteStr.WidgetsC_Quote_Settings_Cadence_Daily.Text()),
                ],
                config.Cadence,
                value => { config.Cadence = value; save(); })));
    }

    /// <summary>
    /// One quote: the line on its own row because a quote is a sentence and not a field, with the
    /// attribution under it.
    /// </summary>
    private static FrameworkElement QuoteBody(QuoteItem item, Action save)
    {
        var text = new TextBox
        {
            Text = item.Text,
            PlaceholderText = QuoteStr.WidgetsC_Quote_Settings_TextPlaceholder.Text(),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            IsSpellCheckEnabled = false,
        };

        // Written on blur, not per keystroke: the widget is on the desktop behind this window and
        // would otherwise redraw a half-typed sentence on every character.
        text.LostFocus += (_, _) =>
        {
            item.Text = text.Text.Trim();
            save();
        };

        var author = new TextBox
        {
            Text = item.Author,
            PlaceholderText = QuoteStr.WidgetsC_Quote_Settings_AuthorPlaceholder.Text(),
            Margin = new Thickness(0, Fluent.Space2, 0, 0),
        };

        author.LostFocus += (_, _) =>
        {
            item.Author = author.Text.Trim();
            save();
        };

        return new StackPanel { Children = { text, author } };
    }
}
