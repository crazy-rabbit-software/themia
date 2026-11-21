#if DEBUG
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-14342] Debug only: every widget kind this build has, for the checks and the widget audit, from the catalog itself
/// (never a folder name or a regex over the sources, which missed the families' own ids).
/// <c>Themia.exe --render catalog &lt;out.json&gt;</c> writes <c>{"kinds": [{"id", "project", "traits", "styles",
/// "states"}]}</c>: the kind's id, the module project that declares it (a family declares several), its traits, its
/// styles and the states its renders offer (<c>--state</c>): each kind's card is built at its natural size with its
/// default config on a preview host, as a render builds it, and asked once it has loaded (a card that shows its Set up
/// is run for the preview first, as a render does). [S-69] <c>Themia.exe --render states &lt;Kind&gt;</c> prints one
/// kind's entry the same way, on stdout: never scrape a render's error text for them.
/// </summary>
internal static class CatalogDump
{
    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "catalog",
        "<out.json>",
        "every kind of the build: id, project, traits, styles, states [S-14342]",
        async run =>
        {
            var outPath = run.PathArg(0);
            var count = await WriteAsync(run, [.. ModuleCatalog.Widgets.Select(w => w.Id)], outPath).ConfigureAwait(true);
            Console.WriteLine($"catalog: wrote {count} kinds to {outPath}");
            return RenderCommandLine.Passed;
        });

    public static RenderCommand States { get; } = new(
        RenderVerb.Render,
        "states",
        "<Kind>",
        "one kind's catalog entry (id, project, traits, styles, states) as JSON on stdout [S-69]",
        async run =>
        {
            if (!ModuleCatalog.TryFind(run.Arg(0), out var picked))
            {
                throw run.Usage($"no kind '{run.Arg(0)}'");
            }

            await WriteAsync(run, [picked.Id], null).ConfigureAwait(true);
            return RenderCommandLine.Passed;
        });

    /// <summary>The entries of <paramref name="kinds"/>: to <paramref name="outPath"/> as the catalog, or one kind's entry on stdout.</summary>
    private static async Task<int> WriteAsync(RenderRun run, IReadOnlyList<WidgetId> kinds, string? outPath)
    {
        var page = new StackPanel();
        using var harness = RenderHarness.Open(run);
        harness.MakeWindow("Themia catalog", new ScrollViewer { Content = page }).Show(new SizeInt32(800, 600));
        harness.UseServices(sampleMachine: false);
        var host = harness.CardHost();

        // Every kind's card, as a render places it: its default config at its natural size.
        var cards = new List<WidgetCard>();
        var loaded = new List<Task>();
        foreach (var kind in kinds)
        {
            var model = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
            var natural = SizeFit.Of(model.Config).Natural;
            var card = harness.Track(new WidgetCard(host, model) { Width = natural.Width, Height = natural.Height });
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

        using var stream = outPath is null ? Console.OpenStandardOutput() : CreateFile(outPath);
        using var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        if (outPath is not null)
        {
            json.WriteStartObject();
            json.WriteStartArray("kinds");
        }

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

        if (outPath is not null)
        {
            json.WriteEndArray();
            json.WriteEndObject();
        }

        await json.FlushAsync().ConfigureAwait(true);
        return cards.Count;
    }

    private static FileStream CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.Create(path);
    }
}
#endif
