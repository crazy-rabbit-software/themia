#if DEBUG
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services.Files;
using Themia.Core.Files;
using Themia.Core.Shell;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: <c>Themia.exe --check drop &lt;outDir&gt;</c> exercises the surface's drop plumbing
/// off-screen ([S-1026]) and writes <c>drop-check.txt</c>: the drop target on every window of a WinUI
/// window (the content bridge and input site included), a shell data object read back as a drag, a
/// forwarded hover and drop into a scratch folder under <c>outDir</c> (the copy must arrive: the check
/// fails without it, without the registration or without the data object), the drag-image helper and the
/// caption, and what a drag of our own offers ([S-5549]: its marker, its files as storage items handed out
/// when asked, read by the surface from its offer). No input is sent, so no drag is started, and nothing
/// outside <c>outDir</c> is touched.
/// </summary>
internal static partial class DropCheck
{
    private const string Scope = "drop-check";
    private const string FileName = "dropped é.txt";

    /// <summary>The longest the shell's copy is given to arrive (it may copy on its own thread).</summary>
    private static readonly TimeSpan CopyCeiling = TimeSpan.FromSeconds(5);

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "drop",
        "<outDir>",
        """
        the surface's drop plumbing off-screen, to <outDir>/drop-check.txt: the drop target on every window of a WinUI
        window, a shell data object read back as a drag, a hover and a drop forwarded into a scratch folder under <outDir>
        (the copy must arrive), the drag-image helper and the caption [S-1026], what a drag of our own offers [S-5549];
        no input sent, nothing outside <outDir> touched; PASS or FAIL
        """,
        run => RunAsync(run.PathArg(0)));

    private static async Task<int> RunAsync(string outDir)
    {
        var report = new CheckReport(Scope, System.IO.Path.Combine(outDir, "drop-check.txt"));
        OffscreenWindow? window = null;
        IDisposable? registration = null;

        try
        {
            Directory.CreateDirectory(outDir);

            // AllowDrop on the content, so XAML would register its own target if it ever does.
            var content = new Grid { AllowDrop = true };
            window = OffscreenWindow.Open("Themia drop check", content, 400, 300);
            var hwnd = window.Hwnd;
            await Task.Delay(1000).ConfigureAwait(true);

            report.Say("before registration:");
            Walk(hwnd, 0, report);

            registration = DragDropInterop.RegisterDropTarget(hwnd, new Recorder(report));
            report.Say($"registered: {registration is not null}", registration is null ? 1 : 0);
            report.Say("after registration:");
            Walk(hwnd, 0, report);

            await Forward(outDir, hwnd, report).ConfigureAwait(true);

            registration?.Dispose();
            registration = null;
            report.Say("after revoking:");
            Walk(hwnd, 0, report);
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }
        finally
        {
            registration?.Dispose();
            window?.Dispose();
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }

    private static async Task Forward(string outDir, nint hwnd, CheckReport report)
    {
        var source = System.IO.Path.Combine(outDir, "src");
        var destination = System.IO.Path.Combine(outDir, "dst");

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        var file = System.IO.Path.Combine(source, FileName);
        await File.WriteAllTextAsync(file, "drop check").ConfigureAwait(true);

        using var data = DragDropInterop.CreateDataObject([file], DropEffect.None);
        report.Say($"data object: {!data.IsNull}", data.IsNull ? 1 : 0);

        if (data.IsNull)
        {
            return;
        }

        using var helper = DragDropInterop.CreateDropHelper();
        report.Say($"drag-image helper: {!helper.IsNull}");

        using var drag = new Themia.App.Widgets.WidgetDrag(data.Pointer, SurfaceDrops.KeyLeftButton, -31900, -31900, DropEffect.Copy | DropEffect.Move | DropEffect.Link);
        report.Say($"drag content: {drag.Content}; paths: {string.Join(" | ", drag.Paths)}; urls: {drag.Urls.Count}; virtual: {drag.VirtualFiles}");

        if (!helper.IsNull)
        {
            DragDropInterop.HelperEnter(helper.Pointer, hwnd, data.Pointer, -31900, -31900, DropEffect.Copy);
        }

        DragDropInterop.SetDescription(data.Pointer, SurfaceDrops.Feedback(DropEffect.Copy, "dst"));
        DragDropInterop.SetDescription(data.Pointer, DropFeedback.Refused);
        report.Say("caption set and cleared");

        DragDropInterop.SetDescription(data.Pointer, DropFeedback.Plain(DropEffect.Move));
        DragDropInterop.SetDescription(data.Pointer, DropFeedback.Refused);
        report.Say("plain caption set and cleared");

        await OwnDrag(file, source, destination, data.Pointer, report).ConfigureAwait(true);

        report.Say($"forwarded hover into dst (forced copy): {drag.ForwardOver(destination, DropEffect.Copy)}");
        report.Say($"forwarded hover into src (forced copy, folder switched): {drag.ForwardOver(source, DropEffect.Copy)}");

        var done = drag.ForwardDrop(destination, DropEffect.Copy);
        report.Say($"forwarded drop into dst: {done}");

        if (!helper.IsNull)
        {
            DragDropInterop.HelperDrop(helper.Pointer, data.Pointer, -31900, -31900, done);
        }

        // The shell may copy on its own thread: the check waits for the file to be made.
        var arrived = await ArrivesAsync(System.IO.Path.Combine(destination, FileName)).ConfigureAwait(true);
        var kept = File.Exists(file);
        report.Say($"copy arrived: {arrived}; source kept: {kept}", arrived && kept ? 0 : 1);
    }

    /// <summary>Whether <paramref name="path"/> is there, or is made within <see cref="CopyCeiling"/> (watched, not polled).</summary>
    private static async Task<bool> ArrivesAsync(string path)
    {
        using var watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(path)!, System.IO.Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.FileName,
        };
        var made = new TaskCompletionSource();
        watcher.Created += (_, _) => made.TrySetResult();
        watcher.Renamed += (_, _) => made.TrySetResult();
        watcher.EnableRaisingEvents = true;
        if (!File.Exists(path))
        {
            // a timing, not a wait: the ceiling on the shell's copy; the file's arrival ends it.
            await Task.WhenAny(made.Task, Task.Delay(CopyCeiling)).ConfigureAwait(true);
        }

        return File.Exists(path);
    }

    /// <summary>
    /// [S-5549] [S-5586] A drag of our own, short of starting it: what its package offers (the marker
    /// always, the files as storage items only when asked: the provider must hand them out), a drag with
    /// no file (a shell item alone), and the surface's reading of it from the offer, with the shell's own
    /// data object forwarded to a folder's target.
    /// </summary>
    private static async Task OwnDrag(string file, string source, string destination, nint oleData, CheckReport report)
    {
        var offer = new ItemDragOffer([file, source, "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"], DropEffect.Copy | DropEffect.Move | DropEffect.Link);
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        Themia.Sdk.WinUI.ItemDrag.Offer(package, offer);
        var view = package.GetView();
        report.Say($"own drag: marker {view.Contains(Themia.Sdk.WinUI.ItemDrag.Marker)}; storage items offered {view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)}; operation {view.RequestedOperation}");
        var items = await view.GetStorageItemsAsync();
        report.Say($"own drag: storage items when asked: {string.Join(" | ", items.Select(item => $"{(item.IsOfType(Windows.Storage.StorageItemTypes.Folder) ? "folder" : "file")} {item.Path}"))}");

        var bare = new Windows.ApplicationModel.DataTransfer.DataPackage();
        Themia.Sdk.WinUI.ItemDrag.Offer(bare, new ItemDragOffer(["::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"], DropEffect.Move));
        var bareView = bare.GetView();
        report.Say($"own drag with no file: marker {bareView.Contains(Themia.Sdk.WinUI.ItemDrag.Marker)}; storage items offered {bareView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)}; formats {string.Join(", ", bareView.AvailableFormats)}");

        // The surface's side: read from the offer (the OLE object stands in for the source's), forwarded as the shell's own.
        using var own = new Themia.App.Widgets.WidgetDrag(oleData, SurfaceDrops.KeyLeftButton, -31900, -31900, offer.Effects, new ItemDragOffer([source], offer.Effects));
        report.Say($"own drag on the surface: content {own.Content}; paths: {string.Join(" | ", own.Paths)}; shell items read: {own.ReadShellItems().Count}");
        report.Say($"own drag forwarded hover into dst (forced copy): {own.ForwardOver(destination, DropEffect.Copy)}");

        using var nothing = new Themia.App.Widgets.WidgetDrag(oleData, SurfaceDrops.KeyLeftButton, -31900, -31900, DropEffect.Move, new ItemDragOffer([], DropEffect.Move));
        report.Say($"own drag with no file on the surface: content {nothing.Content}; forwarded hover: {nothing.ForwardOver(destination, DropEffect.None)}");
    }

    private static unsafe void Walk(nint hwnd, int depth, CheckReport report)
    {
        var name = stackalloc char[128];
        var length = Win32.GetClassName(hwnd, name, 128);
        var cls = new string(name, 0, Math.Max(0, length));
        var prop = GetPropW(hwnd, "OleDropTargetInterface");
        report.Say($"  {new string(' ', depth * 2)}'{cls}' drop target: {(prop != 0 ? "yes" : "no")}");

        for (var child = Win32.FindWindowEx(hwnd, 0, null, null); child != 0; child = Win32.FindWindowEx(hwnd, child, null, null))
        {
            Walk(child, depth + 1, report);
        }
    }

    /// <summary>Nothing drags over the off-screen window; a call here would be news.</summary>
    private sealed class Recorder(CheckReport report) : DragDropInterop.IOleDropHandler
    {
        public DropEffect Enter(nint data, uint keys, int screenX, int screenY, DropEffect allowed)
        {
            report.Say("unexpected enter");
            return DropEffect.None;
        }

        public DropEffect Over(uint keys, int screenX, int screenY, DropEffect allowed) => DropEffect.None;

        public void Leave()
        {
            // ignore: no drag is ever started over the check's window
        }

        public DropEffect Drop(nint data, uint keys, int screenX, int screenY, DropEffect allowed) => DropEffect.None;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetPropW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetPropW(nint hwnd, string name);
}
#endif
