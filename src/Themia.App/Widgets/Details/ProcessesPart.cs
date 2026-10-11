using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2410]..[S-2414] The busiest processes, ranked by processor or memory, one row each (or one per
/// program, grouped). The list scrolls, fades at its foot and is walked with the keys ([S-2404]). The
/// snapshot is the hub's, one per due tick for every list ([S-2501]).
/// </summary>
internal sealed class ProcessesPart : DetailPart
{
    private readonly WatchSlot<IReadOnlyList<ProcessSample>?> _processes = new();
    private TopProcessesConfig _config = new();
    private IDetailHost? _host;

    public override WidgetKind Kind => WidgetKind.TopProcesses;

    public override bool HasHero => false;

    public override int MaxRows => _config.MaxRows;

    public override void Watch(SystemHub hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        _config = config as TopProcessesConfig ?? new TopProcessesConfig();

        // A new sort, grouping or ceiling is the same snapshot ranked again: shown at once.
        _processes.Hold(hub, Sources.Processes, _config.TickInterval, _ => Show());
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
            host.Present(new DetailSheet(null, null, [], [], Note: Str.Details_Processes_Unavailable.Text()));
            return;
        }

        var ranked = ProcessTable.Rank(samples, _config.SortBy, _config.GroupByName);
        host.Present(new DetailSheet(null, null, [], ProcessTable.Rows(Strings.Current, ranked, _config.SortBy, _config.MaxRows)));
    }

    public override void Detach()
    {
        _processes.Release();
        _host = null;
    }
}
