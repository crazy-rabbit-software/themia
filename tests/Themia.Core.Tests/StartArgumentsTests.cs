using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-68] A start's first argument: the app, a render this build knows, or an unknown flag that starts nothing.</summary>
public sealed class StartArgumentsTests
{
    private const string Exe = @"C:\Themia\Themia.exe";

    private static readonly string[] Renders = ["--widget-png", "--editbar-png"];

    private static StartDecision Classify(params string[] args) => StartArguments.Classify([Exe, .. args], Renders);

    [Fact]
    public void S68_no_argument_starts_the_app()
        => Assert.Equal(new StartDecision(StartRequest.App, null), Classify());

    [Fact]
    public void S68_the_run_entrys_flag_starts_the_app()
        => Assert.Equal(new StartDecision(StartRequest.App, "--autostart"), Classify("--autostart"));

    [Fact]
    public void S68_a_render_flag_this_build_knows_is_a_render()
        => Assert.Equal(new StartDecision(StartRequest.Render, "--widget-png"), Classify("--widget-png", "clock", "out.png"));

    [Theory]
    [InlineData("--some-flag")]
    [InlineData("--desktop-select-png")]
    [InlineData("--Widget-png")]
    [InlineData("-x")]
    [InlineData("--")]
    [InlineData("-")]
    public void S68_an_unknown_flag_is_refused_and_named(string flag)
    {
        // The D1 incident: a render flag run before it existed took the normal start and toggled the
        // running copy's edit mode. It must be Unknown, never App.
        var decision = Classify(flag, "out.png");
        Assert.Equal(StartRequest.Unknown, decision.Request);
        Assert.Equal(flag, decision.Flag);
        Assert.Contains(flag, StartArguments.UnknownFlagMessage(flag), StringComparison.Ordinal);
    }

    [Fact]
    public void S68_a_render_flag_in_a_build_without_renders_is_unknown()
        => Assert.Equal(StartRequest.Unknown, StartArguments.Classify([Exe, "--widget-png", "clock", "out.png"], []).Request);

    [Fact]
    public void S68_only_the_first_argument_decides()
    {
        // A value first is no flag: the app (a later flag is that start's own business, [S-934]).
        Assert.Equal(StartRequest.App, Classify("notes.txt", "--bogus").Request);
        Assert.Equal(StartRequest.Unknown, Classify("--bogus", "--widget-png").Request);
    }

    [Fact]
    public void S68_the_exit_code_is_not_zero()
        => Assert.NotEqual(0, StartArguments.UnknownFlagExitCode);
}
