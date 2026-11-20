using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell.Gallery;
using Themia.Core.Notes;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: the note's sample (<see cref="ShowSampleAsync"/>) at rest (<c>sample</c>; with <c>--hover</c> the
/// pill shows, [S-14771]); <c>selection</c>, "Coffee beans" selected and the selection's bar open where the pointer let go,
/// and <c>context</c>, the text's menu opened there by a right-click ([S-14772]); <c>picker</c> and <c>more</c>, the pill's
/// two menus open over sample notes; <c>notsaved</c>, the pill standing at rest with the problem glyph ([S-1362]);
/// <c>confirm</c>, the delete question open ([S-1500]). Nothing is saved, deleted or renamed: there is no pool behind them.
/// </summary>
internal sealed partial class NotesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "selection", "context", "picker", "more", "notsaved", "confirm"];

    /// <summary>A picture's notes for the picker: the sample's first.</summary>
    private static readonly NoteMeta[] PictureNotes =
    [
        new("00000000000000000000000000000001", "Groceries", 0),
        new("00000000000000000000000000000002", "Ideas for the garden", 0),
        new("00000000000000000000000000000003", "Reading list", 0),
    ];

    public async Task ShowPreviewStateAsync(string state)
    {
        await ShowSampleAsync().ConfigureAwait(true);
        if (state == "sample")
        {
            return;
        }

        // The card is laid out before anything opens against it.
        for (var i = 0; i < 20 && (_editor.Box.XamlRoot is null || _editor.Box.ActualWidth <= 0); i++)
        {
            await Task.Delay(50).ConfigureAwait(true);
        }

        switch (state)
        {
            case "selection" or "context":
                var at = SelectForPicture("Coffee beans");
                if (state == "selection")
                {
                    _editor.OpenBar(at);
                }
                else
                {
                    _editor.Box.ContextFlyout?.ShowAt(_editor.Box, new FlyoutShowOptions
                    {
                        Position = at,
                        ShowMode = FlyoutShowMode.Standard,
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                    });
                }

                break;

            case "picker":
                OpenPicker(PictureNotes, PictureNotes[0].Id);
                break;

            case "more":
                OpenMore(PictureNotes[0].Id);
                break;

            case "notsaved":
                _notSavedGlyph.Visibility = Visibility.Visible;
                Lay();
                Pill(_shows.WithProblem(true));
                break;

            case "confirm":
                // Never answered: the render closes with the question open.
                PillMenu(true);
                _ = AskToDeleteAsync(SampleName);
                break;
        }

        // A menu opens with an animation; the render draws it once it has settled.
        await Task.Delay(900).ConfigureAwait(true);
    }

    /// <summary>Select <paramref name="words"/> of the sample as the pointer would and say where it let go (the selection's end).</summary>
    private Windows.Foundation.Point SelectForPicture(string words)
    {
        var start = SampleNote.IndexOf(words, StringComparison.Ordinal);
        _editor.Box.Focus(FocusState.Programmatic);

        // A render's window is never the active one, so the text never has the keyboard there: its selection is drawn as
        // the focused text draws it (the selection's bar takes no focus, [S-14772]).
        _editor.Box.SelectionHighlightColorWhenNotFocused = _editor.Box.SelectionHighlightColor;
        var range = _editor.Box.Document.Selection;
        range.SetRange(start, start + words.Length);

        range.GetRect(PointOptions.ClientCoordinates, out var rect, out _);
        return rect.Width > 0 && rect.Height > 0
            ? new Windows.Foundation.Point(rect.Right, rect.Bottom)
            : new Windows.Foundation.Point(_editor.Box.ActualWidth / 2, _editor.Box.ActualHeight / 3);
    }
}
