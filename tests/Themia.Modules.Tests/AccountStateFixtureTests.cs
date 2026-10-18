using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-44] The account widgets' settings as users have them, generated once with the build from before
/// they became projects (`e0d31a3f`; Fixtures/state-account-configured.json, ids w00.., made-up
/// accounts on reserved names): three mail widgets (a table of chosen columns over two accounts in Sent,
/// oldest first; a compact one; a fresh one), four calendars (an agenda over two chosen calendars with
/// their colours and its own range and count, a fortnight's week, a year, a fresh one), two contacts
/// widgets (a details table of chosen columns, a grid), two next meetings, two week planners (with and
/// without tasks) and three task lists (an account's list with every filter set, a list kept on this PC,
/// a fresh one). As each widget moves into its project and reads its config from its own module's
/// context, this pins that the file still loads with no repair and writes back unchanged.
/// </summary>
public sealed class AccountStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-account-configured.json"));

    [Fact]
    public void S44_the_account_widgets_configured_state_loads_without_a_repair_and_writes_back_unchanged()
    {
        var json = Fixture();
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var written = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the account widgets' state does not write back as it was read");
    }

    [Fact]
    public void S44_every_account_widget_reads_its_own_config()
    {
        var widgets = Assert.Single(AppStateStore.Parse(Fixture()).State!.Views).Widgets;
        Assert.Equal(16, widgets.Count);
        Assert.Equal(
            ["email", "email", "email", "calendar", "calendar", "calendar", "calendar", "contacts", "contacts", "nextMeeting", "nextMeeting", "weekPlanner", "weekPlanner", "tasks", "tasks", "tasks"],
            widgets.Select(w => w.Config.Kind.Value));
    }
}
