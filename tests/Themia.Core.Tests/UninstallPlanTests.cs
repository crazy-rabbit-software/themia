using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1552]..[S-1555] ⚙ › About Themia › Uninstall Themia's plan: the route, the folders it may delete, the survey.</summary>
public sealed class UninstallPlanTests
{
    private const string Roaming = @"C:\Users\someone\AppData\Roaming";
    private const string Local = @"C:\Users\someone\AppData\Local";

    [Fact]
    public void S1552_route_follows_packaging()
    {
        var packaged = UninstallPlan.Create(packaged: true, deleteData: false, Roaming, Local, "Themia", "1.2.0");
        Assert.Equal(UninstallRoute.RemovePackage, packaged.Route);
        Assert.Equal(UninstallPlan.OwnAppPage, packaged.Fallback);

        var loose = UninstallPlan.Create(packaged: false, deleteData: false, Roaming, Local, "Themia", "1.2.0");
        Assert.Equal(UninstallRoute.OpenInstalledApps, loose.Route);
        Assert.Equal(UninstallPlan.InstalledApps, loose.Fallback);
    }

    [Fact]
    public void S1553_nothing_is_deleted_unless_asked()
    {
        var plan = UninstallPlan.Create(packaged: false, deleteData: false, Roaming, Local, "Themia", "1.0.0");
        Assert.Empty(plan.FoldersToDelete);
    }

    [Fact]
    public void S1553_asked_deletes_exactly_the_two_app_folders()
    {
        var plan = UninstallPlan.Create(packaged: false, deleteData: true, Roaming, Local, "Themia", "1.0.0");
        Assert.Equal([$@"{Roaming}\Themia", $@"{Local}\Themia"], plan.FoldersToDelete);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData(@"Themia\..")]
    [InlineData(@"..\Roaming")]
    [InlineData("Themia/x")]
    [InlineData(" Themia")]
    public void S1553_a_suspicious_folder_name_deletes_nothing(string name)
        => Assert.Empty(UninstallPlan.DataFolders(Roaming, Local, name));

    [Fact]
    public void S1553_a_missing_or_relative_root_is_skipped()
    {
        Assert.Equal([$@"{Local}\Themia"], UninstallPlan.DataFolders(null, Local, "Themia"));
        Assert.Equal([$@"{Local}\Themia"], UninstallPlan.DataFolders(@"relative\path", Local, "Themia"));
        Assert.Empty(UninstallPlan.DataFolders(string.Empty, "  ", "Themia"));

        // The same root twice is one folder.
        Assert.Single(UninstallPlan.DataFolders(Roaming, Roaming + @"\", "Themia"));
    }

    [Fact]
    public void S1554_the_survey_carries_the_version_escaped()
    {
        Assert.Equal("https://themia.app/goodbye?v=1.2.0", UninstallPlan.SurveyUrl("1.2.0").AbsoluteUri);
        Assert.Equal("https://themia.app/goodbye?v=1.0.0-beta%2B1", UninstallPlan.SurveyUrl("1.0.0-beta+1").AbsoluteUri);
        Assert.Equal("https://themia.app/goodbye?v=unknown", UninstallPlan.SurveyUrl(null).AbsoluteUri);
    }
}
