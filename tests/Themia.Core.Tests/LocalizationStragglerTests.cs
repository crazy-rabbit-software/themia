using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1100] Regression tests for the user-visible sentences that still bypassed the string layer after
/// the extraction rounds: each one is English for English and in the user's language otherwise.
/// Every call names its locale, so these never depend on (or disturb) the global one.
/// </summary>
public class LocalizationStragglerTests
{
    private static readonly AudioDevice Headphones = new("{b}", "Headphones");

    [Fact]
    public void S1100_rename_problems_follow_the_language()
    {
        Assert.Equal("That name is too long", FileNames.Message(FileNameProblem.TooLong, Locale.En));
        Assert.Equal("Dieser Name ist zu lang", FileNames.Message(FileNameProblem.TooLong, Locale.De));
        Assert.Equal(string.Empty, FileNames.Message(FileNameProblem.None, Locale.De));
    }

    [Fact]
    public void S1100_every_rename_problem_has_a_sentence_in_every_language()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            foreach (var problem in Enum.GetValues<FileNameProblem>().Where(p => p != FileNameProblem.None))
            {
                Assert.False(string.IsNullOrWhiteSpace(FileNames.Message(problem, locale)), $"{problem} in {locale}");
            }
        }
    }

    [Fact]
    public void S1100_feed_address_problems_follow_the_language()
    {
        Assert.Equal("Only http and https addresses", WebInput.Explain(WebUrlProblem.UnsupportedScheme, Locale.En));
        Assert.Equal("Nur http- und https-Adressen", WebInput.Explain(WebUrlProblem.UnsupportedScheme, Locale.De));
        Assert.Equal(string.Empty, WebInput.Explain(WebUrlProblem.None, Locale.De));
    }

    [Fact]
    public void S1100_launch_refusals_follow_the_language()
    {
        Assert.Equal("That is not a Windows shell location.", LaunchTargets.RefusalMessage(LaunchRefusal.InvalidShellName, Locale.En));
        Assert.Equal("Das ist kein Windows-Shell-Ort.", LaunchTargets.RefusalMessage(LaunchRefusal.InvalidShellName, Locale.De));
        Assert.Equal(string.Empty, LaunchTargets.RefusalMessage(LaunchRefusal.None, Locale.De));
    }

    [Theory]
    [InlineData("shell:RecycleBinFolder", "Recycle Bin", "Papierkorb")]
    [InlineData("shell:MyComputerFolder", "This PC", "Dieser PC")]
    [InlineData("shell:Personal", "Documents", "Dokumente")]
    [InlineData("shell:SomethingNobodyNamed", "SomethingNobodyNamed", "SomethingNobodyNamed")]
    public void S1100_windows_locations_are_named_as_explorer_names_them(string target, string english, string german)
    {
        Assert.Equal(english, LaunchTargets.ShellDisplayName(target, Locale.En));
        Assert.Equal(german, LaunchTargets.ShellDisplayName(target, Locale.De));
    }

    [Fact]
    public void S1100_an_unnamed_output_device_follows_the_language()
    {
        Assert.Equal("Output device", AudioRules.DeviceName("{gone}", [], null, Locale.En));
        Assert.Equal("Ausgabegerät", AudioRules.DeviceName("{gone}", [], null, Locale.De));
    }

    [Fact]
    public void S1100_default_output_notices_follow_the_language()
    {
        Assert.Equal("Headphones is no longer connected.",
            AudioDefaultRules.After(DefaultSwitchResult.DeviceGone, Headphones, "{c}", "Old", Locale.En).Notice);
        Assert.Equal("Headphones ist nicht mehr verbunden.",
            AudioDefaultRules.After(DefaultSwitchResult.DeviceGone, Headphones, "{c}", "Old", Locale.De).Notice);
        Assert.Equal("Das Gerät ist nicht mehr verbunden.",
            AudioDefaultRules.After(DefaultSwitchResult.DeviceGone, new AudioDevice("{x}", " "), null, null, Locale.De).Notice);
        Assert.Contains("Headphones", AudioDefaultRules.After(DefaultSwitchResult.AccessDenied, Headphones, null, null, Locale.De).Notice, StringComparison.Ordinal);
        Assert.Contains("Standardausgabe", AudioDefaultRules.After(DefaultSwitchResult.Failed, Headphones, null, null, Locale.De).Notice, StringComparison.Ordinal);
    }
}
