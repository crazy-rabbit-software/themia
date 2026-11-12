using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-11751] A kind's own lines built over a config, saved by <paramref name="save"/>: the module's, with the host's asks.</summary>
internal delegate WidgetMenuLines? LinesOver(WidgetId kind, WidgetConfig config, Action save, Action<WidgetTextAsk> ask, Action<WidgetConfirm> confirm);

/// <summary>
/// [S-11751] [S-11753] A shared setting's lines on a group's page (or several widgets'): the very lines one
/// holder's module builds for it (marked <see cref="ItemMenuEntry.Shares"/>), over a scratch copy of that
/// holder's config; every write they make (a pick, a toggle, a typed value, a place found by a lookup, run
/// once) is then set on every holder at once, as one change. While the holders disagree the lines read
/// "Mixed": a choice holds no option, a value or a toggle's words say Mixed, and any pick makes them agree.
/// </summary>
internal static class SharedRowLines
{
    /// <param name="holders">The widgets that hold the setting, in view order: the first whose module offers its line builds it.</param>
    /// <param name="spread">Sets what the scratch copy holds on every holder (the host's one mutation; nothing where they agree).</param>
    /// <param name="built">Told of the lines built, so a line still reading ([S-3746]) can ask for the page again.</param>
    public static IReadOnlyList<ItemMenuEntry> Of(
        SharedRow row,
        IReadOnlyList<WidgetInstance> holders,
        LinesOver build,
        Action<WidgetTextAsk> ask,
        Action<WidgetConfirm> confirm,
        Action<WidgetConfig> spread,
        Action<WidgetMenuLines>? built = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(holders);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(spread);

        var holding = holders.Where(h => row.Setting.Holds(h.Config)).ToList();
        foreach (var holder in holding)
        {
            var scratch = holder.Config.Clone();
            void Spread() => spread(scratch);
            var lines = build(
                holder.Kind,
                scratch,
                Spread,
                question => ask(question with { Done = typed => After(() => question.Done(typed), Spread) }),
                sure => confirm(sure with { Done = () => After(sure.Done, Spread) }));
            var marked = lines is null ? null : Marked(lines, row.Setting);
            if (lines is null || marked is not { Count: > 0 })
            {
                continue;
            }

            built?.Invoke(lines);
            var differ = row.Mixed ? Differing(marked, holding.Where(h => !ReferenceEquals(h, holder)), row.Setting, build) : null;
            return [.. marked.Select((line, at) => Wired(line, Spread, differ?[at] ?? false, top: true))];
        }

        return [];
    }

    private static List<ItemMenuEntry> Marked(WidgetMenuLines lines, SharedSetting setting)
        => [.. lines.StyleLines.Concat(lines.Lines).Where(l => l.Shares == setting.Key)];

    /// <summary>
    /// [S-11751] Which of the lines read differently on another holder (its value, its switch, the option
    /// that holds): those say Mixed, a line all of them read alike does not (Use my location stays Off while
    /// only the places differ). Where no line shows the difference (two places under one name), all do.
    /// </summary>
    private static bool[] Differing(List<ItemMenuEntry> marked, IEnumerable<WidgetInstance> others, SharedSetting setting, LinesOver build)
    {
        var differ = new bool[marked.Count];
        foreach (var other in others)
        {
            var theirs = build(other.Kind, other.Config.Clone(), () => { }, _ => { }, _ => { }) is { } lines ? Marked(lines, setting) : [];
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

    /// <summary>What a line reads as: its value, its switch, the option of its choice that holds.</summary>
    private static (string? Value, bool? On, string? Holds) Reading(ItemMenuEntry line)
        => (line.Value ?? line.Search?.Value, line.Checked, line.Children?.FirstOrDefault(c => c is { Checked: true, Group: not null }).Text);

    /// <summary>
    /// The line with every write it makes followed by the spread (a pick of what the scratch copy already
    /// holds then still makes the holders agree); while mixed, read as no option and "Mixed".
    /// </summary>
    private static ItemMenuEntry Wired(ItemMenuEntry line, Action spread, bool mixed, bool top)
    {
        if (line.IsSeparator || line.Note)
        {
            return line;
        }

        var wired = line with
        {
            Action = line.Action is { } run ? () => After(run, spread) : null,
            Children = line.Children is { } children ? [.. children.Select(c => Wired(c, spread, mixed, top: false))] : null,
            Search = line.Search is { } search ? Wired(search, spread, mixed) : null,
        };

        if (!mixed)
        {
            return wired;
        }

        // [S-11751] A value they disagree on: no option of a choice holds, a top line's value and a switch's words say Mixed.
        if (line.Group is not null && line.Checked is not null)
        {
            return wired with { Checked = false };
        }

        return top ? wired with { Value = Str.Groups_Page_Mixed.Text(), Checked = line.Checked is null ? null : false } : wired;
    }

    /// <summary>A lookup's answers and fixed lines wired as the line is: a place found once is set on every holder.</summary>
    private static MenuSearch Wired(MenuSearch search, Action spread, bool mixed) => search with
    {
        Find = async (typed, cancel) =>
        {
            var answer = await search.Find(typed, cancel).ConfigureAwait(true);
            return answer with { Lines = [.. answer.Lines.Select(l => Wired(l, spread, mixed, top: false))] };
        },
        Fixed = [.. search.Fixed.Select(l => Wired(l, spread, mixed, top: false))],
    };

    private static void After(Action first, Action then)
    {
        first();
        then();
    }
}
