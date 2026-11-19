using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13022] [S-13023] Fences that show items by a rule: collecting the desktop's, or files a search finds.</summary>
public sealed class FenceSourceTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    private static WidgetInstance Icon(string name) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = Desk + name }, Rect = new FracRect(0, 0, 0.05, 0.05) };

    private static (WidgetInstance Widget, FenceFake Fence) Fence(string id, FenceSource source, params FenceFileType[] types)
    {
        var fence = new FenceFake { Source = source, Rules = new FenceRuleSet { Types = [.. types] } };
        return (new WidgetInstance { Id = id, Config = fence, Rect = new FracRect(0.5, 0.1, 0.3, 0.3) }, fence);
    }

    private static IEnumerable<string> Names(IEnumerable<string> keys) => keys.Select(k => k.Replace(Desk, string.Empty, StringComparison.Ordinal));

    // ---- collecting the desktop's items ([S-13022]) ----------------------------------------------------------------

    [Fact]
    public void S13022_a_collecting_fence_takes_every_listed_item_its_rules_match_and_lets_go_of_what_no_longer_does()
    {
        var view = ViewState.Create("Home");
        var state = new AppState { Views = [view], ActiveViewId = view.Id };
        view.Widgets.AddRange([Icon("a.png"), Icon("b.txt"), Icon("c.jpg")]);
        var (widget, fence) = Fence("pics", FenceSource.Desktop, FenceFileType.Images);
        view.Widgets.Add(widget);
        IReadOnlyList<DesktopItem> listing = [File("a.png"), File("b.txt"), File("c.jpg")];

        Assert.Empty(DesktopFences.Collect(state, listing));
        Assert.Equal(["a.png", "c.jpg"], Names(fence.Items));
        Assert.Equal(["b.txt"], Names(DesktopViews.Free(view)));

        // Steady: a second pass changes nothing.
        Assert.Empty(DesktopFences.Collect(state, listing));
        Assert.Equal(["a.png", "c.jpg"], Names(fence.Items));

        // The rules now take documents: the pictures are let go for the free desktop, the text file comes in.
        fence.Rules = new FenceRuleSet { Types = [FenceFileType.Documents] };
        var released = DesktopFences.Collect(state, listing);
        Assert.Equal(["b.txt"], Names(fence.Items));
        var (releasedView, keys) = Assert.Single(released);
        Assert.Same(view, releasedView);
        Assert.Equal(["a.png", "c.jpg"], Names(keys).Order());
    }

    [Fact]
    public void S13022_the_first_fence_of_the_view_keeps_an_item_and_an_unlisted_one_is_left_for_the_reconcile()
    {
        var view = ViewState.Create("Home");
        var state = new AppState { Views = [view], ActiveViewId = view.Id };
        var (heldWidget, held) = Fence("held", FenceSource.Held);
        held.Items.Add(Desk + "a.png");
        held.Items.Add(Desk + "gone.png");
        var (collectWidget, collect) = Fence("collect", FenceSource.Desktop, FenceFileType.Images);
        view.Widgets.AddRange([heldWidget, collectWidget]);

        DesktopFences.Collect(state, [File("a.png"), File("d.png")], key => new WidgetInstance { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = key }, Rect = new FracRect(0, 0, 0, 0) });

        Assert.Equal(["a.png", "gone.png"], Names(held.Items));
        Assert.Equal(["d.png"], Names(collect.Items));
    }

    [Fact]
    public void S13023_a_search_fence_holds_no_desktop_item_and_new_items_never_route_to_it()
    {
        var view = ViewState.Create("Home");
        var state = new AppState { Views = [view], ActiveViewId = view.Id };
        var (widget, fence) = Fence("search", FenceSource.Search, FenceFileType.Images);
        fence.Items.Add(Desk + "a.png");
        view.Widgets.Add(widget);

        var released = DesktopFences.Collect(state, [File("a.png")]);

        Assert.Empty(fence.Items);
        Assert.Equal(["a.png"], Names(Assert.Single(released).Keys));
        Assert.Null(DesktopFences.Route(view, File("b.png")));
        Assert.Equal(0, DesktopFences.MoveIn(view, "search", [Desk + "a.png"], 0));
    }

    // ---- the search's query ([S-13023]) ----------------------------------------------------------------------------

    [Fact]
    public void S13023_the_query_is_any_kind_or_the_name_then_the_recency()
    {
        var rules = new FenceRuleSet { Types = [FenceFileType.Shortcuts, FenceFileType.Folders], Extensions = [".dwg"], NameContains = "ra\"te" };
        var query = new FenceQuery { Folder = @"D:\Projects", Modified = FenceRecency.Week };

        var aqs = FenceSearch.Aqs(rules, query, Now);
        Assert.Equal("(ext:.lnk OR ext:.url OR kind:folder OR ext:.dwg OR System.FileName:~~\"rate\") System.DateModified:>=2026-11-12", aqs);

        // One kind alone needs no brackets; no rule searches nothing.
        Assert.Equal("ext:.dwg", FenceSearch.Aqs(new FenceRuleSet { Extensions = [".dwg"] }, new FenceQuery(), Now));
        Assert.Equal(string.Empty, FenceSearch.Aqs(new FenceRuleSet(), query, Now));
    }

    [Fact]
    public void S13023_what_the_index_answers_is_checked_newest_first_once_each_and_cut_at_the_count()
    {
        var rules = new FenceRuleSet { Types = [FenceFileType.Images] };
        var query = new FenceQuery { Modified = FenceRecency.Month, Max = 2 };
        FoundFile[] found =
        [
            new(@"C:\p\old.png", "old.png", false, Now.AddMonths(-2)),
            new(@"C:\p\a.png", "a.png", false, Now.AddDays(-3)),
            new(@"C:\p\A.PNG", "A.PNG", false, Now.AddDays(-3)),
            new(@"C:\p\b.jpg", "b.jpg", false, Now.AddDays(-1)),
            new(@"C:\p\c.webp", "c.webp", false, Now.AddDays(-9)),
            new(@"C:\p\notes.txt", "notes.txt", false, Now),
            new(@"C:\p\pics.png", "pics.png", true, Now),
            new(@"C:\p\undated.png", "undated.png", false, null),
        ];

        var shown = FenceSearch.Shown(rules, query, found, Now);

        Assert.Equal(["b.jpg", "a.png"], shown.Select(f => f.Name));
        Assert.True(FenceSearch.Matches(rules, new FenceQuery(), found[^1], Now));
    }

    [Fact]
    public void S13023_a_stored_query_is_made_whole()
    {
        var query = new FenceQuery { Folder = "relative\\dir", Modified = (FenceRecency)42, Max = 9000 };

        Assert.Equal(3, FenceSearch.Repair(query).Count);
        Assert.Equal(string.Empty, query.Folder);
        Assert.Equal(FenceRecency.Any, query.Modified);
        Assert.Equal(FenceSearch.MostResults, query.Max);

        var good = new FenceQuery { Folder = @"C:\Users\a\Pictures", Max = 10 };
        Assert.Empty(FenceSearch.Repair(good));
        Assert.Empty(FenceSearch.Repair(new FenceQuery { Folder = @"D:\" }));
    }
}
