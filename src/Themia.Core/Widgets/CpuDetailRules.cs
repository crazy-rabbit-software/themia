using System.Text;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-2405] One read of the processor's figures. Every field is null when Windows did not answer
/// it on this machine, and a null fact is left off the tile rather than shown as "--".
/// </summary>
/// <param name="Load">Share of all cores busy since the last read, 0..1.</param>
/// <param name="Mhz">The effective clock now ([S-2406]).</param>
/// <param name="BaseMhz">The rated (base) clock.</param>
/// <param name="Cores">Physical cores.</param>
/// <param name="Logical">Logical processors (hardware threads).</param>
/// <param name="Processes">Processes running.</param>
/// <param name="Threads">Threads running.</param>
/// <param name="Handles">Kernel handles open.</param>
/// <param name="Uptime">Time since boot.</param>
/// <param name="Celsius">The hottest ACPI thermal zone ([S-2407]).</param>
/// <param name="Name">The processor's own name, cleaned.</param>
public sealed record CpuFigures(
    double? Load = null,
    double? Mhz = null,
    double? BaseMhz = null,
    int? Cores = null,
    int? Logical = null,
    long? Processes = null,
    long? Threads = null,
    long? Handles = null,
    TimeSpan? Uptime = null,
    double? Celsius = null,
    string? Name = null);

/// <summary>[S-2405]..[S-2407] The CPU details widget's rules, pure.</summary>
public static class CpuDetailRules
{
    /// <summary>The widest hero: "10.00 GHz".</summary>
    public const double HeroCharacters = 9;

    /// <summary>A thermal zone colder than this, or hotter than <see cref="MaxCelsius"/>, is a bogus sensor.</summary>
    public const double MinCelsius = 1;

    public const double MaxCelsius = 150;

    /// <summary>
    /// [S-2406] The clock the processor is really running at. <c>CallNtPowerInformation</c>'s current
    /// MHz is the base clock on nearly every modern machine, so Task Manager's figure is the base
    /// clock times <c>\Processor Information(_Total)\% Processor Performance</c> (which passes 100
    /// under turbo). Without the counter, the reported clock stands in; nothing usable is null.
    /// </summary>
    public static double? EffectiveMhz(double? performancePercent, double baseMhz, double reportedMhz)
    {
        if (performancePercent is { } p && double.IsFinite(p) && p > 0 && double.IsFinite(baseMhz) && baseMhz > 0)
        {
            return baseMhz * p / 100;
        }

        return double.IsFinite(reportedMhz) && reportedMhz > 0 ? reportedMhz : null;
    }

    /// <summary>"3.42 GHz": two decimals, as Task Manager prints it.</summary>
    public static string Gigahertz(Locale locale, double mhz)
        => Strings.Format(locale, Str.Details_Units_Gigahertz, ("value", LocaleFormat.Number(locale, Math.Max(0, mhz) / 1000, 2)));

    /// <summary>
    /// [S-2407] The hottest plausible ACPI thermal zone, in °C, from the counter's Kelvin readings.
    /// Null when there is none: a zone at 0 K or at 400 °C is a sensor that is not wired, not a figure.
    /// </summary>
    public static double? HottestZone(IEnumerable<double> kelvins)
    {
        ArgumentNullException.ThrowIfNull(kelvins);
        double? hottest = null;

        foreach (var kelvin in kelvins)
        {
            if (!double.IsFinite(kelvin))
            {
                continue;
            }

            var celsius = kelvin - 273.15;

            if (celsius is >= MinCelsius and <= MaxCelsius && (hottest is null || celsius > hottest))
            {
                hottest = celsius;
            }
        }

        return hottest;
    }

    /// <summary>"23 °C", whole degrees.</summary>
    public static string Celsius(Locale locale, double celsius)
        => Strings.Format(locale, Str.Details_Units_Celsius, ("value", LocaleFormat.Number(locale, celsius, 0)));

    /// <summary>
    /// [S-2405] The processor's name as a person reads it: the registry's
    /// <c>ProcessorNameString</c> without the trademark marks, the "CPU @ 3.20GHz" tail and the
    /// padding some vendors put in it. Null for an empty or control-laden value.
    /// </summary>
    public static string? CleanName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Any(char.IsControl))
        {
            return null;
        }

        var text = raw.Replace("(R)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(TM)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("™", string.Empty, StringComparison.Ordinal);

        var at = text.IndexOf(" @ ", StringComparison.Ordinal);
        if (at > 0)
        {
            text = text[..at];
        }

        if (text.EndsWith(" CPU", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^4];
        }

        // The vendor and the generation say nothing a header has room for: "12th Gen Intel Core
        // i9-12900K" is "Core i9-12900K", "AMD Ryzen 7 5800X 8-Core Processor" is "Ryzen 7 5800X".
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count >= 2 && words[1].Equals("Gen", StringComparison.OrdinalIgnoreCase) && words[0].Length > 2 && char.IsAsciiDigit(words[0][0]))
        {
            words.RemoveRange(0, 2);
        }

        words.RemoveAll(w => w.Equals("Intel", StringComparison.OrdinalIgnoreCase) || w.Equals("AMD", StringComparison.OrdinalIgnoreCase));

        if (words.Count >= 2 && words[^1].Equals("Processor", StringComparison.OrdinalIgnoreCase) && words[^2].EndsWith("-Core", StringComparison.OrdinalIgnoreCase))
        {
            words.RemoveRange(words.Count - 2, 2);
        }
        else if (words.Count >= 1 && words[^1].Equals("Processor", StringComparison.OrdinalIgnoreCase))
        {
            words.RemoveAt(words.Count - 1);
        }

        text = words.Count > 0 ? string.Join(' ', words) : text;

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
        {
            if (c != ' ' || (builder.Length > 0 && builder[^1] != ' '))
            {
                builder.Append(c);
            }
        }

        var clean = builder.ToString().Trim();
        return clean.Length == 0 ? null : clean;
    }

    /// <summary>
    /// [S-2405] The sheet: the clock as the hero, utilization as its caption, then base speed, cores,
    /// logical processors, processes, threads, handles, up time and the thermal zone, in
    /// that order (the name is the header title). A figure Windows does not give is left out.
    /// </summary>
    public static DetailSheet Sheet(Locale locale, CpuFigures f)
    {
        ArgumentNullException.ThrowIfNull(f);
        var facts = new List<DetailFact>(10);

        void Add(Str label, string? value)
        {
            if (value is not null)
            {
                facts.Add(new DetailFact(Strings.Get(locale, label), value));
            }
        }

        Add(Str.Details_Cpu_BaseSpeed, f.BaseMhz is > 0 and var b ? Gigahertz(locale, b) : null);
        Add(Str.Details_Cpu_Cores, f.Cores is > 0 and var c ? LocaleFormat.Integer(locale, c) : null);
        Add(Str.Details_Cpu_Logical, f.Logical is > 0 and var l ? LocaleFormat.Integer(locale, l) : null);
        Add(Str.Details_Cpu_Processes, f.Processes is >= 0 and var p ? LocaleFormat.Integer(locale, p) : null);
        Add(Str.Details_Cpu_Threads, f.Threads is >= 0 and var t ? LocaleFormat.Integer(locale, t) : null);
        Add(Str.Details_Cpu_Handles, f.Handles is >= 0 and var h ? LocaleFormat.Integer(locale, h) : null);
        Add(Str.Details_Cpu_Uptime, f.Uptime is { } up ? CpuRules.Uptime(locale, up) : null);
        Add(Str.Details_Cpu_Temperature, f.Celsius is { } celsius ? Celsius(locale, celsius) : null);

        var caption = f.Load is { } load
            ? Strings.Format(locale, Str.Details_Cpu_Caption, ("percent", Formatting.Percent(locale, load)))
            : null;

        return f.Mhz is { } mhz
            ? new DetailSheet(Gigahertz(locale, mhz), caption, facts, [])
            : new DetailSheet("--", caption, facts, [], HeroAvailable: false);
    }
}
