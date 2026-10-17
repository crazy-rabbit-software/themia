using Themia.Core.Shell;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1751] In edit mode a widget's own content takes no pointer input: every press, right-click,
/// long press and hover lands on the card (select, drag, the card menu), never on the view's rows,
/// buttons or item menus. Mail's message menu opened in edit mode because each view guarded itself,
/// or did not. The guard is the card's, so these tests also read the repository.
/// </summary>
public sealed class EditModeInertTests
{
    private static readonly string Shell = RepoFiles.At("src", "Themia.App", "Shell");

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void S1751_the_view_takes_input_only_outside_edit_mode(bool editMode, bool takes)
        => Assert.Equal(takes, CardGestures.ContentTakesInput(editMode));

    /// <summary>
    /// The card, not each view, makes its content inert: one guard covers every widget, current and
    /// future. The surface border holds the header and the view (or its error and setup cards).
    /// </summary>
    [Fact]
    public void S1751_the_card_makes_its_whole_content_inert_in_edit_mode()
    {
        var source = File.ReadAllText(Path.Combine(Shell, "WidgetCard.cs"));

        Assert.Contains("_surface.IsHitTestVisible = CardGestures.ContentTakesInput(editing);", source, StringComparison.Ordinal);
    }
}
