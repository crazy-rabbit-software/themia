using System.Runtime.CompilerServices;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1500] A widget's confirmation opens as a popup of its own, never inside the desktop surface's
/// window, whose region is cut to the cards and clipped the scratchpad's clear to its tile. The rule is
/// a property of the App's flyout, so these tests read the repository.
/// </summary>
public sealed class ConfirmFlyoutTests
{
    private static readonly string Widgets = Path.Combine(FindRoot(), "src", "Themia.App", "Widgets");

    /// <summary>Walks up from this source file, not the test binaries, which may build outside the repository.</summary>
    private static string FindRoot([CallerFilePath] string here = "")
    {
        for (var dir = new FileInfo(here).Directory; dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Themia.App", "Widgets")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("repository root (src/Themia.App/Widgets) not found above this test's source");
    }

    [Fact]
    public void S1500_the_confirmation_is_never_constrained_to_the_surface_window()
    {
        var source = File.ReadAllText(Path.Combine(Widgets, "ConfirmFlyout.cs"));

        Assert.Contains("ShouldConstrainToRootBounds = false", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldConstrainToRootBounds = true", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// A hand-built confirmation (a flyout with a Cancel button) takes the flyout's default, which
    /// is constrained to the window: the scratchpad's clear was clipped to the tile and the folder's
    /// delete, opening under it, was not drawn at all.
    /// </summary>
    [Fact]
    public void S1500_no_widget_builds_its_own_confirmation_flyout()
    {
        var handBuilt = Directory.GetFiles(Widgets, "*.cs")
            .Where(path => Path.GetFileName(path) != "ConfirmFlyout.cs")
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("new Flyout", StringComparison.Ordinal)
                    && source.Contains("Str.Common_Cancel", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(handBuilt);
    }

    [Theory]
    [InlineData("ScratchpadView.cs")]
    [InlineData("FolderView.Commands.cs")]
    public void S1500_the_destructive_widget_actions_ask_through_the_one_confirmation(string file)
    {
        var source = File.ReadAllText(Path.Combine(Widgets, file));

        Assert.Contains("ConfirmFlyout.Create(", source, StringComparison.Ordinal);
    }
}
