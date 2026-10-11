using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Details;

/// <summary>
/// [S-2420]..[S-2422] Free and used space, one row per drive, named and walked by the disk meter's
/// own volume list (<see cref="DiskMetrics.Volumes"/>, re-walked at most every 30 s, so no drive is
/// woken per tick). Enter on a row opens the drive in Explorer.
/// </summary>
internal sealed class DiskSpacePart : DetailPart
{
    private DiskSpaceConfig _config = new();
    private string? _lastFailure;

    public override WidgetKind Kind => WidgetKind.DiskSpace;

    public override bool HasHero => false;

    /// <summary>The name and share, the free space under them, a bar under that.</summary>
    public override double ListRowHeight => Fluent.RowHeight + Fluent.Space3;

    public override bool RowsOpen => true;

    public override bool RowsGrow => true;

    public override bool ApplyConfig(WidgetConfig config, IDetailHost host)
    {
        var next = config as DiskSpaceConfig ?? new DiskSpaceConfig();
        var changed = !next.Drives.SequenceEqual(_config.Drives, StringComparer.OrdinalIgnoreCase);
        _config = next;
        return changed;
    }

    public override void Sample(IDetailHost host)
    {
        var walked = DiskMetrics.Volumes(refresh: true);
        var volumes = DiskSpaceRules.Select(walked, _config.Drives);
        var drives = new List<DriveSpace>(volumes.Count);

        foreach (var volume in volumes)
        {
            // A chosen drive the walk did not find is not connected: it is not asked, so nothing is woken.
            drives.Add(walked.Contains(volume) ? Read(volume) : new DriveSpace(volume, 0, 0));
        }

        host.Present(DiskSpaceRules.Sheet(Strings.Current, drives));
    }

    /// <summary>One drive's space, or a drive that did not answer (Total 0), logged once on a change.</summary>
    private DriveSpace Read(DiskVolume volume)
    {
        try
        {
            // Free to this user (quotas included), as Explorer counts it. The walk found the drive
            // ready, so this is GetDiskFreeSpaceEx on a mounted volume, not a wake-up.
            var drive = new DriveInfo(volume.Root);
            return new DriveSpace(volume, (ulong)Math.Max(0, drive.TotalSize), (ulong)Math.Max(0, drive.AvailableFreeSpace));
        }
        catch (IOException ex)
        {
            Note($"{volume.Root} did not answer: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Note($"{volume.Root} refused: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            Note($"{volume.Root} is not a drive: {ex.Message}");
        }

        return new DriveSpace(volume, 0, 0);
    }

    private void Note(string failure)
    {
        if (failure != _lastFailure)
        {
            _lastFailure = failure;
            Log.Debug(Scope, failure);
        }
    }

    public override void Open(DetailRow row) => ShellLauncher.Open(row.Key);

    public override IReadOnlyList<WidgetAction> ContextActions(IDetailHost host)
        => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), host.SampleNow)];
}
