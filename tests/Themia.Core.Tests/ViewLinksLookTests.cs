using Themia.Core.Links;
using Themia.Core.Scenes;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11900]..[S-11909] The links' one look, the keys every kind uses, edit mode's quiet, a scene's links.</summary>
public sealed class ViewLinksLookTests
{
    [Fact]
    public void S11901_a_row_lights_as_pointed_or_picked_and_picked_wins()
    {
        Assert.Equal(LinkLight.None, LinkEmphasis.Light("a", null, null));
        Assert.Equal(LinkLight.Pointed, LinkEmphasis.Light("a", "a", null));
        Assert.Equal(LinkLight.Picked, LinkEmphasis.Light("a", null, "a"));
        Assert.Equal(LinkLight.Picked, LinkEmphasis.Light("a", "a", "a"));
        Assert.Equal(LinkLight.None, LinkEmphasis.Light("a", "b", "c"));
        Assert.Equal(LinkLight.None, LinkEmphasis.Light<string>(null, "a", "a"));
    }

    [Fact]
    public void S11902_a_marks_others_go_to_six_tenths_for_a_hover_and_three_for_a_pick()
    {
        Assert.Equal(0.6, FamilyShape.LinkPointed);
        Assert.Equal(0.3, FamilyShape.LinkPicked);
        Assert.Equal(1, LinkEmphasis.Strength("a", null, null));
        Assert.Equal(1, LinkEmphasis.Strength("a", "a", null));
        Assert.Equal(0.6, LinkEmphasis.Strength("b", "a", null));
        Assert.Equal(0.3, LinkEmphasis.Strength("b", null, "a"));

        // Pointed and picked together: the picked look, the pointed part whole too.
        Assert.Equal(1, LinkEmphasis.Strength("p", "p", "s"));
        Assert.Equal(1, LinkEmphasis.Strength("s", "p", "s"));
        Assert.Equal(0.3, LinkEmphasis.Strength("x", "p", "s"));
    }

    [Fact]
    public void S11900_a_value_the_widget_does_not_show_dims_nothing()
    {
        Assert.Null(LinkEmphasis.Shown("z", ["a", "b"]));
        Assert.Equal("a", LinkEmphasis.Shown("a", ["a", "b"]));
        Assert.Equal(1, LinkEmphasis.Strength("b", LinkEmphasis.Shown("z", ["a", "b"]), null));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Microsoft VS Code\Code.exe", "code")]
    [InlineData("msedge.exe", "msedge")]
    [InlineData("Spotify", "spotify")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void S11931_an_app_is_its_lower_case_executable_stem(string? executable, string? key)
        => Assert.Equal(key, LinkKeys.App(executable));

    [Fact]
    public void S11931_a_process_name_is_the_same_key()
    {
        Assert.Equal("code", LinkKeys.AppOfProcess("Code"));
        Assert.Equal(LinkKeys.App(@"C:\x\Code.exe"), LinkKeys.AppOfProcess("Code"));
        Assert.Null(LinkKeys.AppOfProcess(""));
    }

    [Theory]
    [InlineData("Ada.Lovelace@Example.com", "Ada Lovelace", "ada.lovelace@example.com")]
    [InlineData("Ada Lovelace <ada@example.com>", null, "ada@example.com")]
    [InlineData("mailto:ADA@example.com", null, "ada@example.com")]
    [InlineData(null, "  Ada   LOVELACE ", "ada lovelace")]
    [InlineData("not an address", "Ada", "ada")]
    [InlineData(null, null, null)]
    public void S11941_a_person_is_the_address_lower_cased_else_the_name_folded(string? address, string? name, string? key)
        => Assert.Equal(key, LinkKeys.Person(address, name));

    [Theory]
    [InlineData(@"C:\Users\Ada\Documents\", @"C:\USERS\ADA\DOCUMENTS")]
    [InlineData("c:/users/ada/report.docx", @"C:\USERS\ADA\REPORT.DOCX")]
    [InlineData(@"D:\", @"D:\")]
    [InlineData("", null)]
    public void S11946_a_file_is_its_full_path_case_folded(string path, string? key)
        => Assert.Equal(key, LinkKeys.File(path));

    [Theory]
    [InlineData(@"c:\Users", "C:")]
    [InlineData("D:", "D:")]
    [InlineData(@"\\server\share", null)]
    [InlineData("", null)]
    public void S11951_a_drive_is_its_letter_and_colon(string path, string? key)
        => Assert.Equal(key, LinkKeys.Drive(path));

    [Theory]
    [InlineData("a4:c1:38:0b:22:9f", "A4C1380B229F")]
    [InlineData("A4-C1-38-0B-22-9F", "A4C1380B229F")]
    [InlineData("A4C1380B229F", "A4C1380B229F")]
    [InlineData("A4C1380B22", null)]
    [InlineData("Headphones", null)]
    public void S11956_a_device_is_its_address_twelve_hex_digits(string address, string? key)
        => Assert.Equal(key, LinkKeys.Device(address));

    [Fact]
    public void S11956_a_numeric_address_is_the_same_key()
    {
        Assert.Equal("A4C1380B229F", LinkKeys.Device(0xA4C1380B229FUL));
        Assert.Null(LinkKeys.Device(0UL));
    }

    [Fact]
    public void S11908_a_quiet_board_takes_no_value_and_drops_its_hovers()
    {
        var registry = new LinkRegistry();
        var board = registry.For("v");
        var a = board.Join();
        var b = board.Join();
        var heard = new List<AppLink?>();
        b.Watch(LinkTopics.HoveredApp, heard.Add);
        a.Set(LinkTopics.HoveredApp, new AppLink("code"));
        a.Set(LinkTopics.SelectedApp, new AppLink("msedge"));

        registry.Quiet = true;
        Assert.Null(board.Peek(LinkTopics.HoveredApp));
        Assert.Equal(new AppLink("msedge"), board.Peek(LinkTopics.SelectedApp));
        Assert.Null(heard[^1]);
        Assert.False(a.Set(LinkTopics.HoveredApp, new AppLink("slack")));
        Assert.True(registry.For("w").Quiet);

        registry.Quiet = false;
        Assert.True(a.Set(LinkTopics.HoveredApp, new AppLink("slack")));
    }

    [Fact]
    public void S11905_a_declaration_is_ordered_and_each_topic_once()
    {
        var use = LinkUse.Of([LinkTopicId.HoveredDate, LinkTopicId.HoveredApp, LinkTopicId.HoveredDate], [LinkTopicId.SelectedDate]);
        Assert.Equal([LinkTopicId.HoveredApp, LinkTopicId.HoveredDate], use.Publishes);
        Assert.Equal([LinkTopicId.HoveredApp, LinkTopicId.SelectedDate, LinkTopicId.HoveredDate], use.Topics);
        Assert.Empty(LinkUse.None.Topics);
    }

    [Fact]
    public void S3301_every_topic_is_in_the_list_once_in_id_order()
        => Assert.Equal(Enum.GetValues<LinkTopicId>(), LinkTopics.All.Select(t => t.Id));

    private static string Scene(string links)
        => $$"""
        { "scenes": [ { "name": "a", "links": {{links}}, "widgets": [ { "kind": "cpu", "at": [0, 0, 300, 200] } ] } ] }
        """;

    [Fact]
    public void S11909_a_scene_reads_its_links_and_sets_them_on_the_board()
    {
        var file = SceneParser.Parse(Scene("""
            { "hoveredDate": "today+1", "selectedDate": "2026-11-13", "hoveredApp": "Code", "selectedApp": "msedge",
              "hoveredInstant": { "at": "-00:00:30", "subject": "cpu" }, "hoveredPerson": "Ada@Example.com",
              "hoveredFile": "C:\\Users\\Ada\\report.docx", "hoveredDrive": "C:", "hoveredDevice": "a4:c1:38:0b:22:9f" }
            """), out var refusal);
        Assert.Null(refusal);
        var links = file!.Scenes[0].Links!;

        var board = new LinkBoard();
        var now = new DateTimeOffset(2026, 11, 12, 14, 5, 0, TimeSpan.Zero);
        links.Apply(board.Join(), now);

        Assert.Equal(new DateLink(DateOnly.FromDateTime(now.LocalDateTime).AddDays(1)), board.Peek(LinkTopics.HoveredDate));
        Assert.Equal(new DateLink(new DateOnly(2026, 11, 13)), board.Peek(LinkTopics.SelectedDate));
        Assert.Equal(new AppLink("code"), board.Peek(LinkTopics.HoveredApp));
        Assert.Equal(new AppLink("msedge"), board.Peek(LinkTopics.SelectedApp));
        Assert.Equal(new InstantLink(now.AddSeconds(-30), "cpu"), board.Peek(LinkTopics.HoveredInstant));
        Assert.Equal(new PersonLink("ada@example.com"), board.Peek(LinkTopics.HoveredPerson));
        Assert.Equal(new FileLink(@"C:\USERS\ADA\REPORT.DOCX"), board.Peek(LinkTopics.HoveredFile));
        Assert.Equal(new DriveLink("C:"), board.Peek(LinkTopics.HoveredDrive));
        Assert.Equal(new DeviceLink("A4C1380B229F"), board.Peek(LinkTopics.HoveredDevice));
    }

    [Fact]
    public void S11909_a_scene_inherits_its_links_through_like_and_a_bare_moment_marks_the_charts_only()
    {
        var file = SceneParser.Parse("""
            { "scenes": [
              { "name": "a", "links": { "hoveredInstant": "-00:01:00" }, "widgets": [ { "kind": "cpu", "at": [0, 0, 300, 200] } ] },
              { "name": "b", "like": "a" } ] }
            """, out var refusal);
        Assert.Null(refusal);
        Assert.Equal(new SceneInstant(TimeSpan.FromMinutes(1), null, string.Empty), file!.Scenes[1].Links!.HoveredInstant);
    }

    [Theory]
    [InlineData("""{ }""", "links")]
    [InlineData("""{ "hoveredDate": "tomorrow" }""", "hoveredDate")]
    [InlineData("""{ "hoveredDrive": "C" }""", "hoveredDrive")]
    [InlineData("""{ "hoveredFile": "report.docx" }""", "hoveredFile")]
    [InlineData("""{ "hoveredDevice": "Headphones" }""", "hoveredDevice")]
    [InlineData("""{ "hoveredInstant": "30 s ago" }""", "hoveredInstant")]
    [InlineData("""{ "hoveredThing": "x" }""", "hoveredThing")]
    public void S11909_a_wrong_link_is_refused_where_it_is_written(string links, string where)
    {
        Assert.Null(SceneParser.Parse(Scene(links), out var refusal));
        Assert.Contains(where, refusal!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void S11937_a_scene_points_at_an_event_keyed_as_the_calendars_key_it()
    {
        var file = SceneParser.Parse(Scene("""{ "hoveredEvent": { "account": "sample:work", "id": "one-on-one-0" } }"""), out var refusal);
        Assert.Null(refusal);
        var board = new LinkBoard();
        file!.Scenes[0].Links!.Apply(board.Join(), DateTimeOffset.Now);
        Assert.Equal(new EventLink("sample:work", null, "one-on-one-0"), board.Peek(LinkTopics.HoveredEvent));

        var home = SceneParser.Parse(Scene("""{ "hoveredEvent": { "account": "sample:home", "calendar": "home", "id": "lunch-lena-0" } }"""), out _);
        Assert.Equal(new SceneEvent("sample:home", "home", "lunch-lena-0"), home!.Scenes[0].Links!.HoveredEvent);
    }

    [Theory]
    [InlineData("""{ "hoveredEvent": "one-on-one-0" }""")]
    [InlineData("""{ "hoveredEvent": { "account": "sample:work" } }""")]
    [InlineData("""{ "hoveredEvent": { "id": "x" } }""")]
    [InlineData("""{ "hoveredEvent": { "account": "a", "id": "x", "when": "now" } }""")]
    public void S11937_a_wrong_event_is_refused_where_it_is_written(string links)
    {
        Assert.Null(SceneParser.Parse(Scene(links), out var refusal));
        Assert.Contains("hoveredEvent", refusal!.ToString(), StringComparison.Ordinal);
    }
}
