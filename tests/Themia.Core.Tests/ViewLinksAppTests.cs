using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11932] [S-11933] An app on the view's links beyond screen time: a process, a mixer row, a notification's sender.</summary>
public sealed class ViewLinksAppTests
{
    [Theory]
    [InlineData("Code", @"C:\Users\a\AppData\Local\Programs\Microsoft VS Code\Code.exe", "code")]
    [InlineData("msedge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", "msedge")]
    [InlineData("SearchHost", null, "searchhost")]
    [InlineData("explorer", "", "explorer")]
    [InlineData("", null, null)]
    public void S11932_a_process_is_its_executables_stem_else_its_name(string name, string? path, string? key)
        => Assert.Equal(key, ProcessTable.AppKey(name, path));

    [Fact]
    public void S11932_a_process_row_carries_its_app()
    {
        ProcessSample[] samples =
        [
            new("Code", 0.2, 100, 3, new ProcessIdentity(10, 1)),
            new("System", 0.1, 50),
        ];

        var rows = ProcessTable.Rows(Localization.Locale.En, samples, ProcessSort.Cpu, 0, s => s.Name == "Code" ? @"C:\VS\Code.exe" : null);

        Assert.Equal("code", rows[0].App);
        Assert.Equal("system", rows[1].App);
    }

    [Fact]
    public void S11932_a_mixer_row_is_its_executable_and_the_system_sounds_none()
    {
        MixerRow Row(string key, string? path, bool system = false) => new(key, "x", path, 1, false, true, system, 1);

        Assert.Equal("spotify", MixerRules.AppKey(Row(@"c:\users\a\appdata\roaming\spotify\spotify.exe", @"C:\Users\a\AppData\Roaming\Spotify\Spotify.exe")));

        // Grouped, the key is the executable's path: it names the app when the path was not kept.
        Assert.Equal("msedge", MixerRules.AppKey(Row("msedge.exe", null)));
        Assert.Null(MixerRules.AppKey(MixerRules.StandIn));
        Assert.Null(MixerRules.AppKey(Row(MixerRules.SystemSoundsKey, null, system: true)));
        Assert.Null(MixerRules.AppKey(Row("pid:4242", null)));

        // Ungrouped, an instance id without a path is no app, even with ".exe" in it.
        Assert.Null(MixerRules.AppKey(Row(@"{0.0.0.00000000}.{abc}|\Device\HarddiskVolume3\Apps\x.exe%b{def}", null)));
    }

    [Theory]
    [InlineData("Microsoft.Windows.Explorer", "explorer")]
    [InlineData("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "windowsterminal")]
    [InlineData("Microsoft.OutlookForWindows_8wekyb3d8bbwe!App", "olk")]
    [InlineData("MSTeams_8wekyb3d8bbwe!MSTeams", "ms-teams")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "spotify")]
    [InlineData("windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel", "systemsettings")]
    [InlineData("MSEdge", "msedge")]
    [InlineData("Microsoft.Office.OUTLOOK.EXE.15", "outlook")]
    [InlineData("com.squirrel.slack.slack", "slack")]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Notepad++\notepad++.exe", "notepad++")]
    [InlineData(@"C:\Tools\Foo.EXE", "foo")]
    public void S11933_a_sender_is_the_app_its_id_names(string appId, string key)
        => Assert.Equal(key, NotificationRules.AppKey(appId));

    [Theory]
    [InlineData("Windows.SystemToast.SecurityAndMaintenance")]
    [InlineData("Windows.SystemToast.WindowsUpdate")]
    [InlineData("Contoso.Chat_8wekyb3d8bbwe!App")]
    [InlineData("308046B0AF4A39CB")]
    [InlineData("")]
    [InlineData(null)]
    public void S11933_a_sender_it_cannot_name_is_none(string? appId)
        => Assert.Null(NotificationRules.AppKey(appId));
}
