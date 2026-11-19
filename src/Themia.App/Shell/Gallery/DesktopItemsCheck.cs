#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-5510] [S-5512] Debug only: <c>--desktop-items-check &lt;out.txt&gt;</c> reads this PC's desktop
/// namespace once (read only: nothing is hidden, watched or changed) and writes what came back
/// without the user's file names: how many items of each kind, the shell items' class ids, each
/// file's extension, and whether each item's overlaid icon could be drawn. What tells a broken
/// enumeration or image-list call from a working one without looking at the user's desktop.
/// </summary>
internal static class DesktopItemsCheck
{
    private const string Scope = "desktop-check";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--desktop-items-check");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --desktop-items-check <out.txt>");
            app.Exit();
            return true;
        }

        var outPath = Path.GetFullPath(args[at + 1]);
        _ = dispatcher.TryEnqueue(async () =>
        {
            var lines = new List<string>();
            try
            {
                // [S-13023] --search <ext> [folder]: a search fence's search for one extension, timed (no file names written).
                var searchAt = Array.IndexOf(args, "--search");
                if (searchAt >= 0 && searchAt + 1 < args.Length)
                {
                    var rules = new FenceRuleSet { Extensions = FenceRules.ParseExtensions(args[searchAt + 1]) };
                    var query = new FenceQuery { Folder = searchAt + 2 < args.Length ? args[searchAt + 2] : string.Empty, Max = FenceSearch.MostResults };
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    var found = await Services.Files.FileSearch.FindAsync(rules, query, CancellationToken.None).ConfigureAwait(true);
                    lines.Add($"search: {found.Count} file(s) in {clock.ElapsedMilliseconds} ms for {FenceSearch.Aqs(rules, query, DateTime.UtcNow)}");
                    foreach (var group in found.GroupBy(f => FenceRules.ExtensionOfKey(f.Path)))
                    {
                        lines.Add($"  {group.Key}: {group.Count()}, newest {group.Max(f => f.ModifiedUtc):yyyy-MM-dd}");
                    }

                    await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
                    app.Exit();
                    return;
                }

                // --key <key> <kind>: one item's icon only (a key on no disk tests the type fallback).
                var keyAt = Array.IndexOf(args, "--key");
                IReadOnlyList<DesktopItem>? items = keyAt >= 0 && keyAt + 2 < args.Length && Enum.TryParse<DesktopItemKind>(args[keyAt + 2], out var kindArg)
                    ? [new DesktopItem(args[keyAt + 1], DesktopItemKeys.FallbackName(args[keyAt + 1]), kindArg)]
                    : await Task.Run(DesktopNamespace.Read).ConfigureAwait(true);
                if (items is null)
                {
                    lines.Add("read: failed (see the log)");
                }
                else
                {
                    lines.Add($"read: {items.Count} item(s)");
                    foreach (var kind in Enum.GetValues<DesktopItemKind>())
                    {
                        lines.Add($"  {kind}: {items.Count(i => i.Kind == kind)}");
                    }

                    var drawn = 0;
                    foreach (var item in DesktopItemOrder.Sort(items))
                    {
                        var icon = await AppIcons.LoadOverlaidAsync(item.Key, item.Name, item.Kind, 48).ConfigureAwait(true);
                        drawn += icon is null ? 0 : 1;
                        var shape = item.Kind == DesktopItemKind.ShellItem ? item.Key : "*" + Path.GetExtension(item.Key);
                        lines.Add($"  {item.Kind,-9} {shape,-48} icon={(icon is null ? "none" : "ok")}");
                    }

                    lines.Add($"icons: {drawn} of {items.Count}");

                    // Items Explorer shows under one name (the user's and the Public desktop each holding it): each its own key.
                    var keys = new HashSet<string>(items.Select(i => i.Key), DesktopItemKeys.Comparer);
                    foreach (var shared in items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                    {
                        var folders = shared.Select(i => Path.GetDirectoryName(i.Key) ?? "?").Distinct(StringComparer.OrdinalIgnoreCase).Count();
                        lines.Add($"one name, {shared.Count()} items: {folders} folder(s), keys distinct {shared.Select(i => i.Key).Distinct(DesktopItemKeys.Comparer).Count() == shared.Count()}, rooted {shared.All(i => Path.IsPathFullyQualified(i.Key))}");
                    }

                    lines.Add($"keys: {keys.Count} distinct of {items.Count}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the desktop items check threw", ex);
                lines.Add("threw: " + ex);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(Scope, $"could not write {outPath}", ex);
            }

            app.Exit();
        });

        return true;
    }
}
#endif
