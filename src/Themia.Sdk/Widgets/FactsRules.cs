using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-11007] One fact a facts atom can show: its key (stored in the config) and its name.</summary>
public sealed record FactOption(string Key, Str Label);

/// <summary>
/// [S-11007] What every facts atom shares (CPU facts, memory facts, ...): which of its kind's facts it
/// shows and in what order, an ordered pick of keys. A new one shows the first four of its kind's
/// facts. Its height follows how many it shows ([S-2439]).
/// </summary>
public abstract class FactsConfig : DetailConfig
{
    private readonly IReadOnlyList<FactOption> _options;

    /// <param name="options">Every fact the kind has, in its own order.</param>
    /// <param name="defaults">How many of them a new one shows, from the first.</param>
    protected FactsConfig(IReadOnlyList<FactOption> options, int defaults = FactsRules.DefaultCount)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        Facts = [.. options.Take(Math.Clamp(defaults, 1, options.Count)).Select(o => o.Key)];
    }

    /// <summary>[S-11007] The facts shown, by key, in the order shown.</summary>
    public List<string> Facts { get; set; }

    /// <summary>Every fact the kind has, in its own order.</summary>
    [JsonIgnore]
    public IReadOnlyList<FactOption> Options => _options;

    /// <summary>A copy's pick: its own list.</summary>
    protected void CopyTo(FactsConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        other.Facts = [.. Facts];
    }

    /// <summary>[S-11007] A key the kind lacks or a second copy of one is dropped; none left is the defaults, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        var known = new HashSet<string>(_options.Select(o => o.Key), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<string>();
        foreach (var key in Facts ?? [])
        {
            if (key is not null && known.Contains(key) && seen.Add(key))
            {
                kept.Add(key);
            }
        }

        if (Facts is null || kept.Count != Facts.Count)
        {
            notes.Add($"{GetType().Name}: facts {string.Join(", ", Facts ?? [])} hold unknown or repeated keys; keeping {string.Join(", ", kept)}");
        }

        if (kept.Count == 0)
        {
            kept = [.. _options.Take(FactsRules.DefaultCount).Select(o => o.Key)];
            notes.Add($"{GetType().Name}: no fact left; showing the first {kept.Count}");
        }

        Facts = kept;
        return notes;
    }
}

/// <summary>
/// [S-11007] A facts atom's rules, pure: its sizing (the header, a 28 row per fact, the foot's inset;
/// 200..420 wide), the pick a dragged height means, and the rows a sheet gives a pick.
/// </summary>
public static class FactsRules
{
    public const int DefaultCount = 4;

    public const double MinWidth = 200;

    public const double NaturalWidth = 240;

    public const double MaxWidth = 420;

    /// <summary>[S-11007] The height's steps: the header and the foot, a fact row each.</summary>
    public static SizeSteps Steps(int count, int most)
        => new(FamilySpace.ContentTop + FamilySpace.Inset, FamilyShape.Row1, 1, Math.Max(1, most), Math.Clamp(count, 1, Math.Max(1, most)));

    /// <summary>[S-11007] The sizing of a facts atom showing <paramref name="config"/>'s pick.</summary>
    public static WidgetSizing Sizing(FactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return WidgetSizing.Range(new(MinWidth, 1), new(NaturalWidth, 1), new(MaxWidth, 1))
            .WithHeightSteps(Steps(config.Facts.Count, config.Options.Count));
    }

    /// <summary>
    /// [S-2442] [S-11007] The pick a height means: fewer rows keep the first facts of the pick, more add
    /// the kind's next facts not shown yet, in its order. A copy when it changes; never the given config changed.
    /// </summary>
    public static WidgetConfig SizeSets(WidgetConfig config, SizeDip size)
    {
        if (config is not FactsConfig facts)
        {
            return config;
        }

        var wanted = Steps(facts.Facts.Count, facts.Options.Count).Nearest(size.Height);
        if (wanted == facts.Facts.Count)
        {
            return config;
        }

        var copy = (FactsConfig)config.Clone();
        copy.Facts = Resize(facts.Facts, facts.Options, wanted);
        return copy;
    }

    /// <summary>[S-11007] <paramref name="pick"/> grown or cut to <paramref name="count"/> facts.</summary>
    public static List<string> Resize(IReadOnlyList<string> pick, IReadOnlyList<FactOption> options, int count)
    {
        ArgumentNullException.ThrowIfNull(pick);
        ArgumentNullException.ThrowIfNull(options);
        var kept = pick.Take(Math.Max(1, count)).ToList();
        foreach (var option in options)
        {
            if (kept.Count >= count)
            {
                break;
            }

            if (!kept.Contains(option.Key))
            {
                kept.Add(option.Key);
            }
        }

        return kept;
    }

    /// <summary>
    /// [S-11007] The rows of a pick from one sheet, one per picked key in its order: the sheet's fact of
    /// that key, or "--" dimmed under the fact's own name where this read has none (Windows does not
    /// report it here, or not yet), so the card keeps its rows.
    /// </summary>
    public static IReadOnlyList<DetailFact> Pick(Locale locale, DetailSheet sheet, IReadOnlyList<string> pick, IReadOnlyList<FactOption> options)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(pick);
        ArgumentNullException.ThrowIfNull(options);
        var rows = new List<DetailFact>(pick.Count);
        foreach (var key in pick)
        {
            if (options.FirstOrDefault(o => o.Key == key) is not { } option)
            {
                continue;
            }

            var fact = sheet.Facts.FirstOrDefault(f => f.Key == key);
            rows.Add(fact.Key == key ? fact : DetailFact.Unavailable(Strings.Get(locale, option.Label)) with { Key = key });
        }

        return rows;
    }
}
