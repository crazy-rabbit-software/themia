using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;
using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.Modules.Tests;

/// <summary>[S-6540] [S-6543] The item text colour over the first-run desktop: it needs two real widgets, which are their modules', so it runs here. Moved from Core's ItemTextTests, unchanged in what they assert.</summary>
public sealed class ItemTextHostTests
{

    [Fact]
    public void S6543_load_validation_repairs_a_bad_text_colour_loudly_and_keeps_a_good_one()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemTextColor = "white-ish";
        var first = state.Views[0].Widgets[0];
        var second = state.Views[0].Widgets[1];
        first.ItemTextColor = "#abcdef";
        second.ItemTextColor = "#12";

        var repairs = AppStateStore.Validate(state);

        Assert.Equal(string.Empty, state.Design.ItemTextColor);
        Assert.Equal("#ABCDEF", first.ItemTextColor);
        Assert.Equal(string.Empty, second.ItemTextColor);
        Assert.Contains(repairs, r => r.Contains("item text colour 'white-ish'", StringComparison.Ordinal));
        Assert.Contains(repairs, r => r.Contains(second.Id, StringComparison.Ordinal) && r.Contains("item text colour", StringComparison.Ordinal));
        Assert.DoesNotContain(repairs, r => r.Contains(first.Id, StringComparison.Ordinal) && r.Contains("item text", StringComparison.Ordinal));
    }

    [Fact]
    public void S6540_the_text_colour_survives_a_save_a_load_and_a_duplicate()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemTextColor = "#FFFFFF";
        state.Views[0].Widgets[0].ItemTextColor = "#101010";

        var json = System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, StateJson.AppState)!;

        Assert.Equal("#FFFFFF", back.Design.ItemTextColor);
        Assert.Equal("#101010", back.Views[0].Widgets[0].ItemTextColor);
        Assert.Equal(string.Empty, back.Views[0].Widgets[1].ItemTextColor);
        Assert.Equal("#101010", state.Views[0].Widgets[0].Clone().ItemTextColor);
        Assert.Equal("#FFFFFF", state.Design.Clone().ItemTextColor);
    }
}
