#if DEBUG
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-5510] [S-5512] Debug only: <c>Themia.exe --check desktop-items &lt;out.txt&gt;</c> reads this PC's desktop
/// namespace once (read only: nothing is hidden, watched or changed) and writes what came back
/// without the user's file names: how many items of each kind, the shell items' class ids, each
/// file's extension, and whether each item's overlaid icon could be drawn. What tells a broken
/// enumeration or image-list call from a working one without looking at the user's desktop. A read
/// that fails fails the check.
/// </summary>
internal static class DesktopItemsCheck
{
    private const string Scope = "desktop-items-check";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "desktop-items",
        "<out.txt> [--search <ext> [<folder>]] [--key <key> <kind>]",
        """
        this PC's desktop namespace read once, read only, written without the user's file names: the items of each kind,
        the shell items' class ids, each file's extension, whether its overlaid icon draws; one name's items each their own
        key; PASS, or FAIL when the read fails [S-5510] [S-5512]
        --search <ext> [<folder>]   a search fence's search for one extension instead, timed [S-13023]
        --key <key> <kind>          one item's icon only (a key on no disk tests the type fallback)
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);
        var report = new CheckReport(Scope, outPath);
        void Say(string line) => report.Say(line);
        var args = run.Args.ToList();
        string? After(string option, int nth) => args.IndexOf(option) is var at and >= 0 && at + nth < args.Count ? args[at + nth] : null;

        try
        {
            // [S-13023] --search <ext> [folder]: a search fence's search for one extension, timed (no file names written).
            if (After("--search", 1) is { } extensions)
            {
                var rules = new FenceRuleSet { Extensions = FenceRules.ParseExtensions(extensions) };
                var query = new FenceQuery { Folder = After("--search", 2) ?? string.Empty, Max = FenceSearch.MostResults };
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var found = await Services.Files.FileSearch.FindAsync(rules, query, CancellationToken.None).ConfigureAwait(true);
                Say($"search: {found.Count} file(s) in {clock.ElapsedMilliseconds} ms for {FenceSearch.Aqs(rules, query, DateTime.UtcNow)}");
                foreach (var group in found.GroupBy(f => FenceRules.ExtensionOfKey(f.Path)))
                {
                    Say($"  {group.Key}: {group.Count()}, newest {group.Max(f => f.ModifiedUtc):yyyy-MM-dd}");
                }

                return await report.FinishAsync().ConfigureAwait(true);
            }

            // --key <key> <kind>: one item's icon only (a key on no disk tests the type fallback).
            IReadOnlyList<DesktopItem>? items = After("--key", 1) is { } key && Enum.TryParse<DesktopItemKind>(After("--key", 2), out var kindArg)
                ? [new DesktopItem(key, DesktopItemKeys.FallbackName(key), kindArg)]
                : await Task.Run(DesktopNamespace.Read).ConfigureAwait(true);
            if (items is null)
            {
                report.Say("read: failed (see the log)", 1);
            }
            else
            {
                Say($"read: {items.Count} item(s)");
                foreach (var kind in Enum.GetValues<DesktopItemKind>())
                {
                    Say($"  {kind}: {items.Count(i => i.Kind == kind)}");
                }

                var drawn = 0;
                foreach (var item in DesktopItemOrder.Sort(items))
                {
                    var icon = await AppIcons.LoadOverlaidAsync(item.Key, item.Name, item.Kind, 48).ConfigureAwait(true);
                    drawn += icon is null ? 0 : 1;
                    var shape = item.Kind == DesktopItemKind.ShellItem ? item.Key : "*" + Path.GetExtension(item.Key);
                    Say($"  {item.Kind,-9} {shape,-48} icon={(icon is null ? "none" : "ok")}");
                }

                Say($"icons: {drawn} of {items.Count}");

                // Items Explorer shows under one name (the user's and the Public desktop each holding it): each its own key.
                var keys = new HashSet<string>(items.Select(i => i.Key), DesktopItemKeys.Comparer);
                foreach (var shared in items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                {
                    var folders = shared.Select(i => Path.GetDirectoryName(i.Key) ?? "?").Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    Say($"one name, {shared.Count()} items: {folders} folder(s), keys distinct {shared.Select(i => i.Key).Distinct(DesktopItemKeys.Comparer).Count() == shared.Count()}, rooted {shared.All(i => Path.IsPathFullyQualified(i.Key))}");
                }

                Say($"keys: {keys.Count} distinct of {items.Count}");
            }
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }
}
#endif
