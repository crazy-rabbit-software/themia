using Themia.Core.Scenes;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2458] A scene's groups and the group selection its edit block draws.</summary>
public sealed class SceneGroupsTests
{
    private static string Scene(string edit, string secondGroup = "\"pc\"")
        => $$"""
        { "scenes": [ { "name": "a", "edit": {{edit}}, "widgets": [
            { "kind": "cpu", "at": [0, 0, 300, 200], "group": "pc" },
            { "kind": "memory", "at": [300, 0, 300, 200], "group": {{secondGroup}} },
            { "kind": "clock", "at": [700, 0, 300, 200] }
        ] } ] }
        """;

    private static SceneRefusal Refused(string json)
    {
        Assert.Null(SceneParser.Parse(json, out var refusal));
        return refusal!;
    }

    [Fact]
    public void S2458_a_widget_names_its_group_and_edit_group_selects_the_members()
    {
        var scene = SceneParser.Parse(Scene("""{ "group": "pc" }"""), out var refusal)!.Scenes[0];

        Assert.Null(refusal);
        Assert.Equal(["pc"], scene.Groups);
        Assert.Equal(["cpu", "memory"], scene.Selected.Select(w => w.Id));
    }

    [Fact]
    public void S2458_edit_enter_selects_one_member()
    {
        var scene = SceneParser.Parse(Scene("""{ "enter": "memory" }"""), out _)!.Scenes[0];

        Assert.Equal("memory", Assert.Single(scene.Selected).Id);
    }

    [Fact]
    public void S2458_a_group_of_one_widget_is_refused()
    {
        Assert.Contains("two or more", Refused(Scene("{}", secondGroup: "\"other\"")).Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void S2458_edit_names_only_a_group_or_a_member_that_is_there()
    {
        Assert.EndsWith(".edit.group", Refused(Scene("""{ "group": "nope" }""")).Path, StringComparison.Ordinal);
        Assert.EndsWith(".edit.enter", Refused(Scene("""{ "enter": "clock" }""")).Path, StringComparison.Ordinal);
        Assert.EndsWith(".edit", Refused(Scene("""{ "group": "pc", "enter": "cpu" }""")).Path, StringComparison.Ordinal);
    }

    [Fact]
    public void S2458_a_blank_group_name_is_refused()
    {
        Assert.EndsWith(".group", Refused(Scene("{}", secondGroup: "\" \"")).Path, StringComparison.Ordinal);
    }
}
