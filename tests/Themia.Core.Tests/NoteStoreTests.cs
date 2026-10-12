using Themia.Core.Notes;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The note store, ported from the original <c>src/lib.rs</c> tests: id validation, title cleaning,
/// index recovery and atomicity. Each test gets its own folder under the temp directory.
/// </summary>
public sealed class NoteStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-tests", "notes-" + Guid.NewGuid().ToString("n"));

    public NoteStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: temp cleanup is best effort and must not fail a test run
        }
    }

    private NoteStore Store() => new(_dir);

    private string BodyPath(string id) => Path.Combine(_dir, id + ".md");

    private string IndexPath => Path.Combine(_dir, NoteStore.IndexFileName);

    // -- ids and titles -----------------------------------------------------------------------

    [Theory]
    [InlineData("../../secrets")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("0123456789ABCDEF0123456789abcdef")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData(null)]
    public void S1342_ids_that_could_leave_the_folder_are_refused(string? id)
        => Assert.False(NoteStore.IsValidId(id));

    [Fact]
    public void S1342_a_minted_id_is_valid()
        => Assert.True(NoteStore.IsValidId(NoteStore.NewId()));

    [Fact]
    public void S1344_titles_are_flattened_trimmed_and_capped()
    {
        Assert.Equal("hello", NoteStore.CleanTitle("  hello  "));
        Assert.Equal(NoteStore.Untitled, NoteStore.CleanTitle(string.Empty));
        Assert.Equal(NoteStore.Untitled, NoteStore.CleanTitle("   "));
        Assert.Equal(NoteStore.Untitled, NoteStore.CleanTitle(null));
        Assert.Equal("one two", NoteStore.CleanTitle("one\ntwo"));
        Assert.Equal(NoteStore.MaxTitle, NoteStore.CleanTitle(new string('x', 500)).Length);
    }

    [Fact]
    public void S1344_sort_is_by_title_ignoring_case_then_id()
    {
        var sorted = NoteStore.Sorted(
        [
            new NoteMeta("b", "beta", 0),
            new NoteMeta("c", "Alpha", 0),
            new NoteMeta("a", "Alpha", 0),
        ]);

        Assert.Equal(["a", "c", "b"], sorted.Select(n => n.Id));
    }

    [Fact]
    public void S1344_unique_titles_number_on()
    {
        NoteMeta[] notes = [new("a", "Note", 0), new("b", "note 2", 0)];
        Assert.Equal("Note 3", NoteStore.UniqueTitle(notes, "Note"));
        Assert.Equal("Shopping", NoteStore.UniqueTitle(notes, "Shopping"));
        Assert.Equal(NoteStore.Untitled, NoteStore.UniqueTitle([], "  "));
    }

    // -- index --------------------------------------------------------------------------------

    [Fact]
    public void S1343_a_missing_index_is_a_quiet_empty_list()
        => Assert.Empty(Store().List());

    [Fact]
    public void S1343_a_corrupt_index_recovers_every_body_and_keeps_ids()
    {
        var id = NoteStore.NewId();
        File.WriteAllText(BodyPath(id), "# kept");
        File.WriteAllText(IndexPath, "{ not json at all");

        var notes = Store().List();

        var note = Assert.Single(notes);
        Assert.Equal(id, note.Id);
        Assert.Equal(NoteStore.Untitled, note.Title);
        Assert.Equal("# kept", File.ReadAllText(BodyPath(id)));

        // The unusable index is kept for its titles, never overwritten.
        Assert.Single(Directory.GetFiles(_dir, NoteStore.IndexFileName + ".corrupt-*"));
    }

    [Fact]
    public void S1343_an_unknown_schema_is_rebuilt_rather_than_half_read()
    {
        var id = NoteStore.NewId();
        File.WriteAllText(BodyPath(id), "body");
        File.WriteAllText(IndexPath, $$"""{"schemaVersion":99,"notes":[{"id":"{{id}}","title":"Real","updatedAt":5}]}""");

        var note = Assert.Single(Store().List());
        Assert.Equal(NoteStore.Untitled, note.Title);
    }

    [Fact]
    public void S1343_rows_without_a_body_are_pruned()
    {
        var alive = NoteStore.NewId();
        var dead = NoteStore.NewId();
        File.WriteAllText(BodyPath(alive), "here");
        File.WriteAllText(IndexPath, NoteStore.SerializeIndex([new NoteMeta(alive, "Alive", 1), new NoteMeta(dead, "Dead", 2)]));

        var note = Assert.Single(Store().List());
        Assert.Equal(alive, note.Id);
    }

    [Fact]
    public void S1343_a_valid_index_round_trips()
    {
        var id = NoteStore.NewId();
        File.WriteAllText(BodyPath(id), "body");
        File.WriteAllText(IndexPath, NoteStore.SerializeIndex([new NoteMeta(id, "Shopping", 42)]));

        Assert.Equal([new NoteMeta(id, "Shopping", 42)], Store().List());
    }

    [Fact]
    public void S1343_malformed_rows_are_dropped_and_counted()
    {
        var good = NoteStore.NewId();
        var json = $$"""
            {"schemaVersion":1,"notes":[
              {"id":"{{good}}","title":"Good","updatedAt":3},
              {"id":"{{good}}","title":"Duplicate","updatedAt":4},
              {"id":"../x","title":"Escape"},
              {"title":"No id"},
              {"id":"{{NoteStore.NewId()}}"},
              42
            ]}
            """;

        var (notes, dropped, refusal) = NoteStore.ParseIndex(json);

        Assert.Null(refusal);
        Assert.Equal(5, dropped);
        Assert.Equal([new NoteMeta(good, "Good", 3)], notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"notes\":[]}")]
    [InlineData("{\"schemaVersion\":\"1\"}")]
    public void S1343_documents_that_cannot_be_used_are_refused(string json)
        => Assert.NotNull(NoteStore.ParseIndex(json).Refusal);

    // -- operations ---------------------------------------------------------------------------

    [Fact]
    public void S1340_create_write_rename_read_round_trip_on_disk()
    {
        var store = Store();
        var id = NoteStore.NewId();

        Assert.NotNull(store.Create(id, "  Shopping\n", "milk"));
        Assert.Equal(NoteOutcome.Done, store.Write(id, "milk\nbread"));
        Assert.Equal(NoteOutcome.Done, store.Rename(id, "Groceries"));

        // A fresh store reads what the first one wrote: the files are the truth.
        var reopened = Store();
        Assert.Equal(new NoteRead(NoteReadStatus.Found, "milk\nbread"), reopened.Read(id));
        Assert.Equal("Groceries", Assert.Single(reopened.List()).Title);
        Assert.Equal("milk\nbread", File.ReadAllText(BodyPath(id)));
    }

    [Fact]
    public void S1345_missing_is_not_empty()
    {
        var store = Store();
        var empty = NoteStore.NewId();
        store.Create(empty, "Empty", string.Empty);

        Assert.Equal(new NoteRead(NoteReadStatus.Found, string.Empty), store.Read(empty));
        Assert.Equal(NoteReadStatus.Missing, store.Read(NoteStore.NewId()).Status);
        Assert.Equal(NoteReadStatus.Missing, store.Read("../../secrets").Status);
    }

    [Fact]
    public void S1342_a_write_to_a_deleted_note_does_not_resurrect_it()
    {
        var store = Store();
        var id = NoteStore.NewId();
        store.Create(id, "Gone soon", "text");

        Assert.Equal(NoteOutcome.Done, store.Delete(id));
        Assert.Equal(NoteOutcome.NotFound, store.Write(id, "typed after the delete"));
        Assert.False(File.Exists(BodyPath(id)));
        Assert.Empty(store.List());
        Assert.Equal(NoteOutcome.NotFound, store.Delete(id));
    }

    [Fact]
    public void S1342_bad_ids_never_reach_a_path()
    {
        var store = Store();
        Assert.Equal(NoteOutcome.InvalidId, store.Write("../escape", "x"));
        Assert.Equal(NoteOutcome.InvalidId, store.Rename("..", "x"));
        Assert.Equal(NoteOutcome.InvalidId, store.Delete("a/b"));
        Assert.Null(store.Create("..", "x", "x"));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public void S1342_create_refuses_a_taken_id()
    {
        var store = Store();
        var id = NoteStore.NewId();
        store.Create(id, "One", "first");
        Assert.Null(store.Create(id, "Two", "second"));
        Assert.Equal("first", store.Read(id).Text);
    }

    [Fact]
    public void S1340_a_write_stamps_the_note()
    {
        var clock = new StepClock(DateTimeOffset.FromUnixTimeSeconds(1_000));
        var store = new NoteStore(_dir, clock);
        var id = NoteStore.NewId();
        store.Create(id, "Note", string.Empty);

        clock.Now = DateTimeOffset.FromUnixTimeSeconds(2_000);
        store.Write(id, "later");

        Assert.Equal(2_000, Assert.Single(store.List()).UpdatedAt);
    }

    [Fact]
    public void S1341_atomic_writes_leave_no_temp_files()
    {
        var store = Store();
        var id = NoteStore.NewId();
        store.Create(id, "Note", "one");
        store.Write(id, "two");
        store.Rename(id, "Renamed");

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        Assert.Equal("two", File.ReadAllText(BodyPath(id)));
    }

    [Fact]
    public void S1341_concurrent_writers_lose_nothing()
    {
        var store = Store();
        var ids = Enumerable.Range(0, 8).Select(_ => NoteStore.NewId()).ToArray();
        foreach (var id in ids)
        {
            store.Create(id, "Note", string.Empty);
        }

        Parallel.ForEach(ids, id =>
        {
            for (var i = 0; i < 10; i++)
            {
                store.Write(id, $"{id}:{i}");
                store.Rename(id, $"Title {i}");
            }
        });

        var reopened = Store();
        Assert.Equal(ids.Length, reopened.List().Count);
        Assert.All(ids, id => Assert.Equal($"{id}:9", reopened.Read(id).Text));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void S1340_notes_live_beside_the_state_file()
        => Assert.Equal(Path.Combine(@"C:\profile", "notes"), NoteStore.DirectoryBeside(@"C:\profile\state.json"));

    // -- layout -------------------------------------------------------------------------------

    private static NotesLayoutPlan Plan(double width, double height, bool notSaved = false)
        => NotesLayout.Plan(new WidgetArea(width, height - 44), inset: 16, gap: 8, lineHeight: 18, statusHeight: 16, notSaved);

    [Fact]
    public void S1363_xs_and_s_show_text_alone()
    {
        foreach (var plan in new[] { Plan(120, 120), Plan(200, 150) })
        {
            Assert.False(plan.Status);
            Assert.True(plan.TextLines >= NotesLayout.MinTextLines);
        }
    }

    [Fact]
    public void S3963_the_body_gives_no_row_to_a_formatting_bar()
    {
        // [S-3963] The bar lives in the header: at M and on a wide strip the body is the text alone,
        // as many lines as the box holds.
        var m = Plan(320, 240);
        var wide = Plan(480, 160);

        Assert.False(m.Status);
        Assert.False(wide.Status);
        Assert.Equal(NotesLayout.MinTextLines + Disclosure.WholeRows((240 - 44 - 32) - (18 * NotesLayout.MinTextLines), 18), m.TextLines);
    }

    /// <summary>[S-1362] No word or character count and no "Saved" (user requirement): a saved note is its text alone at every size.</summary>
    [Theory]
    [InlineData(120, 120)]
    [InlineData(200, 150)]
    [InlineData(320, 240)]
    [InlineData(480, 160)]
    [InlineData(200, 400)]
    [InlineData(560, 420)]
    [InlineData(800, 560)]
    public void S1362_a_saved_note_has_no_status_line_at_any_size(double width, double height)
        => Assert.False(Plan(width, height).Status);

    [Fact]
    public void S1362_a_failed_save_is_said_under_the_text_where_a_line_is_left()
    {
        var saved = Plan(320, 240);
        var failed = Plan(320, 240, notSaved: true);

        Assert.True(failed.Status);
        Assert.True(failed.TextLines < saved.TextLines);

        // The text keeps its two lines first.
        Assert.False(Plan(120, 120, notSaved: true).Status);
    }

    [Fact]
    public void S1363_xl_gives_the_rest_to_the_text()
        => Assert.True(Plan(800, 560).TextLines > Plan(560, 420).TextLines);

    /// <summary>
    /// [S-1363] The widget is the one note at every size: the text takes the whole width even at
    /// XL, where a note list used to sit beside it (user decision: never a list on the left).
    /// </summary>
    [Fact]
    public void S1363_xl_never_gives_width_to_a_note_list()
    {
        var xl = NotesLayout.Plan(new WidgetArea(800, 516), inset: 16, gap: 8, lineHeight: 18, statusHeight: 16);
        Assert.Equal(800 - 32, xl.TextWidth);
    }

    [Fact]
    public void S1363_unmeasured_shows_the_text_only()
        => Assert.False(NotesLayout.Plan(default, 16, 8, 18, 16).Status);

    [Theory]
    [InlineData(400, true, 9, false)]
    [InlineData(800, true, 9, false)]
    [InlineData(320, true, 5, true)]
    [InlineData(240, true, 2, true)]
    [InlineData(180, true, 0, true)]
    [InlineData(170, false, 0, true)]
    [InlineData(120, false, 0, false)]
    public void S3963_the_header_bar_keeps_what_fits_and_folds_the_rest_into_more(double width, bool picker, int inline, bool overflow)
    {
        var bar = NotesLayout.HeaderBar(width, picker);

        Assert.Equal(inline, bar.Inline);
        Assert.Equal(overflow, bar.Overflow);
    }

    [Fact]
    public void S3963_the_header_bar_never_takes_the_titles_minimum()
    {
        for (var width = 100.0; width <= 900; width += 7)
        {
            var bar = NotesLayout.HeaderBar(width, picker: true);
            var used = bar.Width;

            Assert.True(used <= Math.Max(0, width - NotesLayout.HeaderReserve(picker: true)), $"{width}: {used}");
            Assert.InRange(bar.Inline, 0, NotesLayout.AllButtons);
            Assert.True(bar.Overflow == (bar.Inline < NotesLayout.AllButtons && bar.Shown), $"{width}");
        }

        Assert.False(NotesLayout.HeaderBar(double.NaN, picker: true).Shown);
        Assert.False(NotesLayout.HeaderBar(0, picker: true).Shown);
    }

    private sealed class StepClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
