#if DEBUG
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-14342] Debug only: every widget kind this build has, for the checks and the widget audit, from the catalog itself
/// (never a folder name or a regex over the sources, which missed the families' own ids).
/// <c>Themia.exe --catalog-json &lt;out.json&gt;</c> writes <c>{"kinds": [{"id", "project", "traits", "styles",
/// "states"}]}</c>: the kind's id, the module project that declares it (a family declares several), its traits, its
/// styles and the states its renders offer (<c>--state</c>; what <c>--widget-png … --state &lt;unknown&gt;</c> lists):
/// each kind's card is built at its natural size with its default config on a preview host, as a render builds it, and
/// asked once it has loaded (a card that shows its Set up is run for the preview first, as a render does).
/// </summary>
internal static class CatalogDump
{
    public const string Flag = "--catalog-json";

    private const string Scope = "catalog-json";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Console.WriteLine($"usage: {Flag} <out.json>");
            Environment.Exit(64);
            return true;
        }

        var outPath = Path.GetFullPath(args[at + 1]);
        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            var cards = new List<WidgetCard>();
            try
            {
                window = new Window { Title = "Themia catalog" };
                var page = new StackPanel();
                window.Content = new ScrollViewer { Content = page };
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32(800, 600));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                AppServices.System = new Services.SystemData.SystemHub(ticker, dispatcher);
                AppServices.Gallery = new Services.GalleryService(ticker, dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker);

                // Every kind's card, as a render places it: its default config at its natural size.
                var loaded = new List<Task>();
                foreach (var widget in ModuleCatalog.Widgets)
                {
                    var model = WidgetInstance.Create(widget.Id, new FracRect(0, 0, 0.1, 0.1));
                    var natural = SizeFit.Of(model.Config).Natural;
                    var card = new WidgetCard(host, model) { Width = natural.Width, Height = natural.Height };
                    var shown = new TaskCompletionSource();
                    card.Loaded += (_, _) => shown.TrySetResult();
                    loaded.Add(shown.Task);
                    page.Children.Add(card);
                    cards.Add(card);
                }

                await Task.WhenAny(Task.WhenAll(loaded), Task.Delay(TimeSpan.FromSeconds(60))).ConfigureAwait(true);
                foreach (var card in cards.Where(c => c.ShowsSetup))
                {
                    card.RunForPreview();
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                using (var stream = File.Create(outPath))
                using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    json.WriteStartObject();
                    json.WriteStartArray("kinds");
                    foreach (var card in cards)
                    {
                        var kind = card.Model.Kind;
                        json.WriteStartObject();
                        json.WriteString("id", kind.Value);
                        json.WriteString("project", ModuleHost.Module(kind).GetType().Assembly.GetName().Name);
                        json.WriteString("traits", ModuleCatalog.Of(kind).Traits.ToString());
                        json.WriteStartArray("styles");
                        foreach (var style in WidgetStyles.For(kind))
                        {
                            json.WriteStringValue(style.Name);
                        }

                        json.WriteEndArray();
                        json.WriteStartArray("states");
                        foreach (var state in card.View is IWidgetPreviewState preview ? preview.PreviewStates : [])
                        {
                            json.WriteStringValue(state);
                        }

                        json.WriteEndArray();
                        json.WriteEndObject();
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                }

                Console.WriteLine($"catalog: wrote {cards.Count} kinds to {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "writing the catalog failed", ex);
                Console.WriteLine($"catalog: failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
            finally
            {
                foreach (var card in cards)
                {
                    card.Detach();
                }

                AppServices.System?.Dispose();
                AppServices.System = null;
                AppServices.Gallery?.Dispose();
                AppServices.Gallery = null;
                ticker?.Dispose();
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }
}
#endif
