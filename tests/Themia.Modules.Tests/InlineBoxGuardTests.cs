using System.Text.RegularExpressions;
using Themia.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-13950] A widget types in place of its own words through one box (owner, 2026-11-19: "layout shifts for these kind
/// of inputs should never happen. fix for all the widgets"): <c>InlineBox</c>, which stands in exactly the box of the
/// words it replaces. No widget and no part of the kit builds a text box of its own, which would bring back WinUI's 32
/// minimum, padding, frame, clear button and wrapping.
/// </summary>
public sealed partial class InlineBoxGuardTests
{
    /// <summary>
    /// Where a text box may still be made, and why: the one box itself; a list's and a note's name typed in a flyout of
    /// their own (a popup over the card, which moves nothing in it); the note's own field, which is the card's whole text;
    /// the desktop icon's rename box, Explorer's own (wider than the name, over the icons below as Windows draws it,
    /// [S-5607]; it moves nothing either).
    /// </summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "InlineBox.cs", "TasksView.Picker.cs", "NotesView.Picker.cs", "MarkdownEditor.cs", "DesktopItemView.cs",
    };

    [GeneratedRegex(@"new\s+(TextBox|RichEditBox)\s*[({]")]
    private static partial Regex NewTextBox();

    [Fact]
    public void S13950_no_widget_and_no_kit_part_builds_a_text_box_of_its_own()
    {
        string[] roots = [Path.Combine(RepoFiles.Src, "Widgets"), Path.Combine(RepoFiles.Src, "Themia.Sdk.WinUI")];
        var offenders = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Allowed.Contains(Path.GetFileName(f)) && NewTextBox().IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }
}
