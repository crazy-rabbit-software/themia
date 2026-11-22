#if DEBUG
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-69] Debug only: what every render shares, owned once. A render opens it on the UI thread
/// (<see cref="RenderCommands"/> made the hop) and gets the design its <c>--theme</c> and <c>--design</c> ask for (put in
/// force by <see cref="PutDesignInForce"/> where the render draws with it, before it builds what reads its brushes), its
/// off-screen window (<see cref="MakeWindow"/>, holding the content, shown by <see cref="OffscreenWindow.Show(double, double, double?)"/>
/// once the content is in place), a ticker, preview card hosts, the system and gallery services its cards read, and the PNG
/// writer, and the one wait (<see cref="SettleAsync"/>, [S-15900]); and disposes it: the cards detach, the services and the ticker go and the window closes, whatever threw. Each
/// render keeps the order its steps always had, so its pictures are the same.
/// </summary>
internal sealed class RenderHarness : IDisposable
{
    private readonly List<WidgetCard> _cards = [];
    private readonly List<IDisposable> _owned = [];
    private OffscreenWindow? _window;
    private bool _services;

    private RenderHarness(RenderRun run, IReadOnlyList<string> args)
    {
        Run = run;
        Ticker = new Ticker(run.Dispatcher);
        Design = DesignArgs.From([.. args], RenderCommands.ThemeOf(Environment.GetCommandLineArgs()) ?? AppTheme.System);
    }

    public RenderRun Run { get; }

    /// <summary>The render's window, once made (<see cref="MakeWindow"/>).</summary>
    public OffscreenWindow Window => _window ?? throw new InvalidOperationException("the render has no window yet");

    public Ticker Ticker { get; }

    /// <summary>The design the render draws with: its <c>--theme</c> (the application's) and its <c>--design</c> values.</summary>
    public DesignState Design { get; }

    /// <summary>
    /// The harness of a render: its design read from <paramref name="args"/> (a batch job's), else the run's, not yet in
    /// force; its ticker. No window yet.
    /// </summary>
    public static RenderHarness Open(RenderRun run, IReadOnlyList<string>? args = null) => new(run, args ?? run.Args);

    /// <summary>
    /// The render's design in force: the themed brushes follow the design's theme, not the application's (the app's is
    /// chosen before the resources load): both are set.
    /// </summary>
    public void PutDesignInForce() => DesignApplier.Apply(Design);

    /// <summary>
    /// The application's theme alone in force, for the renders of the shell's pages (the edit bar, its flyouts, the menus):
    /// they draw Windows' look, never a design's.
    /// </summary>
    public void PutAppThemeInForce() => DesignApplier.Apply(new DesignState
    {
        Theme = Run.App.RequestedTheme == ApplicationTheme.Light ? AppTheme.Light : AppTheme.Dark,
    });

    /// <summary>The render's window, titled <paramref name="title"/> and holding <paramref name="content"/>, not shown yet.</summary>
    public OffscreenWindow MakeWindow(string title, UIElement? content)
    {
        _window = OffscreenWindow.Create(title, content);
        return _window;
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

    /// <summary>
    /// [S-15900] The one wait of every render: <paramref name="root"/> settled (laid out, nothing under it loading, nothing
    /// <paramref name="busy"/> names, its picture the same frame after frame) or the ceiling passed, said loudly as
    /// <paramref name="what"/>'s (<see cref="RenderSettle"/>). Never a fixed sleep. Returns whether it settled.
    /// </summary>
    public static Task<bool> SettleAsync(UIElement root, string what, TimeSpan? ceiling = null, Func<string?>? busy = null)
        => RenderSettle.SettleAsync(root, what, ceiling, busy);

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
        _window?.Dispose();
    }
}
#endif
