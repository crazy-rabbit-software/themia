using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2420]..[S-2422] Free and used space, one row per drive, named and walked by the disk meter's own
/// volume list (re-walked at most every 30 s, so no drive is woken per tick), from the hub's disk space
/// feed. Enter on a row opens the drive in Explorer.
/// </summary>
internal sealed class DiskSpacePart : DetailPart
{
    private readonly WatchSlot<DiskSpaceSnapshot> _space = new();
    private DiskSpaceConfig _config = new();
    private IDetailHost? _host;

    public override WidgetKind Kind => WidgetKind.DiskSpace;

    public override bool HasHero => false;

    /// <summary>The name and share, the free space under them, a bar under that.</summary>
    public override double ListRowHeight => Fluent.RowHeight + Fluent.Space3;

    public override bool RowsOpen => true;

    public override bool RowsGrow => true;

    public override void Watch(SystemHub hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        _config = config as DiskSpaceConfig ?? new DiskSpaceConfig();

        // Another choice of drives is the same read, picked again: shown at once.
        _space.Hold(hub, Sources.DiskSpace, _config.TickInterval, _ => Show());
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_space.HasValue)
        {
            return;
        }

        var snapshot = _space.Value;
        var volumes = DiskSpaceRules.Select(snapshot.Walked, _config.Drives);
        var drives = new List<DriveSpace>(volumes.Count);

        foreach (var volume in volumes)
        {
            // A chosen drive the walk did not find is not connected: it was not asked, so nothing was woken.
            drives.Add(snapshot.Space.TryGetValue(volume.Root, out var space) ? space : new DriveSpace(volume, 0, 0));
        }

        host.Present(DiskSpaceRules.Sheet(Strings.Current, drives));
    }

    public override void Refresh() => _space.Current?.Refresh();

    public override void Open(DetailRow row) => ShellLauncher.Open(row.Key);

    public override IReadOnlyList<WidgetAction> ContextActions(IDetailHost host)
        => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), host.SampleNow)];

    public override void Detach()
    {
        _space.Release();
        _host = null;
    }
}
