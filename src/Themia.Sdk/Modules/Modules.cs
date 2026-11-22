using System.Text.Json.Serialization.Metadata;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-6901] Declares a widget module: its id and the class the host creates. Read at build time by
/// the registry generator, never at run time.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ThemiaWidgetAttribute(string id, Type module) : Attribute
{
    public string Id { get; } = id;

    public Type Module { get; } = module;
}

/// <summary>
/// [S-6901] Marks a widget module project as a family (owner, 2026-11-19: widgets split into smaller pieces, one
/// reading each): it declares several widgets, each a kind of its own with its own id, sharing the project's code
/// (the weather facts: one kind per fact). Without it a module project declares the one widget its name says.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ThemiaWidgetFamilyAttribute : Attribute;

/// <summary>[S-6901] Declares a provider module (an account or data service).</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ThemiaProviderAttribute(string id, Type module) : Attribute
{
    public string Id { get; } = id;

    public Type Module { get; } = module;
}

/// <summary>
/// [S-14713] [S-15022] A removed kind a kind stands in for (owner, 2026-11-21: "i think it would be good if we merge all the
/// different graph types into the general metric widget"; "weather summary should also be split into three widgets"; a
/// placed one is replaced in place, as the facts cards were, owner 2026-11-20: "Replace in place"): the removed kind's id and,
/// where several kinds share it out by what it drew, the config's key and the value it held for this one (<see cref="When"/>,
/// compared ignoring case; null: every one the others do not take), or a value for which this one does not stand in
/// (<see cref="Unless"/>: a look of the old card that never drew it). A placed one becomes this kind in its spot with its
/// place, drive, adapter or host; a graph's with its graph shown (<see cref="Graph"/>: its trend Line, its chart look and its
/// span); where several kinds stand in for one card, each in its share of the card's rect (<see cref="Share"/>).
/// </summary>
public sealed record MergeFrom(string Kind)
{
    /// <summary>The config's key and the value it held for this kind; null for the rest.</summary>
    public (string Key, string Value)? When { get; init; }

    /// <summary>[S-15022] The config's key and a value it held for which this kind does not stand in; null for none.</summary>
    public (string Key, string Value)? Unless { get; init; }

    /// <summary>[S-14713] Whether a placed one shows its graph: its trend Line, its window or span the metric's span.</summary>
    public bool Graph { get; init; }

    /// <summary>[S-15022] Where in the removed card's rect this kind stands when several stand in for it; null: the whole rect.</summary>
    public MergeShare? Share { get; init; }

    /// <summary>
    /// [S-16230] The removed kind was a part of this one, which is back: a placed one folds into it in place (the load's
    /// <c>KindFolds</c>), never replaced one for one. Null: it is replaced.
    /// </summary>
    public MergeFold? Fold { get; init; }
}

/// <summary>[S-16230] How a placed part of a removed kind folds back into the widget it was split from.</summary>
public enum MergeFold
{
    /// <summary>With the parts and cards of the kind it touches: one card at the union of their boxes, once per group.</summary>
    Touching,

    /// <summary>Into each card of the kind right under it, which grows up over it; with none, it becomes the kind in its box.</summary>
    Under,
}

/// <summary>[S-15022] A share of a removed card's rect, as fractions of its width and height from its top-left corner.</summary>
public readonly record struct MergeShare(double X, double Y, double Width, double Height);

/// <summary>
/// [S-6902] What the host knows of a widget without its UI: one record per widget, in place of the
/// per-kind tables the host used to keep (docs/WIDGET-KIT.md section 4.3). A member is added here
/// when a table moves onto it, and the table goes in the same commit.
/// </summary>
public sealed record WidgetDescriptor
{
    /// <summary>The widget's id ([S-6900]): its config's discriminator in state.json.</summary>
    public required WidgetId Id { get; init; }

    /// <summary>[S-6903] Its config's type: the member of the config union written with <see cref="Id"/> as its discriminator.</summary>
    public required Type ConfigType { get; init; }

    /// <summary>A new config with the widget's defaults; always a <see cref="ConfigType"/>.</summary>
    public required Func<WidgetConfig> CreateDefault { get; init; }

    /// <summary>[S-6903] The source-generated JSON metadata that knows <see cref="ConfigType"/> (AOT: no reflection).</summary>
    public required IJsonTypeInfoResolver Json { get; init; }

    /// <summary>[S-6909] Its name, from its own string table: read in the current language each time.</summary>
    public required LocalText Label { get; init; }

    /// <summary>
    /// [S-11709] Its tile's name on the add page where that is not the kind's own: a kind of two sources
    /// whose tile makes one of them (the desktop icon's tile makes an own icon, "Shortcut"). Null, the
    /// rule: the tile is called what the kind is.
    /// </summary>
    public LocalText? AddLabel { get; init; }

    /// <summary>[S-11709] What its tile on the add page is called, in the current language.</summary>
    public string TileName => (AddLabel ?? Label).Text();

    /// <summary>The picker's one line under the name.</summary>
    public required LocalText Description { get; init; }

    /// <summary>[S-1115] The facts its description's placeholders take ("15-minute steps"), written in the language's digits.</summary>
    public IReadOnlyList<StringArg> DescriptionFacts { get; init; } = [];

    /// <summary>Segoe Fluent Icons code point: the picker, the Set up prompt, the settings search.</summary>
    public required string Glyph { get; init; }

    private readonly Func<WidgetConfig, WidgetSizing> _sizing = static _ => throw new InvalidOperationException("no sizing");

    /// <summary>
    /// [S-2438] The sizes the widget may take, from its config alone: a pure function, so the host, the
    /// renders and the tests ask it as often as they like. [S-10] A new widget lands at its Natural.
    /// [S-12411] A kind sets the range its content needs; what this hands out is that range relaxed
    /// (<see cref="SizeStretch.Relax"/>): the one sizing every caller sees.
    /// </summary>
    public required Func<WidgetConfig, WidgetSizing> Sizing
    {
        get => _sizing;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _sizing = config => SizeStretch.Relax(value(config));
        }
    }

    /// <summary>[S-2438] The sizing of <paramref name="config"/> (the defaults when null).</summary>
    public WidgetSizing SizingOf(WidgetConfig? config = null) => Sizing(config ?? CreateDefault());

    /// <summary>[S-10] Where a new widget of the kind lands, in DIPs: its default config's Natural.</summary>
    public SizeDip Natural => SizingOf().Natural;

    /// <summary>[S-6907] Its home in the add menu's tree, and its place among Popular's rows if it is one.</summary>
    public required AddPlacement Add { get; init; }

    /// <summary>[S-6905] What its card offers; stated in full by every widget.</summary>
    public required WidgetTraits Traits { get; init; }

    /// <summary>
    /// [S-943] Curated search words, English only on purpose (search indexes the translated name and
    /// description beside the English ones): what the widget shows ("wifi"), the vendor or protocol
    /// behind it ("outlook", "imap"), the job it does ("timer") and the other common spelling ("todo").
    /// </summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>
    /// [S-943] [S-6910] The providers whose accounts it reads, by id: each is a registered provider,
    /// and their ids are searched as their own field, so "google" finds every widget that can show a
    /// Google account.
    /// </summary>
    public IReadOnlyList<ProviderId> Providers { get; init; } = [];

    /// <summary>
    /// [S-11905] What the kind takes part in on its view's links: the topics it publishes and those it
    /// follows. Its code names exactly these (the conformance test); SPEC's topic table is made from them.
    /// </summary>
    public Themia.Core.Links.LinkUse Links { get; init; } = Themia.Core.Links.LinkUse.None;

    /// <summary>
    /// [S-14713] [S-15022] The removed kinds this kind stands in for (the wind graph is the Wind metric with its graph; the
    /// weather summary is the sky, the temperature and the condition): a placed one is replaced in place on load by this kind
    /// (with the others that share it out). Empty for every other kind.
    /// </summary>
    public IReadOnlyList<MergeFrom> Merges { get; init; } = [];

    /// <summary>The name in the current language, read each time so a language switch reaches every caller.</summary>
    public string Name => Label.Text();

    /// <summary>The description in the current language, its facts filled.</summary>
    public string Describe() => Describe(Strings.Current);

    /// <summary>The description in <paramref name="locale"/>, its facts filled.</summary>
    public string Describe(Locale locale) => Description.Format(locale, [.. DescriptionFacts]);
}
