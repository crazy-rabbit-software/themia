#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw the context menus ([S-1740]..[S-1744]) to PNGs, off-screen.
/// <c>Themia.exe --menu-png &lt;outDir&gt; [--theme light|dark]</c>. Menus draw in the app's theme (a popup does not
/// follow an element's <c>RequestedTheme</c>), on a transparent background (acrylic does not render).
/// <para>
/// Each menu is opened for real — the same <see cref="ItemMenu"/> a right-click opens — on a window
/// that never shows on the desktop, kept inside that window's bounds so it can be captured, and the
/// open popup's content is drawn. The card menus come from a real <see cref="WidgetCard"/> over a
/// stand-in host; the surface menu from the same builder the desktop uses.
/// </para>
/// </summary>
internal static class MenuRender
{
    private const string Scope = "menu-render";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--menu-png");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --menu-png <outDir>");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;

            try
            {
                System.IO.Directory.CreateDirectory(outDir);

                EditBarRender.ApplyDesignTheme(app);

                var root = new Grid
                {
                    Background = DesignApplier.Brush("SolidBackgroundFillColorBaseBrush", Colors.Black),
                };

                window = new Window { Content = root, Title = "Themia menu render" };
                UiLanguage.Apply(root);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)(900 * scale), (int)(900 * scale)));
                window.AppWindow.Show(activateWindow: false);

                await Task.Delay(400).ConfigureAwait(true);

                ticker = new Ticker(dispatcher);

                // A view with a second widget, so the layering commands have something to act on.
                var view = ViewState.Create("Render");
                var clock = WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.1, 0.1, 0.2, 0.2));
                var notes = WidgetInstance.Create(new WidgetId("notes"), new FracRect(0.5, 0.1, 0.3, 0.4));
                view.Widgets.Add(clock);
                view.Widgets.Add(notes);

                var host = new MenuRenderHost(ticker, view);
                var card = new WidgetCard(host, clock) { Width = 200, Height = 160 };
                root.Children.Add(new Canvas { Children = { card } });
                await Task.Delay(300).ConfigureAwait(true);

                host.Set(editMode: true, selection: 1, canPaste: true);
                await RenderAsync(root, card.CardMenu(), System.IO.Path.Combine(outDir, "card-edit.png")).ConfigureAwait(true);

                host.Set(editMode: false, selection: 3, canPaste: false);
                await RenderAsync(root, card.CardMenu(), System.IO.Path.Combine(outDir, "card-desktop-3.png")).ConfigureAwait(true);

                // [S-1758] Outside edit mode: the view's own commands and Edit layout, nothing that arranges.
                host.Set(editMode: false, selection: 1, canPaste: true);

                var drive = WidgetInstance.Create(new WidgetId("drive"), new FracRect(0.3, 0.6, 0.2, 0.2));
                view.Widgets.Add(drive);
                var withActions = new WidgetCard(host, drive) { Width = 200, Height = 120 };
                root.Children.Add(new Canvas { Children = { withActions } });
                await Task.Delay(300).ConfigureAwait(true);
                await RenderAsync(root, withActions.CardMenu(), System.IO.Path.Combine(outDir, "card-desktop-actions.png")).ConfigureAwait(true);

                var noop = static () => { };
                var surface = DesktopWindow.SurfaceEntries(
                    new SurfaceMenuFacts(EditMode: true, Placing: false, CanPaste: true, HasWidgets: true),
                    new SurfaceMenuActions(noop, noop, noop, noop, noop, noop, noop));
                await RenderAsync(root, surface, System.IO.Path.Combine(outDir, "surface.png")).ConfigureAwait(true);

                // An item menu in File Explorer's shape: the row on top, a submenu and a danger entry below.
                List<ItemMenuEntry> item =
                [
                    new(Glyphs.Open, "Open", noop, Shortcut: KeyChord.Plain(ChordKey.Enter)),
                    new(Glyphs.Folder, "Show in Explorer", noop),
                    ItemMenuEntry.Separator,
                    new(Glyphs.Cut, "Cut", noop, Quick: QuickCommand.Cut, Shortcut: KeyChord.Ctrl(ChordKey.X)),
                    new(Glyphs.Copy, "Copy", noop, Quick: QuickCommand.Copy, Shortcut: KeyChord.Ctrl(ChordKey.C)),
                    new(Glyphs.Paste, "Paste", noop, Disabled: true, Quick: QuickCommand.Paste),
                    new(Glyphs.Rename, "Rename", noop, Quick: QuickCommand.Rename),
                    new(Glyphs.Delete, "Delete", noop, Danger: true, Quick: QuickCommand.Delete),
                    ItemMenuEntry.Separator,
                    new(Glyphs.Add, "New", null, Children: [new(Glyphs.NewFolder, "Folder", noop)]),
                    new(Glyphs.Properties, "Properties", noop, Shortcut: KeyChord.Alt(ChordKey.Enter)),
                ];
                await RenderAsync(root, item, System.IO.Path.Combine(outDir, "item.png")).ConfigureAwait(true);

                // [S-1741] One quick command alone stays labelled in the list.
                List<ItemMenuEntry> lone =
                [
                    new(Glyphs.Open, "Open", noop),
                    new(Glyphs.Folder, "Show in Explorer", noop, Disabled: true),
                    ItemMenuEntry.Separator,
                    new(Glyphs.Delete, "Delete", noop, Danger: true, Quick: QuickCommand.Delete),
                ];
                await RenderAsync(root, lone, System.IO.Path.Combine(outDir, "item-lone.png")).ConfigureAwait(true);

                // [S-6416]..[S-6418] The folder widget's own menus, from a real view over read-only
                // Windows folders (its module's preview): a file's, its Open with submenu, a folder's,
                // and the background's.
                var folderId = new WidgetId("folder");
                if (ModuleHost.CreateView(folderId, new CardWidgetHost(host)) is IWidgetView folderView and IWidgetPreviewMenus menus)
                {
                    folderView.Attach(ticker, WidgetConfig.CreateDefault(folderId));
                    root.Children.Add(new Border { Width = 1, Height = 1, Child = (FrameworkElement)folderView });
                    foreach (var (name, entries) in await menus.PreviewMenusAsync().ConfigureAwait(true))
                    {
                        await RenderAsync(root, entries, System.IO.Path.Combine(outDir, $"folder-{name}.png")).ConfigureAwait(true);
                    }

                    folderView.Detach();
                }

                // [S-1757] The menus' pictures must cost kilobytes, not a screen-sized raster each.
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                Console.WriteLine($"menu: private {process.PrivateMemorySize64 / (1024.0 * 1024.0):0.0} MB after every menu");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the menus failed", ex);
            }
            finally
            {
                ticker?.Dispose();
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>Open the menu inside the window, draw the popup's content, close it.</summary>
    private static async Task RenderAsync(Grid root, IEnumerable<ItemMenuEntry> entries, string path)
    {
        if (ItemMenu.Build(entries) is not { } menu)
        {
            Console.WriteLine($"menu: nothing to show for {path}");
            return;
        }

        await ShowAndSaveAsync(root, menu, path).ConfigureAwait(true);
    }

    /// <summary>Open the menu inside the window, draw the popup's content, close it.</summary>
    private static async Task ShowAndSaveAsync(Grid root, FlyoutBase menu, string path)
    {
        menu.ShouldConstrainToRootBounds = true;
        menu.ShowAt(root, new FlyoutShowOptions { Position = new Point(16, 16), ShowMode = FlyoutShowMode.Standard });
        await Task.Delay(700).ConfigureAwait(true);

        // The first menu of a fresh window can take a moment longer to open.
        if (VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot).Count == 0)
        {
            await Task.Delay(1500).ConfigureAwait(true);
        }

        // The command bar and its list (the "overflow") can be separate popups; each is written, the
        // first as <name>.png and the others as <name>-<n>.png. Acrylic does not render to a bitmap,
        // so the PNGs have a transparent background.
        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot);
        if (popups.Count == 0)
        {
            Log.Error(Scope, $"no popup opened for {path}");
            Console.WriteLine($"menu: no popup opened for {path}");
        }

        for (var i = 0; i < popups.Count; i++)
        {
            if (popups[i].Child is not UIElement content)
            {
                continue;
            }

            var target = i == 0 ? path : System.IO.Path.ChangeExtension(path, null) + $"-{i}.png";
            await RenderPng.SaveAsync(content, target).ConfigureAwait(true);
            Console.WriteLine($"menu: wrote {target}");
        }

        menu.Hide();
        await Task.Delay(300).ConfigureAwait(true);
    }

    /// <summary>A card host whose mode, selection and clipboard the render sets.</summary>
    private sealed class MenuRenderHost(Ticker ticker, ViewState view) : IWidgetCardHost
    {
        private bool _editMode;
        private int _selection = 1;
        private bool _canPaste;

        public void Set(bool editMode, int selection, bool canPaste)
        {
            _editMode = editMode;
            _selection = selection;
            _canPaste = canPaste;
        }

        public ScreenMetrics Metrics { get; } = new(1920, 1080);

        public DesignState Design { get; } = new();

        public Ticker Ticker => ticker;

        public bool EditMode => _editMode;

        public ViewState? ViewForCards => view;

        public bool CanPaste => _canPaste;

        public int SelectionCount => _selection;

        public bool IsPreview => true;

        public bool IsSelected(WidgetCard card) => true;

        public void OnGeometryPreview(WidgetCard card)
        {
            // ignore: a render does not move cards
        }

        public void OnGeometryCommitted(WidgetCard card)
        {
            // ignore: as above
        }

        public void RequestSetup(WidgetCard card)
        {
            // ignore: the menu is drawn, never clicked
        }

        public bool RequestAccounts() => false;

        public void RequestDelete(WidgetCard card)
        {
            // ignore: as above
        }

        public void RequestEditMode(bool enabled)
        {
            // ignore: as above
        }

        public void RequestSelect(WidgetCard card)
        {
            // ignore: as above
        }

        public void RequestCopy(WidgetCard card)
        {
            // ignore: as above
        }

        public void RequestCut(WidgetCard card)
        {
            // ignore: as above
        }

        public void RequestPaste()
        {
            // ignore: as above
        }

        public void RequestSaveConfig()
        {
            // ignore: nothing is persisted
        }

        public void RequestTextInput(WidgetCard card, bool wanted)
        {
            // ignore: no keyboard here
        }

        public void KeyboardPress(WidgetCard card)
        {
            // ignore: as above
        }
    }
}
#endif
