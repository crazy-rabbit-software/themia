#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1411] Debug only: <c>Themia.exe --layout-editor-png &lt;out.png&gt; [waitMs] [--empty]</c> draws
/// the app-layout editor off-screen over a demo state that is never saved, then exits. <c>--empty</c> shows a fresh one-pane layout.
/// Nothing is launched and no window but its own is touched.
/// </summary>
internal static class LayoutEditorRender
{
    private const string Scope = "layout-editor-render";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--layout-editor-png");

        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --layout-editor-png <out.png> [waitMs] [--empty]");
            app.Exit();
            return true;
        }

        var outPath = Path.GetFullPath(args[at + 1]);
        var wait = at + 2 < args.Length && int.TryParse(args[at + 2], out var ms) ? ms : 2500;
        var empty = args.Contains("--empty");

        _ = RenderAsync(app, dispatcher, outPath, wait, empty);
        return true;
    }

    private static async Task RenderAsync(Application app, DispatcherQueue dispatcher, string outPath, int wait, bool empty)
    {
        AppHost? host = null;
        LayoutEditorWindow? editor = null;

        try
        {
            var item = empty ? ShortcutItem.CreateLayout() : DemoAppLayout();
            var view = ViewState.Create("Desktop");
            var widget = WidgetInstance.Create(ShortcutsConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.2, 0.2));
            widget.Config = new ShortcutsConfig { Items = [item] };
            view.Widgets.Add(widget);

            var statePath = Path.Combine(Path.GetTempPath(), "themia-layout-editor-render", "state.json");
            var state = new AppState { Views = [view], ActiveViewId = view.Id };

            // [S-1109] --lang: the host applies the state's language, so the demo state carries it.
            if (RenderLanguage.Setting(Environment.GetCommandLineArgs()) is { } language)
            {
                state.Preferences.Language = language;
            }

            host = new AppHost(dispatcher, AppStateStore.Detached(statePath), state);

            editor = new LayoutEditorWindow(host);
            var window = editor.DebugWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var dpi = Win32.GetDpiForWindow(hwnd);
            var scale = dpi > 0 ? dpi / 96.0 : 1.0;

            // Off-screen and never activated: it must not take the user's focus or appear on their desktop.
            window.AppWindow.Move(new PointInt32(-32000, -32000));
            window.AppWindow.Resize(new SizeInt32((int)(1000 * scale), (int)(900 * scale)));
            window.AppWindow.Show(activateWindow: false);

            if (editor.DebugRoot is Panel panel)
            {
                // What Mica falls back to; the backdrop itself is not in a RenderTargetBitmap.
                panel.Background = FluentLayout.Themed("SolidBackgroundFillColorBaseBrush", Colors.Black);
            }

            editor.DebugShow(item.Id);
            await Task.Delay(wait).ConfigureAwait(true);

            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await RenderPng.SaveAsync(editor.DebugRoot, outPath).ConfigureAwait(true);
            Console.WriteLine($"layout editor: wrote {outPath}");
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the layout editor render failed", ex);
            Console.Error.WriteLine($"layout editor: failed: {ex.Message}");
        }
        finally
        {
            editor?.Close();
            host?.Dispose();
            app.Exit();
        }
    }

    /// <summary>
    /// [S-1405] A demo app layout: three panes (an app, a site, a folder). Never launched: the
    /// render only draws the editor.
    /// </summary>
    private static ShortcutItem DemoAppLayout()
    {
        var item = ShortcutItem.CreateLayout("Morning");
        var root = item.Layout!;
        (root, _) = LayoutTree.Split(root, root.Id, SplitDirection.Vertical);
        LayoutTree.SetRatio(root, root.Id, 0.6);
        (root, _) = LayoutTree.Split(root, root.B!.Id, SplitDirection.Horizontal);
        LayoutTree.Assign(root, root.A!.Id, new LayoutAssignment { Source = LayoutSource.App, Target = @"C:\Windows\notepad.exe", Label = "Notepad" });
        LayoutTree.Assign(root, root.B!.A!.Id, new LayoutAssignment { Source = LayoutSource.Url, Target = "https://github.com" });
        LayoutTree.Assign(root, root.B!.B!.Id, new LayoutAssignment { Source = LayoutSource.Folder, Target = @"C:\Windows" });
        item.Layout = root;
        return item;
    }
}
#endif
