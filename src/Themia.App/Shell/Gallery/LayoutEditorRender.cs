#if DEBUG
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1411] Debug only: <c>Themia.exe --render layout-editor &lt;out.png&gt; [waitMs] [--empty]</c> draws the app-layout
/// editor off-screen over a demo state that is never saved, once it has settled ([S-15900]) and <c>waitMs</c> after that
/// when asked (a time on purpose: a moment of its opening; none by default), then exits. <c>--empty</c> shows a fresh one-pane layout.
/// Nothing is launched and no window but its own is touched.
/// </summary>
internal static class LayoutEditorRender
{
    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "layout-editor",
        "<out.png> [waitMs] [--empty]",
        """
        the app-layout editor over a demo state that is never saved: a desktop icon's three-pane layout "Morning" [S-1411] [S-1405];
        drawn once it has settled; waitMs: that long after (a moment on purpose); --empty: a fresh one-pane layout. Nothing is launched
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);
        var wait = run.Args.Count > 1 && int.TryParse(run.Args[1], out var ms) ? ms : 0;

        // A desktop icon, named by id, that is an own icon with an app layout: empty, or the demo layout.
        var view = ViewState.Create("Desktop");
        var widget = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.1, 0.1, 0.2, 0.2));
        widget.Config = StateJson.Merged(widget.Config, run.Has("--empty") ? EmptyLayout : DemoAppLayout);
        view.Widgets.Add(widget);
        var itemId = JsonNode.Parse(JsonSerializer.Serialize(widget.Config, StateJson.WidgetConfig))?["itemId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("the demo layout has no id");

        var state = new AppState { Views = [view], ActiveViewId = view.Id };

        // [S-1109] --lang: the host applies the state's language, so the demo state carries it.
        if (RenderLanguage.Setting(Environment.GetCommandLineArgs()) is { } language)
        {
            state.Preferences.Language = language;
        }

        using var desktop = StandInDesktop.Create("layout-editor-render", state);
        var editor = new LayoutEditorWindow(desktop.Host);
        try
        {
            // The editor's own window, off-screen and never activated: it must not take the user's focus or appear on their desktop.
            var window = editor.DebugWindow;
            var dpi = Win32.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
            var scale = dpi > 0 ? dpi / 96.0 : 1.0;
            window.AppWindow.Move(new PointInt32(-32000, -32000));
            window.AppWindow.Resize(new SizeInt32((int)(1000 * scale), (int)(900 * scale)));
            window.AppWindow.Show(activateWindow: false);

            if (editor.DebugRoot is Panel panel)
            {
                // What Mica falls back to; the backdrop itself is not in a RenderTargetBitmap.
                panel.Background = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.Black);
            }

            editor.DebugShow(itemId);
            await RenderHarness.SettleAsync(editor.DebugRoot, Path.GetFileName(outPath)).ConfigureAwait(true);
            if (wait > 0)
            {
                // A time on purpose: the moment after it settled that the command line asked to see.
                await Task.Delay(wait).ConfigureAwait(true);
            }

            await RenderHarness.SaveAsync(editor.DebugRoot, outPath, "layout editor").ConfigureAwait(true);
            return RenderCommandLine.Passed;
        }
        finally
        {
            editor.Close();
        }
    }

    /// <summary>A new layout: one empty pane.</summary>
    private const string EmptyLayout = """{"itemId":"l","layout":{"id":"p","kind":"Pane"}}""";

    /// <summary>
    /// [S-1405] A demo app layout, "Morning": three panes (Notepad on the left 60 %, GitHub over the
    /// Windows folder on the right). Never launched: the render only draws the editor.
    /// </summary>
    private const string DemoAppLayout = """
        {"itemId":"l","label":"Morning","layout":{"id":"r","kind":"Split","direction":"Vertical","ratio":0.6,
          "a":{"id":"p1","kind":"Pane","assignment":{"source":"App","target":"C:\\Windows\\notepad.exe","label":"Notepad"}},
          "b":{"id":"s","kind":"Split","direction":"Horizontal","ratio":0.5,
            "a":{"id":"p2","kind":"Pane","assignment":{"source":"Url","target":"https://github.com"}},
            "b":{"id":"p3","kind":"Pane","assignment":{"source":"Folder","target":"C:\\Windows"}}}}}
        """;
}
#endif
