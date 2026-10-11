using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-880]..[S-883] the notice stack's rules, [S-890] the crash-loop guard, [S-893] the first-run layout.
/// </summary>
public sealed class NoticesBootTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-boot-" + Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Notices
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void S880_every_notice_has_its_own_id()
    {
        var a = Notice.Info("a");
        var b = Notice.Info("a");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(NoticeKind.Info, a.Kind);
        Assert.Equal(NoticeKind.Error, Notice.Error("x").Kind);
    }

    [Fact]
    public void S882_at_most_three_show_and_the_oldest_goes_first()
    {
        var stack = new NoticeStack();
        var first = Notice.Info("1");
        stack.Push(first);
        stack.Push(Notice.Info("2"));
        stack.Push(Notice.Info("3"));

        var removed = stack.Push(Notice.Info("4"));

        Assert.Equal([first], removed);
        Assert.Equal(["2", "3", "4"], stack.Visible.Select(n => n.Title));
    }

    [Fact]
    public void S882_a_keyed_notice_replaces_its_namesake_and_becomes_the_newest()
    {
        var stack = new NoticeStack();
        var first = new Notice(NoticeKind.Info, "Undid move") { Key = "undo" };
        stack.Push(first);
        stack.Push(Notice.Info("other"));

        var removed = stack.Push(new Notice(NoticeKind.Info, "Undid delete") { Key = "undo" });

        Assert.Equal([first], removed);
        Assert.Equal(["other", "Undid delete"], stack.Visible.Select(n => n.Title));
    }

    [Fact]
    public void S882_a_sticky_notice_is_pushed_out_last()
    {
        var stack = new NoticeStack();
        var sticky = new Notice(NoticeKind.Warning, "safe mode") { Sticky = true };
        stack.Push(sticky);
        stack.Push(Notice.Info("2"));
        stack.Push(Notice.Info("3"));

        var removed = stack.Push(Notice.Info("4"));

        Assert.Equal("2", Assert.Single(removed).Title);
        Assert.Contains(sticky, stack.Visible);
    }

    [Fact]
    public void S882_dismiss_takes_one_down()
    {
        var stack = new NoticeStack();
        var a = Notice.Info("a");
        stack.Push(a);

        Assert.True(stack.Dismiss(a.Id));
        Assert.False(stack.Dismiss(a.Id));
        Assert.Empty(stack.Visible);
    }

    [Fact]
    public void S883_lifetime_grows_with_weight_and_with_a_button()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), NoticeStack.Lifetime(Notice.Info("i")));
        Assert.Equal(TimeSpan.FromSeconds(6), NoticeStack.Lifetime(Notice.Warning("w")));
        Assert.Equal(TimeSpan.FromSeconds(8), NoticeStack.Lifetime(Notice.Error("e")));

        var withAction = new Notice(NoticeKind.Info, "Deleted") { Action = new NoticeAction("Undo", () => { }) };
        Assert.Equal(TimeSpan.FromSeconds(7), NoticeStack.Lifetime(withAction));

        Assert.Null(NoticeStack.Lifetime(new Notice(NoticeKind.Error, "safe") { Sticky = true }));
    }

    // ---------------------------------------------------------------------------------------
    // Boot guard
    // ---------------------------------------------------------------------------------------

    private BootGuard Guard() => new(Path.Combine(_dir, BootGuard.FileName));

    [Fact]
    public void S890_the_third_start_in_a_row_without_getting_stable_is_safe()
    {
        Assert.False(Guard().Begin());
        Assert.False(Guard().Begin());
        Assert.True(Guard().Begin());
        Assert.Equal(3, Guard().Read());
    }

    [Fact]
    public void S890_a_stable_start_clears_the_count()
    {
        Guard().Begin();
        Guard().Begin();
        Guard().MarkStable();

        Assert.Equal(0, Guard().Read());
        Assert.False(Guard().Begin());
    }

    [Fact]
    public void S890_a_corrupt_counter_starts_over_rather_than_trapping_the_app()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, BootGuard.FileName), "not a number");

        Assert.Equal(0, Guard().Read());
        Assert.False(Guard().Begin());
        Assert.Equal(1, Guard().Read());
    }

    [Fact]
    public void S890_marking_stable_with_no_counter_is_harmless()
    {
        Guard().MarkStable();
        Assert.Equal(0, Guard().Read());
    }

    [Fact]
    public void S892_a_reset_keeps_the_current_settings_under_their_own_name()
    {
        var path = SettingsTransfer.BackupPath(
            Path.Combine("C:", "data", "state.json"),
            new DateTime(2026, 10, 4, 9, 5, 7),
            SettingsTransfer.ResetOccasion);

        Assert.Equal(Path.Combine("C:", "data", "state.before-reset-20261004-090507.json"), path);
    }

    [Fact]
    public void S892_a_reset_is_the_first_run_desktop_and_keeps_start_with_windows()
    {
        var state = AppState.CreateDefault();
        state.Design.RunAtStartup = true;
        state.Design.WidgetGapPx = 20;
        state.Views.Add(ViewState.Create("Extra"));

        SettingsTransfer.Apply(state, AppState.CreateDefault());

        Assert.True(state.Design.RunAtStartup);
        Assert.Equal(new DesignState().WidgetGapPx, state.Design.WidgetGapPx);
        Assert.Equal(2, state.Views.Count);
        Assert.Equal(3, state.Views[0].Widgets.Count);
    }

    // ---------------------------------------------------------------------------------------
    // First run
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void S893_a_fresh_profile_gets_a_home_and_a_work_view()
    {
        var state = AppState.CreateDefault();

        Assert.Equal(2, state.Views.Count);
        Assert.Equal(Str.Shell_View_HomeName.Text(), state.Views[0].Name);
        Assert.Equal(Str.Shell_View_WorkName.Text(), state.Views[1].Name);
        Assert.Equal(state.Views[0].Id, state.ActiveViewId);
        Assert.NotEqual(state.Views[0].Id, state.Views[1].Id);

        // Two seeded views must still fit the free tier, with room left to add widgets.
        Assert.True(state.Views.Count <= Themia.Core.Licensing.LicenseRules.FreeViewLimit);
        Assert.True(state.Views.Sum(v => v.Widgets.Count) < Themia.Core.Licensing.LicenseRules.FreeWidgetLimit);
    }

    [Fact]
    public void S893_the_home_view_is_the_originals_first_run_desktop()
    {
        var widgets = AppState.CreateDefault().Views[0].Widgets;

        Assert.Equal(
            [WidgetKind.Clock, WidgetKind.Folder, WidgetKind.Shortcuts],
            widgets.Select(w => w.Kind));

        var places = Assert.IsType<ShortcutsConfig>(widgets[2].Config);
        Assert.Equal(AppState.FirstRunPlaces.Select(p => "shell:" + p), places.Items.Select(i => i.Target));
        Assert.All(places.Items, i => Assert.Equal(string.Empty, i.Label));
        AssertInsideAndApart(widgets);
    }

    [Fact]
    public void S893_the_work_view_is_calendar_tasks_notes_and_a_clock()
    {
        var widgets = AppState.CreateDefault().Views[1].Widgets;

        // The clock first, as on Home, so it is the one drawn on top.
        Assert.Equal(
            [WidgetKind.Clock, WidgetKind.MonthCalendar, WidgetKind.Tasks, WidgetKind.Notes],
            widgets.Select(w => w.Kind));
        Assert.DoesNotContain(widgets, w => w.Kind == WidgetKind.Folder);
        AssertInsideAndApart(widgets);
    }

    [Fact]
    public void S893_every_seeded_widget_has_its_own_id()
    {
        var ids = AppState.CreateDefault().Views.SelectMany(v => v.Widgets).Select(w => w.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void S893_load_validation_changes_nothing_in_the_first_run_desktop()
    {
        var state = AppState.CreateDefault();
        var before = state.Views.SelectMany(v => v.Widgets).Select(w => (w.Kind, w.Rect)).ToList();

        Assert.Empty(AppStateStore.Validate(state));
        Assert.Equal(before, state.Views.SelectMany(v => v.Widgets).Select(w => (w.Kind, w.Rect)));
    }

    /// <summary>Every widget inside the screen and none overlapping another.</summary>
    private static void AssertInsideAndApart(List<WidgetInstance> widgets)
    {
        foreach (var w in widgets)
        {
            Assert.InRange(w.X, 0, 1 - w.W);
            Assert.InRange(w.Y, 0, 1 - w.H);
        }

        for (var i = 0; i < widgets.Count; i++)
        {
            for (var j = i + 1; j < widgets.Count; j++)
            {
                var a = widgets[i].Rect;
                var b = widgets[j].Rect;
                var overlap = a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;
                Assert.False(overlap, $"{widgets[i].Kind} overlaps {widgets[j].Kind}");
            }
        }
    }

    [Fact]
    public async Task S893_the_first_run_desktop_survives_a_save_and_load()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "state.json");
        var store = new AppStateStore(path);
        var state = AppState.CreateDefault();

        store.SaveSoon(state);
        await store.FlushAsync();

        var loaded = new AppStateStore(path).Load();
        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        Assert.Equal(2, loaded.State.Views.Count);
        Assert.Equal(3, loaded.State.Views[0].Widgets.Count);
        Assert.Equal(
            [WidgetKind.Clock, WidgetKind.MonthCalendar, WidgetKind.Tasks, WidgetKind.Notes],
            loaded.State.Views[1].Widgets.Select(w => w.Kind));
        Assert.Equal(6, Assert.IsType<ShortcutsConfig>(loaded.State.Views[0].Widgets[2].Config).Items.Count);
    }
}
