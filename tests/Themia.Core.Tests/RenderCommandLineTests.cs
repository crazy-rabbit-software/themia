using Themia.Core.Model;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-69] The renders' and checks' one command surface: <c>--render &lt;name&gt;</c> and <c>--check &lt;name&gt;</c>.</summary>
public sealed class RenderCommandLineTests
{
    private const string Exe = @"C:\Themia\Themia.exe";

    [Fact]
    public void S69_a_render_is_its_verb_its_name_and_the_rest()
    {
        var invocation = RenderCommandLine.Parse([Exe, "--render", "widget", "clock", "out.png", "--theme", "dark"]);
        Assert.NotNull(invocation);
        Assert.Equal(RenderVerb.Render, invocation.Verb);
        Assert.Equal("widget", invocation.Name);
        Assert.Equal(["clock", "out.png", "--theme", "dark"], invocation.Args);
    }

    [Fact]
    public void S69_a_check_is_the_other_verb()
    {
        var invocation = RenderCommandLine.Parse([Exe, "--check", "displays", "out"]);
        Assert.Equal(RenderVerb.Check, invocation?.Verb);
        Assert.Equal("displays", invocation?.Name);
    }

    [Fact]
    public void S69_a_verb_without_a_name_parses_with_none()
    {
        var invocation = RenderCommandLine.Parse([Exe, "--render"]);
        Assert.NotNull(invocation);
        Assert.Equal(string.Empty, invocation.Name);
        Assert.Empty(invocation.Args);
    }

    [Theory]
    [InlineData]
    [InlineData("--widget-png", "clock", "out.png")]
    [InlineData("--autostart")]
    [InlineData("--Render", "widget")]
    [InlineData("notes.txt", "--render", "widget")]
    public void S69_only_a_verb_as_the_first_argument_is_a_render_command(params string[] args)
        => Assert.Null(RenderCommandLine.Parse([Exe, .. args]));

    [Fact]
    public void S69_the_verbs_are_the_render_flags_the_start_knows()
    {
        Assert.Equal(StartRequest.Render, StartArguments.Classify([Exe, "--render", "list"], RenderCommandLine.Flags).Request);
        Assert.Equal(StartRequest.Render, StartArguments.Classify([Exe, "--check", "add-pictures", "a.png"], RenderCommandLine.Flags).Request);
        Assert.Equal(StartRequest.Unknown, StartArguments.Classify([Exe, "--widget-png", "clock", "a.png"], RenderCommandLine.Flags).Request);
    }

    [Fact]
    public void S69_an_option_is_the_value_after_it()
    {
        string[] args = ["clock", "out.png", "--state", "sample", "--transparent"];
        Assert.Equal("sample", RenderCommandLine.Option(args, "--state"));
        Assert.Null(RenderCommandLine.Option(args, "--transparent"));
        Assert.Null(RenderCommandLine.Option(args, "--theme"));
        Assert.True(RenderCommandLine.Has(args, "--transparent"));
        Assert.False(RenderCommandLine.Has(args, "--Transparent"));
    }

    [Theory]
    [InlineData("light", AppTheme.Light)]
    [InlineData("Dark", AppTheme.Dark)]
    public void S69_a_render_commands_theme_is_the_applications(string word, AppTheme theme)
        => Assert.Equal(theme, RenderCommandLine.Theme([Exe, "--render", "editbar", "out", "--theme", word]));

    [Theory]
    [InlineData("--render", "editbar", "out", "--theme", "blue")]
    [InlineData("--render", "editbar", "out")]
    [InlineData("--widget-png", "clock", "out.png", "--theme", "light")]
    public void S69_no_theme_unless_a_render_command_names_one(params string[] args)
        => Assert.Null(RenderCommandLine.Theme([Exe, .. args]));

    [Fact]
    public void S69_a_commands_line_names_its_verb_and_name()
    {
        Assert.Equal("Themia.exe --render widget <Kind> <out.png>", RenderCommandLine.Line(RenderVerb.Render, "widget", "<Kind> <out.png>"));
        Assert.Equal("Themia.exe --check displays", RenderCommandLine.Line(RenderVerb.Check, "displays", string.Empty));
    }

    [Fact]
    public void S69_a_checks_exit_code_is_its_verdict()
    {
        Assert.Equal(RenderCommandLine.Passed, RenderCommandLine.Verdict(0, crashed: false));
        Assert.Equal(RenderCommandLine.Failed, RenderCommandLine.Verdict(3, crashed: false));
        Assert.Equal(RenderCommandLine.Crashed, RenderCommandLine.Verdict(0, crashed: true));
        Assert.Equal(RenderCommandLine.Crashed, RenderCommandLine.Verdict(2, crashed: true));
        Assert.Equal(0, RenderCommandLine.Passed);
        Assert.Equal(64, RenderCommandLine.UsageError);
    }
}
