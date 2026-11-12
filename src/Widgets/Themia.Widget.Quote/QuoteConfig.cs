using System.Text.Json.Serialization;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Quote;

namespace Themia.Core.Model;

/// <summary>[S-6404] The quote's styles: the same quote and attribution, two ways of setting them.</summary>
[JsonConverter(typeof(StyleJsonConverter<QuoteStyle>))]
public enum QuoteStyle
{
    /// <summary>The quote in the language's own inline marks (“…”, „…“, « … »).</summary>
    Plain = 0,

    /// <summary>[S-6405] One large opening mark in the accent above the quote, which then has no inline marks.</summary>
    Marks = 1,
}

/// <summary>[S-6074] The quote's rows that depend on another setting, in menu order.</summary>
public enum QuoteSetting
{
    /// <summary>Shuffle: only with two quotes or more to turn between.</summary>
    Shuffle,

    /// <summary>Reshuffle: only with Shuffle on.</summary>
    Reshuffle,

    /// <summary>How often it turns: only with two quotes or more.</summary>
    Cadence,
}

/// <summary>One quote, as stored.</summary>
public sealed class QuoteItem
{
    public string Text { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public QuoteItem Clone() => new() { Text = Text, Author = Author };
}

/// <summary>
/// A line worth rereading, on a rotation.
/// <para>
/// Entirely offline. The quotes are the user's own list, seeded with a bundled set, and which one
/// shows is a pure function of the clock and the seed — see <see cref="QuoteRotation"/>. There is
/// no quote service, no fetch and no cache, which is also why this widget works on the first run
/// with no network and keeps working on a laptop in a tunnel.
/// </para>
/// </summary>
public sealed class QuoteConfig : WidgetConfig, IStyled, IShownSettings<QuoteSetting>, IContentAligned, ISetupRule, IReadoutSized
{
    /// <summary>[S-11306] How big the quote is set: Small (Body), Medium (Title), Large (28); the card follows it.</summary>
    public ReadoutSize Size { get; set; } = ReadoutSizes.Default;

    /// <summary>[S-11306] How many lines the quote may take, <see cref="QuoteLayout.MinLines"/> to <see cref="QuoteLayout.MostLines"/>; a longer one ends in an ellipsis.</summary>
    public int Lines { get; set; } = QuoteLayout.DefaultLines;

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => EffectiveQuotes.Count == 0;

    /// <summary>[S-1862] The Set up row's sentence while the list is empty: "Add a quote".</summary>
    string? ISetupRule.SetupSentence => QuoteStr.WidgetsC_Quote_Settings_Add_Title.Text();

    public QuoteConfig()
    {
        foreach (var quote in QuoteRotation.Bundled)
        {
            Quotes.Add(new QuoteItem { Text = quote.Text, Author = quote.Author });
        }
    }

    /// <summary>[S-6404] Plain or large marks: the widget's style.</summary>
    public QuoteStyle Style { get; set; } = QuoteStyle.Plain;

    private static readonly StyleOption[] QuoteStyles =
    [
        StyleOption.Of(QuoteStyle.Plain, QuoteStr.Styles_Quote_Plain.Local(), 0xEC34),
        StyleOption.Of(QuoteStyle.Marks, QuoteStr.Styles_Quote_Marks.Local(), 0xE9B2),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => QuoteStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (QuoteStyle)value;
    }

    /// <summary>The user's list, in order.</summary>
    public List<QuoteItem> Quotes { get; set; } = [];

    /// <summary>How often it turns over on its own.</summary>
    public QuoteCadence Cadence { get; set; } = QuoteCadence.Daily;

    /// <summary>Walk the list in order, or hash the period so consecutive turns are unrelated.</summary>
    public bool Shuffle { get; set; }

    /// <summary>Which shuffle. Changing it reshuffles the whole rotation, deterministically.</summary>
    public int Seed { get; set; } = 1;

    /// <summary>
    /// [S-6074] [S-1502] The rotation's lines turn between quotes, so they are offered with two or
    /// more (one quote, or none, shows the same whatever they say); Reshuffle is a new seed, and the
    /// seed is only read by the shuffle, so it is offered with Shuffle on.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<QuoteSetting> ShownSettings => EffectiveQuotes.Count < 2 ? []
        : Shuffle ? [QuoteSetting.Shuffle, QuoteSetting.Reshuffle, QuoteSetting.Cadence]
        : [QuoteSetting.Shuffle, QuoteSetting.Cadence];

    /// <summary>Show who said it.</summary>
    public bool ShowAuthor { get; set; } = true;

    /// <summary>
    /// The quotes that are actually showable: blank rows in the editor are not quotes, and a list
    /// of them is an empty list rather than a widget full of quotation marks round nothing.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<QuoteLine> EffectiveQuotes
    {
        get
        {
            var lines = new List<QuoteLine>(Quotes.Count);

            foreach (var quote in Quotes)
            {
                if (string.IsNullOrWhiteSpace(quote.Text))
                {
                    continue;
                }

                lines.Add(new QuoteLine(quote.Text.Trim(), quote.Author?.Trim() ?? string.Empty));
            }

            return lines;
        }
    }

    /// <summary>How often the widget needs to wake. Zero means it never does.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => QuoteRotation.TickInterval(Cadence);

    /// <summary>[S-11306] A size this build does not know reads Medium; lines out of range are held to it.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        if (!Enum.IsDefined(Size))
        {
            repairs.Add($"quote size {(int)Size} unknown; medium");
            Size = ReadoutSizes.Default;
        }

        if (Lines is < QuoteLayout.MinLines or > QuoteLayout.MostLines)
        {
            repairs.Add($"quote lines {Lines} out of range");
            Lines = Math.Clamp(Lines, QuoteLayout.MinLines, QuoteLayout.MostLines);
        }

        return repairs;
    }

    public override WidgetConfig Clone()
    {
        // Not the seeding constructor: see ColorSwatchConfig.Clone.
        var copy = new QuoteConfig
        {
            Style = Style,
            Cadence = Cadence,
            Shuffle = Shuffle,
            Seed = Seed,
            ShowAuthor = ShowAuthor,
            Size = Size,
            Lines = Lines,
        };

        copy.Quotes.Clear();

        foreach (var quote in Quotes)
        {
            copy.Quotes.Add(quote.Clone());
        }

        return copy;
    }
}
