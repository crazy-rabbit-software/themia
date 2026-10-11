using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2410]..[S-2414] The busiest processes, ranked by processor or memory, one row each (or one per
/// program, grouped). The list scrolls, fades at its foot and is walked with the keys ([S-2404]).
/// </summary>
internal sealed class ProcessesPart : DetailPart
{
    private TopProcessesConfig _config = new();
    private IDisposable? _lease;

    public override WidgetKind Kind => WidgetKind.TopProcesses;

    public override bool HasHero => false;

    public override int MaxRows => _config.MaxRows;

    public override bool ApplyConfig(WidgetConfig config, IDetailHost host)
    {
        var next = config as TopProcessesConfig ?? new TopProcessesConfig();
        var changed = next.SortBy != _config.SortBy || next.GroupByName != _config.GroupByName || next.MaxRows != _config.MaxRows;
        _config = next;
        return changed;
    }

    public override void Attach() => _lease ??= ProcessList.Acquire();

    public override void Sample(IDetailHost host)
    {
        var locale = Strings.Current;

        if (ProcessList.Read(_config.TickInterval) is not { } samples)
        {
            host.Present(new DetailSheet(null, null, [], [], Note: Str.Details_Processes_Unavailable.Text()));
            return;
        }

        var ranked = ProcessTable.Rank(samples, _config.SortBy, _config.GroupByName);
        host.Present(new DetailSheet(null, null, [], ProcessTable.Rows(locale, ranked, _config.SortBy, _config.MaxRows)));
    }

    public override void Detach()
    {
        _lease?.Dispose();
        _lease = null;
    }
}
