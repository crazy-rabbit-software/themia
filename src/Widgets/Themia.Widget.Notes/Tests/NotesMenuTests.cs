using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Notes;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-1349] The Notes widget's options on its properties menu: the note shown and the pool, the text size, Clear note.</summary>
public sealed class NotesMenuTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private int _saves;
    private WidgetTextAsk? _asked;
    private WidgetConfirm? _confirm;

    private WidgetMenuLines Build(NotesConfig config, INotes? pool)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask, confirm => _confirm = confirm);
        NotesMenu.Lines(lines, config, pool, Now);
        return lines;
    }

    private static Pool Two() => new() { Bodies = { ["a"] = "Milk, eggs", ["b"] = string.Empty }, Titles = { ["a"] = "Shopping", ["b"] = "Ideas" } };

    private static string NoteTitle => NotesStr.WidgetsC_Notes_Settings_Note.Text();

    private static string Clear => NotesStr.WidgetsC_Notes_Settings_Clear_Label.Text();

    private static ItemMenuEntry Line(WidgetMenuLines lines, string text) => lines.Lines.Single(l => l.Text == text);

    [Fact]
    public void S1349_appearance_then_note_font_size_and_clear_last()
    {
        var lines = Build(new NotesConfig { NoteId = "a" }, Two());

        Assert.Equal([NoteTitle, "|", NotesStr.Notes_FontSizeLabel.Text(), "|", Clear], lines.Lines.Select(l => l.IsSeparator ? "|" : l.Text));
        Assert.True(Line(lines, Clear).Danger);
        Assert.Equal("13 px", Line(lines, NotesStr.Notes_FontSizeLabel.Text()).Value);
    }

    [Fact]
    public void S1349_the_note_shown_has_the_dot_and_picking_another_saves_once()
    {
        var config = new NotesConfig { NoteId = "a" };
        var notes = Line(Build(config, Two()), NoteTitle);
        Assert.Equal("Shopping", notes.Value);

        notes.Children!.Single(l => l.Text == "Shopping").Action!();
        Assert.Equal(0, _saves);

        notes.Children!.Single(l => l.Text == "Ideas").Action!();
        Assert.Equal("b", config.NoteId);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1349_new_note_makes_one_and_shows_it()
    {
        var pool = Two();
        var config = new NotesConfig { NoteId = "a" };
        Line(Build(config, pool), NoteTitle).Children!.Single(l => l.Text == NotesStr.Notes_NewNote.Text()).Action!();

        Assert.Equal(3, pool.Notes.Count);
        Assert.Equal(pool.Notes[^1].Id, config.NoteId);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1349_rename_retitles_the_note_shown_in_the_pool()
    {
        var pool = Two();
        Line(Build(new NotesConfig { NoteId = "b" }, pool), NoteTitle).Children!.Single(l => l.Text == Str.Menu_Rename.Text()).Action!();
        Assert.Equal("Ideas", _asked!.Initial);

        _asked.Done("Later");
        Assert.Equal("Later", pool.Titles["b"]);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1349_delete_is_always_asked_and_names_the_note()
    {
        var pool = Two();
        Line(Build(new NotesConfig { NoteId = "a" }, pool), NoteTitle).Children!.Single(l => l.Text == NotesStr.Notes_DeleteTitle.Text()).Action!();
        Assert.Equal(2, pool.Notes.Count);

        var confirm = Assert.IsType<WidgetConfirm>(_confirm);
        Assert.Equal(NotesStr.WidgetsC_Notes_DeleteConfirmTitle.Text(), confirm.Question);
        Assert.Equal(NotesStr.WidgetsC_Notes_DeleteConfirmBody.Format(("name", "Shopping")), confirm.Detail);
        confirm.Done();
        Assert.Null(pool.Find("a"));
    }

    [Fact]
    public void S1349_a_deleted_note_is_said_and_offers_no_rename_or_delete()
    {
        var notes = Line(Build(new NotesConfig { NoteId = "gone" }, Two()), NoteTitle).Children!;

        Assert.Contains(notes, l => l.Note && l.Text == NotesStr.WidgetsC_Notes_Settings_ShownDeleted.Text());
        Assert.DoesNotContain(notes, l => l.Text == Str.Menu_Rename.Text() || l.Text == NotesStr.Notes_DeleteTitle.Text());
    }

    [Fact]
    public void S130_clear_note_asks_first_empties_the_body_and_is_greyed_with_nothing_to_clear()
    {
        var pool = Two();
        Assert.True(Line(Build(new NotesConfig { NoteId = "b" }, pool), Clear).Disabled);

        Line(Build(new NotesConfig { NoteId = "a" }, pool), Clear).Action!();
        Assert.Equal("Milk, eggs", pool.Bodies["a"]);

        _confirm!.Done();
        Assert.Equal(string.Empty, pool.Bodies["a"]);
        Assert.Equal("Shopping", pool.Titles["a"]);
    }

    [Fact]
    public void S130_no_clear_line_while_no_note_is_shown()
    {
        Assert.DoesNotContain(Build(new NotesConfig { NoteId = "gone" }, Two()).Lines, l => l.Text == Clear);
        Assert.DoesNotContain(Build(new NotesConfig { NoteId = "a" }, null).Lines, l => l.Text == Clear);
        Assert.Contains(Build(new NotesConfig { NoteId = "b" }, Two()).Lines, l => l.Text == Clear);
    }

    [Fact]
    public void S1349_without_notes_the_menu_says_they_are_not_available()
        => Assert.True(Assert.Single(Line(Build(new NotesConfig(), null), NoteTitle).Children!).Note);

    /// <summary>Notes in memory: titles and bodies by id, in their order.</summary>
    private sealed class Pool : INotes
    {
        public Dictionary<string, string> Titles { get; } = [];

        public Dictionary<string, string> Bodies { get; } = [];

        public event Action<string?>? Changed;

        public event Action? SaveStateChanged;

        public IReadOnlyList<NoteMeta> Notes => [.. Titles.Select(t => new NoteMeta(t.Key, t.Value, 1_799_000_000))];

        public bool WriteFailed => false;

        public bool Saving => false;

        public NoteMeta? Find(string? id) => Notes.FirstOrDefault(n => n.Id == id);

        public NoteRead Read(string? id) => new(NoteReadStatus.Found, id is not null && Bodies.TryGetValue(id, out var body) ? body : string.Empty);

        public string Create(string baseName, string content = "")
        {
            var id = $"n{Titles.Count + 1}";
            Titles[id] = baseName;
            Bodies[id] = content;
            Changed?.Invoke(null);
            return id;
        }

        public void Write(string? id, string content)
        {
            if (id is not null)
            {
                Bodies[id] = content;
                Changed?.Invoke(id);
            }
        }

        public void Rename(string id, string title) => Titles[id] = title;

        public void Delete(string id)
        {
            Titles.Remove(id);
            Bodies.Remove(id);
            SaveStateChanged?.Invoke();
        }
    }
}
