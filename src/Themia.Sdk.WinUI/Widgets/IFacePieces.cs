namespace Themia.App.Widgets;

/// <summary>
/// [S-12833] A block of a face whose smaller pieces give way where the face is drawn small (<c>FaceFloor</c>): the
/// <see cref="ReadingFit"/> it stands in tells it the least text size its room draws, and it hides the pieces under it.
/// </summary>
public interface IFacePieces
{
    /// <summary>Draw only the pieces of at least <paramref name="floor"/> DIP; 0 draws them all.</summary>
    void Keep(double floor);
}
