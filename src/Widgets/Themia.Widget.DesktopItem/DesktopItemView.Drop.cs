using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Desktop;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5587] [S-9508] Drops onto the icon. A drop onto a desktop item, or onto an own icon whose target
/// is a local path or the Recycle Bin, is handed to that target's own shell drop target, as a drop
/// onto an icon of Explorer's desktop is (a folder takes the files, a program opens them, the bin
/// deletes them); the icon is drawn as the drop target while it accepts. While an own icon still
/// waits for setup, the first dropped thing the rule accepts becomes what it opens: a file, a folder,
/// a link, a web address from a browser, an app from the Start menu. Every other icon refuses, and an
/// icon in the drag takes no drop of itself.
/// <para>
/// <b>One drop path.</b> Nothing here registers an OLE target or sets XAML's <c>AllowDrop</c>: the
/// desktop surface owns the app's only drop target and forwards a drop that lands on this widget
/// through <see cref="IWidgetDropTarget"/>. Never throws into the caller (OLE is on the stack).
/// </para>
/// </summary>
internal sealed partial class DesktopItemView
{
    DropEffect IWidgetDropTarget.DragOver(IWidgetDrag drag, Point local)
    {
        try
        {
            var effect = Over(drag);
            MarkDropTarget(effect != DropEffect.None);
            return effect;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drag over a desktop icon failed", ex);
            MarkDropTarget(false);
            return DropEffect.None;
        }
    }

    void IWidgetDropTarget.DragLeave() => MarkDropTarget(false);

    DropEffect IWidgetDropTarget.Drop(IWidgetDrag drag, Point local)
    {
        try
        {
            MarkDropTarget(false);
            if (!Acts || _host.DesktopItems.Dragging(Key))
            {
                return DropEffect.None;
            }

            if (Own && _config.OpensNothing)
            {
                var effect = SetupEffect(drag);
                if (effect != DropEffect.None)
                {
                    SetUpFrom(drag);
                }

                return effect;
            }

            return ForwardTarget() is { } target ? drag.ForwardDrop(target, DropEffect.None) : DropEffect.None;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drop onto a desktop icon failed", ex);
            return DropEffect.None;
        }
    }

    /// <summary>What a drag over the icon would do, and where the caption says it lands ([S-1024]).</summary>
    private DropEffect Over(IWidgetDrag drag)
    {
        if (!Acts || _host.DesktopItems.Dragging(Key))
        {
            return DropEffect.None;
        }

        drag.Place = ShownName;
        if (Own && _config.OpensNothing)
        {
            return SetupEffect(drag);
        }

        return ForwardTarget() is { } target ? drag.ForwardOver(target, DropEffect.None) : DropEffect.None;
    }

    /// <summary>[S-5587] What a drop onto this icon is forwarded to (the desktop's one rule), or null. Nothing is probed here.</summary>
    private string? ForwardTarget() => DesktopCommands.DropTarget(DeskIcon.Of(_config, _ => false));

    /// <summary>[S-5549] The icon is a drag's drop target now (or no longer): the host says so to every icon, so one at most is lit.</summary>
    private void MarkDropTarget(bool on)
    {
        if (WidgetId is not { } id)
        {
            return;
        }

        if (on)
        {
            _host.DesktopItems.SetDropTarget(id);
        }
        else if (string.Equals(_host.DesktopItems.DropTargetWidget, id, StringComparison.Ordinal))
        {
            _host.DesktopItems.SetDropTarget(null);
        }
    }

    /// <summary>An own icon is made by reference, never by copying the file: Link where the source allows it, else Copy.</summary>
    private static DropEffect SetupEffect(IWidgetDrag drag)
        => drag.Paths.Count == 0 && drag.Urls.Count == 0 && !drag.HasShellItems ? DropEffect.None
            : drag.Allowed.HasFlag(DropEffect.Link) ? DropEffect.Link
            : drag.Allowed.HasFlag(DropEffect.Copy) ? DropEffect.Copy
            : DropEffect.None;

    /// <summary>
    /// [S-9508] The first dropped thing becomes the target. A Start menu app (shell items, no paths) is
    /// found by name among the Start menu's shortcuts, read on the thread pool.
    /// </summary>
    private void SetUpFrom(IWidgetDrag drag)
    {
        if (drag.Paths.Count > 0 || drag.Urls.Count > 0)
        {
            Take(OwnIconDrop.FromDrop(drag.Paths, drag.Urls, ResolveLink, ReadUrlFile));
            return;
        }

        if (drag.Data == 0)
        {
            return;
        }

        // Read now: the data object is only valid during the drop.
        var shellItems = drag.ReadShellItems();
        var paths = shellItems.Where(item => item.FileSystemPath is not null).Select(item => item.FileSystemPath!).ToList();

        if (paths.Count > 0)
        {
            Take(OwnIconDrop.FromDrop(paths, [], ResolveLink, ReadUrlFile));
            return;
        }

        var apps = shellItems.Where(item => item.DisplayName.Length > 0).Select(item => (item.DisplayName, item.ParsingName)).ToList();
        if (apps.Count > 0)
        {
            _ = TakeStartMenuAppAsync(apps);
        }
    }

    /// <summary>Set the shortcut up as <paramref name="dropped"/>, unless something set it up meanwhile; a drop the rule refuses is logged.</summary>
    private void Take(DroppedTarget? dropped)
    {
        if (dropped is null)
        {
            Log.Info(Scope, "a drop onto a desktop icon held nothing it may open; it still waits for its target");
            return;
        }

        if (!_attached || !_config.OpensNothing)
        {
            return;
        }

        dropped.ApplyTo(_config);
        Save();
    }

    private async Task TakeStartMenuAppAsync(IReadOnlyList<(string Name, string ParsingName)> dropped)
    {
        try
        {
            var apps = await Task.Run(() => _host.InstalledApps.Read(2000, CancellationToken.None)).ConfigureAwait(true);

            string? ShortcutOf(string name)
                => apps.FirstOrDefault(app => string.Equals(app.Name, name, StringComparison.OrdinalIgnoreCase)).Target;

            Take(OwnIconDrop.FromStartMenu(dropped, ShortcutOf, ResolveLink, ReadUrlFile));
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not make a desktop icon of the dropped Start menu app", ex);
        }
    }

    /// <summary>A dropped link's target, or null to keep the link (unreadable, it passes arguments or it has its own icon).</summary>
    private string? ResolveLink(string path)
        => LaunchTargets.MayProbe(LaunchTargets.Classify(path)) ? _host.FileOps.ResolveShortcut(path, plainOnly: true) : null;

    /// <summary>A dropped <c>.url</c> file's text: local only, and only as long as such a file can be.</summary>
    private static string? ReadUrlFile(string path)
    {
        if (!LaunchTargets.MayProbe(LaunchTargets.Classify(path)))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists || info.Length > OwnIconDrop.MaxUrlFileLength)
            {
                return null;
            }

            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log.Debug(Scope, $"could not read a dropped .url file: {ex.Message}");
            return null;
        }
    }
}
