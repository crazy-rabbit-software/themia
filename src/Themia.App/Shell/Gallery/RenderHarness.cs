#if DEBUG
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-69] Debug only: what every render shares, owned once. A render opens it on the UI thread
/// (<see cref="RenderCommands"/> made the hop), gets its off-screen window, the design its <c>--theme</c> and
/// <c>--design</c> ask for (put in force), a ticker, preview card hosts, the system and gallery services its cards read,
/// and the PNG writer, and disposes it: the cards detach, the services and the
/// ticker go and the window closes, whatever threw.
/// </summary>
internal sealed class RenderHarness : IDisposable
{
    private readonly List<WidgetCard> _cards = [];
    private readonly List<IDisposable> _owned = [];
    private bool _services;

    private RenderHarness(RenderRun run, OffscreenWindow window, IReadOnlyList<string> args)
    {
        Run = run;
        Window = window;
        Ticker = new Ticker(run.Dispatcher);
        Design = DesignArgs.From([.. args], RenderCommands.ThemeOf(Environment.GetCommandLineArgs()) ?? AppTheme.System);
    }

    public RenderRun Run { get; }

    public OffscreenWindow Window { get; }

    public Ticker Ticker { get; }

    /// <summary>The design the render draws with: its <c>--theme</c> (the application's) and its <c>--design</c> values.</summary>
    public DesignState Design { get; }

    /// <summary>
    /// Open the render's window (titled <paramref name="title"/>, holding <paramref name="content"/>, sized for
    /// <paramref name="width"/> × <paramref name="height"/> DIPs) and put its design in force. The themed brushes follow
    /// the design's theme, not the application's: both are set. The design's <c>--design</c> is read from
    /// <paramref name="args"/> (a batch job's), else the run's.
    /// </summary>
    public static RenderHarness Open(RenderRun run, string title, UIElement? content, double width, double height, IReadOnlyList<string>? args = null)
    {
        var harness = new RenderHarness(run, OffscreenWindow.Open(title, content, width, height), args ?? run.Args);
        DesignApplier.Apply(harness.Design);
        return harness;
    }

    /// <summary>
    /// [S-2500] The system and gallery services the cards read, as on the desktop: this PC's hub, or with
    /// <paramref name="sampleMachine"/> the sample machine ([S-1931]). Gone on dispose.
    /// </summary>
    public void UseServices(bool sampleMachine)
    {
        if (sampleMachine)
        {
            _owned.Add(RenderMachine.Install(Ticker, Run.Dispatcher));
        }
        else
        {
            AppServices.System = new Services.SystemData.SystemHub(Ticker, Run.Dispatcher);
        }

        AppServices.Gallery = new GalleryService(Ticker, Run.Dispatcher);
        _services = true;
    }

    /// <summary>A preview card host on the render's ticker and design; [S-9929] its kit cards replay <paramref name="fixtureState"/>.</summary>
    public NoticeRender.RenderCardHost CardHost(string? fixtureState = null) => new(Ticker) { Design = Design, FixtureState = fixtureState };

    /// <summary>A card the render built: detached on dispose.</summary>
    public WidgetCard Track(WidgetCard card)
    {
        _cards.Add(card);
        return card;
    }

    /// <summary>Something the render set up: disposed on dispose, last first.</summary>
    public T Own<T>(T disposable)
        where T : IDisposable
    {
        _owned.Add(disposable);
        return disposable;
    }

    /// <summary><paramref name="element"/> written to <paramref name="path"/> (its folder made), said on stdout as <c>&lt;say&gt;: wrote &lt;path&gt;</c>.</summary>
    public static async Task SaveAsync(UIElement element, string path, string say)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await RenderPng.SaveAsync(element, path).ConfigureAwait(true);
        Console.WriteLine($"{say}: wrote {path}");
    }

    public void Dispose()
    {
        foreach (var card in _cards)
        {
            card.Detach();
        }

        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            _owned[i].Dispose();
        }

        if (_services)
        {
            AppServices.System?.Dispose();
            AppServices.System = null;
            AppServices.Gallery?.Dispose();
            AppServices.Gallery = null;
        }

        Ticker.Dispose();
        Window.Dispose();
    }
}
#endif
