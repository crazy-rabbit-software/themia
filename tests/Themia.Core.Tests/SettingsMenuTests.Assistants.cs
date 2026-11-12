using Themia.Core.Mcp;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-9979] ⚙ › AI assistants: the server's one switch, what it is doing, and its setups.</summary>
public sealed partial class SettingsMenuTests
{
    private const string McpToken = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ";

    private sealed class FakeMcp : IMcpMenuHost
    {
        public McpServerStatus Status { get; set; } = new(McpServerState.Off);

        public List<string> Copied { get; } = [];

        public bool CopyLands { get; set; } = true;

        public int NewAddresses { get; private set; }

        public bool Copy(string text)
        {
            Copied.Add(text);
            return CopyLands;
        }

        public void NewAddress() => NewAddresses++;
    }

    private static (FakeHost Host, FakeMcp Mcp) WithServer(bool on, McpServerStatus? status = null)
    {
        var mcp = new FakeMcp { Status = status ?? new McpServerStatus(McpServerState.Off) };
        var host = new FakeHost { Mcp = mcp };
        host.State.Preferences.McpServer = on;
        return (host, mcp);
    }

    private static McpServerStatus Listening => new(McpServerState.Listening, new McpAddress(48213, McpToken));

    [Fact]
    public void S9979_ai_assistants_follows_motion_on_general_and_is_absent_without_a_server()
    {
        var keys = SettingsPages.General(WithServer(on: false).Host).Sections.Select(s => s.Key).ToArray();
        Assert.Equal(["page", "language", "online", "motion", "assistants", "backup", "pro", "about", "-about"], keys);

        Assert.DoesNotContain(SettingsPages.General(new FakeHost()).Sections, s => s.Key == SettingsMenu.Keys.Mcp);
    }

    [Fact]
    public void S9960_the_server_is_off_by_default_and_its_switch_writes_only_the_preference()
    {
        Assert.False(new Model.AppState().Preferences.McpServer);

        var (host, _) = WithServer(on: false);
        var toggle = At<SettingsToggle>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpServer);
        Assert.False(toggle.On);
        Assert.Equal("Off: nothing listens for assistants.", At<SettingsNote>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpServer + "/status").Text);

        var before = Snapshot(host.State.Preferences);
        toggle.Set(true);
        Assert.True(host.State.Preferences.McpServer);
        Assert.Equal(["McpServer"], Changed(before, host.State.Preferences));
        Assert.Equal(1, host.Mutations);
        Assert.Equal([false], host.RelayoutFlags);

        At<SettingsToggle>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpServer).Set(false);
        Assert.False(host.State.Preferences.McpServer);
    }

    [Fact]
    public void S9979_while_off_there_is_no_setup_to_copy()
    {
        var (host, _) = WithServer(on: false, Listening);
        Assert.Null(SettingsMenu.Find(Root(host), [SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpCopyClaudeCode]));
        Assert.Null(SettingsMenu.Find(Root(host), [SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpNewAddress]));
        Assert.Equal("Off", At<SettingsSubmenu>(host, SettingsMenu.Keys.Mcp).Value);
    }

    [Fact]
    public void S9979_listening_says_the_url_without_the_token_and_copies_setups_with_it()
    {
        var (host, mcp) = WithServer(on: true, Listening);
        Assert.Equal("On", At<SettingsSubmenu>(host, SettingsMenu.Keys.Mcp).Value);

        var status = At<SettingsNote>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpServer + "/status");
        Assert.Equal("Listening on http://127.0.0.1:48213/mcp, for this PC only.", status.Text);
        Assert.DoesNotContain(
            Flatten(Root(host)),
            line => line switch
            {
                SettingsNote note => note.Text.Contains(McpToken, StringComparison.Ordinal),
                SettingsCommand command => command.Text.Contains(McpToken, StringComparison.Ordinal),
                _ => false,
            });

        At<SettingsCommand>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpCopyClaudeCode).Run();
        Assert.Equal(McpAccess.ClaudeCodeCommand(Listening.Address!), Assert.Single(mcp.Copied));
        Assert.StartsWith("Copied.", host.Outcomes.Of(SettingsMenu.Keys.McpCopyClaudeCode)!.Text, StringComparison.Ordinal);
        Assert.StartsWith("Copied.", At<SettingsNote>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpCopyClaudeCode + "/outcome").Text, StringComparison.Ordinal);

        mcp.CopyLands = false;
        At<SettingsCommand>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpCopyJson).Run();
        Assert.Equal(McpAccess.JsonConfig(Listening.Address!), mcp.Copied[1]);
        Assert.True(host.Outcomes.Of(SettingsMenu.Keys.McpCopyJson)!.Failed);
    }

    [Fact]
    public void S9979_a_new_address_is_asked_first_and_only_the_answer_makes_it()
    {
        var (host, mcp) = WithServer(on: true, Listening);
        At<SettingsCommand>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpNewAddress).Run();
        Assert.Equal(0, mcp.NewAddresses);

        Assert.Single(host.Asked).Done();
        Assert.Equal(1, mcp.NewAddresses);
    }

    [Fact]
    public void S9979_a_taken_port_is_said_in_the_critical_colour_with_what_to_do()
    {
        var (host, _) = WithServer(on: true, new McpServerStatus(McpServerState.Failed, new McpAddress(48213, McpToken), McpProblem.PortTaken));
        var status = At<SettingsNote>(host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpServer + "/status");
        Assert.True(status.Critical);
        Assert.Contains("New address and key", status.Text, StringComparison.Ordinal);
        Assert.NotNull(SettingsMenu.Find(Root(host), [SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpNewAddress]));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void S9960_an_import_never_switches_the_server(bool here, bool inFile)
    {
        var target = new Model.AppState();
        target.Preferences.McpServer = here;
        var incoming = new Model.AppState();
        incoming.Preferences.McpServer = inFile;

        Storage.SettingsTransfer.Apply(target, incoming);

        Assert.Equal(here, target.Preferences.McpServer);
    }

    private static IEnumerable<SettingsLine> Flatten(IReadOnlyList<SettingsLine> lines) => lines.SelectMany(l => l switch
    {
        SettingsSubmenu submenu => [l, .. Flatten(submenu.Lines)],
        _ => new[] { l },
    });

    [Fact]
    public void S9979_starting_shows_a_ring()
        => Assert.True(At<SettingsNote>(WithServer(on: true, new McpServerStatus(McpServerState.Starting)).Host, SettingsMenu.Keys.Mcp, SettingsMenu.Keys.McpServer + "/status").Busy);
}
