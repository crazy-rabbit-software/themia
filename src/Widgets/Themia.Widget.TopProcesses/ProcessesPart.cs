using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.TopProcesses;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2410]..[S-2414] The busiest processes, ranked by processor or memory, one row each (or one per
/// program, grouped). The list scrolls, fades at its foot and is walked with the keys ([S-2404]). The
/// snapshot is the hub's, one per due tick for every list ([S-2501]). [S-2433] Each row leads with its
/// program's icon, as Task Manager's does: the executable's path is asked once per process (shared by
/// every list) and the icon once per path and size (<see cref="Themia.Sdk.WinUI.AppIconSource"/>).
/// </summary>
internal sealed class ProcessesPart : DetailPart
{
    /// <inheritdoc />
    public override CardOpen? Opens(WidgetConfig config) => TopProcessesModule.Opens;

    /// <summary>One for the app: every list shows the same processes, read on the same tick.</summary>
    private static readonly ProcessPathCache Paths = new(pid => ProcessPaths.ExePath((uint)pid));

    /// <summary>
    /// [S-2433] A system process Windows will not open (dwm, svchost, the antimalware service) by its
    /// image in System32, as Task Manager draws it; looked up once per name, null when there is none.
    /// </summary>
    private static readonly Dictionary<string, string?> SystemImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly WatchSlot<IReadOnlyList<ProcessSample>?> _processes = new();

    /// <summary>[S-2437] The read the rows on screen came from: what a row's End task ends.</summary>
    private IReadOnlyList<ProcessSample> _shown = [];
    private TopProcessesConfig _config = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new WidgetId("topProcesses");

    public override bool HasHero => false;

    public override int MaxRows => _config.MaxRows;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        _config = config as TopProcessesConfig ?? new TopProcessesConfig();

        // A new sort, grouping or ceiling is the same snapshot ranked again: shown at once.
        _processes.Hold(hub, Sources.Processes, _ => Show());
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_processes.HasValue)
        {
            return;
        }

        if (_processes.Value is not { } samples)
        {
            host.Present(new DetailSheet(null, null, [], [], Note: TopProcessesStr.Details_Processes_Unavailable.Text()));
            return;
        }

        // Forget the processes that exited before asking about new ones: a pid is soon someone else's.
        Paths.Retain(samples.Select(s => s.Process));
        _shown = samples;
        var ranked = ProcessTable.Rank(samples, _config.SortBy, _config.GroupByName);
        host.Present(new DetailSheet(null, null, [], ProcessTable.Rows(Strings.Current, ranked, _config.SortBy, _config.MaxRows, s => Paths.PathOf(s.Process) ?? SystemImage(s.Name), _config.GroupByName)));
    }

    /// <summary>
    /// [S-2437] A row's right-click menu: End task, for what the row stands for (every process of the
    /// program when grouped). Not on Themia's own row, and not on a row whose processes are gone.
    /// </summary>
    public override IReadOnlyList<ItemMenuEntry> RowMenu(DetailRow row)
    {
        var members = ProcessTable.Members(_shown, row.Key, _config.GroupByName);
        if (members.Count == 0 || members.Any(p => p.Pid == Environment.ProcessId))
        {
            return [];
        }

        var name = _shown.First(s => members.Contains(s.Process)).Name;
        return [new ItemMenuEntry(Glyphs.Stop, TopProcessesStr.Details_Processes_EndTask.Text(), () => End(name, members), Danger: true)];
    }

    private void End(string name, IReadOnlyList<ProcessIdentity> members)
    {
        var denied = 0;
        foreach (var process in members)
        {
            if (ProcessControl.End(process) == ProcessEndResult.Denied)
            {
                denied++;
            }
        }

        if (denied > 0)
        {
            Notices.Warning(
                TopProcessesStr.Details_Processes_EndDenied.Format(("name", name)),
                TopProcessesStr.Details_Processes_EndDeniedWhy.Text());
        }

        // The list shows the ended processes gone at once rather than at the next tick.
        _host?.SampleNow();
    }

    private static string? SystemImage(string name)
    {
        if (SystemImages.TryGetValue(name, out var known))
        {
            return known;
        }

        string? path = null;

        // An image name is untrusted text from the kernel's list: only a bare file name is looked up.
        if (name.Length is > 0 and < 64 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.Contains('.', StringComparison.Ordinal))
        {
            var candidate = Path.Combine(Environment.SystemDirectory, name + ".exe");
            path = File.Exists(candidate) ? candidate : null;
        }

        if (SystemImages.Count > 512)
        {
            SystemImages.Clear();
        }

        SystemImages[name] = path;
        return path;
    }

#if DEBUG
    /// <summary>[S-1917] A fixed set of processes, so the renders are one picture on every machine.</summary>
    public override DetailSheet? SampleSheet()
    {
        ProcessSample[] samples =
        [
            new("dwm", 0.08, 249L << 20), new("Themia", 0.07, 492L << 20, 9), new("MsMpEng", 0.04, 589L << 20), new("System", 0.02, 16L << 10),
            new("explorer", 0.012, 154L << 20), new("Karpo", 0.011, 43L << 20, 2), new("python", 0.01, 8L << 20), new("claude", 0.009, 1900L << 20, 14),
            new("csrss", 0.008, 2L << 20, 2), new("Memory Compression", 0.004, 9900L << 20), new("dotnet", 0.003, 2200L << 20, 24),
            new("VBCSCompiler", 0.002, 581L << 20), new("brave", 0.002, 524L << 20, 17), new("msedgewebview2", 0.001, 522L << 20, 26),
            new("Spotify", 0.001, 448L << 20, 8), new("svchost", 0.001, 366L << 20, 98),
        ];
        var ranked = ProcessTable.Rank(samples, _config.SortBy, _config.GroupByName);
        return new DetailSheet(null, null, [], ProcessTable.Rows(Strings.Current, ranked, _config.SortBy, _config.MaxRows, _ => null));
    }
#endif

    public override void Detach()
    {
        _shown = [];
        _processes.Release();
        _host = null;
    }
}
