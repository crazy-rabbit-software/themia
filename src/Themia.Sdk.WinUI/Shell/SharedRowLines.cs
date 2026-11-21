using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-11751] A kind's own lines built over a config, saved by <paramref name="save"/>: the module's.</summary>
public delegate WidgetMenuLines? LinesOver(WidgetId kind, WidgetConfig config, Action save);

/// <summary>
/// [S-11751] [S-11753] A shared setting's lines on several widgets' page: the very lines one
/// holder's module builds for it (marked <see cref="SettingsLine.Shares"/>), over a scratch copy of that
/// holder's config; every write they make (a pick, a toggle, a typed value, a place found by a lookup, run
/// once) is then set on every holder at once, as one change. While the holders disagree the lines read
/// "Mixed": a choice holds no option, a value or a toggle's words say Mixed, and any pick makes them agree.
/// </summary>
public static class SharedRowLines
{
    /// <param name="setting">The setting the widgets share.</param>
    /// <param name="mixed">Whether the holders disagree on it now.</param>
    /// <param name="holders">The widgets that may hold the setting (their kind and config), in view order: the first whose module offers its line builds it.</param>
    /// <param name="spread">Sets what the scratch copy holds on every holder (the host's one mutation; nothing where they agree).</param>
    /// <param name="built">Told of the lines built, so a line still reading ([S-3746]) can ask for the page again.</param>
    public static IReadOnlyList<SettingsLine> Of(
        SharedSetting setting,
        bool mixed,
        IReadOnlyList<(WidgetId Kind, WidgetConfig Config)> holders,
        LinesOver build,
        Action<WidgetConfig> spread,
        Action<WidgetMenuLines>? built = null)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(holders);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(spread);

        var holding = holders.Where(h => setting.Holds(h.Config)).ToList();
        for (var at = 0; at < holding.Count; at++)
        {
            var holder = holding[at];
            var scratch = holder.Config.Clone();
            void Spread() => spread(scratch);
            var lines = build(holder.Kind, scratch, Spread);
            var marked = lines is null ? null : Marked(lines, setting);
            if (lines is null || marked is not { Count: > 0 })
            {
                continue;
            }

            built?.Invoke(lines);
            var self = at;
            var differ = mixed ? Differing(marked, holding.Where((_, i) => i != self), setting, build) : null;
            return [.. marked.Select((line, i) => Wired(line, Spread, differ?[i] ?? false, top: true))];
        }

        return [];
    }

    private static List<SettingsLine> Marked(WidgetMenuLines lines, SharedSetting setting)
        => [.. lines.StyleLines.Concat(lines.Lines).Where(l => l.Shares == setting.Key)];

    /// <summary>
    /// [S-11751] Which of <paramref name="marked"/> read otherwise on any of <paramref name="others"/> (its value, its
    /// switch, its choice): those say Mixed. Where none can tell (the lines read alike), all of them do.
    /// </summary>
    private static bool[] Differing(List<SettingsLine> marked, IEnumerable<(WidgetId Kind, WidgetConfig Config)> others, SharedSetting setting, LinesOver build)
    {
        var differ = new bool[marked.Count];
        foreach (var other in others)
        {
            var theirs = build(other.Kind, other.Config.Clone(), () => { }) is { } lines ? Marked(lines, setting) : [];
            for (var i = 0; i < marked.Count; i++)
            {
                differ[i] |= i >= theirs.Count || Reading(marked[i]) != Reading(theirs[i]);
            }
        }

        if (!differ.Contains(true))
        {
            Array.Fill(differ, true);
        }

        return differ;
    }

    /// <summary>What a line reads as: what it says at its end, its switch, the option that holds.</summary>
    private static (string? Value, bool? On, string? Holds) Reading(SettingsLine line) => line switch
    {
        SettingsToggle toggle => (toggle.Value, toggle.On, null),
        SettingsRadio radio => (null, radio.Holds, null),
        SettingsChoice choice => (choice.Value, null, choice.Options.FirstOrDefault(o => o.Holds)?.Text),
        SettingsSubmenu submenu => (submenu.Value, null, submenu.Lines.OfType<SettingsRadio>().FirstOrDefault(r => r.Holds)?.Text),
        SettingsSearch search => (search.Value, null, null),
        SettingsSlider slider => (slider.ValueText, null, null),
        SettingsCommand command => (command.Value, null, null),
        SettingsPrompt prompt => (prompt.Value, null, null),
        SettingsAsk ask => (ask.Value, null, null),
        _ => (null, null, null),
    };

    /// <summary>
    /// <paramref name="line"/> with every write it makes followed by <paramref name="spread"/>, its children's too; reading
    /// Mixed where <paramref name="mixed"/> (a top line its value, a switch off with its words Mixed, no option holding).
    /// </summary>
    private static SettingsLine Wired(SettingsLine line, Action spread, bool mixed, bool top)
    {
        var wired = line switch
        {
            SettingsToggle toggle => toggle with { Set = on => After(() => toggle.Set(on), spread) },
            SettingsRadio radio => radio with { Pick = () => After(radio.Pick, spread) },
            SettingsCommand command => command with
            {
                Run = () => After(command.Run, spread),

                // [S-2351] What a line asks in place, answered, is set on every holder too.
                Confirm = command.Confirm is { } sure ? sure with { Done = () => After(sure.Done, spread) } : null,
            },
            SettingsLink link => link with { Run = () => After(link.Run, spread) },
            SettingsChoice choice => choice with
            {
                Options = [.. choice.Options.Select(o => (SettingsRadio)Wired(o, spread, mixed, top: false))],
                More = [.. choice.More.Select(l => Wired(l, spread, mixed, top: false))],
            },
            SettingsSubmenu submenu => submenu with { Lines = [.. submenu.Lines.Select(c => Wired(c, spread, mixed, top: false))] },
            SettingsSearch search => Wired(search, spread, mixed),

            // [S-2332] A text typed in place is set on every holder; while they disagree its box is empty, its hint Mixed.
            SettingsAsk ask => Wired(ask, spread, mixed && top),
            SettingsPrompt prompt => prompt with { Ask = prompt.Ask with { Done = typed => After(() => prompt.Ask.Done(typed), spread) } },
            _ => line,
        };

        if (!mixed || line is SettingsSeparator or SettingsNote)
        {
            return wired;
        }

        // [S-11751] A value they disagree on: no option of a choice holds, a top line's value and a switch's words say Mixed.
        if (wired is SettingsRadio held)
        {
            return held with { Holds = false };
        }

        return !top ? wired : wired switch
        {
            SettingsToggle toggle => toggle with { On = false, Value = Mixed },
            SettingsChoice choice => choice with { Value = Mixed },
            SettingsSubmenu submenu => submenu with { Value = Mixed },
            SettingsSearch search => search with { Value = Mixed },
            SettingsSlider slider => slider with { ValueText = Mixed },
            SettingsCommand command => command with { Value = Mixed },
            SettingsPrompt prompt => prompt with { Value = Mixed },
            SettingsAsk ask => ask with { Value = Mixed },
            _ => wired,
        };
    }

    private static string Mixed => Str.Groups_Page_Mixed.Text();

    private static SettingsSearch Wired(SettingsSearch search, Action spread, bool mixed) => search with
    {
        Find = async (typed, cancel) =>
        {
            var answer = await search.Find(typed, cancel).ConfigureAwait(true);
            return answer with { Lines = [.. answer.Lines.Select(l => Wired(l, spread, mixed, top: false))] };
        },
        Fixed = [.. search.Fixed.Select(l => Wired(l, spread, mixed, top: false))],
    };

    private static SettingsAsk Wired(SettingsAsk ask, Action spread, bool mixed)
    {
        var kept = ask with { Done = typed => After(() => ask.Done(typed), spread) };
        return mixed ? kept with { Initial = string.Empty, Placeholder = Mixed } : kept;
    }

    private static void After(Action first, Action then)
    {
        first();
        then();
    }
}
