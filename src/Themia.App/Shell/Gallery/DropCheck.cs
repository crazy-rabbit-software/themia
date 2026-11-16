#if DEBUG
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services.Files;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: <c>Themia.exe --drop-check &lt;outDir&gt;</c> exercises the surface's drop plumbing
/// off-screen ([S-1026]) and writes <c>drop-check.txt</c>: the drop target on every window of a WinUI
/// window (the content bridge and input site included), a shell data object read back as a drag, a
/// forwarded hover and drop into a scratch folder under <c>outDir</c> (the copy must arrive), the
/// drag-image helper and the caption, and what a drag of our own offers ([S-5549]: its marker, its
/// files as storage items handed out when asked, read by the surface from its offer). No input is sent,
/// so no drag is started, and nothing outside <c>outDir</c> is touched.
/// </summary>
internal static partial class DropCheck
{
    private const string Scope = "drop-check";
    private const string FileName = "dropped é.txt";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--drop-check");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --drop-check <outDir>");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);

        _ = dispatcher.TryEnqueue(async () =>
        {
            var lines = new List<string>();
            Window? window = null;
            IDisposable? registration = null;

            try
            {
                Directory.CreateDirectory(outDir);

                // AllowDrop on the content, so XAML would register its own target if it ever does.
                window = new Window { Content = new Grid { AllowDrop = true }, Title = "Themia drop check" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32(400, 300));
                window.AppWindow.Show(activateWindow: false);
                await Task.Delay(1000).ConfigureAwait(true);

                lines.Add("before registration:");
                Walk(hwnd, 0, lines);

                registration = DragDropInterop.RegisterDropTarget(hwnd, new Recorder(lines));
                lines.Add($"registered: {registration is not null}");
                lines.Add("after registration:");
                Walk(hwnd, 0, lines);

                await Forward(outDir, hwnd, lines).ConfigureAwait(true);

                registration?.Dispose();
                registration = null;
                lines.Add("after revoking:");
                Walk(hwnd, 0, lines);
            }
            catch (Exception ex)
            {
                lines.Add("FAILED: " + ex);
            }
            finally
            {
                registration?.Dispose();
                File.WriteAllLines(System.IO.Path.Combine(outDir, "drop-check.txt"), lines);
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    private static async Task Forward(string outDir, nint hwnd, List<string> lines)
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
        lines.Add($"data object: {!data.IsNull}");

        if (data.IsNull)
        {
            return;
        }

        using var helper = DragDropInterop.CreateDropHelper();
        lines.Add($"drag-image helper: {!helper.IsNull}");

        using var drag = new Themia.App.Widgets.WidgetDrag(data.Pointer, SurfaceDrops.KeyLeftButton, -31900, -31900, DropEffect.Copy | DropEffect.Move | DropEffect.Link);
        lines.Add($"drag content: {drag.Content}; paths: {string.Join(" | ", drag.Paths)}; urls: {drag.Urls.Count}; virtual: {drag.VirtualFiles}");

        if (!helper.IsNull)
        {
            DragDropInterop.HelperEnter(helper.Pointer, hwnd, data.Pointer, -31900, -31900, DropEffect.Copy);
        }

        DragDropInterop.SetDescription(data.Pointer, SurfaceDrops.Feedback(DropEffect.Copy, "dst"));
        DragDropInterop.SetDescription(data.Pointer, DropFeedback.Refused);
        lines.Add("caption set and cleared");

        DragDropInterop.SetDescription(data.Pointer, DropFeedback.Plain(DropEffect.Move));
        DragDropInterop.SetDescription(data.Pointer, DropFeedback.Refused);
        lines.Add("plain caption set and cleared");

        await OwnDrag(file, source, destination, data.Pointer, lines).ConfigureAwait(true);

        lines.Add($"forwarded hover into dst (forced copy): {drag.ForwardOver(destination, DropEffect.Copy)}");
        lines.Add($"forwarded hover into src (forced copy, folder switched): {drag.ForwardOver(source, DropEffect.Copy)}");

        var done = drag.ForwardDrop(destination, DropEffect.Copy);
        lines.Add($"forwarded drop into dst: {done}");

        if (!helper.IsNull)
        {
            DragDropInterop.HelperDrop(helper.Pointer, data.Pointer, -31900, -31900, done);
        }

        // The shell may copy on its own thread; the check waits for the file (a harness, not a correctness path).
        var arrived = System.IO.Path.Combine(destination, FileName);
        for (var i = 0; i < 50 && !File.Exists(arrived); i++)
        {
            await Task.Delay(100).ConfigureAwait(true);
        }

        lines.Add($"copy arrived: {File.Exists(arrived)}; source kept: {File.Exists(file)}");
    }

    /// <summary>
    /// [S-5549] [S-5586] A drag of our own, short of starting it: what its package offers (the marker
    /// always, the files as storage items only when asked: the provider must hand them out), a drag with
    /// no file (a shell item alone), and the surface's reading of it from the offer, with the shell's own
    /// data object forwarded to a folder's target.
    /// </summary>
    private static async Task OwnDrag(string file, string source, string destination, nint oleData, List<string> lines)
    {
        var offer = new ItemDragOffer([file, source, "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"], DropEffect.Copy | DropEffect.Move | DropEffect.Link);
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        Themia.Sdk.WinUI.ItemDrag.Offer(package, offer);
        var view = package.GetView();
        lines.Add($"own drag: marker {view.Contains(Themia.Sdk.WinUI.ItemDrag.Marker)}; storage items offered {view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)}; operation {view.RequestedOperation}");
        var items = await view.GetStorageItemsAsync();
        lines.Add($"own drag: storage items when asked: {string.Join(" | ", items.Select(item => $"{(item.IsOfType(Windows.Storage.StorageItemTypes.Folder) ? "folder" : "file")} {item.Path}"))}");

        var bare = new Windows.ApplicationModel.DataTransfer.DataPackage();
        Themia.Sdk.WinUI.ItemDrag.Offer(bare, new ItemDragOffer(["::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"], DropEffect.Move));
        var bareView = bare.GetView();
        lines.Add($"own drag with no file: marker {bareView.Contains(Themia.Sdk.WinUI.ItemDrag.Marker)}; storage items offered {bareView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)}; formats {string.Join(", ", bareView.AvailableFormats)}");

        // The surface's side: read from the offer (the OLE object stands in for the source's), forwarded as the shell's own.
        using var own = new Themia.App.Widgets.WidgetDrag(oleData, SurfaceDrops.KeyLeftButton, -31900, -31900, offer.Effects, new ItemDragOffer([source], offer.Effects));
        lines.Add($"own drag on the surface: content {own.Content}; paths: {string.Join(" | ", own.Paths)}; shell items read: {own.ReadShellItems().Count}");
        lines.Add($"own drag forwarded hover into dst (forced copy): {own.ForwardOver(destination, DropEffect.Copy)}");

        using var nothing = new Themia.App.Widgets.WidgetDrag(oleData, SurfaceDrops.KeyLeftButton, -31900, -31900, DropEffect.Move, new ItemDragOffer([], DropEffect.Move));
        lines.Add($"own drag with no file on the surface: content {nothing.Content}; forwarded hover: {nothing.ForwardOver(destination, DropEffect.None)}");
    }

    private static unsafe void Walk(nint hwnd, int depth, List<string> lines)
    {
        var name = stackalloc char[128];
        var length = Win32.GetClassName(hwnd, name, 128);
        var cls = new string(name, 0, Math.Max(0, length));
        var prop = GetPropW(hwnd, "OleDropTargetInterface");
        lines.Add($"  {new string(' ', depth * 2)}'{cls}' drop target: {(prop != 0 ? "yes" : "no")}");

        for (var child = Win32.FindWindowEx(hwnd, 0, null, null); child != 0; child = Win32.FindWindowEx(hwnd, child, null, null))
        {
            Walk(child, depth + 1, lines);
        }
    }

    /// <summary>Nothing drags over the off-screen window; a call here would be news.</summary>
    private sealed class Recorder(List<string> lines) : DragDropInterop.IOleDropHandler
    {
        public DropEffect Enter(nint data, uint keys, int screenX, int screenY, DropEffect allowed)
        {
            lines.Add("unexpected enter");
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
