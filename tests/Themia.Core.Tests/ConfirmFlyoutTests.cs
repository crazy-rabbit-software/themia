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
    [InlineData("ScratchpadView.cs", "ConfirmFlyout.Create(")]
    [InlineData("FolderView.Commands.cs", "ConfirmFlyout.AskAsync(")]
    [InlineData("ClipboardView.cs", "ConfirmFlyout.AskAsync(")]
    [InlineData("NotesView.Picker.cs", "ConfirmFlyout.AskAsync(")]
    public void S1500_the_destructive_widget_actions_ask_through_the_one_confirmation(string file, string call)
    {
        var source = File.ReadAllText(Path.Combine(Widgets, file));

        Assert.Contains(call, source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The countdown's remove, the clipboard's clear and the notes' delete asked through a
    /// <c>ContentDialog</c> (<c>FluentLayout.ConfirmAsync</c> is one): centred on the monitor-sized
    /// surface, far from the widget, and drawn inside the region that is cut to the cards. Neither a
    /// widget view nor the desktop surface may open one; the settings window may.
    /// </summary>
    [Fact]
    public void S1500_no_widget_view_or_surface_opens_a_content_dialog()
    {
        var shell = Path.Combine(FindRoot(), "src", "Themia.App", "Shell");
        var surface = Directory.GetFiles(shell, "DesktopWindow*.cs")
            .Concat(Directory.GetFiles(shell, "WidgetCard*.cs"));

        var offenders = Directory.GetFiles(Widgets, "*.cs")
            .Concat(surface)
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return (source.Contains("ContentDialog", StringComparison.Ordinal)
                    && Path.GetFileName(path) != "ConfirmFlyout.cs")
                    || source.Contains("FluentLayout.ConfirmAsync", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// A <c>Flyout</c>'s default is to stay inside the window, where the region clips it to the card:
    /// every one a widget opens says otherwise (a rename box, a note, the confirmation).
    /// </summary>
    [Fact]
    public void S1500_every_widget_flyout_opens_as_its_own_popup()
    {
        var constrained = Directory.GetFiles(Widgets, "*.cs")
            .SelectMany(path => System.Text.RegularExpressions.Regex
                .Matches(File.ReadAllText(path), @"new (Flyout|MenuFlyout|CommandBarFlyout)\b[^;]*;")
                .Where(m => !m.Value.Contains("ShouldConstrainToRootBounds = false", StringComparison.Ordinal))
                .Select(m => $"{Path.GetFileName(path)}: {m.Value.Split('\n')[0].Trim()}"))
            .ToArray();

        Assert.Empty(constrained);
    }
}
