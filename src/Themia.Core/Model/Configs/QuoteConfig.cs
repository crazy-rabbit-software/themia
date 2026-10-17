using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

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

/// <summary>[S-6074] The quote's rows that depend on another setting.</summary>
public enum QuoteSetting
{
    /// <summary>Reshuffle: only with Shuffle on.</summary>
    Reshuffle,
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
public sealed class QuoteConfig : WidgetConfig, IStyled, IShownSettings<QuoteSetting>
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Quote;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("quote"),
        Kind = WidgetKind.Quote,
        ConfigType = typeof(QuoteConfig),
        CreateDefault = () => new QuoteConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["inspiration", "daily quote", "saying", "motivation"],
        LabelId = Str.WidgetsA_Catalog_Quote_Label,
        DescriptionId = Str.WidgetsA_Catalog_Quote_Description,
        Glyph = Glyphs.Quote,
        DefaultWidth = 320,
        DefaultHeight = 180,
    };

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
        StyleOption.Of(QuoteStyle.Plain, Str.Styles_Quote_Plain, 0xEC34),
        StyleOption.Of(QuoteStyle.Marks, Str.Styles_Quote_Marks, 0xE9B2),
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

    /// <summary>[S-6074] Reshuffle is a new seed, and the seed is only read by the shuffle: offered with Shuffle on.</summary>
    [JsonIgnore]
    public IReadOnlyList<QuoteSetting> ShownSettings => Shuffle ? [QuoteSetting.Reshuffle] : [];

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
        };

        copy.Quotes.Clear();

        foreach (var quote in Quotes)
        {
            copy.Quotes.Add(quote.Clone());
        }

        return copy;
    }
}
